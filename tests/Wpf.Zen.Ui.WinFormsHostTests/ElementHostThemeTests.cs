// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Forms.Integration;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Zen.Ui.Appearance;
using Wpf.Zen.Ui.Theming;
using Xunit;
using Forms = System.Windows.Forms;
using UiAutoSuggestBox = Wpf.Zen.Ui.Controls.AutoSuggestBox;
using UiComboBox = Wpf.Zen.Ui.Controls.ComboBox;
using UiNavigationView = Wpf.Zen.Ui.Controls.NavigationView;
using UiNavigationViewItem = Wpf.Zen.Ui.Controls.NavigationViewItem;

namespace Wpf.Zen.Ui.WinFormsHostTests;

/// <summary>
/// Exercises theme resource lookup through a real WinForms <see cref="ElementHost"/> rather than constructing an
/// artificial WPF state that a supported host would never use.
/// </summary>
public class ElementHostThemeTests
{
    /// <summary>
    /// Verifies that ThemeManager uses WPF's system-theme resource path without creating an Application, and that
    /// native controls inside a navigated page update after the visual tree has already been presented.
    /// </summary>
    [Fact]
    public void ThemeSwitch_ReachesGenericAndNativeControls_WithoutApplicationOrRootResources()
    {
        RunOnStaThread(() =>
        {
            EnsureWindowsDirectoryEnvironment();
            Assert.Null(Application.Current);

            var textBox = new TextBox { Text = "Text" };
            var comboBox = new ComboBox { ItemsSource = new[] { "Item" }, SelectedIndex = 0 };
            var uiComboBox = new UiComboBox { ItemsSource = new[] { "Item" }, SelectedIndex = 0 };
            var autoSuggestBox = new UiAutoSuggestBox
            {
                PlaceholderText = "Search",
                ItemsSource = new[] { "Dashboard" },
            };
            var navigationItem = new UiNavigationViewItem { Header = "Dashboard" };
            var navigationView = new UiNavigationView { AutoSuggestBox = autoSuggestBox };
            navigationView.MenuItems.Add(navigationItem);
            var globalBrushProbe = new Border();
            globalBrushProbe.SetResourceReference(
                Border.BackgroundProperty,
                BrushKeys.TextFillColorPrimaryBrush
            );
            var page = new Page
            {
                Content = new StackPanel
                {
                    Children =
                    {
                        textBox,
                        comboBox,
                        uiComboBox,
                        navigationView,
                        globalBrushProbe,
                    },
                },
            };
            var frame = new Frame();
            frame.Navigate(page);

            var root = new Grid();
            root.Children.Add(frame);

            // Deliberately configure after constructing the controls but before connecting the ElementHost. This
            // reproduces XAML/programmatic trees that fetched Generic.xaml's Light ThemeStyle while detached from
            // every PresentationSource. No root opt-in is present: the system host must detect the new visual island.
            ThemeManager.Configure(
                UiThemeMode.Light,
                AccentColorSource.Default,
                enableNativeControlStyling: true
            );

            using var form = new Forms.Form
            {
                ShowInTaskbar = false,
                StartPosition = Forms.FormStartPosition.Manual,
                Location = new System.Drawing.Point(-10_000, -10_000),
                Size = new System.Drawing.Size(320, 240),
            };
            using var host = new ElementHost { Dock = Forms.DockStyle.Fill, Child = root };
            form.Controls.Add(host);

            try
            {
                form.Show();
                Forms.Application.DoEvents();
                DrainDispatcher();

                Assert.Null(Application.Current);
                Assert.False(Theme.GetApplyToRootVisual(root));
                Assert.Empty(root.Resources.MergedDictionaries);
                Assert.Same(Dispatcher.CurrentDispatcher, GetThemeDispatcher());

                // Frame sets InheritanceBehavior.SkipToAppNow, so the page cannot see the theme dictionary installed
                // on the ElementHost root. DefaultStyle value sources prove these templates came from the injected
                // SystemResources ThemeStyles rather than implicit Styles inherited through the resource tree.
                Assert.Equal(
                    BaseValueSource.DefaultStyle,
                    DependencyPropertyHelper.GetValueSource(
                        textBox,
                        Control.TemplateProperty
                    ).BaseValueSource
                );
                Assert.Equal(
                    BaseValueSource.DefaultStyle,
                    DependencyPropertyHelper.GetValueSource(
                        comboBox,
                        Control.TemplateProperty
                    ).BaseValueSource
                );
                Assert.Equal(
                    BaseValueSource.DefaultStyle,
                    DependencyPropertyHelper.GetValueSource(
                        uiComboBox,
                        Control.TemplateProperty
                    ).BaseValueSource
                );
                Assert.Equal(
                    BaseValueSource.DefaultStyle,
                    DependencyPropertyHelper.GetValueSource(
                        autoSuggestBox,
                        Control.TemplateProperty
                    ).BaseValueSource
                );
                Assert.Equal(
                    BaseValueSource.DefaultStyleTrigger,
                    DependencyPropertyHelper.GetValueSource(
                        navigationView,
                        Control.TemplateProperty
                    ).BaseValueSource
                );
                _ = textBox.ApplyTemplate();
                _ = comboBox.ApplyTemplate();
                _ = uiComboBox.ApplyTemplate();
                _ = navigationView.ApplyTemplate();
                _ = autoSuggestBox.ApplyTemplate();
                _ = navigationItem.ApplyTemplate();
                Assert.NotNull(textBox.Template.FindName("FocusIndicator", textBox));
                Assert.NotNull(comboBox.Template.FindName("AccentBorder", comboBox));
                Assert.NotNull(uiComboBox.Template.FindName("AccentBorder", uiComboBox));

                Wpf.Zen.Ui.Controls.TextBox autoSuggestTextBox = Assert.IsType<Wpf.Zen.Ui.Controls.TextBox>(
                    autoSuggestBox.Template.FindName("PART_TextBox", autoSuggestBox)
                );
                Button navigationHeaderButton = Assert.IsType<Button>(
                    navigationItem.Template.FindName("PART_HeaderButton", navigationItem)
                );
                _ = navigationHeaderButton.ApplyTemplate();
                ContentPresenter navigationHeaderPresenter = Assert.IsType<ContentPresenter>(
                    navigationHeaderButton.Template.FindName("HeaderPresenter", navigationHeaderButton)
                );
                FrameworkElement paneGrid = Assert.IsType<Grid>(
                    navigationView.Template.FindName("PaneGrid", navigationView)
                );
                Popup suggestionPopup = Assert.IsType<Popup>(
                    autoSuggestBox.Template.FindName("SuggestionPopup", autoSuggestBox)
                );

                Assert.Equal(ApplicationTheme.Light, ThemeManager.CurrentTheme);
                Assert.Equal(
                    Color.FromArgb(0xE4, 0x00, 0x00, 0x00),
                    GetBrushColor(globalBrushProbe.Background)
                );
                Assert.Equal(
                    Color.FromArgb(0xE4, 0x00, 0x00, 0x00),
                    GetTextForeground(navigationHeaderPresenter)
                );
                Assert.Equal(
                    Color.FromArgb(0x80, 0xF0, 0xF0, 0xF0),
                    GetBrushColor(autoSuggestTextBox.Background)
                );
                Assert.Equal(
                    Color.FromArgb(0x80, 0xF0, 0xF0, 0xF0),
                    GetBrushColor(uiComboBox.Background)
                );
                Style initialTextBoxThemeStyle = GetThemeStyle(textBox);
                Style initialComboBoxThemeStyle = GetThemeStyle(comboBox);
                Style initialUiComboBoxThemeStyle = GetThemeStyle(uiComboBox);
                Style initialAutoSuggestBoxThemeStyle = GetThemeStyle(autoSuggestBox);
                Style initialNavigationViewThemeStyle = GetThemeStyle(navigationView);
                ControlTemplate initialTextBoxTemplate = textBox.Template;
                ControlTemplate initialComboBoxTemplate = comboBox.Template;
                ControlTemplate initialUiComboBoxTemplate = uiComboBox.Template;
                ControlTemplate initialAutoSuggestBoxTemplate = autoSuggestBox.Template;
                ControlTemplate initialNavigationViewTemplate = navigationView.Template;

                // Open a detached Popup and keep the pane expanded while switching. Palette replacement must not
                // rebuild either template or manufacture a contradictory Pane/Popup state.
                autoSuggestBox.IsSuggestionListOpen = true;
                Forms.Application.DoEvents();
                DrainDispatcher();
                Assert.True(navigationView.IsPaneOpen);
                Assert.True(suggestionPopup.IsOpen);

                ThemeManager.Configure(
                    UiThemeMode.Dark,
                    AccentColorSource.Default,
                    enableNativeControlStyling: true
                );
                Forms.Application.DoEvents();
                DrainDispatcher();

                Assert.Null(Application.Current);
                Assert.Equal(ApplicationTheme.Dark, ThemeManager.CurrentTheme);
                Assert.Equal(
                    Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF),
                    GetBrushColor(textBox.Background)
                );
                Assert.Equal(
                    Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF),
                    GetBrushColor(comboBox.Background)
                );
                Assert.Equal(
                    Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF),
                    GetBrushColor(uiComboBox.Background)
                );
                Assert.Equal(
                    Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF),
                    GetBrushColor(autoSuggestTextBox.Background)
                );
                Assert.Same(initialTextBoxThemeStyle, GetThemeStyle(textBox));
                Assert.Same(initialComboBoxThemeStyle, GetThemeStyle(comboBox));
                Assert.Same(initialUiComboBoxThemeStyle, GetThemeStyle(uiComboBox));
                Assert.Same(initialAutoSuggestBoxThemeStyle, GetThemeStyle(autoSuggestBox));
                Assert.Same(initialNavigationViewThemeStyle, GetThemeStyle(navigationView));
                Assert.Same(initialTextBoxTemplate, textBox.Template);
                Assert.Same(initialComboBoxTemplate, comboBox.Template);
                Assert.Same(initialUiComboBoxTemplate, uiComboBox.Template);
                Assert.Same(initialAutoSuggestBoxTemplate, autoSuggestBox.Template);
                Assert.Same(initialNavigationViewTemplate, navigationView.Template);
                Assert.Same(paneGrid, navigationView.Template.FindName("PaneGrid", navigationView));
                Assert.Same(
                    suggestionPopup,
                    autoSuggestBox.Template.FindName("SuggestionPopup", autoSuggestBox)
                );
                Assert.True(navigationView.IsPaneOpen);
                Assert.True(autoSuggestBox.IsSuggestionListOpen);
                Assert.True(suggestionPopup.IsOpen);
                Assert.Equal(Colors.White, GetBrushColor(globalBrushProbe.Background));
                Assert.Equal(Colors.White, GetTextForeground(navigationHeaderPresenter));

