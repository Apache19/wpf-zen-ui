// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Interop;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.Ime;

namespace Wpf.Zen.Ui.Gallery.Diagnostics;

/// <summary>
/// Observes the input-language and IME messages delivered to one WPF top-level window.
/// </summary>
/// <remarks>
/// The monitor is a manual diagnostic aid. It never marks a message as handled and therefore does
/// not change WPF or IME message processing. Each relevant message is paired with the IMM state
/// that can be queried from the window at that instant. Activation-like messages also schedule
/// follow-up reads so experiments can distinguish a late state update from an absent notification.
/// </remarks>
internal sealed class ImeWindowMessageMonitor : IDisposable
{
    private const int WmSetFocus               = 0x0007;
    private const int WmKillFocus              = 0x0008;
    private const int WmInputLanguageRequest   = 0x0050;
    private const int WmInputLanguageChange    = 0x0051;
    private const int WmImeStartComposition    = 0x010D;
    private const int WmImeEndComposition      = 0x010E;
    private const int WmImeComposition         = 0x010F;
    private const int WmImeSetContext          = 0x0281;
    private const int WmImeNotify              = 0x0282;
    private const int WmImeControl             = 0x0283;
    private const int WmImeCompositionFull     = 0x0284;
    private const int WmImeSelect              = 0x0285;
    private const int WmImeChar                = 0x0286;
    private const int WmImeSystem              = 0x0287;
    private const int WmImeRequest             = 0x0288;
    private const int WmImeKeyDown             = 0x0290;
    private const int WmImeKeyUp               = 0x0291;

    private static readonly int[] FollowUpDelays = [10, 50, 100, 250, 500];

    private readonly HwndSource _source;
    private readonly CancellationTokenSource _disposeCancellation = new();
    private long _sequence;
    private bool _disposed;

    /// <summary>
    /// Initializes a monitor and attaches its passive hook to the supplied window source.
    /// </summary>
    /// <param name="source">The WPF source whose native messages will be observed.</param>
    internal ImeWindowMessageMonitor(HwndSource source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _source.AddHook(WindowProcedure);
        InputLanguageManager.Current.InputLanguageChanged += OnInputLanguageChanged;
    }

    /// <summary>
    /// Raised for each formatted message or state-sampling record.
    /// </summary>
    internal event EventHandler<string>? RecordCaptured;

    /// <summary>
    /// Gets or sets whether new diagnostic records are captured.
    /// </summary>
    internal bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Captures the current keyboard layout and IMM state without waiting for a window message.
    /// </summary>
    internal void CaptureSnapshot()
    {
        CaptureState("MANUAL", "requested snapshot");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _disposeCancellation.Cancel();
        _source.RemoveHook(WindowProcedure);
        InputLanguageManager.Current.InputLanguageChanged -= OnInputLanguageChanged;
        _disposeCancellation.Dispose();
    }

