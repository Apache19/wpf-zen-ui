// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Resources;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Owns one modal invocation. Only standard results can complete this window.
/// </summary>
internal partial class MessageBoxWindow : FluentWindow
{
    private static readonly ResourceManager Strings = new(
        "Wpf.Zen.Ui.Controls.MessageBox.MessageBoxStrings",
        typeof(MessageBoxWindow).Assembly
    );

    private readonly MessageBoxButton _buttons;
    private readonly Button           _defaultButton;

    /// <summary>
    /// Creates the message content and snapshots localized labels for this invocation.
    /// </summary>
    internal MessageBoxWindow(
        string text,
        string caption,
        MessageBoxButton buttons,
        MessageBoxImage icon,
        MessageBoxResult defaultResult,
        MessageBoxOptions options,
        MessageBoxButtonText? labels)
    {
        _buttons = buttons;

        InitializeComponent();

        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        Title = caption ?? string.Empty;

        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        MessageText.Text = text ?? string.Empty;

        FlowDirection = (options & MessageBoxOptions.RtlReading) != 0
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;

        MessageText.TextAlignment = (options & MessageBoxOptions.RightAlign) != 0
            ? TextAlignment.Right
            : TextAlignment.Left;

        MessageIcon.Symbol = icon switch
        {
            MessageBoxImage.Error    => SymbolRegular.ErrorCircle24,
            MessageBoxImage.Question => SymbolRegular.QuestionCircle24,
            MessageBoxImage.Warning  => SymbolRegular.Warning24,

            _ => SymbolRegular.Info24,
        };

        MessageIcon.Visibility = icon == MessageBoxImage.None ? Visibility.Collapsed : Visibility.Visible;

        // todo: UI 自动化名称应本地化
        AutomationProperties.SetName(MessageIcon, icon.ToString());

        var results  = GetResults(buttons);
        var selected = results.Contains(defaultResult) ? defaultResult : results[0];

        foreach (var result in results)
        {
            var label = GetLabel(result, labels, CultureInfo.CurrentUICulture);
            var action = new Button
            {
                Content = new AccessText
                {
                    Text         = label,
                    TextWrapping = TextWrapping.Wrap
                },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding             = new Thickness(16, 7, 16, 7),
                IsDefault           = result == selected,
                Appearance          = result == selected ? ControlAppearance.Accent : ControlAppearance.Normal,
                Tag                 = result,
            };

            AutomationProperties.SetAutomationId(action, "MessageBox" + result);

            action.Click += (_, _) => Complete(result);

            ButtonsPanel.Items.Add(action);
        }

        _defaultButton = ButtonsPanel.Items.OfType<Button>().Single(action => action.IsDefault);
    }

    /// <summary>
    /// Gets whether Escape and the window close action have a standard dismissal result.
    /// Yes/No requires an explicit choice, as in the native message box.
    /// </summary>
    public bool CanDismiss => _buttons != MessageBoxButton.YesNo;

    /// <summary>
    /// Gets the result after the modal loop exits.
    /// </summary>
    internal MessageBoxResult Result { get; private set; }

    /// <summary>
    /// Returns the ordered native button set, also used to resolve an unavailable default result.
    /// </summary>
    internal static MessageBoxResult[] GetResults(MessageBoxButton buttons)
    {
        return buttons switch
        {
            MessageBoxButton.OK          => [MessageBoxResult.OK],
            MessageBoxButton.OKCancel    => [MessageBoxResult.OK, MessageBoxResult.Cancel],
            MessageBoxButton.YesNo       => [MessageBoxResult.Yes, MessageBoxResult.No],
            MessageBoxButton.YesNoCancel => [MessageBoxResult.Yes, MessageBoxResult.No, MessageBoxResult.Cancel],

            _ => throw new InvalidEnumArgumentException(
                nameof(buttons),
                (int)buttons,
                typeof(MessageBoxButton)
            )
        };
    }

    /// <summary>
    /// Resolves each override independently, with normal resource parent-culture fallback to
    /// English.
    /// </summary>
    internal static string GetLabel(MessageBoxResult result, MessageBoxButtonText? labels, CultureInfo culture)
    {
        var custom = result switch
        {
            MessageBoxResult.OK     => labels?.OK,
            MessageBoxResult.Cancel => labels?.Cancel,
            MessageBoxResult.Yes    => labels?.Yes,
            MessageBoxResult.No     => labels?.No,

            _ => throw new ArgumentOutOfRangeException(nameof(result)),
        };

        return string.IsNullOrEmpty(custom) ? Strings.GetString(result.ToString(), culture)! : custom!;
    }