                // ComboBoxItem is presented under a PopupRoot rather than under the ElementHost visual root. Its
                // effective theme style exercises WPF's detached-presentation-source invalidation path as well.
                comboBox.IsDropDownOpen = true;
                Forms.Application.DoEvents();
                DrainDispatcher();

                ComboBoxItem item = Assert.IsType<ComboBoxItem>(
                    comboBox.ItemContainerGenerator.ContainerFromIndex(0)
                );
                _ = item.ApplyTemplate();
                Assert.NotNull(item.Template.FindName("ActiveIndicator", item));
                Assert.Equal(
                    BaseValueSource.DefaultStyle,
                    DependencyPropertyHelper.GetValueSource(
                        item,
                        Control.TemplateProperty
                    ).BaseValueSource
                );

                Style initialComboBoxItemThemeStyle = GetThemeStyle(item);
                ControlTemplate initialComboBoxItemTemplate = item.Template;

                // Exercise the opposite Pane state and a native Popup during another ordinary palette switch.
                autoSuggestBox.IsSuggestionListOpen = false;
                navigationView.IsPaneOpen = false;
                Forms.Application.DoEvents();
                DrainDispatcher();

                ThemeManager.Configure(
                    UiThemeMode.Light,
                    AccentColorSource.Default,
                    enableNativeControlStyling: true
                );
                Forms.Application.DoEvents();
                DrainDispatcher();

