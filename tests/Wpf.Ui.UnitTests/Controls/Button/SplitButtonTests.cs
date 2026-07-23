// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Controls;
using Wpf.Ui.Markup;
using Wpf.Ui.Theming;

namespace Wpf.Ui.UnitTests.Controls.ButtonControl;

public class SplitButtonTests
{
    [Fact]
    public void SplitButton_IsAWrapperWithIndependentButtonParts()
    {
        RunOnStaThread(
            () =>
            {
                var splitButton = CreateTemplatedSplitButton();

                Assert.IsAssignableFrom<ContentControl>(splitButton);
                Assert.IsNotAssignableFrom<ButtonBase>(splitButton);
                Assert.IsType<Wpf.Ui.Controls.Button>(
                    splitButton.Template.FindName("PART_PrimaryButton", splitButton)
                );
                Assert.IsType<Wpf.Ui.Controls.Button>(
                    splitButton.Template.FindName("PART_DropDownButton", splitButton)
                );
            }
        );
    }

    [Fact]
    public void Template_BridgesContentCommandAppearanceAndSplitGeometry()
    {
        RunOnStaThread(
            () =>
            {
                var command = new RoutedCommand();
                var icon = new SymbolIcon(SymbolRegular.PaintBrush24);
                var content = new Border();
                var splitButton = CreateTemplatedSplitButton(
                    new SplitButton
                    {
                        Appearance       = ControlAppearance.Accent,
                        Background       = Brushes.AliceBlue,
                        BorderBrush      = Brushes.Bisque,
                        BorderThickness  = new Thickness(1, 2, 3, 4),
                        ClickMode        = ClickMode.Press,
                        Command          = command,
                        CommandParameter = "parameter",
                        Content          = content,
                        CornerRadius     = new CornerRadius(5, 6, 7, 8),
                        Foreground       = Brushes.Coral,
                        Icon             = icon,
                        MouseOverBackground  = Brushes.DarkBlue,
                        MouseOverBorderBrush = Brushes.DarkCyan,
                        PressedBackground    = Brushes.DarkGray,
                        PressedBorderBrush   = Brushes.DarkGreen,
                        PressedForeground    = Brushes.DarkGoldenrod,
                    }
                );

                var primaryButton = Assert.IsType<Wpf.Ui.Controls.Button>(
                    splitButton.Template.FindName("PART_PrimaryButton", splitButton)
                );
                var dropDownButton = Assert.IsType<Wpf.Ui.Controls.Button>(
                    splitButton.Template.FindName("PART_DropDownButton", splitButton)
                );

                Assert.Equal(ControlAppearance.Accent, primaryButton.Appearance);
                Assert.Equal(ControlAppearance.Accent, dropDownButton.Appearance);
                AssertSegmentBrushes(primaryButton);
                AssertSegmentBrushes(dropDownButton);
                Assert.Equal(new Thickness(1, 2, 0, 4), primaryButton.BorderThickness);
                Assert.Equal(new Thickness(0, 2, 3, 4), dropDownButton.BorderThickness);
                Assert.Equal(new CornerRadius(5, 0, 0, 8), primaryButton.CornerRadius);
                Assert.Equal(new CornerRadius(0, 6, 7, 0), dropDownButton.CornerRadius);
                Assert.Equal(ClickMode.Press, primaryButton.ClickMode);
                Assert.True(splitButton.Focusable);
                Assert.False(primaryButton.Focusable);
                Assert.False(primaryButton.IsTabStop);
                Assert.Equal(ClickMode.Press, dropDownButton.ClickMode);
                Assert.Same(command, primaryButton.Command);
                Assert.Equal("parameter", primaryButton.CommandParameter);
                Assert.Same(content, primaryButton.Content);
                Assert.Same(icon, primaryButton.Icon);
            }
        );
    }

    [Fact]
    public void InteractionBrushProperties_ShareButtonDependencyProperties()
    {
        Assert.Same(
            Wpf.Ui.Controls.Button.MouseOverBackgroundProperty,
            SplitButton.MouseOverBackgroundProperty
        );
        Assert.Same(
            Wpf.Ui.Controls.Button.MouseOverBorderBrushProperty,
            SplitButton.MouseOverBorderBrushProperty
        );
        Assert.Same(
            Wpf.Ui.Controls.Button.PressedForegroundProperty,
            SplitButton.PressedForegroundProperty
        );
        Assert.Same(
            Wpf.Ui.Controls.Button.PressedBackgroundProperty,
            SplitButton.PressedBackgroundProperty
        );
        Assert.Same(
            Wpf.Ui.Controls.Button.PressedBorderBrushProperty,
            SplitButton.PressedBorderBrushProperty
        );
    }

