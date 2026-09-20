// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.Ime;

namespace Wpf.Zen.Ui.VirtualInput.Infrastructure;

/// <summary>
/// Observes Dispatcher-wide TSF changes and supplemental IME messages for the focused HWND.
/// </summary>
/// <remarks>
/// Each native callback reads its own subsystem at the notification boundary and publishes
/// one shared open/conversion pair. Existing observations never suppress a native read.
/// Presentation, duplicate-value detection and scheduling run through the observation coordinator.
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

    private readonly Dispatcher                _dispatcher;
    private readonly Action<InputMethodChange> _changed;

    private ITfSource?         _source;
    private ITfSource?         _keyboardOpenSource;
    private ITfSource?         _keyboardConversionSource;
    private IntPtr             _profileActivationSinkPointer;
    private IntPtr             _compartmentSinkPointer;
    private IntPtr             _languageBarItemSinkPointer;
    private ITfLangBarItemMgr? _languageBarItemManager;
    private uint               _profileActivationCookie;
    private uint               _keyboardOpenCookie;
    private uint               _keyboardConversionCookie;
    private uint               _languageBarItemCookie;
    private bool               _disposed;
    private HwndSource?        _windowSource;

    internal InputMethodMonitor(Dispatcher dispatcher, Action<InputMethodChange> changed)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _changed    = changed    ?? throw new ArgumentNullException(nameof(changed));

        InputLanguageManager.Current.InputLanguageChanged += OnInputLanguageChanged;
        _dispatcher.ShutdownStarted += OnDispatcherShutdownStarted;
        EnsureNotifications();
    }

    /// <summary>
    /// Supplements TSF profile notifications through WPF's independent language source.
    /// This source can notify before a WPF text-services host is available for sink registration.
    /// </summary>
    private void OnInputLanguageChanged(object sender, InputLanguageEventArgs args)
    {
        Debug.WriteLine(
            "[SoftKeyboard.InputMode.Monitor] WPF LANGUAGE changed; "
            + $"language={args.NewLanguage.Name}."
        );

        EnsureNotifications();
        Publish(new InputMethodChange(InputMethodChangeKind.RefreshAll));
    }

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

        ConnectWindowNotifications();

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
                out var profileActivationCookie) < 0)
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
        uint            profileType,
        ushort          languageId,
        ref Guid        classId,
        ref Guid        categoryId,
        ref Guid        profileId,
        IntPtr          keyboardLayout,
        uint            flags)
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
        var relevantFlags = flags & (LanguageBarIconFlag | LanguageBarStatusFlag | 0x2u | 0x4u);

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
        if (compartmentId == KeyboardOpenId || compartmentId == KeyboardConversionId)
        {
            var state = ReadTsfState();

            Debug.WriteLine(
                "[SoftKeyboard.InputMode.Monitor] TSF STATE pair observed; "
                + $"trigger={(compartmentId == KeyboardOpenId ? "OPEN" : "CONVERSION")}, "
                + $"open={FormatValue(state.OpenValue)}, conversion={FormatValue(state.ConversionValue)}."
            );

            Publish(
                new InputMethodChange(
                    compartmentId == KeyboardOpenId
                        ? InputMethodChangeKind.ImeOpen
                        : InputMethodChangeKind.ImeConversion,
                    State: state
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
    /// Follows the focused editor's HWND so supplementary IME messages belong to the same
    /// input context as the Dispatcher-wide TSF observations.
    /// </summary>
    private void ConnectWindowNotifications()
    {
        var source = Keyboard.FocusedElement is DependencyObject target
            ? PresentationSource.FromDependencyObject(target) as HwndSource
            : null;

        if (ReferenceEquals(source, _windowSource))
        {
            return;
        }

        DisconnectWindowNotifications();
        _windowSource = source;

        if (source is not null)
        {
            source.AddHook(WindowProcedure);
            source.Disposed += OnWindowDisposed;
        }
    }

    /// <summary>
    /// Captures IMM state for mode-change messages using the sending window's input context.
    /// These notification codes use wParam as the code and leave lParam unused.
    /// The hook leaves normal WPF and IME message processing intact.
    /// </summary>
    private IntPtr WindowProcedure(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int  wmImeNotify          = 0x0282;
        const long imnSetConversionMode = 0x0006;
        const long imnSetOpenStatus     = 0x0008;

        if (_disposed || message != wmImeNotify
            || wParam.ToInt64() is not (imnSetConversionMode or imnSetOpenStatus)
            || Keyboard.FocusedElement is not DependencyObject target
            || PresentationSource.FromDependencyObject(target) is not HwndSource source
            || source.Handle != window)
        {
            return IntPtr.Zero;
        }

        var open  = wParam.ToInt64() == imnSetOpenStatus;
        var state = ReadImmState(window);

        Debug.WriteLine(
            "[SoftKeyboard.InputMode.Monitor] IME MESSAGE observed; "
            + $"window=0x{window.ToInt64():X}, message=WM_IME_NOTIFY, "
            + $"notification={(open ? "IMN_SETOPENSTATUS" : "IMN_SETCONVERSIONMODE")}, "
            + $"open={FormatValue(state.OpenValue)}, conversion={FormatValue(state.ConversionValue)}."
        );

        Publish(
            new InputMethodChange(
                open ? InputMethodChangeKind.ImeOpen : InputMethodChangeKind.ImeConversion,
                State: state
            )
        );

        return IntPtr.Zero;
    }

    /// <summary>
    /// Borrows one HWND's IMM context and reads both state fields from that same context.
    /// Missing contexts and failed conversion queries remain unavailable; every borrowed
    /// context is released even if querying it fails.
    /// </summary>
    internal static unsafe SoftKeyboardInputObservation ReadImmState(IntPtr window)
    {
        if (window == IntPtr.Zero)
        {
            return new SoftKeyboardInputObservation(true, null, true, null);
        }

        var hwnd    = new HWND(window);
        var context = PInvoke.ImmGetContext(hwnd);

        if (context.IsNull)
        {
            return new SoftKeyboardInputObservation(true, null, true, null);
        }

        try
        {
            var open = PInvoke.ImmGetOpenStatus(context) ? 1 : 0;

            IME_CONVERSION_MODE conversion;
            IME_SENTENCE_MODE   sentence;

            int? conversionValue = PInvoke.ImmGetConversionStatus(context, &conversion, &sentence)
                ? unchecked((int)conversion)
                : null;

            return new SoftKeyboardInputObservation(true, open, true, conversionValue);
        }
        finally
        {
            _ = PInvoke.ImmReleaseContext(hwnd, context);
        }
    }

    /// <summary>
    /// Releases the old editor window when focus moves or the HWND is disposed.
    /// </summary>
    private void DisconnectWindowNotifications()
    {
        if (_windowSource is { } source)
        {
            _windowSource   =  null;
            source.Disposed -= OnWindowDisposed;
            source.RemoveHook(WindowProcedure);
        }
    }

    /// <summary>
    /// Drops the hook when its native window closes before the Dispatcher shuts down.
    /// </summary>
    private void OnWindowDisposed(object? sender, EventArgs args)
    {
        DisconnectWindowNotifications();
    }

    /// <summary>
    /// Samples both TSF compartments at a notification or an explicit settled fallback boundary.
    /// Each field can fail independently. The pair is published as a single observation update.
    /// </summary>
    internal static SoftKeyboardInputObservation ReadTsfState()
    {
        return new SoftKeyboardInputObservation(
            true, ReadCompartmentValue(open: true), true, ReadCompartmentValue(open: false)
        );
    }

    /// <summary>
    /// Reads one raw compartment on the owning Dispatcher, preserving failure as null.
    /// </summary>
    private static int? ReadCompartmentValue(bool open)
    {
        try
        {
            return open
                ? SoftKeyboardTsfInputMode.GetKeyboardOpenValue()
                : SoftKeyboardTsfInputMode.GetKeyboardConversionValue();
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            return null;
        }
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
        ITfCompartmentMgr        manager,
        Guid                     compartmentId,
        IntPtr                   sinkPointer,
        out ITfSource?           source,
        out uint                 cookie)
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

            _languageBarItemManager     = manager;
            _languageBarItemSinkPointer = sinkPointer;
            _languageBarItemCookie      = cookie;
            sinkPointer                 = IntPtr.Zero;
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
    /// Releases the focused window hook and all TSF subscriptions. Repeated calls are safe.
    /// </summary>
    public void Dispose()
    {
        _dispatcher.VerifyAccess();

        if (_disposed)
        {
            return;
        }

        _disposed = true;

        InputLanguageManager.Current.InputLanguageChanged -= OnInputLanguageChanged;

        DisconnectWindowNotifications();
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
