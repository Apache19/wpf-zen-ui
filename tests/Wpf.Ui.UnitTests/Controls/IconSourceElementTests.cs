// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace Wpf.Ui.UnitTests.Controls;

public class IconSourceElementTests
{
    [Fact]
    public void ConcreteIcons_HostTheirRenderingElementDirectly()
    {
        RunOnStaThread(() =>
        {
            var fontIcon = new FontIcon { Glyph = "A" };
            fontIcon.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            Assert.IsType<Wpf.Ui.Controls.TextBlock>(VisualTreeHelper.GetChild(fontIcon, 0));

            var imageIcon = new ImageIcon();
            imageIcon.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            Assert.IsType<System.Windows.Controls.Image>(VisualTreeHelper.GetChild(imageIcon, 0));
        });
    }

    [Fact]
    public void Element_RemainsRenderableWhileIconSourceChangesBetweenNullAndNonNull()
    {
        RunOnStaThread(() =>
        {
            var element = new IconSourceElement();

            var presenter = GetIconPresenter(element);
            Assert.Null(presenter.Content);

            element.IconSource = new SymbolIconSource { Symbol = SymbolRegular.Home24 };
            var firstIcon = Assert.IsType<SymbolIcon>(presenter.Content);
            Assert.Equal(SymbolRegular.Home24, firstIcon.Symbol);

            element.IconSource = new FontIconSource { Glyph = "A" };
            var replacementIcon = Assert.IsType<FontIcon>(presenter.Content);
            Assert.Equal("A", replacementIcon.Glyph);
            Assert.NotSame(firstIcon, replacementIcon);

            element.IconSource = null;
            Assert.Null(presenter.Content);

            // The stable presenter remains the sole direct visual child throughout source replacement.
            Assert.Same(presenter, VisualTreeHelper.GetChild(element, 0));
        });
    }

    [Fact]
    public void Element_CanBePresentedDirectlyByAContentPresenter()
    {
        RunOnStaThread(() =>
        {
            var iconSourceElement = new IconSourceElement
            {
                IconSource = new SymbolIconSource { Symbol = SymbolRegular.Navigation24 },
            };
            var presenter = new ContentPresenter { Content = iconSourceElement };

            presenter.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            Assert.True(iconSourceElement.IsMeasureValid);
            Assert.IsType<SymbolIcon>(GetIconPresenter(iconSourceElement).Content);
        });
    }

    [Fact]
    public void SymbolIconSource_CreateIconElement_ShouldNotSetFontPropertiesLocally_WhenDefaults()
    {
        RunOnStaThread(() =>
        {
            var source = new SymbolIconSource { Symbol = SymbolRegular.Home24 };
            SymbolIcon icon = (SymbolIcon)source.CreateIconElement();

            Assert.Equal(DependencyProperty.UnsetValue, icon.ReadLocalValue(FontIcon.FontSizeProperty));
            Assert.Equal(DependencyProperty.UnsetValue, icon.ReadLocalValue(FontIcon.FontWeightProperty));
            Assert.Equal(DependencyProperty.UnsetValue, icon.ReadLocalValue(FontIcon.FontStyleProperty));
            Assert.Equal(DependencyProperty.UnsetValue, icon.ReadLocalValue(IconElement.ForegroundProperty));
        });
    }

    [Fact]
    public void SymbolIconSource_CreateIconElement_ShouldSetFontSizeLocally_WhenExplicitlyAssigned()
    {
        RunOnStaThread(() =>
        {
            const double expected = 20d;
            var source = new SymbolIconSource { Symbol = SymbolRegular.Home24, FontSize = expected };
            SymbolIcon icon = (SymbolIcon)source.CreateIconElement();

            Assert.Equal(expected, icon.FontSize);
            Assert.Equal(expected, icon.ReadLocalValue(FontIcon.FontSizeProperty));
        });
    }

    [Fact]
    public void SymbolIconSource_CreateIconElement_ShouldSetForegroundLocally_WhenExplicitlyAssigned()
    {
        RunOnStaThread(() =>
        {
            var brush = new SolidColorBrush(Colors.Red);
            var source = new SymbolIconSource { Symbol = SymbolRegular.Home24, Foreground = brush };
            SymbolIcon icon = (SymbolIcon)source.CreateIconElement();

            Assert.Same(brush, icon.Foreground);
            Assert.Equal(brush, icon.ReadLocalValue(IconElement.ForegroundProperty));
        });
    }

    [Fact]
    public void SymbolIconSource_CreateIconElement_ShouldTransferAllExplicitProperties()
    {
        RunOnStaThread(() =>
        {
            const double fontSize = 24d;
            var source = new SymbolIconSource
            {
                Symbol = SymbolRegular.Home24,
                FontSize = fontSize,
                FontWeight = FontWeights.Bold,
                FontStyle = FontStyles.Italic,
            };
            SymbolIcon icon = (SymbolIcon)source.CreateIconElement();

            Assert.Equal(fontSize, icon.ReadLocalValue(FontIcon.FontSizeProperty));
            Assert.Equal(FontWeights.Bold, icon.ReadLocalValue(FontIcon.FontWeightProperty));
            Assert.Equal(FontStyles.Italic, icon.ReadLocalValue(FontIcon.FontStyleProperty));
        });
    }

