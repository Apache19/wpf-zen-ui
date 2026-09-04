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
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Zen.Ui.Appearance;
using Wpf.Zen.Ui.Theming;
using Xunit;
using Forms = System.Windows.Forms;
using UiAutoSuggestBox = Wpf.Zen.Ui.Controls.AutoSuggestBox;
using UiComboBox = Wpf.Zen.Ui.Controls.ComboBox;
using UiDropDownButton = Wpf.Zen.Ui.Controls.DropDownButton;
using UiFluentWindow = Wpf.Zen.Ui.Controls.FluentWindow;
using UiNavigationView = Wpf.Zen.Ui.Controls.NavigationView;
using UiNavigationViewItem = Wpf.Zen.Ui.Controls.NavigationViewItem;
using UiScrollBar = Wpf.Zen.Ui.Controls.UiScrollBar;

namespace Wpf.Zen.Ui.WinFormsHostTests;

/// <summary>
/// Exercises applicationless theme resource lookup through real WPF windows and WinForms <see cref="ElementHost"/>
/// presentation sources rather than constructing an artificial resource state that a supported host would never use.
/// </summary>
public class ElementHostThemeTests
{
    /// <summary>
    /// Verifies that generated and explicit drop-down input remains inside its Popup boundary when WPF is hosted
    /// by WinForms without an <see cref="Application"/> or WPF <see cref="Window"/>.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DropDownButtonPopup_ContainsWheelAndFocusInsideElementHost(bool explicitPopup)
    {
        RunOnStaThread(() =>
        {
            EnsureWindowsDirectoryEnvironment();
            ResetThemeManagerToUnconfiguredState();
            Assert.Null(Application.Current);
            ThemeManager.Configure(ThemePreference.Light, AccentColorSource.Default);

            var editor = new TextBox { Width = 120 };
            var button = new UiDropDownButton
            {
                Content = "Options",
                Flyout = explicitPopup ? (object)new Popup { Child = editor } : editor,
            };
            int ownerWheelEvents = 0;
            button.MouseWheel += (_, _) => ownerWheelEvents++;

            using var form = new Forms.Form
            {
                ShowInTaskbar = false,
                StartPosition = Forms.FormStartPosition.Manual,
                Location = new System.Drawing.Point(-10_000, -10_000),
                Size = new System.Drawing.Size(240, 120),
            };
            using var host = new ElementHost { Dock = Forms.DockStyle.Fill, Child = button };
            form.Controls.Add(host);

            try
            {
                form.Show();
                Forms.Application.DoEvents();
                DrainDispatcher();

                button.IsDropDownOpen = true;
                Forms.Application.DoEvents();
                DrainDispatcher();

                Assert.Null(Application.Current);
                Assert.True(button.IsDropDownOpen);
                Assert.True(editor.IsKeyboardFocused);

                DependencyObject popupRoot = editor;
                while (VisualTreeHelper.GetParent(popupRoot) is { } parent)
                {
                    popupRoot = parent;
                }

                Assert.NotSame(editor, popupRoot);
                var wheelEvent = new MouseWheelEventArgs(
                    Mouse.PrimaryDevice,
                    Environment.TickCount,
                    120
                )
                {
                    RoutedEvent = Mouse.MouseWheelEvent,
                };
                Assert.IsAssignableFrom<UIElement>(popupRoot).RaiseEvent(wheelEvent);

                Assert.True(wheelEvent.Handled);
                Assert.Equal(0, ownerWheelEvents);
            }
            finally
            {
                button.IsDropDownOpen = false;
                form.Close();
                Forms.Application.DoEvents();
                DrainDispatcher();
            }
        });
    }

