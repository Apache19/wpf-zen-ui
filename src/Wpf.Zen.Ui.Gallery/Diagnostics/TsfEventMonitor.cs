// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

namespace Wpf.Zen.Ui.Gallery.Diagnostics;

/// <summary>
/// Observes the existing WPF dispatcher TSF host for manual input-method experiments.
/// </summary>
/// <remarks>
/// Compartment values are read at the native callback boundary; UI delivery is deferred to
/// avoid reentering layout from COM. Notifications cover this dispatcher, including other
/// windows on it. UI-element events depend on the active text service's implementation.
/// </remarks>
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class TsfEventMonitor : ISoftKeyboardTsfInputProcessorProfileActivationSink,
    ISoftKeyboardTsfCompartmentEventSink, ISoftKeyboardTsfUIElementSink,
    ISoftKeyboardTsfLangBarItemSink, IDisposable
{
    private static readonly Guid OpenId = new("58273AAD-01BB-4164-95C6-755BA0B5162D");
    private static readonly Guid ConversionId = new("CCF05DD8-4A87-11D7-A6E2-00065B84435C");
    private static readonly Guid InputModeItemId = new("2C77A81E-41CC-4178-A3A7-5F8A987568E6");
    private readonly Dispatcher _dispatcher;
    private readonly Dictionary<string, (ITfSource Source, uint Cookie)> _subscriptions = [];
    private readonly Dictionary<Guid, ITfCompartment> _compartments = [];
    private readonly Queue<string> _pendingRecords = new();
    private ITfLangBarItemMgr? _languageBarItemManager;
    private uint _languageBarItemCookie;
    private bool _deliveryPending;
    private bool _disposed;
    private bool _isEnabled = true;
    private long _sequence;
    private string? _lastConnectionStatus;

    /// <summary>
    /// Creates an observer; call EnsureConnected after WPF has focused an editor.
    /// </summary>
    internal TsfEventMonitor(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _dispatcher.ShutdownStarted += OnShutdownStarted;
    }

    /// <summary>
    /// Raised on the owning dispatcher for each timestamped diagnostic record.
    /// </summary>
    internal event EventHandler<string>? RecordCaptured;

    /// <summary>
    /// Gets or sets whether new records are retained. Subscriptions remain active while paused.
    /// </summary>
    internal bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _dispatcher.VerifyAccess();
            _isEnabled = value;
            if (!value)
            {
                _pendingRecords.Clear();
            }
        }
    }

    /// <summary>
    /// Retries missing subscriptions without activating or replacing WPF's thread manager.
    /// Each source is independent so an unsupported sink does not disable the other events.
    /// </summary>
    internal void EnsureConnected()
    {
        _dispatcher.VerifyAccess();
        if (_disposed)
        {
            return;
        }

        var manager = WpfTextServicesAccessor.TryGetThreadManager();
        if (manager is ITfSource source)
        {
            Subscribe("Profile", source, typeof(ISoftKeyboardTsfInputProcessorProfileActivationSink));
            Subscribe("UIElement", source, typeof(ISoftKeyboardTsfUIElementSink));
        }

        if (manager is ITfCompartmentMgr compartments)
        {
            ConnectCompartment(compartments, OpenId, "OpenClose");
            ConnectCompartment(compartments, ConversionId, "Conversion");
        }

        if (manager is ITfLangBarItemMgr languageBarItems)
        {
            ConnectLanguageBarItem(languageBarItems);
        }

        var status = $"hostAvailable={manager is not null}, "
            + $"profile={_subscriptions.ContainsKey("Profile")}, "
            + $"uiElement={_subscriptions.ContainsKey("UIElement")}, "
            + $"openClose={_subscriptions.ContainsKey("OpenClose")}, "
            + $"conversion={_subscriptions.ContainsKey("Conversion")}, "
            + $"inputModeItem={_languageBarItemManager is not null}";
        if (status != _lastConnectionStatus)
        {
            _lastConnectionStatus = status;
            Record("SUBSCRIPTION state", status);
        }
    }

    /// <summary>
    /// Reads raw TSF compartment values and reports the current subscription state.
    /// </summary>
    internal void CaptureSnapshot()
    {
        EnsureConnected();
        Record("SNAPSHOT", $"{_lastConnectionStatus}, "
            + $"openClose={ReadValue(OpenId)}, conversion={ReadValue(ConversionId)}");
    }

    /// <summary>
    /// Removes queued records when the user clears the visible log.
    /// </summary>
    internal void ClearPendingRecords()
    {
        _dispatcher.VerifyAccess();
        _pendingRecords.Clear();
    }

    /// <summary>
    /// Borrows a compartment from WPF and subscribes to its value-change notifications.
    /// </summary>
    private void ConnectCompartment(ITfCompartmentMgr manager, Guid id, string name)
    {
        if (_subscriptions.ContainsKey(name))
        {
            return;
        }

        try
        {
            var result = manager.GetCompartment(in id, out var compartment);
            if (result >= 0 && compartment is ITfSource source)
            {
                _compartments[id] = compartment;
                Subscribe(name, source, typeof(ISoftKeyboardTsfCompartmentEventSink));
            }
            else
            {
                Record("COMPARTMENT unavailable", $"name={name}, hresult=0x{result:X8}");
            }
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            Record("COMPARTMENT failed", $"name={name}, hresult=0x{exception.HResult:X8}");
        }
    }

    /// <summary>
    /// Retains each successful source/cookie pair and balances the temporary COM pointer.
    /// </summary>
    private void Subscribe(string name, ITfSource source, Type sinkType)
    {
        if (_subscriptions.ContainsKey(name))
        {
            return;
        }

        var pointer = IntPtr.Zero;
        try
        {
            pointer = Marshal.GetComInterfaceForObject(this, sinkType);
            var id = sinkType.GUID;
            var result = source.AdviseSink(ref id, pointer, out var cookie);
            if (result >= 0)
            {
                _subscriptions.Add(name, (source, cookie));
            }

            Record("SUBSCRIBE completed", $"sink={name}, hresult=0x{result:X8}");
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            Record("SUBSCRIBE failed", $"sink={name}, hresult=0x{exception.HResult:X8}");
        }
        finally
        {
            if (pointer != IntPtr.Zero)
            {
                _ = Marshal.Release(pointer);
            }
        }
    }

    /// <summary>
    /// Subscribes to the standard input-mode item by GUID through WPF's language-bar manager.
    /// A failed subscription is retried on the next focus, profile notification, or snapshot.
    /// </summary>
    private void ConnectLanguageBarItem(ITfLangBarItemMgr manager)
    {
        if (_languageBarItemManager is not null)
        {
            return;
        }

        var pointer = IntPtr.Zero;
        try
        {
            pointer = Marshal.GetComInterfaceForObject(this, typeof(ISoftKeyboardTsfLangBarItemSink));
            var result = manager.AdviseItemSink(pointer, out var cookie, in InputModeItemId);
            if (result >= 0)
            {
                _languageBarItemManager = manager;
                _languageBarItemCookie = cookie;
            }

            Record("SUBSCRIBE completed", "sink=LangBarItem, item=GUID_LBI_INPUTMODE, "
                + $"hresult=0x{result:X8}");
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            Record("SUBSCRIBE failed", "sink=LangBarItem, item=GUID_LBI_INPUTMODE, "
                + $"hresult=0x{exception.HResult:X8}");
        }
        finally
        {
            if (pointer != IntPtr.Zero)
            {
                _ = Marshal.Release(pointer);
            }
        }
    }

    /// <summary>
    /// Records every update, including repeated or unknown flags, at the COM callback boundary.
    /// Compartment values are observations at this instant, not values carried by OnUpdate.
    /// </summary>
    int ISoftKeyboardTsfLangBarItemSink.OnUpdate(uint flags)
    {
        Record("LANGBAR UPDATE", $"item=GUID_LBI_INPUTMODE, flags=0x{flags:X8}, "
            + $"iconChanged={(flags & 0x1) != 0}, textChanged={(flags & 0x2) != 0}, "
            + $"tooltipChanged={(flags & 0x4) != 0}, bitmapChanged={(flags & 0x8) != 0}, "
            + $"balloonChanged={(flags & 0x10) != 0}, statusChanged={(flags & 0x10000) != 0}, "
            + $"unknownFlags=0x{flags & ~0x1001Fu:X8}, "
            + $"openClose={ReadValue(OpenId)}, conversion={ReadValue(ConversionId)}");
        return 0;
    }

    int ISoftKeyboardTsfInputProcessorProfileActivationSink.OnActivated(
        uint profileType, ushort languageId, ref Guid classId, ref Guid categoryId,
        ref Guid profileId, IntPtr keyboardLayout, uint flags)
    {
        Record("PROFILE activation", $"profileType={profileType}, languageId=0x{languageId:X4}, "
            + $"classId={classId}, categoryId={categoryId}, profileId={profileId}, "
            + $"keyboardLayout=0x{keyboardLayout.ToInt64():X}, flags=0x{flags:X8}, "
            + $"active={(flags & 1) != 0}");
        // Let profile activation finish before retrying an unavailable item subscription.
        if (!_disposed && _languageBarItemManager is null && !_dispatcher.HasShutdownStarted)
        {
            _ = _dispatcher.BeginInvoke(new Action(EnsureConnected));
        }

        return 0;
    }

    int ISoftKeyboardTsfCompartmentEventSink.OnChange(ref Guid compartmentId)
    {
        var name = compartmentId == OpenId ? "OPEN/CLOSE" : "CONVERSION";
        Record("COMPARTMENT changed", $"name={name}, compartmentId={compartmentId}, "
            + $"value={ReadValue(compartmentId)}");
        return 0;
    }

    int ISoftKeyboardTsfUIElementSink.BeginUIElement(uint elementId, ref int show)
    {
        // Preserve the presentation decision supplied by TSF and any preceding sink.
        Record("UI BEGIN", $"elementId={elementId}, show={show}");
        return 0;
    }

    int ISoftKeyboardTsfUIElementSink.UpdateUIElement(uint elementId)
    {
        Record("UI UPDATE", $"elementId={elementId}");
        return 0;
    }

    int ISoftKeyboardTsfUIElementSink.EndUIElement(uint elementId)
    {
        Record("UI END", $"elementId={elementId}");
        return 0;
    }

    /// <summary>
    /// Preserves raw integers and distinguishes absent values from COM read failures.
    /// </summary>
    private string ReadValue(Guid id)
    {
        try
        {
            if (_compartments.TryGetValue(id, out var compartment))
            {
                var result = compartment.GetValue(out var value);
                if (result < 0)
                {
                    return $"<failed:0x{result:X8}>";
                }

                return value is int integer ? $"{integer} (0x{integer:X8})"
                    : value?.ToString() ?? "<empty>";
            }
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            return $"<failed:0x{exception.HResult:X8}>";
        }

        return "<unavailable>";
    }

    /// <summary>
    /// Captures callback ordering immediately and bounds the queue during rapid notifications.
    /// </summary>
    private void Record(string message, string properties)
    {
        if (_disposed || !IsEnabled || _dispatcher.HasShutdownStarted)
        {
            return;
        }

        var record = $"{DateTime.Now:HH:mm:ss.fff} #{++_sequence:D6} "
            + $"[Sandbox.TSF] {message}; {properties}.";
        Debug.WriteLine(record);
        _pendingRecords.Enqueue(record);
        while (_pendingRecords.Count > 2000)
        {
            _pendingRecords.Dequeue();
        }

        if (!_deliveryPending)
        {
            _deliveryPending = true;
            _ = _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(DeliverRecords));
        }
    }

    /// <summary>
    /// Delivers a bounded batch outside the native callback stack.
    /// </summary>
    private void DeliverRecords()
    {
        _deliveryPending = false;
        while (!_disposed && _pendingRecords.Count > 0)
        {
            var record = _pendingRecords.Dequeue();
            RecordCaptured?.Invoke(this, record);
        }
    }

    private void OnShutdownStarted(object? sender, EventArgs e)
    {
        Dispose();
    }

    /// <summary>
    /// Disconnects native sinks on their owning dispatcher. Repeated calls are safe.
    /// </summary>
    public void Dispose()
    {
        _dispatcher.VerifyAccess();
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _dispatcher.ShutdownStarted -= OnShutdownStarted;
        _pendingRecords.Clear();
        if (_languageBarItemManager is not null)
        {
            try
            {
                var result = _languageBarItemManager.UnadviseItemSink(_languageBarItemCookie);
                Debug.WriteLine("[Sandbox.TSF] UNSUBSCRIBE completed; "
                    + $"sink=LangBarItem, item=GUID_LBI_INPUTMODE, hresult=0x{result:X8}.");
            }
            catch (Exception exception) when (IsUnavailable(exception))
            {
                Debug.WriteLine("[Sandbox.TSF] UNSUBSCRIBE failed; "
                    + $"sink=LangBarItem, hresult=0x{exception.HResult:X8}.");
            }

            _languageBarItemManager = null;
            _languageBarItemCookie = 0;
        }

        foreach (var subscription in _subscriptions.Values)
        {
            try
            {
                _ = subscription.Source.UnadviseSink(subscription.Cookie);
            }
            catch (Exception exception) when (IsUnavailable(exception))
            {
                Debug.WriteLine("[Sandbox.TSF] UNSUBSCRIBE failed; "
                    + $"hresult=0x{exception.HResult:X8}.");
            }
        }

        _subscriptions.Clear();
        // These RCWs are shared with WPF; only the observer's references are relinquished.
        _compartments.Clear();
    }

    private static bool IsUnavailable(Exception exception)
    {
        return exception is COMException or InvalidCastException or InvalidOperationException
            or NotSupportedException or TypeLoadException;
    }
}
