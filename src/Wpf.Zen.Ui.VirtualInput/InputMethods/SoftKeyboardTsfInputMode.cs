// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.InputMethods;

/// <summary>
/// Provides TSF mode operations for input-mode adapters on the focused WPF editor's dispatcher.
/// </summary>
/// <remarks>
/// These operations borrow WPF's initialized thread manager. They neither initialize TSF nor change focus.
/// Callers must interpret raw compartment values according to the active input method's contract.
/// </remarks>
public static class SoftKeyboardTsfInputMode
{
    private const uint ButtonDisabled = 0x00000002;
    private const uint LeftClick = 2;

    private static readonly Guid KeyboardOpenId       = new("58273AAD-01BB-4164-95C6-755BA0B5162D");
    private static readonly Guid KeyboardConversionId = new("CCF05DD8-4A87-11D7-A6E2-00065B84435C");
    private static readonly Guid InputModeItemId      = new("2C77A81E-41CC-4178-A3A7-5F8A987568E6");

    /// <summary>
    /// Reads the raw TSF input-mode compartments and discovers the active service's standard
    /// input-mode command.
    /// </summary>
    /// <param name="serviceId">The expected active TIP.</param>
    /// <returns>
    /// A raw snapshot when at least one compartment is readable; otherwise null. Command discovery
    /// remains false when the service does not publish <c>GUID_LBI_INPUTMODE</c>.
    /// </returns>
    public static SoftKeyboardTsfInputModeState? GetState(Guid serviceId)
    {
        var open       = GetCompartmentInteger(KeyboardOpenId);
        var conversion = GetCompartmentInteger(KeyboardConversionId);

        return open is null && conversion is null
            ? null
            : new SoftKeyboardTsfInputModeState(
                open,
                conversion,
                TryGetInputModeButton(serviceId, out _)
            );
    }

    /// <summary>
    /// Reads the integer keyboard-open compartment without WPF's Boolean conversion.
    /// </summary>
    /// <returns>The native integer, or null when the host or compartment value is unavailable.</returns>
    public static int? GetKeyboardOpenValue()
    {
        return GetCompartmentInteger(KeyboardOpenId);
    }

    /// <summary>
    /// Invokes the current TSF language bar's standard input-mode button once.
    /// </summary>
    /// <param name="serviceId">The expected active TIP, whose language-bar item may be invoked.</param>
    /// <returns>True when an enabled button accepted the invocation; otherwise false.</returns>
    /// <remarks>
    /// This invokes the service-defined transition rather than setting an absolute Boolean state.
    /// An adapter must first read its native mode and determine whether the command applies. The
    /// keyboard restores editor focus and finishes composition before requesting the transition.
    /// </remarks>
    public static bool TrySwitchInputMode(Guid serviceId)
    {
        if (!TryGetInputModeButton(serviceId, out var button))
        {
            return false;
        }

        // GUID_LBI_INPUTMODE is the TSF input-mode command, even when the language bar itself
        // is hidden. The left-click action does not need a menu anchor or a synthesized hotkey.
        var rectangle = new TfInputModeRectangle();
        return button!.OnClick(LeftClick, default, in rectangle) >= 0;
    }

    /// <summary>
    /// Reads an integer compartment without applying WPF's Boolean or managed-flag conversion.
    /// </summary>
    private static int? GetCompartmentInteger(Guid compartmentId)
    {
        if (WpfTextServicesAccessor.TryGetThreadManager() is not ITfCompartmentMgr manager
            || manager.GetCompartment(in compartmentId, out var compartment) < 0)
        {
            return null;
        }

        // Compartment RCWs can also be used by WPF's state-notification machinery. Keep their
        // managed lifetime intact; unlike enumeration snapshots, they belong to the TSF host.
        return compartment.GetValue(out var value) >= 0 && value is int integer ? integer : null;
    }

    /// <summary>
    /// Resolves the enabled standard input-mode command owned by the expected active service.
    /// </summary>
    private static bool TryGetInputModeButton(Guid serviceId, out ITfLangBarItemButton? button)
    {
        button = null;

        if (serviceId == Guid.Empty
            || WpfTextServicesAccessor.TryGetThreadManager() is not ITfLangBarItemMgr manager
            || manager.GetItem(in InputModeItemId, out var item) < 0
            || item is not ITfLangBarItemButton candidate
            || candidate.GetInfo(out var info) < 0
            || info.ServiceId != serviceId
            || candidate.GetStatus(out var status) < 0
            || (status & ButtonDisabled) != 0)
        {
            return false;
        }

        button = candidate;
        return true;
    }
}
