// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using UiInfoBar = Wpf.Zen.Ui.Controls.InfoBar;

namespace Wpf.Zen.Ui.UnitTests.Controls.InfoBar;

public class InfoBarTests
{
    [Fact]
    public void ContentSlots_AcceptRichObjects()
    {
        WpfTestThread.Run(
            () =>
            {
                var title = new TextBlock { Text = "Rich title" };
                var content = new StackPanel();
                var infoBar = new UiInfoBar { Title = title, Content = content };

                Assert.IsAssignableFrom<ContentControl>(infoBar);
                Assert.Same(title, infoBar.Title);
                Assert.Same(content, infoBar.Content);
                Assert.Equal(typeof(object), UiInfoBar.TitleProperty.PropertyType);
            }
        );
    }

    [Fact]
    public void IsOpen_BindsTwoWayByDefault()
    {
        var metadata = (FrameworkPropertyMetadata)UiInfoBar.IsOpenProperty.GetMetadata(
            typeof(UiInfoBar)
        );

        Assert.True(metadata.BindsTwoWayByDefault);
    }

    [Fact]
    public void AppearanceProperties_AcceptTemplateCustomizationValues()
    {
        WpfTestThread.Run(
            () =>
            {
                var icon = new Border();
                var iconTemplate = new DataTemplate();
                var closeButtonStyle = new Style(typeof(Button));
                var toolTip = new TextBlock { Text = "Dismiss notification" };
                var infoBar = new UiInfoBar
                {
                    CornerRadius = new CornerRadius(8D),
                    IsIconVisible = false,
                    Icon = icon,
                    IconTemplate = iconTemplate,
                    CloseButtonStyle = closeButtonStyle,
                    CloseButtonToolTip = toolTip,
                };

                Assert.Equal(new CornerRadius(8D), infoBar.CornerRadius);
                Assert.False(infoBar.IsIconVisible);
                Assert.Same(icon, infoBar.Icon);
                Assert.Same(iconTemplate, infoBar.IconTemplate);
                Assert.Same(closeButtonStyle, infoBar.CloseButtonStyle);
                Assert.Same(toolTip, infoBar.CloseButtonToolTip);
            }
        );
    }

    [Fact]
    public void IsIconVisible_DefaultsToTrueAndDoesNotDiscardCustomIcon()
    {
        WpfTestThread.Run(
            () =>
            {
                var icon = new Border();
                var infoBar = new UiInfoBar { Icon = icon };

                Assert.True(infoBar.IsIconVisible);

                infoBar.IsIconVisible = false;
                Assert.False(infoBar.IsIconVisible);
                Assert.Same(icon, infoBar.Icon);

                infoBar.IsIconVisible = true;
                Assert.True(infoBar.IsIconVisible);
                Assert.Same(icon, infoBar.Icon);
            }
        );
    }

    [Fact]
    public void CloseButton_ClosesWithoutChangingVisibilityOrRemovingBinding()
    {
        WpfTestThread.Run(
            () =>
            {
                var source = new Border { Tag = true };
                var infoBar = new UiInfoBar { Template = CreateTemplate(typeof(Button)) };
                BindingOperations.SetBinding(
                    infoBar,
                    UiInfoBar.IsOpenProperty,
                    new Binding(nameof(FrameworkElement.Tag))
                    {
                        Source = source,
                        Mode = BindingMode.TwoWay,
                    }
                );

                Assert.True(infoBar.ApplyTemplate());
                var closeButton = Assert.IsType<Button>(
                    infoBar.Template.FindName("PART_CloseButton", infoBar)
                );

                closeButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

                Assert.False(infoBar.IsOpen);
                Assert.Equal(false, source.Tag);
                Assert.True(BindingOperations.IsDataBound(infoBar, UiInfoBar.IsOpenProperty));
                Assert.Equal(Visibility.Visible, infoBar.Visibility);
            }
        );
    }

    [Fact]
    public void TemplateWithoutCloseButton_RemainsUsable()
    {
        WpfTestThread.Run(
            () =>
            {
                var infoBar = new UiInfoBar
                {
                    IsOpen = true,
                    Template = CreateTemplate(typeof(Border), "PresentationRoot"),
                };

                Assert.True(infoBar.ApplyTemplate());
                Assert.True(infoBar.IsOpen);
            }
        );
    }

    [Fact]
    public void CloseButton_IsInterceptedWhenInfoBarIsNotClosable()
    {
        WpfTestThread.Run(
            () =>
            {
                var infoBar = new UiInfoBar
                {
                    IsClosable = false,
                    IsOpen = true,
                    Template = CreateTemplate(typeof(Button)),
                };

                Assert.True(infoBar.ApplyTemplate());
                var closeButton = Assert.IsType<Button>(
                    infoBar.Template.FindName("PART_CloseButton", infoBar)
                );
                var clickReachedParent = false;
                infoBar.AddHandler(
                    ButtonBase.ClickEvent,
                    new RoutedEventHandler((_, _) => clickReachedParent = true)
                );

                closeButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

                Assert.True(infoBar.IsOpen);
                Assert.False(clickReachedParent);
            }
        );
    }

    [Fact]
    public void ReplacingTemplate_DetachesPreviousCloseButtonImmediately()
    {
        WpfTestThread.Run(
            () =>
            {
                var infoBar = new UiInfoBar
                {
                    IsOpen = true,
                    Template = CreateTemplate(typeof(Button)),
                };

                Assert.True(infoBar.ApplyTemplate());
                var previousButton = Assert.IsType<Button>(
                    infoBar.Template.FindName("PART_CloseButton", infoBar)
                );

                infoBar.Template = CreateTemplate(typeof(Border), "PresentationRoot");
                previousButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

                Assert.True(infoBar.IsOpen);
            }
        );
    }

    private static ControlTemplate CreateTemplate(Type rootType, string name = "PART_CloseButton")
    {
#pragma warning disable CS0618 // FrameworkElementFactory is sufficient for small template-contract tests.
        var root = new FrameworkElementFactory(rootType) { Name = name };
        return new ControlTemplate(typeof(UiInfoBar)) { VisualTree = root };
#pragma warning restore CS0618
    }
}