                Assert.Null(Application.Current);
                Assert.Equal(ApplicationTheme.Light, ThemeManager.CurrentTheme);
                Assert.Equal(
                    Color.FromArgb(0x80, 0xF0, 0xF0, 0xF0),
                    GetBrushColor(textBox.Background)
                );
                Assert.Equal(
                    Color.FromArgb(0x80, 0xF0, 0xF0, 0xF0),
                    GetBrushColor(comboBox.Background)
                );
                Assert.Equal(
                    Color.FromArgb(0x80, 0xF0, 0xF0, 0xF0),
                    GetBrushColor(uiComboBox.Background)
                );
                Assert.Equal(
                    Color.FromArgb(0x80, 0xF0, 0xF0, 0xF0),
                    GetBrushColor(autoSuggestTextBox.Background)
                );
                Assert.False(navigationView.IsPaneOpen);
                Assert.False(autoSuggestBox.IsSuggestionListOpen);
                Assert.False(suggestionPopup.IsOpen);
                Assert.True(comboBox.IsDropDownOpen);
                Assert.Same(initialTextBoxThemeStyle, GetThemeStyle(textBox));
                Assert.Same(initialComboBoxThemeStyle, GetThemeStyle(comboBox));
                Assert.Same(initialUiComboBoxThemeStyle, GetThemeStyle(uiComboBox));
                Assert.Same(initialAutoSuggestBoxThemeStyle, GetThemeStyle(autoSuggestBox));
                Assert.Same(initialNavigationViewThemeStyle, GetThemeStyle(navigationView));
                Assert.Same(initialComboBoxItemThemeStyle, GetThemeStyle(item));
                Assert.Same(initialTextBoxTemplate, textBox.Template);
                Assert.Same(initialComboBoxTemplate, comboBox.Template);
                Assert.Same(initialUiComboBoxTemplate, uiComboBox.Template);
                Assert.Same(initialAutoSuggestBoxTemplate, autoSuggestBox.Template);
                Assert.Same(initialNavigationViewTemplate, navigationView.Template);
                Assert.Same(initialComboBoxItemTemplate, item.Template);
                Assert.Same(paneGrid, navigationView.Template.FindName("PaneGrid", navigationView));

