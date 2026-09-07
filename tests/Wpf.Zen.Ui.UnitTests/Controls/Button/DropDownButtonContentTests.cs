// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;
using ContentControl = System.Windows.Controls.ContentControl;
using TextBox = System.Windows.Controls.TextBox;

namespace Wpf.Zen.Ui.UnitTests.Controls.ButtonControl;

[Collection(nameof(KeyboardFocusTestCollection))]
public class DropDownButtonContentTests
{
    [Fact]
    public void PopupStyle_HasThemeDefaultsWithoutButtonStyleOrResourceImports()
    {
        WpfTestThread.Run(() =>
        {
            var button = new DropDownButton
            {
                Style = new Style(typeof(DropDownButton)),
                OverridesDefaultStyle = true,
                Flyout = "Content",
            };
            var popup = GetPopup(button);
            Assert.Null(button.FlyoutPopupStyle);
            Assert.True(popup.AllowsTransparency);
            Assert.False(popup.StaysOpen);
            Assert.Equal(0D, popup.VerticalOffset);
            Assert.Equal(4D, PopupPlacementGap.GetVertical(popup));
            Assert.True(PopupPlacementTracker.GetIsEnabled(popup));

            // Explicit style setters must coexist with independent theme defaults, without BasedOn.
            button.FlyoutPopupStyle = new Style(typeof(Popup))
            {
                Setters = { new Setter(Popup.HorizontalOffsetProperty, 19D) },
            };
            Assert.Equal(19D, popup.HorizontalOffset);
            Assert.True(popup.AllowsTransparency);
            Assert.Equal(0D, popup.VerticalOffset);
            Assert.Equal(4D, PopupPlacementGap.GetVertical(popup));
        });
    }