    /// <summary>
    /// Records selected window messages while allowing the normal WPF window procedure to continue.
    /// </summary>
    private IntPtr WindowProcedure(
        IntPtr window,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled
    )
    {
        var messageName = GetMessageName(message);
        if (!IsEnabled || messageName is null)
        {
            return IntPtr.Zero;
        }

        var startsFollowUps = StartsFollowUpSampling(message, wParam);
        var sequence = startsFollowUps ? Interlocked.Increment(ref _sequence) : 0;
        var samplingStopwatch = startsFollowUps ? Stopwatch.StartNew() : null;
        var detail = DescribeMessage(message, wParam, lParam);
        CaptureState(
            messageName,
            $"msg=0x{message:X4} wParam={FormatPointer(wParam)} "
                + $"lParam={FormatPointer(lParam)}{detail}"
                + (startsFollowUps ? $" sample=#{sequence}" : string.Empty)
        );

        if (startsFollowUps)
        {
            _source.Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(() => CaptureState(
                    "FOLLOW-UP",
                    $"source=#{sequence} {messageName}, after-dispatch, "
                        + $"elapsed={samplingStopwatch!.Elapsed.TotalMilliseconds:F1}ms"
                ))
            );
            _ = CaptureFollowUpsAsync(
                sequence,
                messageName,
                samplingStopwatch!,
                _disposeCancellation.Token
            );
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Samples state after activation-like messages at fixed offsets from message receipt.
    /// </summary>
    private async Task CaptureFollowUpsAsync(
        long sequence,
        string messageName,
        Stopwatch stopwatch,
        CancellationToken cancellationToken
    )
    {
        var previousDelay = 0;
        try
        {
            foreach (var delay in FollowUpDelays)
            {
                await Task.Delay(delay - previousDelay, cancellationToken);
                CaptureState(
                    "FOLLOW-UP",
                    $"source=#{sequence} {messageName}, target=+{delay}ms, "
                        + $"elapsed={stopwatch.Elapsed.TotalMilliseconds:F1}ms"
                );
                previousDelay = delay;
            }
        }
        catch (OperationCanceledException)
        {
            // Closing the sandbox ends all outstanding experimental samples.
        }
    }

    /// <summary>
    /// Reads the state associated with the window's current IMM context and emits one record.
    /// </summary>
    private void CaptureState(string category, string detail)
    {
        if (_disposed || !IsEnabled)
        {
            return;
        }

        var window = new HWND(_source.Handle);
        var layout = PInvoke.GetKeyboardLayout(0);
        var context = PInvoke.ImmGetContext(window);
        var state = DescribeImmState(context);

        if (!context.IsNull)
        {
            _ = PInvoke.ImmReleaseContext(window, context);
        }

        var line = $"{DateTime.Now:HH:mm:ss:fff} [{category}] {detail} | "
            + $"currentHKL={FormatPointer(layout)} lang={DescribeLanguage(layout)} | "
            + state;
        Debug.WriteLine(line);
        RecordCaptured?.Invoke(this, line);
    }

    /// <summary>
    /// Correlates WPF's input-language event with the native messages that caused it.
    /// </summary>
    private void OnInputLanguageChanged(object sender, InputLanguageEventArgs args)
    {
        CaptureState("WPF EVENT", $"InputLanguageChanged newLanguage={args.NewLanguage.Name}");
    }

    /// <summary>
    /// Formats the IMM compatibility state currently exposed for the monitored HWND.
    /// </summary>
    private static unsafe string DescribeImmState(HIMC context)
    {
        if (context.IsNull)
        {
            return "HIMC=null open=n/a conversion=n/a sentence=n/a";
        }

        var open = PInvoke.ImmGetOpenStatus(context);
        IME_CONVERSION_MODE conversion;
        IME_SENTENCE_MODE sentence;
        var statusAvailable = PInvoke.ImmGetConversionStatus(context, &conversion, &sentence);

        if (!statusAvailable)
        {
            return $"HIMC={FormatPointer(context)} open={(int)open} ({(bool)open}) "
                + "conversion=query-failed sentence=query-failed";
        }

        var conversionValue = unchecked((uint)conversion);
        var sentenceValue = unchecked((uint)sentence);
        return $"HIMC={FormatPointer(context)} open={(int)open} ({(bool)open}) "
            + $"conversion={conversionValue} (0x{conversionValue:X8}, "
            + $"{DescribeConversion(conversionValue)}) sentence={sentenceValue} "
            + $"(0x{sentenceValue:X8})";
    }

    private static bool StartsFollowUpSampling(int message, IntPtr wParam)
    {
        return message == WmInputLanguageRequest
            || message == WmInputLanguageChange
            || message == WmImeSelect
            || (message == WmImeNotify && (wParam.ToInt64() == 0x0006
                || wParam.ToInt64() == 0x0008));
    }

    private static string? GetMessageName(int message)
    {
        return message switch
        {
            WmSetFocus => "WM_SETFOCUS",
            WmKillFocus => "WM_KILLFOCUS",
            WmInputLanguageRequest => "WM_INPUTLANGCHANGEREQUEST",
            WmInputLanguageChange => "WM_INPUTLANGCHANGE",
            WmImeStartComposition => "WM_IME_STARTCOMPOSITION",
            WmImeEndComposition => "WM_IME_ENDCOMPOSITION",
            WmImeComposition => "WM_IME_COMPOSITION",
            WmImeSetContext => "WM_IME_SETCONTEXT",
            WmImeNotify => "WM_IME_NOTIFY",
            WmImeControl => "WM_IME_CONTROL",
            WmImeCompositionFull => "WM_IME_COMPOSITIONFULL",
            WmImeSelect => "WM_IME_SELECT",
            WmImeChar => "WM_IME_CHAR",
            WmImeSystem => "WM_IME_SYSTEM",
            WmImeRequest => "WM_IME_REQUEST",
            WmImeKeyDown => "WM_IME_KEYDOWN",
            WmImeKeyUp => "WM_IME_KEYUP",
            _ => null,
        };
    }

    private static string DescribeMessage(int message, IntPtr wParam, IntPtr lParam)
    {
        return message switch
        {
            WmInputLanguageRequest => $" requestedHKL={FormatPointer(lParam)}"
                + $" lang={DescribeLanguage(lParam)}",
            WmInputLanguageChange => $" charset={wParam.ToInt64()}"
                + $" newHKL={FormatPointer(lParam)} lang={DescribeLanguage(lParam)}",
            WmImeNotify => $" notification={DescribeImeNotification(wParam.ToInt64())}",
            WmImeSelect => $" selected={wParam != IntPtr.Zero}"
                + $" selectedHKL={FormatPointer(lParam)} lang={DescribeLanguage(lParam)}",
            WmImeSetContext => $" active={wParam != IntPtr.Zero} uiFlags=0x{lParam.ToInt64():X}",
            WmImeComposition => $" compositionFlags=0x{lParam.ToInt64():X}",
            _ => string.Empty,
        };
    }

    private static string DescribeImeNotification(long notification)
    {
        return notification switch
        {
            0x0001 => "IMN_CLOSESTATUSWINDOW",
            0x0002 => "IMN_OPENSTATUSWINDOW",
            0x0003 => "IMN_CHANGECANDIDATE",
            0x0004 => "IMN_CLOSECANDIDATE",
            0x0005 => "IMN_OPENCANDIDATE",
            0x0006 => "IMN_SETCONVERSIONMODE",
            0x0007 => "IMN_SETSENTENCEMODE",
            0x0008 => "IMN_SETOPENSTATUS",
            0x0009 => "IMN_SETCANDIDATEPOS",
            0x000A => "IMN_SETCOMPOSITIONFONT",
            0x000B => "IMN_SETCOMPOSITIONWINDOW",
            0x000C => "IMN_SETSTATUSWINDOWPOS",
            0x000D => "IMN_GUIDELINE",
            0x000E => "IMN_PRIVATE",
            _ => $"unknown({notification})",
        };
    }

    private static string DescribeConversion(uint conversion)
    {
        if (conversion == 0)
        {
            return "ALPHANUMERIC";
        }

        var names = new List<string>();
        AddFlag(names, conversion, 0x0001, "NATIVE");
        AddFlag(names, conversion, 0x0002, "KATAKANA");
        AddFlag(names, conversion, 0x0008, "FULLSHAPE");
        AddFlag(names, conversion, 0x0010, "ROMAN");
        AddFlag(names, conversion, 0x0020, "CHARCODE");
        AddFlag(names, conversion, 0x0040, "HANJACONVERT");
        AddFlag(names, conversion, 0x0080, "SOFTKBD");
        AddFlag(names, conversion, 0x0100, "NOCONVERSION");
        AddFlag(names, conversion, 0x0200, "EUDC");
        AddFlag(names, conversion, 0x0400, "SYMBOL");
        AddFlag(names, conversion, 0x0800, "FIXED");
        var unknown = conversion & ~0x0FFB;
        if (unknown != 0)
        {
            names.Add($"UNKNOWN=0x{unknown:X}");
        }

        return string.Join("|", names);
    }

    private static void AddFlag(
        ICollection<string> names,
        uint value,
        uint flag,
        string name
    )
    {
        if ((value & flag) != 0)
        {
            names.Add(name);
        }
    }

    private static string DescribeLanguage(IntPtr layout)
    {
        var languageId = unchecked((ushort)layout.ToInt64());
        try
        {
            return CultureInfo.GetCultureInfo(languageId).Name;
        }
        catch (CultureNotFoundException)
        {
            return $"LANGID-0x{languageId:X4}";
        }
    }

    private static string FormatPointer(IntPtr value)
    {
        return $"0x{unchecked((ulong)value.ToInt64()):X}";
    }
}