    [Fact]
    public void FontIconSource_CreateIconElement_ShouldNotSetFontPropertiesLocally_WhenDefaults()
    {
        RunOnStaThread(() =>
        {
            var source = new FontIconSource { Glyph = "A" };
            FontIcon icon = (FontIcon)source.CreateIconElement();

            Assert.Equal(DependencyProperty.UnsetValue, icon.ReadLocalValue(FontIcon.FontSizeProperty));
            Assert.Equal(DependencyProperty.UnsetValue, icon.ReadLocalValue(FontIcon.FontWeightProperty));
            Assert.Equal(DependencyProperty.UnsetValue, icon.ReadLocalValue(FontIcon.FontStyleProperty));
            Assert.Equal(DependencyProperty.UnsetValue, icon.ReadLocalValue(FontIcon.FontFamilyProperty));
            Assert.Equal(DependencyProperty.UnsetValue, icon.ReadLocalValue(IconElement.ForegroundProperty));
        });
    }

    [Fact]
    public void FontIconSource_CreateIconElement_ShouldSetFontSizeLocally_WhenExplicitlyAssigned()
    {
        RunOnStaThread(() =>
        {
            const double expected = 20d;
            var source = new FontIconSource { Glyph = "A", FontSize = expected };
            FontIcon icon = (FontIcon)source.CreateIconElement();

            Assert.Equal(expected, icon.FontSize);
            Assert.Equal(expected, icon.ReadLocalValue(FontIcon.FontSizeProperty));
        });
    }

    [Fact]
    public void FontIconSource_CreateIconElement_ShouldTransferAllExplicitProperties()
    {
        RunOnStaThread(() =>
        {
            var fontFamily = new FontFamily("Consolas");
            const double fontSize = 24d;
            var source = new FontIconSource
            {
                Glyph = "A",
                FontFamily = fontFamily,
                FontSize = fontSize,
                FontWeight = FontWeights.Bold,
                FontStyle = FontStyles.Italic,
            };
            FontIcon icon = (FontIcon)source.CreateIconElement();

            Assert.Equal(fontFamily, icon.ReadLocalValue(FontIcon.FontFamilyProperty));
            Assert.Equal(fontSize, icon.ReadLocalValue(FontIcon.FontSizeProperty));
            Assert.Equal(FontWeights.Bold, icon.ReadLocalValue(FontIcon.FontWeightProperty));
            Assert.Equal(FontStyles.Italic, icon.ReadLocalValue(FontIcon.FontStyleProperty));
        });
    }

    [Fact]
    public void SymbolIconSource_CreateIconElement_ShouldAllowFontSizeInheritance_WhenDefault()
    {
        // Regression: IconElement created from default IconSource must not hold a
        // local FontSize that blocks inheritance from the visual parent template.
        RunOnStaThread(() =>
        {
            var source = new SymbolIconSource { Symbol = SymbolRegular.Home24 };
            SymbolIcon icon = (SymbolIcon)source.CreateIconElement();

            Assert.Equal(SystemFonts.MessageFontSize, icon.FontSize);
            Assert.Equal(DependencyProperty.UnsetValue, icon.ReadLocalValue(FontIcon.FontSizeProperty));
        });
    }

    [Fact]
    public void FontIconSource_CreateIconElement_ShouldAllowFontSizeInheritance_WhenDefault()
    {
        RunOnStaThread(() =>
        {
            var source = new FontIconSource { Glyph = "A" };
            FontIcon icon = (FontIcon)source.CreateIconElement();

            Assert.Equal(DependencyProperty.UnsetValue, icon.ReadLocalValue(FontIcon.FontSizeProperty));
        });
    }

    [Fact]
    public void IconSource_CreateIconElement_ShouldNotCopyNonLocalPropertyFromDefaultStyleOrParentTemplate()
    {
        RunOnStaThread(() =>
        {
            var source = new SymbolIconSource { Symbol = SymbolRegular.Home24 };
            SymbolIcon icon = (SymbolIcon)source.CreateIconElement();

            // Every source property that uses BaseValueSource.Default must remain
            // unset on the target element.
            Assert.Equal(DependencyProperty.UnsetValue, icon.ReadLocalValue(FontIcon.FontSizeProperty));
            Assert.Equal(DependencyProperty.UnsetValue, icon.ReadLocalValue(FontIcon.FontWeightProperty));
            Assert.Equal(DependencyProperty.UnsetValue, icon.ReadLocalValue(FontIcon.FontStyleProperty));
            Assert.Equal(DependencyProperty.UnsetValue, icon.ReadLocalValue(IconElement.ForegroundProperty));
        });
    }

    private static ContentPresenter GetIconPresenter(IconSourceElement element)
    {
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        return Assert.IsType<ContentPresenter>(VisualTreeHelper.GetChild(element, 0));
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