                comboBox.IsDropDownOpen = false;
                SimulateWpfSystemThemeCacheReset();

                ThemeManager.Configure(
                    UiThemeMode.Dark,
                    AccentColorSource.Default,
                    enableNativeControlStyling: true
                );
                Forms.Application.DoEvents();
                DrainDispatcher();

                // WM_THEMECHANGED clears WPF's private themed-dictionary slots. A subsequent ThemeManager refresh
                // must detect that its old overlay was unloaded, capture the newly loaded Windows dictionary, and
                // reinstall the Wpf.Zen.Ui overlay without falling back to Application.
                Assert.Null(Application.Current);
                Assert.Equal(ApplicationTheme.Dark, ThemeManager.CurrentTheme);
                Assert.Equal(
                    Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF),
                    GetBrushColor(textBox.Background)
                );

                Style enabledTextBoxThemeStyle = GetThemeStyle(textBox);
                Style enabledComboBoxThemeStyle = GetThemeStyle(comboBox);
                Style stableUiComboBoxThemeStyle = GetThemeStyle(uiComboBox);
                ControlTemplate stableUiComboBoxTemplate = uiComboBox.Template;

                // Unlike a Light/Dark palette switch, changing this option adds or removes native Type keys from
                // PresentationFramework's themed dictionary. Controls that already exist must therefore make one
                // real ThemeStyle transition. Wpf.Zen.Ui controls still fall through to the stable Generic.xaml graph.
                ThemeManager.EnableNativeControlStyling = false;
                Forms.Application.DoEvents();
                DrainDispatcher();

                Assert.Null(Application.Current);
                Assert.False(ThemeManager.EnableNativeControlStyling);
                Assert.NotSame(enabledTextBoxThemeStyle, GetThemeStyle(textBox));
                Assert.NotSame(enabledComboBoxThemeStyle, GetThemeStyle(comboBox));
                Assert.Same(stableUiComboBoxThemeStyle, GetThemeStyle(uiComboBox));
                Assert.Same(stableUiComboBoxTemplate, uiComboBox.Template);
                _ = textBox.ApplyTemplate();
                _ = comboBox.ApplyTemplate();
                Assert.Null(textBox.Template.FindName("FocusIndicator", textBox));
                Assert.Null(comboBox.Template.FindName("AccentBorder", comboBox));

                Style systemTextBoxThemeStyle = GetThemeStyle(textBox);
                Style systemComboBoxThemeStyle = GetThemeStyle(comboBox);

                ThemeManager.EnableNativeControlStyling = true;
                Forms.Application.DoEvents();
                DrainDispatcher();

                Assert.Null(Application.Current);
                Assert.True(ThemeManager.EnableNativeControlStyling);
                Assert.NotSame(systemTextBoxThemeStyle, GetThemeStyle(textBox));
                Assert.NotSame(systemComboBoxThemeStyle, GetThemeStyle(comboBox));
                Assert.Same(stableUiComboBoxThemeStyle, GetThemeStyle(uiComboBox));
                Assert.Same(stableUiComboBoxTemplate, uiComboBox.Template);
                _ = textBox.ApplyTemplate();
                _ = comboBox.ApplyTemplate();
                Assert.NotNull(textBox.Template.FindName("FocusIndicator", textBox));
                Assert.NotNull(comboBox.Template.FindName("AccentBorder", comboBox));
            }
            finally
            {
                form.Close();
                Forms.Application.DoEvents();
                DrainDispatcher();
            }
        });
    }

    /// <summary>
    /// Verifies that additional Light and Dark palette dictionaries participate in the applicationless system-theme
    /// overlay and update resource references in both a Frame-isolated page and a detached Popup presentation source.
    /// </summary>
    [Fact]
    public void AdditionalPalettes_ReachFrameAndPopup_WithoutApplicationOrRootResources()
    {
        RunOnStaThread(() =>
        {
            EnsureWindowsDirectoryEnvironment();
            Assert.Null(Application.Current);

            var pageProbe = new Border { Width = 20, Height = 20 };
            pageProbe.SetResourceReference(
                Border.BackgroundProperty,
                BrushKeys.TextFillColorPrimaryBrush
            );

            var page = new Page { Content = pageProbe };
            var frame = new Frame();
            frame.Navigate(page);

            var root = new Grid();
            root.Children.Add(frame);

            var popupProbe = new Border { Width = 20, Height = 20 };
            popupProbe.SetResourceReference(
                Border.BackgroundProperty,
                BrushKeys.TextFillColorPrimaryBrush
            );
            var popup = new Popup
            {
                AllowsTransparency = true,
                Child = popupProbe,
                PlacementTarget = root,
                Placement = PlacementMode.Bottom,
            };

            var lightPaletteUri = new Uri(
                "/Wpf.Zen.Ui.WinFormsHostTests;component/TestResources/AdditionalLightPalette.xaml",
                UriKind.Relative
            );
            var darkPaletteUri = new Uri(
                "/Wpf.Zen.Ui.WinFormsHostTests;component/TestResources/AdditionalDarkPalette.xaml",
                UriKind.Relative
            );

            ThemeManager.Configure(
                UiThemeMode.Light,
                AccentColorSource.Default,
                Color.FromRgb(0x00, 0x78, 0xD4),
                enableNativeControlStyling: true,
                additionalLightThemeUri: lightPaletteUri,
                additionalDarkThemeUri: darkPaletteUri,
                additionalHighContrastLightThemeUri: null,
                additionalHighContrastDarkThemeUri: null
            );

            using var form = new Forms.Form
            {
                ShowInTaskbar = false,
                StartPosition = Forms.FormStartPosition.Manual,
                Location = new System.Drawing.Point(-10_000, -10_000),
                Size = new System.Drawing.Size(320, 240),
            };
            using var host = new ElementHost { Dock = Forms.DockStyle.Fill, Child = root };
            form.Controls.Add(host);

            try
            {
                form.Show();
                popup.IsOpen = true;
                Forms.Application.DoEvents();
                DrainDispatcher();

                Assert.Null(Application.Current);
                Assert.Empty(root.Resources.MergedDictionaries);
                Assert.Equal(Color.FromRgb(0x21, 0x43, 0x65), GetBrushColor(pageProbe.Background));
                Assert.Equal(Color.FromRgb(0x21, 0x43, 0x65), GetBrushColor(popupProbe.Background));

                ThemeManager.ThemeMode = UiThemeMode.Dark;
                Forms.Application.DoEvents();
                DrainDispatcher();

                Assert.Null(Application.Current);
                Assert.True(popup.IsOpen);
                Assert.Equal(Color.FromRgb(0x89, 0xAB, 0xCD), GetBrushColor(pageProbe.Background));
                Assert.Equal(Color.FromRgb(0x89, 0xAB, 0xCD), GetBrushColor(popupProbe.Background));
            }
            finally
            {
                popup.IsOpen = false;
                form.Close();
                Forms.Application.DoEvents();
                DrainDispatcher();
                ThemeManager.SetAdditionalThemeUris(null, null, null, null);
            }
        });
    }

    private static void DrainDispatcher() =>
        Dispatcher.CurrentDispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);

    private static Color GetBrushColor(Brush brush) => Assert.IsType<SolidColorBrush>(brush).Color;

    private static Color GetTextForeground(DependencyObject element) =>
        GetBrushColor(Assert.IsAssignableFrom<Brush>(element.GetValue(TextElement.ForegroundProperty)));

    private static Style GetThemeStyle(FrameworkElement element)
    {
        PropertyInfo? themeStyleProperty = typeof(FrameworkElement).GetProperty(
            "ThemeStyle",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        Assert.NotNull(themeStyleProperty);

        return Assert.IsType<Style>(themeStyleProperty.GetValue(element));
    }

    private static Dispatcher? GetThemeDispatcher()
    {
        MethodInfo? method = typeof(ThemeManager).GetMethod(
            "GetThemeDispatcher",
            BindingFlags.Static | BindingFlags.NonPublic
        );
        Assert.NotNull(method);

        return Assert.IsAssignableFrom<Dispatcher>(method.Invoke(null, null));
    }

    private static void SimulateWpfSystemThemeCacheReset()
    {
        Type systemResources = typeof(FrameworkElement).Assembly.GetType(
            "System.Windows.SystemResources",
            throwOnError: true
        )!;
        MethodInfo? onThemeChanged = systemResources.GetMethod(
            "OnThemeChanged",
            BindingFlags.Static | BindingFlags.NonPublic
        );
        Assert.NotNull(onThemeChanged);

        _ = onThemeChanged.Invoke(null, null);
    }

    private static void EnsureWindowsDirectoryEnvironment()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("windir")))
        {
            // WPF's font cache reads the lower-case Win32 environment name. Some test runners preserve only
            // SystemRoot, unlike a normally launched WinForms process, so reproduce the real host environment
            // before constructing the first WPF text control.
            Environment.SetEnvironmentVariable(
                "windir",
                Environment.GetEnvironmentVariable("SystemRoot")
                    ?? Environment.GetEnvironmentVariable("windir", EnvironmentVariableTarget.Machine)
            );
        }
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception caughtException)
            {
                exception = caughtException;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }
}