    /// <summary>
    /// Verifies that ordinary library-control construction does not activate process-wide theme management. A native
    /// control that already resolved the Windows ThemeStyle must retain it until the host explicitly initializes or
    /// configures <see cref="ThemeManager"/>.
    /// </summary>
    [Fact]
    public void UsingLibraryControl_DoesNotActivateThemeManagement()
    {
        RunOnStaThread(() =>
        {
            EnsureWindowsDirectoryEnvironment();
            ResetThemeManagerToUnconfiguredState();
            Assert.Null(Application.Current);

            var nativeTextBox = new TextBox { Text = "Before library initialization" };
            var root = new StackPanel { Children = { nativeTextBox } };
            using var form = new Forms.Form
            {
                ShowInTaskbar = false,
                StartPosition = Forms.FormStartPosition.Manual,
                Location = new System.Drawing.Point(-10_000, -10_000),
                Size = new System.Drawing.Size(240, 120),
            };
            using var host = new ElementHost { Dock = Forms.DockStyle.Fill, Child = root };
            form.Controls.Add(host);

            try
            {
                form.Show();
                Forms.Application.DoEvents();
                DrainDispatcher();
                Style windowsThemeStyle = GetThemeStyle(nativeTextBox);

                root.Children.Add(new UiScrollBar());
                Forms.Application.DoEvents();
                DrainDispatcher();

                Assert.Null(Application.Current);
                Assert.Same(windowsThemeStyle, GetThemeStyle(nativeTextBox));
                _ = nativeTextBox.ApplyTemplate();
                Assert.Null(nativeTextBox.Template.FindName("FocusIndicator", nativeTextBox));
                Assert.False(IsThemeManagementActive());
            }
            finally
            {
                form.Close();
                Forms.Application.DoEvents();
                DrainDispatcher();
            }
        });
    }

    private static bool IsThemeManagementActive()
    {
        const BindingFlags staticNonPublic = BindingFlags.Static | BindingFlags.NonPublic;
        FieldInfo activationField = typeof(ThemeManager).GetField(
                "_themeManagementActivationState",
                staticNonPublic
            )
            ?? throw new MissingFieldException(
                typeof(ThemeManager).FullName,
                "_themeManagementActivationState"
            );

        return (int)activationField.GetValue(null)! != 0;
    }

    /// <summary>
    /// Verifies that resources required for every theme graph reach WPF's system-resource lookup when a host has no
    /// <see cref="Application"/>. Focus adorners use this exact lookup path whenever an element retains WPF's sentinel
    /// default <see cref="FrameworkElement.FocusVisualStyle"/> value.
    /// </summary>
    [Fact]
    public void Initialize_InstallsGlobalFocusVisualStyleWithoutApplicationOrThemeConfiguration()
    {
        RunOnStaThread(() =>
        {
            EnsureWindowsDirectoryEnvironment();
            ResetThemeManagerToUnconfiguredState();
            Assert.Null(Application.Current);

            ThemeManager.Initialize();

            var probe = new Border();
            Style focusVisualStyle = Assert.IsType<Style>(
                probe.TryFindResource(SystemParameters.FocusVisualStyleKey)
            );
            Setter templateSetter = Assert.IsType<Setter>(
                Assert.Single(
                    focusVisualStyle.Setters,
                    setter => setter is Setter candidate && candidate.Property == Control.TemplateProperty
                )
            );
            var template = Assert.IsType<ControlTemplate>(templateSetter.Value);

            Assert.IsType<Control>(template.LoadContent());
        });
    }

