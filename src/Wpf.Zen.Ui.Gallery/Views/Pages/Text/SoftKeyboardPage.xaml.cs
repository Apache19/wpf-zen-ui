// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.ControlsLookup;
using Wpf.Zen.Ui.Gallery.Diagnostics;
using Wpf.Zen.Ui.Gallery.ViewModels.Pages.Text;
using Wpf.Zen.Ui.VirtualInput.Controls;

namespace Wpf.Zen.Ui.Gallery.Views.Pages.Text;

/// <summary>
/// Demonstrates the embedded soft keyboard and its native IME diagnostic tools.
/// </summary>
[GalleryPage("Embedded, focus-preserving touch keyboard for WPF editors.", SymbolRegular.Keyboard24)]
public partial class SoftKeyboardPage : INavigableView<SoftKeyboardViewModel>
{
    private CandidateWindowBoundsTracker? _candidateWindowBoundsTracker;
    private ContextMenu?                  _settingsMenu;
    private IInputElement?                _settingsFocus;

    /// <summary>
    /// Builds the page's configuration menu on demand from the keyboard's current public state.
    /// </summary>
    private void OnSettingsClick(object sender, RoutedEventArgs args)
    {
        _settingsFocus = System.Windows.Input.Keyboard.FocusedElement;
        // Explicitly discover layout choices when the page's configuration UI is requested.
        Keyboard.RefreshLayout();

        if (_settingsMenu is null)
        {
            _settingsMenu = new ContextMenu
            {
                Focusable = false,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Top
            };

            _settingsMenu.Closed  += OnSettingsClosed;
            SettingsButton.Flyout =  _settingsMenu;
        }

        _settingsMenu.Items.Clear();

        var opening = new MenuItem
        {
            Header    = "自动打开",
            Focusable = false
        };

        opening.Items.Add(
            CreateSettingChoice(
                "手动打开",
                Keyboard.AutoShowMode == SoftKeyboardAutoShowMode.Disabled,
                () => Keyboard.SetCurrentValue(
                    SoftKeyboard.AutoShowModeProperty,
                    SoftKeyboardAutoShowMode.Disabled
                )
            )
        );

        opening.Items.Add(
            CreateSettingChoice(
                "触摸或笔输入时打开",
                Keyboard.AutoShowMode == SoftKeyboardAutoShowMode.TouchAndPen,
                () => Keyboard.SetCurrentValue(
                    SoftKeyboard.AutoShowModeProperty,
                    SoftKeyboardAutoShowMode.TouchAndPen
                )
            )
        );

        opening.Items.Add(
            CreateSettingChoice(
                "编辑器获得焦点时打开",
                Keyboard.AutoShowMode == SoftKeyboardAutoShowMode.AnyFocus,
                () => Keyboard.SetCurrentValue(
                    SoftKeyboard.AutoShowModeProperty,
                    SoftKeyboardAutoShowMode.AnyFocus
                )
            )
        );

        _settingsMenu.Items.Add(opening);

        var closing = new MenuItem
        {
            Header    = "自动关闭",
            Focusable = false
        };

        closing.Items.Add(
            CreateSettingChoice(
                "手动关闭",
                Keyboard.AutoHideMode == SoftKeyboardAutoHideMode.Manual,
                () => Keyboard.SetCurrentValue(
                    SoftKeyboard.AutoHideModeProperty,
                    SoftKeyboardAutoHideMode.Manual
                )
            )
        );

        closing.Items.Add(
            CreateSettingChoice(
                "焦点离开输入区域时关闭",
                Keyboard.AutoHideMode == SoftKeyboardAutoHideMode.FocusLeavesScope,
                () => Keyboard.SetCurrentValue(
                    SoftKeyboard.AutoHideModeProperty,
                    SoftKeyboardAutoHideMode.FocusLeavesScope
                )
            )
        );

        _settingsMenu.Items.Add(closing);

        if (Keyboard.AvailableSupplementalLayouts.Count > 2)
        {
            var layouts = new MenuItem
            {
                Header    = "键盘布局",
                Focusable = false
            };

            var profile = Keyboard.ActiveInputMethod;

            foreach (var option in Keyboard.AvailableSupplementalLayouts)
            {
                layouts.Items.Add(CreateSettingChoice(option.DisplayName,
                    Keyboard.SelectedSupplementalLayout == option.Id,
                    () =>
                    {
                        // A native profile or scope change can invalidate an already open menu.
                        if (Equals(profile, Keyboard.ActiveInputMethod)
                            && Keyboard.AvailableSupplementalLayouts.Any(item => item.Id == option.Id))
                        {
                            Keyboard.SelectSupplementalLayout(option.Id);
                        }
                    }
                ));
            }

            _settingsMenu.Items.Add(layouts);
        }
    }

