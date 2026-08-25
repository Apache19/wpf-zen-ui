// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using UiInfoBar = Wpf.Zen.Ui.Controls.InfoBar;
using UiInfoBarDismissedVisibility = Wpf.Zen.Ui.Controls.InfoBarDismissedVisibility;

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
    public void AutoVisibility_DefaultsToEnabledAndFollowsContent()
    {
        WpfTestThread.Run(
            () =>
            {
                var infoBar = new UiInfoBar();

                Assert.True(infoBar.IsAutoVisibilityEnabled);
                Assert.Equal(Visibility.Collapsed, infoBar.Visibility);

                infoBar.Content = "Content";
                Assert.Equal(Visibility.Visible, infoBar.Visibility);

                infoBar.Content = string.Empty;
                Assert.Equal(Visibility.Collapsed, infoBar.Visibility);
            }
        );
    }

    [Theory]
    [InlineData(null, "Content")]
    [InlineData("Title", null)]
    [InlineData("", "Content")]
    [InlineData("Title", "")]
    [InlineData(" ", null)]
    [InlineData(null, " ")]
    public void AutoVisibility_ShowsWhenEitherSlotHasContent(string? title, string? content)
    {
        WpfTestThread.Run(
            () =>
            {
                var infoBar = new UiInfoBar { Title = title, Content = content };

                Assert.Equal(Visibility.Visible, infoBar.Visibility);
            }
        );
    }

    [Fact]
    public void AutoVisibility_DismissesWhenLastContentSlotIsCleared()
    {
        WpfTestThread.Run(
            () =>
            {
                var infoBar = new UiInfoBar { Title = "Title", Content = "Content" };

                infoBar.Title = string.Empty;
                Assert.Equal(Visibility.Visible, infoBar.Visibility);

                infoBar.Content = null;
                Assert.Equal(Visibility.Collapsed, infoBar.Visibility);
            }
        );
    }

    [Fact]
    public void AutoVisibility_CanBeDisabledAndEvaluatesImmediatelyWhenReenabled()
    {
        WpfTestThread.Run(
            () =>
            {
                var infoBar = new UiInfoBar
                {
                    IsAutoVisibilityEnabled = false,
                    Visibility = Visibility.Visible,
                };

                infoBar.Content = "Content";
                infoBar.Content = null;
                Assert.Equal(Visibility.Visible, infoBar.Visibility);

                infoBar.IsAutoVisibilityEnabled = true;

                Assert.Equal(Visibility.Collapsed, infoBar.Visibility);
            }
        );
    }

    [Fact]
    public void AutoVisibility_PreservesBindingAndPublishesBothTransitions()
    {
        WpfTestThread.Run(
            () =>
            {
                var source = new Border { Tag = Visibility.Collapsed };
                var infoBar = new UiInfoBar();
                BindingOperations.SetBinding(
                    infoBar,
                    UIElement.VisibilityProperty,
                    new Binding(nameof(FrameworkElement.Tag))
                    {
                        Source = source,
                        Mode = BindingMode.TwoWay,
                    }
                );

                infoBar.Title = "Title";

                Assert.Equal(Visibility.Visible, infoBar.Visibility);
                Assert.Equal(Visibility.Visible, source.Tag);
                Assert.True(BindingOperations.IsDataBound(infoBar, UIElement.VisibilityProperty));

                infoBar.Title = null;

                Assert.Equal(Visibility.Collapsed, infoBar.Visibility);
                Assert.Equal(Visibility.Collapsed, source.Tag);
                Assert.True(BindingOperations.IsDataBound(infoBar, UIElement.VisibilityProperty));
            }
        );
    }

    [Fact]
    public void DismissedVisibility_UsesDedicatedTypeWithOnlyDismissedStates()
    {
        WpfTestThread.Run(
            () =>
            {
                var infoBar = new UiInfoBar();

                Assert.Equal(
                    UiInfoBarDismissedVisibility.Collapsed,
                    infoBar.DismissedVisibility
                );
                Assert.Equal(
                    typeof(UiInfoBarDismissedVisibility),
                    UiInfoBar.DismissedVisibilityProperty.PropertyType
                );

                infoBar.DismissedVisibility = UiInfoBarDismissedVisibility.Hidden;
                Assert.Equal(Visibility.Hidden, infoBar.Visibility);

                Assert.Throws<ArgumentException>(
                    () =>
                        infoBar.DismissedVisibility = (UiInfoBarDismissedVisibility)int.MaxValue
                );
            }
        );
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
                var infoBar = new UiInfoBar
                {
                    CornerRadius = new CornerRadius(8D),
                    IsIconVisible = false,
                    IconTemplate = iconTemplate,
                    CloseButtonStyle = closeButtonStyle,
                    Icon = icon,
                };

                Assert.Equal(new CornerRadius(8D), infoBar.CornerRadius);
                Assert.False(infoBar.IsIconVisible);
                Assert.Same(icon, infoBar.Icon);
                Assert.Same(iconTemplate, infoBar.IconTemplate);
                Assert.Same(closeButtonStyle, infoBar.CloseButtonStyle);
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

    [Theory]
    [InlineData(UiInfoBarDismissedVisibility.Collapsed, Visibility.Collapsed)]
    [InlineData(UiInfoBarDismissedVisibility.Hidden, Visibility.Hidden)]
    public void CloseButton_DismissesControlUsingConfiguredVisibility(
        UiInfoBarDismissedVisibility dismissedVisibility,
        Visibility expectedVisibility
    )
    {
        WpfTestThread.Run(
            () =>
            {
                var source = new Border { Tag = Visibility.Visible };
                var infoBar = new UiInfoBar
                {
                    Content = "Content",
                    DismissedVisibility = dismissedVisibility,
                    Template = CreateTemplate(typeof(Button)),
                };
                BindingOperations.SetBinding(
                    infoBar,
                    UIElement.VisibilityProperty,
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

                Assert.Equal(expectedVisibility, infoBar.Visibility);
                Assert.Equal(expectedVisibility, source.Tag);
                Assert.True(BindingOperations.IsDataBound(infoBar, UIElement.VisibilityProperty));
            }
        );
    }

    [Theory]
    [InlineData(UiInfoBarDismissedVisibility.Collapsed, 0D, 0D)]
    [InlineData(UiInfoBarDismissedVisibility.Hidden, 120D, 40D)]
    public void Dismissal_UsesWholeControlLayoutSemantics(
        UiInfoBarDismissedVisibility dismissedVisibility,
        double expectedWidth,
        double expectedHeight
    )
    {
        WpfTestThread.Run(
            () =>
            {
                var infoBar = new UiInfoBar
                {
                    Width = 100D,
                    Height = 20D,
                    Margin = new Thickness(10D),
                    Content = "Content",
                    DismissedVisibility = dismissedVisibility,
                    Template = CreateTemplate(typeof(Button)),
                };
                var panel = new StackPanel();
                panel.Children.Add(infoBar);
                panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                Assert.Equal(new Size(120D, 40D), panel.DesiredSize);
                infoBar.ApplyTemplate();
                var closeButton = Assert.IsType<Button>(
                    infoBar.Template.FindName("PART_CloseButton", infoBar)
                );

                closeButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                Assert.Equal(new Size(expectedWidth, expectedHeight), panel.DesiredSize);
            }
        );
    }

    [Fact]
    public void CloseButtonStyle_IsAppliedWhenCustomTemplateDoesNotForwardProperty()
    {
        WpfTestThread.Run(
            () =>
            {
                var initialStyle = new Style(typeof(Button));
                var updatedStyle = new Style(typeof(Button));
                var infoBar = new UiInfoBar
                {
                    CloseButtonStyle = initialStyle,
                    Template = CreateTemplate(typeof(Button)),
                };

                Assert.True(infoBar.ApplyTemplate());
                var closeButton = Assert.IsType<Button>(
                    infoBar.Template.FindName("PART_CloseButton", infoBar)
                );

                Assert.Same(initialStyle, closeButton.Style);
                Assert.True(
                    BindingOperations.IsDataBound(closeButton, FrameworkElement.StyleProperty)
                );

                infoBar.CloseButtonStyle = updatedStyle;

                Assert.Same(updatedStyle, closeButton.Style);
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
                    Content = "Content",
                    Template = CreateTemplate(typeof(Border), "PresentationRoot"),
                };

                Assert.True(infoBar.ApplyTemplate());
                Assert.Equal(Visibility.Visible, infoBar.Visibility);
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
                    Content = "Content",
                    IsClosable = false,
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

                Assert.Equal(Visibility.Visible, infoBar.Visibility);
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
                    Content = "Content",
                    Template = CreateTemplate(typeof(Button)),
                };

                Assert.True(infoBar.ApplyTemplate());
                var previousButton = Assert.IsType<Button>(
                    infoBar.Template.FindName("PART_CloseButton", infoBar)
                );

                infoBar.Template = CreateTemplate(typeof(Border), "PresentationRoot");
                previousButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

                Assert.Equal(Visibility.Visible, infoBar.Visibility);
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
