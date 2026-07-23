// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
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
                        DisabledBackground   = Brushes.DimGray,
                        DisabledBorderBrush  = Brushes.Gray,
                        DisabledForeground   = Brushes.LightGray,
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
        Assert.Same(
            Wpf.Ui.Controls.Button.DisabledBackgroundProperty,
            SplitButton.DisabledBackgroundProperty
        );
        Assert.Same(
            Wpf.Ui.Controls.Button.DisabledBorderBrushProperty,
            SplitButton.DisabledBorderBrushProperty
        );
        Assert.Same(
            Wpf.Ui.Controls.Button.DisabledForegroundProperty,
            SplitButton.DisabledForegroundProperty
        );
    }

    [Fact]
    public void CommandDisabledPrimarySegment_UsesDisabledBrushesIndependently()
    {
        RunOnStaThread(
            () =>
            {
                var splitButton = CreateTemplatedSplitButton(
                    new SplitButton
                    {
                        Background = Brushes.AliceBlue,
                        BorderBrush = Brushes.Bisque,
                        Foreground = Brushes.Coral,
                        DisabledBackground = Brushes.DimGray,
                        DisabledBorderBrush = Brushes.Gray,
                        DisabledForeground = Brushes.LightGray,
                        Command = new DisabledCommand(),
                    }
                );
                var primaryButton = Assert.IsType<Wpf.Ui.Controls.Button>(
                    splitButton.Template.FindName("PART_PrimaryButton", splitButton)
                );
                var dropDownButton = Assert.IsType<Wpf.Ui.Controls.Button>(
                    splitButton.Template.FindName("PART_DropDownButton", splitButton)
                );

                Assert.False(primaryButton.IsEnabled);
                Assert.Same(Brushes.DimGray, primaryButton.VisualBackground);
                Assert.Same(Brushes.Gray, primaryButton.VisualBorderBrush);
                Assert.Same(Brushes.LightGray, primaryButton.VisualForeground);

                Assert.True(dropDownButton.IsEnabled);
                Assert.Same(Brushes.AliceBlue, dropDownButton.VisualBackground);
                Assert.Same(Brushes.Bisque, dropDownButton.VisualBorderBrush);
                Assert.Same(Brushes.Coral, dropDownButton.VisualForeground);
            }
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
    public void SpacePress_InvokesOnceAndIgnoresBubbledContentInput()
    {
        RunOnStaThread(
            () =>
            {
                var contentTextBox = new System.Windows.Controls.TextBox();
                var root = new StackPanel();
                var splitButton = CreateTemplatedSplitButton(
                    new SplitButton
                    {
                        ClickMode = ClickMode.Press,
                        Content = contentTextBox,
                    },
                    root
                );
                var window = new Window { Content = root };
                var clickCount = 0;
                splitButton.Click += (_, _) => clickCount++;

                try
                {
                    window.Show();
                    Assert.True(splitButton.Focus());
                    PresentationSource source = Assert.IsAssignableFrom<PresentationSource>(
                        PresentationSource.FromVisual(splitButton)
                    );

                    splitButton.RaiseEvent(CreateKeyEvent(source, Key.Space, Keyboard.KeyDownEvent));
                    splitButton.RaiseEvent(CreateKeyEvent(source, Key.Space, Keyboard.KeyDownEvent));
                    Assert.Equal(1, clickCount);

                    splitButton.RaiseEvent(CreateKeyEvent(source, Key.Space, Keyboard.KeyUpEvent));
                    Assert.True(contentTextBox.Focus());
                    contentTextBox.RaiseEvent(
                        CreateKeyEvent(source, Key.Space, Keyboard.KeyDownEvent)
                    );

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
    public void MissingPrimaryTemplatePart_DisablesKeyboardInvocation()
    {
        RunOnStaThread(
            () =>
            {
                var template = new ControlTemplate(typeof(SplitButton))
                {
                    VisualTree = new FrameworkElementFactory(typeof(Border)),
                };
                var root = new StackPanel();
                var splitButton = CreateTemplatedSplitButton(
                    new SplitButton { Template = template },
                    root
                );
                var window = new Window { Content = root };
                var clickCount = 0;
                splitButton.Click += (_, _) => clickCount++;

                try
                {
                    window.Show();
                    Assert.True(splitButton.Focus());
                    PresentationSource source = Assert.IsAssignableFrom<PresentationSource>(
                        PresentationSource.FromVisual(splitButton)
                    );

                    splitButton.RaiseEvent(CreateKeyEvent(source, Key.Enter, Keyboard.KeyDownEvent));
                    splitButton.RaiseEvent(CreateKeyEvent(source, Key.Space, Keyboard.KeyDownEvent));
                    splitButton.RaiseEvent(CreateKeyEvent(source, Key.Space, Keyboard.KeyUpEvent));

                    Assert.Equal(0, clickCount);
                }
                finally
                {
                    window.Close();
                }
            }
        );
    }

    [Fact]
    public void AutomationPeer_ExposesInvokeAndExpandCollapseForTheFocusableWrapper()
    {
        RunOnStaThread(
            () =>
            {
                var contextMenu = new ContextMenu { Items = { new MenuItem { Header = "Item" } } };
                var root = new StackPanel();
                var splitButton = CreateTemplatedSplitButton(
                    new SplitButton { Content = "Run", Flyout = contextMenu },
                    root
                );
                var window = new Window { Content = root };
                var clickCount = 0;
                splitButton.Click += (_, _) => clickCount++;

                try
                {
                    window.Show();
                    AutomationPeer peer = CreateAutomationPeer(splitButton);
                    var invokeProvider = Assert.IsAssignableFrom<IInvokeProvider>(
                        peer.GetPattern(PatternInterface.Invoke)
                    );
                    var expandProvider = Assert.IsAssignableFrom<IExpandCollapseProvider>(
                        peer.GetPattern(PatternInterface.ExpandCollapse)
                    );

                    Assert.Equal(AutomationControlType.Button, peer.GetAutomationControlType());
                    Assert.Equal("SplitButton", peer.GetClassName());
                    Assert.Equal("Run", peer.GetName());
                    Assert.DoesNotContain(
                        peer.GetChildren() ?? [],
                        child => child is ButtonBaseAutomationPeer
                    );
                    Assert.Equal(ExpandCollapseState.Collapsed, expandProvider.ExpandCollapseState);

                    invokeProvider.Invoke();
                    DrainDispatcher();
                    Assert.Equal(1, clickCount);

                    expandProvider.Expand();
                    Assert.True(splitButton.IsDropDownOpen);
                    Assert.Equal(ExpandCollapseState.Expanded, expandProvider.ExpandCollapseState);

                    expandProvider.Collapse();
                    Assert.False(splitButton.IsDropDownOpen);
                    Assert.Equal(ExpandCollapseState.Collapsed, expandProvider.ExpandCollapseState);
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

    private static AutomationPeer CreateAutomationPeer(SplitButton splitButton)
    {
        return (AutomationPeer)typeof(SplitButton)
            .GetMethod("OnCreateAutomationPeer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(splitButton, null)!;
    }

    private static void DrainDispatcher()
    {
        var frame = new DispatcherFrame();
        _ = Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false)
        );
        Dispatcher.PushFrame(frame);
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
        Assert.Same(Brushes.DimGray, button.DisabledBackground);
        Assert.Same(Brushes.Gray, button.DisabledBorderBrush);
        Assert.Same(Brushes.LightGray, button.DisabledForeground);
    }

    private sealed class DisabledCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter)
        {
            return false;
        }

        public void Execute(object? parameter)
        {
            throw new InvalidOperationException("A disabled command must not execute.");
        }
    }
}
