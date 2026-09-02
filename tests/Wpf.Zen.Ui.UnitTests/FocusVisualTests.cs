// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Globalization;
using System.Linq;
using System.Windows.Controls;
using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Converters;
using Wpf.Zen.Ui.Theming;
using UiButton = Wpf.Zen.Ui.Controls.Button;

namespace Wpf.Zen.Ui.UnitTests;

[Collection(nameof(ThemeManagerTestCollection))]
public class FocusVisualTests
{
    [Fact]
    public void PresenterStyle_DefaultsToNullAndCanBeConfiguredPerElement()
    {
        WpfTestThread.Run(() =>
        {
            var element = new FrameworkElement();
            var presenterStyle = new Style(typeof(Control));

            Assert.Null(FocusVisual.GetPresenterStyle(element));

            FocusVisual.SetPresenterStyle(element, presenterStyle);

            Assert.Same(presenterStyle, FocusVisual.GetPresenterStyle(element));

            FocusVisual.SetPresenterStyle(element, null);

            Assert.Null(FocusVisual.GetPresenterStyle(element));
        });
    }

    [Fact]
    public void GlobalResources_PublishFocusStyleAndReusableDefaultPresenterStyle()
    {
        WpfApplicationTestHost.Run(() =>
        {
            ThemeManager.Initialize();

            Assert.IsType<Style>(Application.Current!.TryFindResource(SystemParameters.FocusVisualStyleKey));
            var presenterStyle = Assert.IsType<Style>(
                Application.Current.TryFindResource(StyleKeys.DefaultFocusVisualPresenterStyle)
            );

            Assert.Equal(typeof(Control), presenterStyle.TargetType);
            Assert.Contains(
                presenterStyle.Setters,
                setter => setter is Setter { Property: var property } && property == Control.TemplateProperty
            );
        });
    }

    [Fact]
    public void PresenterStyleConverter_SelectsCustomStyleOrGlobalFallback()
    {
        WpfTestThread.Run(() =>
        {
            var converter = new FocusVisualPresenterStyleConverter();
            var defaultPresenterStyle = new Style(typeof(Control));
            var customPresenterStyle = new Style(typeof(Control));
            var adornedElement = new Border();

            var fallbackResult = converter.Convert(
                adornedElement,
                typeof(Style),
                defaultPresenterStyle,
                CultureInfo.InvariantCulture
            );
            FocusVisual.SetPresenterStyle(adornedElement, customPresenterStyle);
            var customResult = converter.Convert(
                adornedElement,
                typeof(Style),
                defaultPresenterStyle,
                CultureInfo.InvariantCulture
            );

            Assert.Same(defaultPresenterStyle, fallbackResult);
            Assert.Same(customPresenterStyle, customResult);
        });
    }

    [Fact]
    public void UiButtonStyle_CustomizesPresenterWithoutShadowingImplicitFocusStyleKey()
    {
        WpfApplicationTestHost.Run(() =>
        {
            ThemeManager.Initialize();

            var buttonStyle = Assert.IsType<Style>(Application.Current!.TryFindResource(StyleKeys.UiButtonStyle));
            Assert.False(buttonStyle.Resources.Contains(SystemParameters.FocusVisualStyleKey));

            var presenterSetter = Assert.Single(
                buttonStyle.Setters.OfType<Setter>(),
                setter => setter.Property == FocusVisual.PresenterStyleProperty
            );
            var presenterStyle = Assert.IsType<Style>(presenterSetter.Value);
            Assert.Equal(typeof(Control), presenterStyle.TargetType);
            Assert.Contains(
                presenterStyle.Setters,
                setter =>
                    setter is Setter { Property: var property, Value: Thickness margin }
                    && property == FrameworkElement.MarginProperty
                    && margin == new Thickness(-2)
            );

            var button = new UiButton { Style = buttonStyle };
            Assert.Same(presenterStyle, FocusVisual.GetPresenterStyle(button));
            Assert.Equal(
                BaseValueSource.Default,
                DependencyPropertyHelper.GetValueSource(button, FrameworkElement.FocusVisualStyleProperty).BaseValueSource
            );
        });
    }
}