    /// <summary>
    /// Applies monitor work-area bounds in WPF units before first layout and disables native Close
    /// for Yes/No. WPF retains responsibility for centering, DPI conversion and modal activation.
    /// </summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        var initialHandle = new HWND(new WindowInteropHelper(this).Handle);

        // FluentWindow removes WS_SYSMENU while retaining this native menu instance.
        var systemMenu = PInvoke.GetSystemMenu(initialHandle, false);

        base.OnSourceInitialized(e);

        var handle = new HWND(new WindowInteropHelper(this).Handle);

        // Source initialization can pump layout; a Loaded handler may already have closed us.
        if (handle.IsNull)
        {
            return;
        }

        var monitor = PInvoke.MonitorFromWindow(handle, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);

        var info = new MONITORINFO
        {
            cbSize = (uint)Marshal.SizeOf<MONITORINFO>()
        };

        if (PInvoke.GetMonitorInfo(monitor, ref info))
        {
            var source     = HwndSource.FromHwnd(handle);
            var fromDevice = source.CompositionTarget.TransformFromDevice;

            MaxWidth                  = Math.Max(1, info.rcWork.Width * fromDevice.M11 - 32);
            MaxHeight                 = Math.Max(1, info.rcWork.Height * fromDevice.M22 - 32);
            Width                     = Math.Min(Width, MaxWidth);
            MessageScroller.MaxHeight = Math.Max(48, MaxHeight - 200);
        }

        if (!CanDismiss)
        {
            PInvoke.EnableMenuItem(
                systemMenu, PInvoke.SC_CLOSE, MENU_ITEM_FLAGS.MF_BYCOMMAND | MENU_ITEM_FLAGS.MF_GRAYED
            );
        }
    }

    /// <summary>
    /// Focuses the selected default action once the native modal window can receive keyboard input.
    /// </summary>
    protected override void OnContentRendered(EventArgs e)
    {
        _defaultButton.Focus();

        base.OnContentRendered(e);
    }

    /// <summary>
    /// Implements native Escape and Ctrl+C behavior without allowing WPF DialogResult to choose
    /// an unrelated result. Enter and access keys are handled by standard WPF buttons.
    /// </summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (e.Key == Key.Escape)
        {
            if (CanDismiss)
            {
                Close();
            }

            e.Handled = true;
        }
        else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            var labels = string.Join("    ", ButtonsPanel.Items.OfType<Button>()
                .Select(
                    action => ((AccessText)action.Content).Text
                        .Replace("__", "\0")
                        .Replace("_", string.Empty)
                        .Replace("\0", "_")
                )
            );

            try
            {
                Clipboard.SetText(
                    $"---------------------------\r\n{Title}\r\n---------------------------\r\n"
                    + $"{MessageText.Text}\r\n---------------------------\r\n{labels}\r\n---------------------------"
                );
            }
            catch (ExternalException)
            {
                // Another process may temporarily own the clipboard; the dialog remains usable.
            }

            e.Handled = true;
        }
    }

    /// <summary>
    /// Commits a configured button result and closes the native modal window.
    /// </summary>
    internal void Complete(MessageBoxResult result)
    {
        if (!GetResults(_buttons).Contains(result))
        {
            throw new ArgumentOutOfRangeException(nameof(result));
        }

        Result = result;
        Close();
    }

    /// <summary>
    /// Maps caption close and Alt+F4 to OK or Cancel, or keeps Yes/No open for an explicit choice.
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        if (Result == MessageBoxResult.None && !CanDismiss)
        {
            e.Cancel = true;
        }

        if (!e.Cancel)
        {
            if (Result == MessageBoxResult.None)
            {
                Result = _buttons == MessageBoxButton.OK ? MessageBoxResult.OK : MessageBoxResult.Cancel;
            }

            Debug.WriteLine($"[MessageBox] MODAL window closed; result={Result}.");
        }
        else
        {
            Result = MessageBoxResult.None;
        }
    }
}