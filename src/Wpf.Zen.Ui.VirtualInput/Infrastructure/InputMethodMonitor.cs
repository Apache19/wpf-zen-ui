// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Threading;

namespace Wpf.Zen.Ui.VirtualInput.Infrastructure;

/// <summary>
/// Observes dispatcher-wide WPF and TSF input-method changes independently of candidate UI.
/// </summary>
/// <remarks>
/// Every callback reports its native state category. Consumers can then read and publish only the
/// state owned by that category while preserving callback order.
/// </remarks>
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class InputMethodMonitor
    : ISoftKeyboardTsfInputProcessorProfileActivationSink,
      ISoftKeyboardTsfActiveLanguageProfileNotifySink,
      ISoftKeyboardTsfLangBarItemSink,
      IDisposable
{
    private const uint ActiveProfileFlag = 0x00000001;

    private static readonly Guid ProfileActivationSinkId =
        new("71C6E74E-0F28-11D8-A82A-00065B84435C");

    private static readonly Guid ActiveLanguageProfileSinkId =
        new("B246CB75-A93E-4652-BF8C-B3FE0CFD7E57");

    private static readonly Guid InputModeItemId =
        new("2C77A81E-41CC-4178-A3A7-5F8A987568E6");

    private readonly Dispatcher _dispatcher;
    private readonly Action<InputMethodChange> _changed;

    private InputMethod? _inputMethod;
    private ITfSource?   _source;
    private IntPtr       _profileActivationSinkPointer;
    private IntPtr       _activeLanguageProfileSinkPointer;
    private IntPtr       _languageBarItemSinkPointer;
    private ITfLangBarItemMgr? _languageBarItemManager;
    private uint         _profileActivationCookie;
    private uint         _activeLanguageProfileCookie;
    private uint         _languageBarItemCookie;
    private bool         _disposed;

    internal InputMethodMonitor(Dispatcher dispatcher, Action<InputMethodChange> changed)
    {
        _dispatcher  = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _changed     = changed ?? throw new ArgumentNullException(nameof(changed));

        InputLanguageManager.Current.InputLanguageChanged += OnInputLanguageChanged;

        _inputMethod = InputMethod.Current;
        if (_inputMethod is not null)
        {
            _inputMethod.StateChanged += OnInputMethodStateChanged;
        }

        _dispatcher.ShutdownStarted += OnDispatcherShutdownStarted;
        EnsureProfileNotifications();
    }

    /// <summary>
    /// Gets whether WPF compartment notifications and the TSF profile sink are both connected.
    /// </summary>
    internal bool HasReliableNotifications => _inputMethod is not null && _source is not null;

    /// <summary>
    /// Retries the TSF profile subscription after WPF has created its text-services host.
    /// </summary>
    internal void EnsureProfileNotifications()
    {
        _dispatcher.VerifyAccess();

        if (_disposed)
        {
            return;
        }

        if (_source is not null)
        {
            ConnectLanguageBarItemNotifications();
            return;
        }

        var source = WpfTextServicesAccessor.TryGetThreadManager() as ITfSource;
        if (source is null)
        {
            return;
        }

        var profileActivationPointer = IntPtr.Zero;
        var activeLanguagePointer = IntPtr.Zero;
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

            activeLanguagePointer = Marshal.GetComInterfaceForObject(
                this,
                typeof(ISoftKeyboardTsfActiveLanguageProfileNotifySink)
            );

            var activeLanguageProfileSinkId = ActiveLanguageProfileSinkId;
            if (source.AdviseSink(
                ref activeLanguageProfileSinkId,
                activeLanguagePointer,
                out var activeLanguageProfileCookie
            ) < 0)
            {
                _ = source.UnadviseSink(profileActivationCookie);
                return;
            }

            _source                           = source;
            _profileActivationSinkPointer     = profileActivationPointer;
            _activeLanguageProfileSinkPointer = activeLanguagePointer;
            _profileActivationCookie          = profileActivationCookie;
            _activeLanguageProfileCookie      = activeLanguageProfileCookie;
            profileActivationPointer          = IntPtr.Zero;
            activeLanguagePointer             = IntPtr.Zero;

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

            if (activeLanguagePointer != IntPtr.Zero)
            {
                _ = Marshal.Release(activeLanguagePointer);
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
        Publish(new InputMethodChange(InputMethodChangeKind.InputModePresentation));
        return 0;
    }

    int ISoftKeyboardTsfActiveLanguageProfileNotifySink.OnActivated(
        ref Guid classId,
        ref Guid profileId,
        int activated)
    {
        Publish(
            new InputMethodChange(
                InputMethodChangeKind.Profile,
                new InputProcessorProfileChange(
                    0,
                    0,
                    classId,
                    Guid.Empty,
                    profileId,
                    IntPtr.Zero,
                    activated != 0
                )
            )
        );

        return 0;
    }

    private void OnInputLanguageChanged(object sender, InputLanguageEventArgs eventArgs)
    {
        Publish(
            new InputMethodChange(
                InputMethodChangeKind.InputLanguage,
                KeyboardLayout: Layouts.Internal.KeyboardLayoutLabels.GetCurrentLayout()
            )
        );
    }

    private void OnInputMethodStateChanged(object sender, InputMethodStateChangedEventArgs eventArgs)
    {
        if (eventArgs.IsImeStateChanged
            && SoftKeyboardTsfInputMode.GetKeyboardOpenValue() is { } openValue)
        {
            Publish(
                new InputMethodChange(
                    InputMethodChangeKind.ImeOpen,
                    StateValue: openValue
                )
            );
        }

        if (eventArgs.IsImeConversionModeChanged
            && SoftKeyboardTsfInputMode.GetKeyboardConversionValue() is { } conversionValue)
        {
            Publish(
                new InputMethodChange(
                    InputMethodChangeKind.ImeConversion,
                    StateValue: conversionValue
                )
            );
        }
    }

    private void Publish(InputMethodChange change)
    {
        _changed(change);
    }

    private void DisconnectProfileNotifications()
    {
        DisconnectLanguageBarItemNotifications();

        if (_source is not null && _profileActivationCookie != 0)
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

        if (_source is not null && _activeLanguageProfileCookie != 0)
        {
            try
            {
                _ = _source.UnadviseSink(_activeLanguageProfileCookie);
            }
            catch (Exception exception) when (IsUnavailable(exception))
            {
                // WPF can release its TSF host before Dispatcher shutdown reaches this monitor.
            }
        }

        _profileActivationCookie     = 0;
        _activeLanguageProfileCookie = 0;
        _source = null;

        if (_profileActivationSinkPointer != IntPtr.Zero)
        {
            _ = Marshal.Release(_profileActivationSinkPointer);
            _profileActivationSinkPointer = IntPtr.Zero;
        }

        if (_activeLanguageProfileSinkPointer != IntPtr.Zero)
        {
            _ = Marshal.Release(_activeLanguageProfileSinkPointer);
            _activeLanguageProfileSinkPointer = IntPtr.Zero;
        }
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
        if (_languageBarItemManager is not null && _languageBarItemCookie != 0)
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

        _languageBarItemCookie = 0;
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
    /// Releases all WPF and TSF subscriptions. Repeated calls are safe.
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

        if (_inputMethod is not null)
        {
            _inputMethod.StateChanged -= OnInputMethodStateChanged;
            _inputMethod              =  null;
        }

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