    [Fact]
    public void PopupStyle_TracksScopeReplacementReparentingAndExplicitOverrideReset()
    {
        WpfTestThread.Run(() =>
        {
            var key = DropDownButton.FlyoutPopupStyleKey;
            static Style Offset(double value) => new(typeof(Popup))
            {
                Setters = { new Setter(Popup.HorizontalOffsetProperty, value) },
            };
            var firstParent = new StackPanel();
            var secondParent = new StackPanel();
            firstParent.Resources[key] = Offset(10);
            secondParent.Resources[key] = Offset(20);
            var button = new DropDownButton { Flyout = "Content" };
            var popup = GetPopup(button);
            firstParent.Children.Add(button);
            Assert.Equal(10D, popup.HorizontalOffset);
            button.Resources[key] = Offset(30);
            Assert.Equal(30D, popup.HorizontalOffset);
            button.Resources[key] = Offset(40);
            Assert.Equal(40D, popup.HorizontalOffset);
            button.FlyoutPopupStyle = Offset(50);
            button.Resources[key] = Offset(60);
            Assert.Equal(50D, popup.HorizontalOffset);
            button.FlyoutPopupStyle = null;
            Assert.Equal(60D, popup.HorizontalOffset);
            button.Resources.Remove(key);
            Assert.Equal(10D, popup.HorizontalOffset);
            firstParent.Children.Remove(button);
            secondParent.Children.Add(button);
            Assert.Equal(20D, popup.HorizontalOffset);
            secondParent.Resources.Remove(key);
            Assert.Equal(0D, popup.HorizontalOffset);
            Assert.True(popup.AllowsTransparency);
            Assert.Same(popup, GetPopup(button));
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void LogicalChildren_SupportsEnumerationAndResetWithoutDisposal(bool hasContent, bool hasFlyout)
    {
        WpfTestThread.Run(() =>
        {
            var content = new object();
            var button = new Probe
            {
                Content = hasContent ? content : null,
                Flyout = hasFlyout ? new object() : null,
            };
            var children = button.GetLogicalChildren();
            Assert.False(children is IDisposable);

            for (int pass = 0; pass < 2; pass++)
            {
                Assert.Throws<InvalidOperationException>(() => children.Current);
                if (hasContent)
                {
                    Assert.True(children.MoveNext());
                    Assert.Same(content, children.Current);
                }

                if (hasFlyout)
                {
                    Assert.True(children.MoveNext());
                    var popup = Assert.IsType<Popup>(children.Current);
                    Assert.Same(button, LogicalTreeHelper.GetParent(popup));
                }

                Assert.False(children.MoveNext());
                Assert.False(children.MoveNext());
                Assert.Throws<InvalidOperationException>(() => children.Current);
                children.Reset();
            }
        });
    }

    [Fact]
    public void UiContent_InheritsContextAndResourcesEvenWhileClosed_AndReleasesOwnership()
    {
        WpfTestThread.Run(() =>
        {
            var content = new TextBox();
            content.SetBinding(TextBox.TextProperty, new Binding { Mode = BindingMode.OneWay });
            content.SetResourceReference(Control.ForegroundProperty, "Ink");
            var button = CreateButton(content);
            var popup = GetPopup(button);
            button.DataContext = "First";
            button.Resources["Ink"] = Brushes.Red;
            Assert.Equal("First", content.DataContext);
            Drain();
            Assert.Equal("First", content.Text);
            Assert.Same(Brushes.Red, content.Foreground);
            button.DataContext = "Second";
            button.Resources["Ink"] = Brushes.Blue;
            Drain();
            Assert.Equal("Second", content.Text);
            Assert.Same(Brushes.Blue, content.Foreground);
            Assert.Same(button, LogicalTreeHelper.GetParent(popup));
            Assert.Same(content, GetHost(button).Content);
            button.Flyout = null;
            Assert.Null(LogicalTreeHelper.GetParent(content));
            Assert.Null(LogicalTreeHelper.GetParent(popup));
            Assert.Null(popup.Child);
            Assert.Null(popup.PlacementTarget);
            Assert.Empty(LogicalTreeHelper.GetChildren(button).OfType<Popup>());
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Popup_ConsumesWheelInputAtItsLogicalBoundary(bool explicitPopup)
    {
        WpfTestThread.Run(() =>
        {
            var content = new TextBox();
            var button = CreateButton(explicitPopup ? new Popup { Child = content } : content);
            int ownerWheelEvents = 0;
            button.MouseWheel += (_, _) => ownerWheelEvents++;

            WithWindow(button, () =>
            {
                button.IsDropDownOpen = true;
                Drain();

                // PopupRoot is the captured element used by a non-sticky Popup. Raising the wheel
                // there models the route WPF builds when the physical pointer is outside the HWND;
                // without the Popup boundary handler it continues through the logical owner.
                DependencyObject popupRoot = content;
                while (VisualTreeHelper.GetParent(popupRoot) is { } parent)
                {
                    popupRoot = parent;
                }

                Assert.NotSame(content, popupRoot);
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
            });
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Popup_ConsumesScrollingKeysAfterContent_AndPreservesOtherKeys(bool explicitPopup)
    {
        WpfTestThread.Run(() =>
        {
            var content = new Border { Focusable = true, Width = 100, Height = 100 };
            var button = CreateButton(explicitPopup ? new Popup { Child = content } : content);
            int contentEvents = 0;
            int ownerEvents = 0;
            content.KeyDown += (_, _) => contentEvents++;
            button.KeyDown += (_, _) => ownerEvents++;
            WithWindow(button, () =>
            {
                button.IsDropDownOpen = true;
                Drain();
                var source = PresentationSource.FromVisual(content)!;
                var scrollingKeys = new[]
                {
                    Key.Left, Key.Right, Key.Up, Key.Down,
                    Key.PageUp, Key.PageDown, Key.Home, Key.End,
                };
                foreach (var key in scrollingKeys)
                {
                    var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key)
                    {
                        RoutedEvent = Keyboard.KeyDownEvent,
                    };
                    content.RaiseEvent(args);
                    Assert.True(args.Handled);
                }

                Assert.Equal(8, contentEvents);
                Assert.Equal(0, ownerEvents);
                foreach (var key in new[] { Key.Tab, Key.A })
                {
                    var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key)
                    {
                        RoutedEvent = Keyboard.KeyDownEvent,
                    };
                    content.RaiseEvent(args);
                    Assert.False(args.Handled);
                }

                Assert.Equal(2, ownerEvents);
            });
        });
    }

    [Fact]
    public void ImplicitTemplate_UsesFlyoutData_AndTracksLocalResourceReplacement()
    {
        WpfTestThread.Run(() =>
        {
            var data = new Model("Alice");
            var button = CreateButton(data);
            button.DataContext = new Model("Owner");
            button.Resources.InvalidatesImplicitDataTemplateResources = true;
            button.Resources[new DataTemplateKey(typeof(Model))] = Template("Implicit: {0}");
            Assert.Equal("Implicit: Alice", RenderText(button));
            button.Resources[new DataTemplateKey(typeof(Model))] = Template("Changed: {0}");
            Assert.Equal("Changed: Alice", RenderText(button));
        });
    }

    [Fact]
    public void ExplicitTemplate_Selector_ImplicitTemplate_AndStringFormatFollowWpfPrecedence()
    {
        WpfTestThread.Run(() =>
        {
            var button = CreateButton(new Model("Alice"));
            var selector = new Selector { Result = Template("Selected: {0}") };
            button.Resources[new DataTemplateKey(typeof(Model))] = Template("Implicit: {0}");
            button.FlyoutTemplateSelector = selector;
            button.FlyoutTemplate = Template("Explicit: {0}");
            Assert.Equal("Explicit: Alice", RenderText(button));
            Assert.Equal(0, selector.Calls);
            button.FlyoutTemplate = null;
            Assert.Equal("Selected: Alice", RenderText(button));
            Assert.True(selector.Calls > 0);
            button.FlyoutTemplateSelector = new Selector();
            Assert.Equal("Implicit: Alice", RenderText(button));
            button.Flyout = 12.5;
            button.FlyoutStringFormat = "Value: {0:F1}";
            Assert.Equal("Value: 12.5", RenderText(button));
            button.FlyoutStringFormat = "{0:F2}";
            Assert.Equal("12.50", RenderText(button));
        });
    }

    [Fact]
    public void GeneratedPanel_PreservesOpenBindingAndHostDuringContentAndTemplateChanges()
    {
        WpfTestThread.Run(() =>
        {
            var button = CreateButton("One");
            var source = new System.Windows.Controls.Primitives.ToggleButton { IsChecked = false };
            button.SetBinding(
                DropDownButton.IsDropDownOpenProperty,
                new Binding(nameof(source.IsChecked)) { Source = source });
            WithWindow(button, () =>
            {
                source.IsChecked = true;
                Drain();
                var popup = GetPopup(button);
                Assert.True(popup.IsOpen);
                button.Flyout = "Two";
                button.Template = new ControlTemplate(typeof(DropDownButton));
                button.ApplyTemplate();
                Assert.Same(popup, GetPopup(button));
                Assert.True(popup.IsOpen);
                Assert.Equal("Two", RenderText(button));
                button.FlyoutPresenterTemplate = (ControlTemplate)XamlReader.Parse(
                    "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='ContentControl'><Border Padding='25'><ContentPresenter ContentSource='Content'/></Border></ControlTemplate>");
                Assert.Equal("Two", RenderText(button));
                popup.SetCurrentValue(Popup.IsOpenProperty, false);
                WaitForFlyoutDismissal(button);
                Assert.False(button.IsDropDownOpen);
                Assert.False(source.IsChecked);
                Assert.True(BindingOperations.IsDataBound(button, DropDownButton.IsDropDownOpenProperty));
                source.IsChecked = true;
                Drain();
                button.Flyout = new ContextMenu();
                Drain();
                Assert.False(popup.IsOpen);
                Assert.Null(popup.Child);
                Assert.False(source.IsChecked);
            });
        });
    }

    [Fact]
    public void GeneratedPopupStyle_UpdatesPlacementWithoutReplacingContent()
    {
        WpfTestThread.Run(() =>
        {
            var button = CreateButton("Content");
            WithWindow(button, () =>
            {
                button.IsDropDownOpen = true;
                Drain();
                var popup = GetPopup(button);
                var child = popup.Child;
                Assert.False(popup.StaysOpen);
                Assert.True(popup.AllowsTransparency);
                button.FlyoutPopupStyle = new Style(typeof(Popup))
                {
                    Setters =
                    {
                        new Setter(Popup.PlacementProperty, PlacementMode.Right),
                        new Setter(Popup.HorizontalOffsetProperty, 17D),
                        new Setter(Popup.StaysOpenProperty, true),
                    },
                };
                Drain();
                Assert.Same(child, popup.Child);
                Assert.Same(button, popup.PlacementTarget);
                Assert.Equal(PlacementMode.Right, popup.Placement);
                Assert.Equal(17D, popup.HorizontalOffset);
                Assert.True(popup.StaysOpen);

                button.FlyoutPopupStyle = null;
                button.Resources[DropDownButton.FlyoutPopupStyleKey] = new Style(typeof(Popup))
                {
                    Setters = { new Setter(Popup.HorizontalOffsetProperty, 23D) },
                };
                Drain();
                Assert.True(popup.IsOpen);
                Assert.Same(child, popup.Child);
                Assert.Equal(23D, popup.HorizontalOffset);
                Assert.True(popup.AllowsTransparency);
                Assert.False(popup.StaysOpen);
            });
        });
    }

    [Fact]
    public void OpenBeforeLoaded_IsDeferred_DisablingAndUnloadingCloseThePanel()
    {
        WpfTestThread.Run(() =>
        {
            var button = CreateButton("Deferred");
            button.IsDropDownOpen = true;
            Assert.False(GetPopup(button).IsOpen);
            WithWindow(button, () =>
            {
                Assert.True(GetPopup(button).IsOpen);
                button.IsEnabled = false;
                Assert.False(GetPopup(button).IsOpen);
                Assert.False(button.IsDropDownOpen);
                button.IsDropDownOpen = true;
                Assert.False(button.IsDropDownOpen);
                button.IsEnabled = true;
                button.IsDropDownOpen = true;
                Assert.True(GetPopup(button).IsOpen);
            });
            Assert.False(GetPopup(button).IsOpen);
            Assert.False(button.IsDropDownOpen);
        });
    }

    [Fact]
    public void KeyboardAndAutomation_ExpandWithoutClick_AndEscapeRestoresOwnerFocus()
    {
        WpfTestThread.Run(() =>
        {
            var editor = new TextBox { Width = 120 };
            var button = CreateButton(editor);
            int clicks = 0;
            button.Click += (_, _) => clicks++;
            WithWindow(button, () =>
            {
                button.Focus();
                var source = PresentationSource.FromVisual(button)!;
                button.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.F4)
                {
                    RoutedEvent = Keyboard.KeyDownEvent,
                });
                Drain();
                Assert.True(button.IsDropDownOpen);
                Assert.True(editor.IsKeyboardFocused);
                editor.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Escape)
                {
                    RoutedEvent = Keyboard.KeyDownEvent,
                });
                Drain();
                Assert.False(button.IsDropDownOpen);
                Assert.True(button.IsKeyboardFocused);
                var peer = UIElementAutomationPeer.CreatePeerForElement(button)!;
                var provider = Assert.IsAssignableFrom<IExpandCollapseProvider>(peer.GetPattern(PatternInterface.ExpandCollapse));
                Assert.NotNull(peer.GetPattern(PatternInterface.Invoke));
                provider.Expand();
                Assert.True(button.IsDropDownOpen);
                provider.Collapse();
                Assert.False(button.IsDropDownOpen);
                Assert.Equal(0, clicks);
            });
        });
    }

    [Fact]
    public void NativePopup_MovesFocusToItsFirstFocusableDescendantWhenOpened()
    {
        WpfTestThread.Run(() =>
        {
            var editor = new TextBox { Width = 120 };
            var popup = new Popup { Child = new Border { Child = editor } };
            var button = CreateButton(popup);

            WithWindow(button, () =>
            {
                button.Focus();
                button.IsDropDownOpen = true;
                Drain();

                Assert.True(editor.IsKeyboardFocused);
            });
        });
    }

    [Fact]
    public void Click_TogglesGeneratedPanelAfterRaisingClick()
    {
        WpfTestThread.Run(() =>
        {
            var button = new Probe { Flyout = "Content" };
            ApplyResources(button);
            var states = new List<bool>();
            button.Click += (_, _) => states.Add(button.IsDropDownOpen);
            WithWindow(button, () =>
            {
                button.InvokeClick();
                Assert.True(button.IsDropDownOpen);
                button.InvokeClick();
                Assert.False(button.IsDropDownOpen);
                Assert.Equal(new[] { false, true }, states);
            });
        });
    }

    [Fact]
    public void InvalidHostConfiguration_IsRejectedBeforeItReachesWpfBindings()
    {
        WpfTestThread.Run(() =>
        {
            var button = CreateButton("Content");
            Assert.Throws<ArgumentException>(() => button.FlyoutPresenterTemplate = new ControlTemplate(typeof(TextBox)));
            Assert.Throws<ArgumentException>(() => button.FlyoutPopupStyle = new Style(typeof(TextBox)));
            Assert.NotNull(button.FlyoutPresenterTemplate);
            Assert.Null(button.FlyoutPopupStyle);
        });
    }

    [Fact]
    public void ParentedUiContent_IsRejectedWithoutStealingItFromItsOwner()
    {
        WpfTestThread.Run(() =>
        {
            var editor = new TextBox();
            var panel = new StackPanel { Children = { editor } };
            var button = CreateButton("Content");
            Assert.Throws<InvalidOperationException>(() => button.Flyout = editor);
            Assert.Same(panel, LogicalTreeHelper.GetParent(editor));
            Assert.Contains(editor, panel.Children.Cast<UIElement>());
            Assert.Equal("Content", button.Flyout);
            Assert.Equal("Content", GetHost(button).Content);
        });
    }

    [Fact]
    public void ContentInheritanceCallback_CanReplaceFlyoutDuringHostCreation()
    {
        WpfTestThread.Run(() =>
        {
            var button = new DropDownButton { DataContext = "Owner" };
            ApplyResources(button);
            var native = new Popup { Child = new TextBox() };
            var content = new TextBox();
            content.DataContextChanged += (_, e) =>
            {
                if (Equals(e.NewValue, "Owner"))
                {
                    button.Flyout = native;
                }
            };
            button.Flyout = content;
            Assert.Same(native, button.Flyout);
            Assert.Same(native, GetPopup(button));
            Assert.Null(LogicalTreeHelper.GetParent(content));
            WithWindow(button, () =>
            {
                button.IsDropDownOpen = true;
                Assert.True(native.IsOpen);
            });
        });
    }

    [Fact]
    public void NativePopup_OpeningExternallyWhileDisabled_IsClosed()
    {
        WpfTestThread.Run(() =>
        {
            var native = new Popup { Child = new TextBox(), StaysOpen = true };
            var button = CreateButton(native);
            WithWindow(button, () =>
            {
                button.IsEnabled = false;
                native.IsOpen = true;
                Drain();
                Assert.False(button.IsDropDownOpen);
                Assert.False(native.IsOpen);
            });
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeOpenedHandler_CanCloseBeforeTheButtonReceivesTheEvent(bool useMenu)
    {
        WpfTestThread.Run(() =>
        {
            var menu = new ContextMenu();
            var popup = new Popup { Child = new TextBox() };
            menu.Opened += (_, _) => menu.IsOpen = false;
            popup.Opened += (_, _) => popup.IsOpen = false;
            var button = CreateButton(useMenu ? menu : popup);
            WithWindow(button, () =>
            {
                button.IsDropDownOpen = true;
                Drain();
                Assert.False(useMenu ? menu.IsOpen : popup.IsOpen);
                Assert.False(button.IsDropDownOpen);
            });
        });
    }

    [Fact]
    public void ReentrantContentReplacement_CompletesOwnershipTransferBeforeApplyingLatestValue()
    {
        WpfTestThread.Run(() =>
        {
            var button = CreateButton("Initial");
            button.DataContext = "Owner";
            var first = new TextBox();
            var last = new TextBox();
            first.DataContextChanged += (_, e) =>
            {
                if (Equals(e.NewValue, "Owner"))
                {
                    button.Flyout = last;
                }
            };
            button.Flyout = first;
            Assert.Same(last, button.Flyout);
            Assert.Same(last, GetHost(button).Content);
            Assert.Null(LogicalTreeHelper.GetParent(first));
            Assert.Same(GetHost(button), LogicalTreeHelper.GetParent(last));
        });
    }

    [Fact]
    public void ParentedContentElement_IsRejectedAndCurrentContentRemainsUsable()
    {
        WpfTestThread.Run(() =>
        {
            var run = new System.Windows.Documents.Run("Owned");
            var text = new System.Windows.Controls.TextBlock(run);
            var button = CreateButton("Current");
            Assert.Throws<InvalidOperationException>(() => button.Flyout = run);
            Assert.Same(text, LogicalTreeHelper.GetParent(run));
            Assert.Equal("Current", button.Flyout);
            Assert.Equal("Current", RenderText(button));
        });
    }

    [Fact]
    public void LogicalAncestorWithoutAVisualTemplate_IsRejectedBeforeCreatingACycle()
    {
        WpfTestThread.Run(() =>
        {
            var button = CreateButton("Current");
            var owner = new ContentControl { Content = button };
            Assert.Null(VisualTreeHelper.GetParent(button));
            Assert.Throws<InvalidOperationException>(() => button.Flyout = owner);
            Assert.Equal("Current", button.Flyout);
            Assert.Same(owner, LogicalTreeHelper.GetParent(button));
            Assert.Null(LogicalTreeHelper.GetParent(owner));
        });
    }

    [Fact]
    public void HandledNativeMenuNotifications_StillSynchronizeExpansion()
    {
        WpfTestThread.Run(() =>
        {
            var menu = new ContextMenu();
            menu.Opened += (_, e) => e.Handled = true;
            menu.Closed += (_, e) => e.Handled = true;
            var button = CreateButton(menu);
            WithWindow(button, () =>
            {
                menu.IsOpen = true;
                Drain();
                Assert.True(button.IsDropDownOpen);
                menu.IsOpen = false;
                WaitForFlyoutDismissal(button);
                Assert.False(button.IsDropDownOpen);
            });
        });
    }

    [Fact]
    public void NativePopup_BypassesPresentationProperties_AndReleasesDefaultOwnerReference()
    {
        WpfTestThread.Run(() =>
        {
            var native = new Popup { Child = new TextBox { Text = "Native" } };
            var button = CreateButton(native);
            button.FlyoutTemplate = Template("Unused: {0}");
            button.FlyoutPopupStyle = new Style(typeof(Popup))
            {
                Setters = { new Setter(Popup.PlacementProperty, PlacementMode.Left) },
            };
            WithWindow(button, () =>
            {
                button.IsDropDownOpen = true;
                Assert.Equal(PlacementMode.Bottom, native.Placement);
                Assert.Equal("Native", Assert.IsType<TextBox>(native.Child).Text);
                Assert.Same(native, GetPopup(button));
                button.Flyout = "Generated";
                Assert.False(native.IsOpen);
                Assert.Null(native.PlacementTarget);
                Assert.Null(LogicalTreeHelper.GetParent(native));
                Assert.False(button.IsDropDownOpen);
                Assert.Same(button, GetPopup(button).PlacementTarget);
            });
        });
    }

    [Fact]
    public void FormattingChange_RefreshesStringContentWithoutReplacingPopup()
    {
        WpfTestThread.Run(() =>
        {
            var button = CreateButton("Ready");
            var popup = GetPopup(button);
            Assert.Equal("Ready", RenderText(button));
            button.FlyoutStringFormat = "Status: {0}";
            Assert.Equal("Status: Ready", RenderText(button));
            Assert.Same(popup, GetPopup(button));
            button.FlyoutStringFormat = null;
            Assert.Equal("Ready", RenderText(button));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ContentInput_DoesNotActivateOrPublishClickOnTheOwningButton(bool explicitPopup)
    {
        WpfTestThread.Run(() =>
        {
            var inner = new System.Windows.Controls.Button { Content = "Action" };
            var surface = new Border { Background = Brushes.White, Child = inner };
            var button = CreateButton(explicitPopup ? new Popup { Child = surface } : surface);
            button.ClickMode = ClickMode.Press;
            int ownerClicks = 0;
            int contentClicks = 0;
            button.Click += (_, _) => ownerClicks++;
            inner.Click += (_, _) => contentClicks++;
            WithWindow(button, () =>
            {
                button.IsDropDownOpen = true;
                Drain();
                inner.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, inner));
                Assert.Equal(1, contentClicks);
                Assert.Equal(0, ownerClicks);
                surface.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                {
                    RoutedEvent = Mouse.MouseDownEvent,
                });
                surface.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                {
                    RoutedEvent = Mouse.MouseUpEvent,
                });
                Assert.Equal(0, ownerClicks);
                Assert.False(button.IsPressed);
                Assert.True(button.IsDropDownOpen);
            });
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Popup_AllowsInnerScrollingWithoutScrollingTheOwner(bool explicitPopup)
    {
        WpfTestThread.Run(() =>
        {
            var viewer = new System.Windows.Controls.ScrollViewer
            {
                Width = 100,
                Height = 60,
                Content = new Border { Height = 600, Background = Brushes.White },
            };
            var button = CreateButton(explicitPopup ? new Popup { Child = viewer } : viewer);
            int ownerWheelEvents = 0;
            button.MouseWheel += (_, _) => ownerWheelEvents++;
            WithWindow(button, () =>
            {
                button.IsDropDownOpen = true;
                Drain();
                viewer.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120)
                {
                    RoutedEvent = Mouse.MouseWheelEvent,
                });
                Drain();
                Assert.True(viewer.VerticalOffset > 0);
                Assert.Equal(0, ownerWheelEvents);
            });
        });
    }

    [Fact]
    public void NativePopup_InheritsWhileClosed_AndRejectsAnotherOwner()
    {
        WpfTestThread.Run(() =>
        {
            var child = new TextBox();
            child.SetResourceReference(Control.ForegroundProperty, "Ink");
            var popup = new Popup { Child = child };
            var button = CreateButton(popup);
            button.DataContext = "First";
            button.Resources["Ink"] = Brushes.Red;
            Assert.Equal("First", child.DataContext);
            Assert.Same(Brushes.Red, child.Foreground);
            button.DataContext = "Second";
            button.Resources["Ink"] = Brushes.Blue;
            Drain();
            Assert.Equal("Second", child.DataContext);
            Assert.Same(Brushes.Blue, child.Foreground);
            var other = CreateButton("Original");
            Assert.Throws<InvalidOperationException>(() => other.Flyout = popup);
            Assert.Equal("Original", other.Flyout);
            button.Flyout = null;
            Assert.Null(LogicalTreeHelper.GetParent(popup));
            Assert.Same(child, popup.Child);
            other.Flyout = popup;
            Assert.Same(other, LogicalTreeHelper.GetParent(popup));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ContextMenu_InheritsListItemData_AndTracksReplacement(bool useListView)
    {
        WpfTestThread.Run(() =>
        {
            var menuItem = new MenuItem();
            menuItem.SetBinding(HeaderedItemsControl.HeaderProperty, new Binding("Name"));
            var menu = new ContextMenu { Items = { menuItem } };
            var button = CreateButton(menu);
            System.Windows.Controls.ListBox list = useListView
                ? new System.Windows.Controls.ListView() : new System.Windows.Controls.ListBox();
            System.Windows.Controls.ListBoxItem item = useListView
                ? new System.Windows.Controls.ListViewItem() : new System.Windows.Controls.ListBoxItem();
            var first = new Model("First");
            var second = new Model("Second");
            item.DataContext = first;
            item.Content = button;
            list.Items.Add(item);
            // A real item container exercises inheritance across the menu's separate HWND.
            WithWindow(list, () =>
            {
                Assert.Same(first, button.DataContext);
                button.IsDropDownOpen = true;
                Drain();
                Assert.Same(first, menu.DataContext);
                Assert.Same(first, menuItem.DataContext);
                Assert.Equal("First", menuItem.Header);
                item.DataContext = second;
                Drain();
                Assert.Same(second, menu.DataContext);
                Assert.Equal("Second", menuItem.Header);
                button.IsDropDownOpen = false;
                WaitForFlyoutDismissal(button);
                item.DataContext = first;
                button.IsDropDownOpen = true;
                Drain();
                Assert.Same(first, menu.DataContext);
                Assert.Equal("First", menuItem.Header);
                button.IsDropDownOpen = false;
                WaitForFlyoutDismissal(button);
            });
        });
    }

    private static DropDownButton CreateButton(object content)
    {
        var button = new DropDownButton { Flyout = content, Content = "Options" };
        ApplyResources(button);
        return button;
    }

    private static void ApplyResources(DropDownButton button)
    {
        foreach (string path in new[] { "Resources/Theme/Light.xaml", "Controls/DropDownButton/DropDownButton.xaml" })
        {
            button.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/Wpf.Zen.Ui;component/{path}", UriKind.Absolute),
            });
        }

        button.ApplyTemplate();
    }

    private static Popup GetPopup(DropDownButton button) =>
        Assert.Single(LogicalTreeHelper.GetChildren(button).OfType<Popup>());

    private static ContentControl GetHost(DropDownButton button) => Assert.IsType<ContentControl>(GetPopup(button).Child);

    private static string RenderText(DropDownButton button)
    {
        var host = GetHost(button);
        host.ApplyTemplate();
        host.Measure(new Size(500, 500));
        host.Arrange(new Rect(host.DesiredSize));
        host.UpdateLayout();
        Drain();
        return Assert.Single(Descendants(host).OfType<System.Windows.Controls.TextBlock>()).Text;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static DataTemplate Template(string format) => (DataTemplate)XamlReader.Parse(
        "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><TextBlock Text=\"{Binding Name, StringFormat='" + format + "'}\"/></DataTemplate>");

    private static void WithWindow(FrameworkElement button, Action action)
    {
        var window = new Window { Content = button, Width = 240, Height = 120, ShowInTaskbar = false };
        try
        {
            window.Show();
            window.Activate();
            window.UpdateLayout();
            Drain();
            action();
        }
        finally
        {
            window.Close();
            Drain();
        }
    }

    private static void Drain() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    private static void WaitForFlyoutDismissal(DropDownButton button)
    {
        // Closed follows Popup's native fade-out timer; reaching dispatcher idle
        // alone does not guarantee the close notification has been delivered.
        var frame = new DispatcherFrame();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        timer.Tick += (_, _) => frame.Continue = button.IsDropDownOpen && DateTime.UtcNow < deadline;
        timer.Start();
        try
        {
            Dispatcher.PushFrame(frame);
        }
        finally
        {
            timer.Stop();
        }
    }

    public record Model(string Name);

    private sealed class Selector : DataTemplateSelector
    {
        public DataTemplate? Result { get; init; }
        public int Calls { get; private set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            Calls++;
            return Result;
        }
    }

    private sealed class Probe : DropDownButton
    {
        public IEnumerator GetLogicalChildren() => LogicalChildren;

        public void InvokeClick() => OnClick();
    }
}