    /// <summary>
    /// Verifies that the slim scroll-bar style scopes WPF's button-size resource overrides where the template Track
    /// can resolve them without relying on Application.Resources or on SystemResources accepting system-key
    /// overrides in a themed dictionary.
    /// </summary>
    [Fact]
    public void ScrollBarThumbMinimum_UsesStyleResourcesWithoutApplication()
    {
        RunOnStaThread(() =>
        {
            EnsureWindowsDirectoryEnvironment();
            ResetThemeManagerToUnconfiguredState();
            Assert.Null(Application.Current);

            var verticalScrollBar = new UiScrollBar
            {
                Width = 12,
                Height = 120,
                Maximum = 10_000,
                ViewportSize = 1,
            };
            var horizontalScrollBar = new UiScrollBar
            {
                Width = 120,
                Height = 12,
                Maximum = 10_000,
                ViewportSize = 1,
                Orientation = Orientation.Horizontal,
            };
            var root = new StackPanel
            {
                Children = { verticalScrollBar, horizontalScrollBar },
            };

            using var form = new Forms.Form
            {
                ShowInTaskbar = false,
                StartPosition = Forms.FormStartPosition.Manual,
                Location = new System.Drawing.Point(-10_000, -10_000),
                Size = new System.Drawing.Size(240, 240),
            };
            using var host = new ElementHost { Dock = Forms.DockStyle.Fill, Child = root };
            form.Controls.Add(host);

            try
            {
                form.Show();
                Forms.Application.DoEvents();
                DrainDispatcher();
                root.UpdateLayout();

                Assert.Null(Application.Current);
                AssertScrollBarThumbMinimum(verticalScrollBar, isVertical: true);
                AssertScrollBarThumbMinimum(horizontalScrollBar, isVertical: false);
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
    /// Verifies that WPF's menu-container preparation resolves <see cref="MenuItem.SeparatorStyleKey"/> from the
    /// system-theme overlay for a top-level ContextMenu separator, which has no MenuItem ancestor that could provide
    /// the resource through Style.Resources.
    /// </summary>
    [Fact]
    public void ContextMenuSeparator_UsesSystemThemeKeyWithoutApplication()
    {
        RunOnStaThread(() =>
        {
            EnsureWindowsDirectoryEnvironment();
            ResetThemeManagerToUnconfiguredState();
            Assert.Null(Application.Current);

            var separator = new Separator();
            var contextMenu = new ContextMenu
            {
                Items =
                {
                    new MenuItem { Header = "Before" },
                    separator,
                    new MenuItem { Header = "After" },
                },
            };
            var placementTarget = new Border
            {
                Width = 80,
                Height = 30,
                ContextMenu = contextMenu,
            };

            ThemeManager.Configure(
                ThemePreference.Light,
                AccentColorSource.Default
            );

            using var form = new Forms.Form
            {
                ShowInTaskbar = false,
                StartPosition = Forms.FormStartPosition.Manual,
                Location = new System.Drawing.Point(-10_000, -10_000),
                Size = new System.Drawing.Size(160, 120),
            };
            using var host = new ElementHost
            {
                Dock = Forms.DockStyle.Fill,
                Child = placementTarget,
            };
            form.Controls.Add(host);

            try
            {
                form.Show();
                contextMenu.PlacementTarget = placementTarget;
                contextMenu.IsOpen = true;
                Forms.Application.DoEvents();
                DrainDispatcher();
                contextMenu.UpdateLayout();

                Assert.Null(Application.Current);
                Assert.True(contextMenu.IsOpen);
                Assert.Same(
                    separator.TryFindResource(MenuItem.SeparatorStyleKey),
                    separator.Style
                );
                Assert.Equal(new Thickness(0, 2, 0, 2), separator.Margin);
                _ = separator.ApplyTemplate();
                Assert.NotNull(separator.Template.FindName("Border", separator));
            }
            finally
            {
                contextMenu.IsOpen = false;
                form.Close();
                Forms.Application.DoEvents();
                DrainDispatcher();
            }
        });
    }

    /// <summary>
    /// Verifies that the first explicit theme switch refreshes resources that were originally resolved from
    /// Generic.xaml before an applicationless presentation source existed.
    /// </summary>
    [Fact]
    public void FirstThemeSwitch_AfterWindowIsShown_RefreshesWindowAndControlResources()
    {
        RunOnStaThread(() =>
        {
            EnsureWindowsDirectoryEnvironment();
            ResetThemeManagerToUnconfiguredState();
            Assert.Null(Application.Current);
            Assert.Equal(ThemeVariant.Light, ThemeManager.EffectiveTheme);

            var comboBox = new UiComboBox { ItemsSource = new[] { "Item" }, SelectedIndex = 0 };
            var autoSuggestBox = new UiAutoSuggestBox
            {
                PlaceholderText = "Search",
                ItemsSource = new[] { "Dashboard" },
            };
            var elementHostRoot = new StackPanel { Children = { comboBox, autoSuggestBox } };
            using var form = new Forms.Form
            {
                ShowInTaskbar = false,
                StartPosition = Forms.FormStartPosition.Manual,
                Location = new System.Drawing.Point(-10_000, -10_000),
                Size = new System.Drawing.Size(320, 240),
            };
            using var elementHost = new ElementHost
            {
                Dock = Forms.DockStyle.Fill,
                Child = elementHostRoot,
            };
            form.Controls.Add(elementHost);

            var window = new UiFluentWindow
            {
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -10_000,
                Top = -10_000,
                Width = 320,
                Height = 240,
            };

            try
            {
                window.Show();
                form.Show();
                Forms.Application.DoEvents();
                DrainDispatcher();

                _ = comboBox.ApplyTemplate();
                _ = autoSuggestBox.ApplyTemplate();
                Wpf.Zen.Ui.Controls.TextBox autoSuggestTextBox =
                    Assert.IsType<Wpf.Zen.Ui.Controls.TextBox>(
                        autoSuggestBox.Template.FindName("PART_TextBox", autoSuggestBox)
                    );

                Assert.Equal(Color.FromRgb(0xFA, 0xFA, 0xFA), GetBrushColor(window.Background));
                Assert.Equal(
                    Color.FromArgb(0x80, 0xF0, 0xF0, 0xF0),
                    GetBrushColor(comboBox.Background)
                );
                Assert.Equal(
                    Color.FromArgb(0x80, 0xF0, 0xF0, 0xF0),
                    GetBrushColor(autoSuggestTextBox.Background)
                );

                // ThemePreference is intentionally the first configuration API used in this process and is called only
                // after Generic.xaml's Light resources have already supplied the live controls' style values.
                ThemeManager.Preference = ThemePreference.Dark;
                DrainDispatcher();

                Assert.Null(Application.Current);
                Assert.Equal(ThemeVariant.Dark, ThemeManager.EffectiveTheme);
                Assert.Equal(Color.FromRgb(0x20, 0x20, 0x20), GetBrushColor(window.Background));
                Assert.Equal(
                    Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF),
                    GetBrushColor(comboBox.Background)
                );
                Assert.Equal(
                    Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF),
                    GetBrushColor(autoSuggestTextBox.Background)
                );
            }
            finally
            {
                window.Close();
                form.Close();
                Forms.Application.DoEvents();
                DrainDispatcher();
            }
        });
    }

    /// <summary>
    /// Verifies that a navigation scope can own pages and contextual services inside ElementHost
    /// without manufacturing a WPF Window or Application singleton.
    /// </summary>
    [Fact]
    public void NavigationScope_NavigatesInsideElementHost_WithoutWpfWindowOrApplication()
    {
        RunOnStaThread(() =>
        {
            Assert.Null(Application.Current);

            var navigationView = new UiNavigationView();
            ElementHostNavigationPage? navigatedPage = null;
            navigationView.Navigated += (_, args) =>
                navigatedPage = Assert.IsType<ElementHostNavigationPage>(args.Page);

            using INavigationScope navigationScope = Wpf.Zen.Ui.NavigationScope.Attach(navigationView);
            using var form = new Forms.Form
            {
                ShowInTaskbar = false,
                StartPosition = Forms.FormStartPosition.Manual,
                Location = new System.Drawing.Point(-10_000, -10_000),
                Size = new System.Drawing.Size(320, 240),
            };
            using var elementHost = new ElementHost
            {
                Dock = Forms.DockStyle.Fill,
                Child = navigationView,
            };
            form.Controls.Add(elementHost);

            form.Show();
            Forms.Application.DoEvents();
            DrainDispatcher();

            Assert.True(
                navigationScope.Navigation
                   .Navigate(typeof(ElementHostNavigationPage), "from-winforms")
                   .Succeeded
            );
            Assert.NotNull(navigatedPage);
            Assert.Null(Application.Current);
            Assert.Same(
                navigationScope.Navigation,
                Wpf.Zen.Ui.Navigation.GetService(navigatedPage)
            );
            Assert.Same(navigationView, Wpf.Zen.Ui.Navigation.GetHost(navigatedPage));
            Assert.Equal("from-winforms", Wpf.Zen.Ui.Navigation.GetParameter(navigatedPage));
        });
    }

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
                ThemePreference.Light,
                AccentColorSource.Default
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

                Assert.Equal(ThemeVariant.Light, ThemeManager.EffectiveTheme);
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
                    ThemePreference.Dark,
                    AccentColorSource.Default
                );
                Forms.Application.DoEvents();
                DrainDispatcher();

                Assert.Null(Application.Current);
                Assert.Equal(ThemeVariant.Dark, ThemeManager.EffectiveTheme);
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
                    ThemePreference.Light,
                    AccentColorSource.Default
                );
                Forms.Application.DoEvents();
                DrainDispatcher();

