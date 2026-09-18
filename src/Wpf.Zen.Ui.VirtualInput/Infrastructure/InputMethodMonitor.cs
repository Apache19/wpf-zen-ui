// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Wpf.Zen.Ui.VirtualInput.Infrastructure;

/// <summary>
/// Observes dispatcher-wide TSF input-method changes independently of candidate UI.
/// </summary>
/// <remarks>
/// Each native callback reports only the state category that TSF has completed changing. This
/// preserves the ordering of independent TSF sources without constructing a mixed snapshot.
/// </remarks>
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class InputMethodMonitor
    : ISoftKeyboardTsfInputProcessorProfileActivationSink,
      ISoftKeyboardTsfCompartmentEventSink,
      ISoftKeyboardTsfLangBarItemSink,
      IDisposable
{
    private const uint ActiveProfileFlag     = 0x00000001;
    private const uint LanguageBarIconFlag   = 0x00000001;
    private const uint LanguageBarStatusFlag = 0x00010000;

    private static readonly Guid ProfileActivationSinkId = new("71C6E74E-0F28-11D8-A82A-00065B84435C");
    private static readonly Guid CompartmentEventSinkId  = new("743ABD5F-F26D-48DF-8CC5-238492419B64");
    private static readonly Guid KeyboardOpenId          = new("58273AAD-01BB-4164-95C6-755BA0B5162D");
    private static readonly Guid KeyboardConversionId    = new("CCF05DD8-4A87-11D7-A6E2-00065B84435C");
    private static readonly Guid InputModeItemId         = new("2C77A81E-41CC-4178-A3A7-5F8A987568E6");

    private readonly Dispatcher _dispatcher;
    private readonly Action<InputMethodChange> _changed;

    private ITfSource?   _source;
    private ITfSource?   _keyboardOpenSource;
    private ITfSource?   _keyboardConversionSource;
    private IntPtr       _profileActivationSinkPointer;
    private IntPtr       _compartmentSinkPointer;
    private IntPtr       _languageBarItemSinkPointer;
    private ITfLangBarItemMgr? _languageBarItemManager;
    private uint         _profileActivationCookie;
    private uint         _keyboardOpenCookie;
    private uint         _keyboardConversionCookie;
    private uint         _languageBarItemCookie;
    private bool         _disposed;

    internal InputMethodMonitor(Dispatcher dispatcher, Action<InputMethodChange> changed)
    {
        _dispatcher  = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _changed     = changed ?? throw new ArgumentNullException(nameof(changed));

        _dispatcher.ShutdownStarted += OnDispatcherShutdownStarted;
        EnsureNotifications();
    }

    /// <summary>
    /// Gets whether the profile and input-mode TSF sinks are connected.
    /// </summary>
    internal bool HasReliableNotifications => _source is not null
        && _keyboardOpenSource is not null
        && _keyboardConversionSource is not null;

    /// <summary>
    /// Connects the TSF notification sinks after WPF has created its text-services host.
    /// </summary>
    internal void EnsureNotifications()
    {
        _dispatcher.VerifyAccess();

        if (_disposed)
        {
            return;
        }

        if (_source is not null)
        {
            ConnectCompartmentNotifications();
            ConnectLanguageBarItemNotifications();
            return;
        }

        var source = WpfTextServicesAccessor.TryGetThreadManager() as ITfSource;
        if (source is null)
        {
            return;
        }

        var profileActivationPointer = IntPtr.Zero;
        try
        {
            profileActivationPointer = Marshal.GetComInterfaceForObject(
                this,
                typeof(ISoftKeyboardTsfInputProcessorProfileActivationSink)
            );

            var profileActivationSinkId = ProfileActivationSinkId;
            if (source.AdviseSink(
                ref profileActivationSinkId,
                profileActivationPointer,
                out var profileActivationCookie
            ) < 0)
            {
                return;
            }

            _source                       = source;
            _profileActivationSinkPointer = profileActivationPointer;
            _profileActivationCookie      = profileActivationCookie;
            profileActivationPointer      = IntPtr.Zero;

            ConnectCompartmentNotifications();
            ConnectLanguageBarItemNotifications();
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            // A later focus or keyboard registration retries after WPF initializes TSF.
        }
        finally
        {
            if (profileActivationPointer != IntPtr.Zero)
            {
                _ = Marshal.Release(profileActivationPointer);
            }
        }
    }

    int ISoftKeyboardTsfInputProcessorProfileActivationSink.OnActivated(
        uint profileType,
        ushort languageId,
        ref Guid classId,
        ref Guid categoryId,
        ref Guid profileId,
        IntPtr keyboardLayout,
        uint flags)
    {
        Debug.WriteLine(
            "[SoftKeyboard.InputMode.Monitor] Profile activation observed; "
            + $"profileType={profileType}, languageId=0x{languageId:X4}, "
            + $"classId={classId}, categoryId={categoryId}, profileId={profileId}, "
            + $"keyboardLayout=0x{keyboardLayout.ToInt64():X}, flags=0x{flags:X8}, "
            + $"active={(flags & ActiveProfileFlag) != 0}."
        );

        Publish(
            new InputMethodChange(
                InputMethodChangeKind.Profile,
                new InputProcessorProfileChange(
                    profileType,
                    languageId,
                    classId,
                    categoryId,
                    profileId,
                    keyboardLayout,
                    (flags & ActiveProfileFlag) != 0
                )
            )
        );

        return 0;
    }

    int ISoftKeyboardTsfLangBarItemSink.OnUpdate(uint flags)
    {
        var relevantFlags = flags & (LanguageBarIconFlag | LanguageBarStatusFlag);

        Debug.WriteLine(
            "[SoftKeyboard.InputMode.Monitor] Language-bar item update observed; "
            + $"flags=0x{flags:X8}, iconChanged={(flags & LanguageBarIconFlag) != 0}, "
            + $"statusChanged={(flags & LanguageBarStatusFlag) != 0}, "
            + $"published={relevantFlags != 0}."
        );

        if (relevantFlags != 0)
        {
            Publish(
                new InputMethodChange(InputMethodChangeKind.InputModePresentation)
            );
        }

        return 0;
    }

    int ISoftKeyboardTsfCompartmentEventSink.OnChange(ref Guid compartmentId)
    {
        if (compartmentId == KeyboardOpenId)
        {
            var openValue = SoftKeyboardTsfInputMode.GetKeyboardOpenValue();

            Debug.WriteLine(
                "[SoftKeyboard.InputMode.Monitor] TSF OPEN/CLOSE compartment notification; "
                + "compartment=GUID_COMPARTMENT_KEYBOARD_OPENCLOSE, "
                + $"openValue={FormatValue(openValue)}, "
                + $"published={openValue is not null}."
            );

            if (openValue is null)
            {
                return 0;
            }

            Publish(
                new InputMethodChange(InputMethodChangeKind.ImeOpen, StateValue: openValue.Value)
            );
        }
        else if (compartmentId == KeyboardConversionId)
        {
            var conversionValue = SoftKeyboardTsfInputMode.GetKeyboardConversionValue();

            Debug.WriteLine(
                "[SoftKeyboard.InputMode.Monitor] TSF CONVERSION compartment notification; "
                + "compartment=GUID_COMPARTMENT_KEYBOARD_INPUTMODE_CONVERSION, "
                + $"conversionValue={FormatValue(conversionValue)}, "
                + $"published={conversionValue is not null}."
            );

            if (conversionValue is null)
            {
                return 0;
            }

            Publish(
                new InputMethodChange(
                    InputMethodChangeKind.ImeConversion,
                    StateValue: conversionValue.Value
                )
            );
        }
        else
        {
            Debug.WriteLine(
                "[SoftKeyboard.InputMode.Monitor] Untracked compartment change ignored; "
                + $"compartmentId={compartmentId}."
            );
        }

        return 0;
    }

    /// <summary>
    /// Formats a monitored TSF integer as both its decimal value and native hexadecimal bit field.
    /// </summary>
    private static string FormatValue(int? value)
    {
        return value is { } integer ? $"{integer} (0x{integer:X8})" : "<unavailable>";
    }

    private void Publish(InputMethodChange change)
    {
        _changed(change);
    }

    private void DisconnectProfileNotifications()
    {
        DisconnectLanguageBarItemNotifications();
        DisconnectCompartmentNotifications();

        if (_source is not null)
        {
            try
            {
                _ = _source.UnadviseSink(_profileActivationCookie);
            }
            catch (Exception exception) when (IsUnavailable(exception))
            {
                // WPF can release its TSF host before Dispatcher shutdown reaches this monitor.
            }
        }

        _profileActivationCookie = 0;
        _source = null;

        if (_profileActivationSinkPointer != IntPtr.Zero)
        {
            _ = Marshal.Release(_profileActivationSinkPointer);
            _profileActivationSinkPointer = IntPtr.Zero;
        }
    }

    /// <summary>
    /// Subscribes directly to the TSF compartments that own input-mode state.
    /// </summary>
    private void ConnectCompartmentNotifications()
    {
        if ((_keyboardOpenSource is not null && _keyboardConversionSource is not null)
            || WpfTextServicesAccessor.TryGetThreadManager() is not ITfCompartmentMgr manager)
        {
            return;
        }

        try
        {
            if (_compartmentSinkPointer == IntPtr.Zero)
            {
                _compartmentSinkPointer = Marshal.GetComInterfaceForObject(
                    this,
                    typeof(ISoftKeyboardTsfCompartmentEventSink)
                );
            }

            if (_keyboardOpenSource is null
                && TryConnectCompartment(
                    manager,
                    KeyboardOpenId,
                    _compartmentSinkPointer,
                    out var openSource,
                    out var openCookie
                ))
            {
                _keyboardOpenSource = openSource;
                _keyboardOpenCookie = openCookie;
            }

            if (_keyboardConversionSource is null
                && TryConnectCompartment(
                    manager,
                    KeyboardConversionId,
                    _compartmentSinkPointer,
                    out var conversionSource,
                    out var conversionCookie
                ))
            {
                _keyboardConversionSource = conversionSource;
                _keyboardConversionCookie = conversionCookie;
            }
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            // The owning dispatcher can expose the thread manager before its compartments.
        }

        if (_keyboardOpenSource is null
            && _keyboardConversionSource is null
            && _compartmentSinkPointer != IntPtr.Zero)
        {
            _ = Marshal.Release(_compartmentSinkPointer);
            _compartmentSinkPointer = IntPtr.Zero;
        }
    }

    private static bool TryConnectCompartment(
        ITfCompartmentMgr manager,
        Guid compartmentId,
        IntPtr sinkPointer,
        out ITfSource? source,
        out uint cookie)
    {
        source = null;
        cookie = 0;

        if (manager.GetCompartment(in compartmentId, out var compartment) < 0
            || compartment is not ITfSource compartmentSource)
        {
            return false;
        }

        var sinkId = CompartmentEventSinkId;
        if (compartmentSource.AdviseSink(ref sinkId, sinkPointer, out cookie) < 0)
        {
            cookie = 0;
            return false;
        }

        source = compartmentSource;
        return true;
    }

    private void DisconnectCompartmentNotifications()
    {
        DisconnectCompartment(ref _keyboardOpenSource, ref _keyboardOpenCookie);
        DisconnectCompartment(ref _keyboardConversionSource, ref _keyboardConversionCookie);

        if (_compartmentSinkPointer != IntPtr.Zero)
        {
            _ = Marshal.Release(_compartmentSinkPointer);
            _compartmentSinkPointer = IntPtr.Zero;
        }
    }

    private static void DisconnectCompartment(ref ITfSource? source, ref uint cookie)
    {
        if (source is not null)
        {
            try
            {
                _ = source.UnadviseSink(cookie);
            }
            catch (Exception exception) when (IsUnavailable(exception))
            {
                // TSF can release a compartment before Dispatcher shutdown.
            }
        }

        cookie = 0;
        source = null;
    }

    /// <summary>
    /// Subscribes to presentation changes of the standard input-mode language-bar item.
    /// </summary>
    private void ConnectLanguageBarItemNotifications()
    {
        if (_languageBarItemManager is not null
            || WpfTextServicesAccessor.TryGetThreadManager() is not ITfLangBarItemMgr manager)
        {
            return;
        }

        var sinkPointer = IntPtr.Zero;
        try
        {
            sinkPointer = Marshal.GetComInterfaceForObject(
                this,
                typeof(ISoftKeyboardTsfLangBarItemSink)
            );

            if (manager.AdviseItemSink(
                sinkPointer,
                out var cookie,
                in InputModeItemId
            ) < 0)
            {
                return;
            }

            _languageBarItemManager = manager;
            _languageBarItemSinkPointer = sinkPointer;
            _languageBarItemCookie = cookie;
            sinkPointer = IntPtr.Zero;
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            // A profile callback or focus change can retry after the item manager is ready.
        }
        finally
        {
            if (sinkPointer != IntPtr.Zero)
            {
                _ = Marshal.Release(sinkPointer);
            }
        }
    }

    private void DisconnectLanguageBarItemNotifications()
    {
        if (_languageBarItemManager is not null)
        {
            try
            {
                _ = _languageBarItemManager.UnadviseItemSink(_languageBarItemCookie);
            }
            catch (Exception exception) when (IsUnavailable(exception))
            {
                // TSF can release its item manager before Dispatcher shutdown.
            }
        }

        _languageBarItemCookie  = 0;
        _languageBarItemManager = null;

        if (_languageBarItemSinkPointer != IntPtr.Zero)
        {
            _ = Marshal.Release(_languageBarItemSinkPointer);
            _languageBarItemSinkPointer = IntPtr.Zero;
        }
    }

    private void OnDispatcherShutdownStarted(object? sender, EventArgs eventArgs)
    {
        Dispose();
    }

    /// <summary>
    /// Releases all TSF subscriptions. Repeated calls are safe.
    /// </summary>
    public void Dispose()
    {
        _dispatcher.VerifyAccess();

        if (_disposed)
        {
            return;
        }

        _disposed = true;

        DisconnectProfileNotifications();
        _dispatcher.ShutdownStarted -= OnDispatcherShutdownStarted;
    }

    private static bool IsUnavailable(Exception exception)
    {
        return exception is COMException
            or InvalidCastException
            or InvalidOperationException
            or PlatformNotSupportedException
            or TypeLoadException;
    }
}