    [Fact]
    public void PrimaryClick_RaisesOnlyTheSplitButtonClickAcrossTheTemplateBoundary()
    {
        RunOnStaThread(
            () =>
            {
                var root = new StackPanel();
                var splitButton = CreateTemplatedSplitButton(parent: root);
                var primaryButton = Assert.IsType<Wpf.Ui.Controls.Button>(
                    splitButton.Template.FindName("PART_PrimaryButton", splitButton)
                );
                var dropDownButton = Assert.IsType<Wpf.Ui.Controls.Button>(
                    splitButton.Template.FindName("PART_DropDownButton", splitButton)
                );
                var splitClickCount = 0;
                var leakedButtonBaseClickCount = 0;

                splitButton.Click += (_, _) => splitClickCount++;
                root.AddHandler(
                    ButtonBase.ClickEvent,
                    new RoutedEventHandler((_, _) => leakedButtonBaseClickCount++)
                );

                primaryButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, primaryButton));

                Assert.Equal(1, splitClickCount);
                Assert.Equal(0, leakedButtonBaseClickCount);

                dropDownButton.RaiseEvent(
                    new RoutedEventArgs(ButtonBase.ClickEvent, dropDownButton)
                );

                Assert.Equal(1, splitClickCount);
                Assert.Equal(0, leakedButtonBaseClickCount);
            }
        );
    }

    [Fact]
    public void FlyoutReplacement_DetachesOldMenuAndPublishesOwnedStateEvents()
    {
        RunOnStaThread(
            () =>
            {
                var splitButton = new SplitButton();
                var oldMenu = new ContextMenu();
                var currentMenu = new ContextMenu();
                var openedCount = 0;
                var closedCount = 0;

                splitButton.DropDownOpened += (_, _) => openedCount++;
                splitButton.DropDownClosed += (_, _) => closedCount++;
                splitButton.Flyout = oldMenu;
                splitButton.Flyout = currentMenu;

                oldMenu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent, oldMenu));
                Assert.False(splitButton.IsDropDownOpen);
                Assert.Equal(0, openedCount);

                currentMenu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent, currentMenu));
                Assert.True(splitButton.IsDropDownOpen);
                Assert.Equal(1, openedCount);

                currentMenu.RaiseEvent(new RoutedEventArgs(ContextMenu.ClosedEvent, currentMenu));
                Assert.False(splitButton.IsDropDownOpen);
                Assert.Equal(1, closedCount);

                splitButton.Flyout = null;
                splitButton.IsDropDownOpen = true;
                Assert.False(splitButton.IsDropDownOpen);
            }
        );
    }

    [Fact]
    public void DropDownClick_ClosesAnOpenFlyoutInsteadOfReopeningIt()
    {
        RunOnStaThread(
            () =>
            {
                var contextMenu = new ContextMenu();
                var splitButton = CreateTemplatedSplitButton(
                    new SplitButton { Flyout = contextMenu }
                );
                var dropDownButton = Assert.IsType<Wpf.Ui.Controls.Button>(
                    splitButton.Template.FindName("PART_DropDownButton", splitButton)
                );

                contextMenu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent, contextMenu));
                Assert.True(splitButton.IsDropDownOpen);

                dropDownButton.RaiseEvent(
                    new RoutedEventArgs(ButtonBase.ClickEvent, dropDownButton)
                );

                Assert.False(splitButton.IsDropDownOpen);
                Assert.False(contextMenu.IsOpen);
            }
        );
    }

    [Fact]
    public void TemplateButtonMouseDown_MovesKeyboardFocusBeforeClick()
    {
        RunOnStaThread(
            () =>
            {
                var root = new StackPanel();
                var previousFocus = new System.Windows.Controls.TextBox();
                root.Children.Add(previousFocus);
                var splitButton = CreateTemplatedSplitButton(parent: root);
                var primaryButton = Assert.IsType<Wpf.Ui.Controls.Button>(
                    splitButton.Template.FindName("PART_PrimaryButton", splitButton)
                );
                var dropDownButton = Assert.IsType<Wpf.Ui.Controls.Button>(
                    splitButton.Template.FindName("PART_DropDownButton", splitButton)
                );
                var window = new Window { Content = root };
                var clickCount = 0;
                splitButton.Click += (_, _) => clickCount++;

                try
                {
                    window.Show();
                    Assert.True(previousFocus.Focus());

                    RaisePreviewMouseLeftButtonDown(primaryButton);

                    Assert.True(splitButton.IsKeyboardFocused);
                    Assert.Equal(0, clickCount);

                    primaryButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, primaryButton));
                    Assert.Equal(1, clickCount);

                    Assert.True(previousFocus.Focus());
                    RaisePreviewMouseLeftButtonDown(dropDownButton);

                    Assert.True(splitButton.IsKeyboardFocused);
                    Assert.Equal(1, clickCount);
                }
                finally
                {
                    window.Close();
                }
            }
        );
    }

    [Fact]
    public void F4_OpensFromSplitButtonAndClosesFromContextMenu()
    {
        RunOnStaThread(
            () =>
            {
                var contextMenu = new ContextMenu { Items = { new MenuItem { Header = "Item" } } };
                var root = new StackPanel();
                var splitButton = CreateTemplatedSplitButton(
                    new SplitButton { Flyout = contextMenu },
                    root
                );
                var window = new Window { Content = root };

                try
                {
                    window.Show();
                    Assert.True(splitButton.Focus());
                    PresentationSource source = Assert.IsAssignableFrom<PresentationSource>(
                        PresentationSource.FromVisual(splitButton)
                    );

                    splitButton.RaiseEvent(CreateKeyEvent(source, Key.F4, Keyboard.KeyDownEvent));
                    Assert.True(splitButton.IsDropDownOpen);
                    Assert.True(contextMenu.IsOpen);

                    contextMenu.RaiseEvent(
                        CreateKeyEvent(source, Key.F4, Keyboard.PreviewKeyDownEvent)
                    );
                    Assert.False(splitButton.IsDropDownOpen);
                    Assert.False(contextMenu.IsOpen);
                    Assert.True(splitButton.IsKeyboardFocused);
                }
                finally
                {
                    window.Close();
                }
            }
        );
    }

    [Fact]
    public void StyleResourceExtension_MapsTheSplitButtonComponentKey()
    {
        var extension = new StyleResourceExtension(StyleId.UiSplitButtonStyle);

        Assert.Same(StyleKeys.UiSplitButtonStyle, extension.ResourceKey);
    }

    private static SplitButton CreateTemplatedSplitButton(
        SplitButton? splitButton = null,
        Panel? parent = null
    )
    {
        splitButton ??= new SplitButton();
        parent ??= new StackPanel();
        parent.Resources.MergedDictionaries.Add(
            new ResourceDictionary
            {
                Source = new Uri(
                    "pack://application:,,,/Wpf.Ui;component/Themes/Generic.xaml",
                    UriKind.Absolute
                ),
            }
        );
        parent.Children.Add(splitButton);

        Assert.True(splitButton.ApplyTemplate());
        parent.Measure(new Size(800, 600));
        parent.Arrange(new Rect(0, 0, 800, 600));
        return splitButton;
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(
            () =>
            {
                try
                {
                    action();
                }
                catch (Exception caughtException)
                {
                    exception = caughtException;
                }
            }
        );

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    private static KeyEventArgs CreateKeyEvent(
        PresentationSource source,
        Key key,
        RoutedEvent routedEvent
    )
    {
        return new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
        {
            RoutedEvent = routedEvent,
        };
    }

    private static void RaisePreviewMouseLeftButtonDown(UIElement element)
    {
        element.RaiseEvent(
            new MouseButtonEventArgs(
                Mouse.PrimaryDevice,
                Environment.TickCount,
                MouseButton.Left
            )
            {
                RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent,
            }
        );
    }

    private static void AssertSegmentBrushes(Wpf.Ui.Controls.Button button)
    {
        Assert.Same(Brushes.AliceBlue, button.Background);
        Assert.Same(Brushes.Bisque, button.BorderBrush);
        Assert.Same(Brushes.Coral, button.Foreground);
        Assert.Same(Brushes.DarkBlue, button.MouseOverBackground);
        Assert.Same(Brushes.DarkCyan, button.MouseOverBorderBrush);
        Assert.Same(Brushes.DarkGray, button.PressedBackground);
        Assert.Same(Brushes.DarkGreen, button.PressedBorderBrush);
        Assert.Same(Brushes.DarkGoldenrod, button.PressedForeground);
    }
}