                Assert.Null(Application.Current);
                Assert.Equal(ThemeVariant.Light, ThemeManager.EffectiveTheme);
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
                    ThemePreference.Dark,
                    AccentColorSource.Default
                );
                Forms.Application.DoEvents();
                DrainDispatcher();

                // WM_THEMECHANGED clears WPF's private themed-dictionary slots. A subsequent ThemeManager refresh
                // must detect that its old overlay was unloaded, capture the newly loaded Windows dictionary, and
                // reinstall the Wpf.Zen.Ui overlay without falling back to Application.
                Assert.Null(Application.Current);
                Assert.Equal(ThemeVariant.Dark, ThemeManager.EffectiveTheme);
                Assert.Equal(
                    Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF),
                    GetBrushColor(textBox.Background)
                );

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
                ThemePreference.Light,
                AccentColorSource.Default,
                Color.FromRgb(0x00, 0x78, 0xD4),
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

                ThemeManager.Preference = ThemePreference.Dark;
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

    private static void AssertScrollBarThumbMinimum(UiScrollBar scrollBar, bool isVertical)
    {
        _ = scrollBar.ApplyTemplate();
        var track = Assert.IsType<Track>(scrollBar.Template.FindName("PART_Track", scrollBar));
        ResourceKey resourceKey = isVertical
            ? SystemParameters.VerticalScrollBarButtonHeightKey
            : SystemParameters.HorizontalScrollBarButtonWidthKey;

        Assert.Equal(36d, Assert.IsType<double>(track.TryFindResource(resourceKey)));
        Assert.Equal(18d, isVertical ? track.Thumb.ActualHeight : track.Thumb.ActualWidth);
    }

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

    /// <summary>
    /// Restores the process-wide manager state needed to exercise its one-time activation path deterministically.
    /// </summary>
    /// <remarks>
    /// xUnit does not create a new process for every test, while WPF's SystemResources table and ThemeManager are
    /// process-wide singletons. Public APIs cannot deactivate theme management, so this test-only reset deliberately
    /// reflects the same private state whose first transition is under test. Restore is invoked before clearing the
    /// host reference so WPF never retains an overlay that ThemeManager can no longer remove. A missing member is a
    /// hard test failure: unlike production reflection, silently degrading here would make the regression test pass
    /// without exercising the intended path.
    /// </remarks>
    private static void ResetThemeManagerToUnconfiguredState()
    {
        const BindingFlags staticNonPublic = BindingFlags.Static | BindingFlags.NonPublic;
        Type managerType = typeof(ThemeManager);
        FieldInfo hostField = managerType.GetField("_systemThemeResourceHost", staticNonPublic)
            ?? throw new MissingFieldException(managerType.FullName, "_systemThemeResourceHost");
        object? host = hostField.GetValue(null);
        if (host is not null)
        {
            MethodInfo restoreMethod = host.GetType().GetMethod(
                    "Restore",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?? throw new MissingMethodException(host.GetType().FullName, "Restore");
            _ = restoreMethod.Invoke(host, null);
        }

        hostField.SetValue(null, null);
        managerType.GetField("_systemThemeResourceHostUnavailable", staticNonPublic)!.SetValue(null, false);
        managerType.GetField("_themeManagementActivationState", staticNonPublic)!.SetValue(null, 0);
        managerType.GetField("_themePreference", staticNonPublic)!.SetValue(null, ThemePreference.Light);
        typeof(ThemeManager)
           .GetProperty(nameof(ThemeManager.EffectiveTheme), BindingFlags.Static | BindingFlags.Public)!
           .SetValue(null, ThemeVariant.Light);
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
            finally
            {
                // Each test owns this temporary WPF UI thread. Shutting down its Dispatcher is part of tearing down
                // the visual island: ThemeManager deliberately retains discovered roots weakly, and an ElementHost
                // tree can remain reachable briefly after disposal. Without an explicit shutdown, that stale root
                // still exposes a Dispatcher whose thread has exited but whose shutdown flags remain false, so a
                // later test island could incorrectly select it as the active theme dispatcher.
                Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
                if (!dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
                {
                    dispatcher.InvokeShutdown();
                }
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

    private sealed class ElementHostNavigationPage : Border { }
}