    /// <summary>
    /// Creates a checked choice that dismisses this page's menu before changing keyboard behavior.
    /// </summary>
    private MenuItem CreateSettingChoice(string header, bool isChecked, Action apply)
    {
        var item = new MenuItem
        {
            Header      = header,
            Focusable   = false,
            IsCheckable = true,
            IsChecked   = isChecked,
            IsEnabled   = Keyboard.IsEnabled
        };

        item.Click += (_, args) =>
        {
            args.Handled          = true;
            _settingsMenu!.IsOpen = false;
            if (Keyboard.IsEnabled)
            {
                apply();
            }
        };

        return item;
    }

    /// <summary>
    /// Restores the original editor only if closing the menu left focus unclaimed in this page.
    /// </summary>
    private void OnSettingsClosed(object sender, RoutedEventArgs args)
    {
        var target = _settingsFocus;

        _settingsFocus = null;

        _ = Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            if (IsLoaded && Window.GetWindow(this)?.IsActive == true
                && target is UIElement { IsVisible: true, IsEnabled: true } editor
                && IsAncestorOf(editor)
                && (System.Windows.Input.Keyboard.FocusedElement is null or MenuItem))
            {
                editor.Focus();
            }
        }));
    }

    /// <summary>
    /// Gets the view model used by the soft-keyboard gallery page.
    /// </summary>
    public SoftKeyboardViewModel ViewModel { get; }

    /// <summary>
    /// Initializes the soft-keyboard gallery page.
    /// </summary>
    /// <param name="viewModel">The page view model supplied by dependency injection.</param>
    public SoftKeyboardPage(SoftKeyboardViewModel viewModel)
    {
#if DEBUG
        var started = Stopwatch.GetTimestamp();
#endif
        ViewModel   = viewModel;
        DataContext = viewModel;

        InitializeComponent();

        Unloaded += OnUnloaded;
#if DEBUG
        // Includes synchronous XAML property callbacks, but excludes later layout and Loaded.
        var elapsed = (Stopwatch.GetTimestamp() - started) * 1000D / Stopwatch.Frequency;
        if (elapsed >= 16)
        {
            Debug.WriteLine(
                FormattableString.Invariant(
                    $"[SoftKeyboard.Gallery] Slow INITIALIZATION; elapsedMs={elapsed:F3}, isOpen={Keyboard.IsOpen}."
                )
            );
        }
#endif
    }

    private void OnCandidateWindowBoundsChecked(object sender, RoutedEventArgs e)
    {
        _candidateWindowBoundsTracker ??= CreateCandidateWindowBoundsTracker();
        _candidateWindowBoundsTracker.Start();
    }

    private void OnCandidateWindowBoundsUnchecked(object sender, RoutedEventArgs e)
    {
        _candidateWindowBoundsTracker?.Stop();
    }

    /// <summary>
    /// Closes page-owned presentation and releases native-window diagnostics on navigation.
    /// </summary>
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_settingsMenu is not null)
        {
            _settingsMenu.IsOpen = false;
        }

        _settingsFocus = null;

        CandidateWindowBoundsCheckBox.IsChecked = false;

        _candidateWindowBoundsTracker?.Dispose();
        _candidateWindowBoundsTracker = null;
    }

    /// <summary>
    /// Creates the native-window diagnostic and forwards snapshots to the page's status text.
    /// </summary>
    private CandidateWindowBoundsTracker CreateCandidateWindowBoundsTracker()
    {
        var tracker = new CandidateWindowBoundsTracker();

        tracker.SnapshotChanged += (_, description) =>
            CandidateWindowDiagnosticStatus.Text = description;

        return tracker;
    }
}