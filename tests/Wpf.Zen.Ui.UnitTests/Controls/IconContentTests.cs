// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Extensions;
using Wpf.Zen.Ui.Markup;

namespace Wpf.Zen.Ui.UnitTests.Controls;

public class IconContentTests
{
    [Fact]
    public void FluentIconSourceExtension_ReturnsReusableIconData()
    {
        var extension = new FluentIconSourceExtension(SymbolRegular.Settings24, filled: true);

        var icon = Assert.IsType<FluentIconSource>(extension.ProvideValue(null!));
        Assert.Equal(SymbolRegular.Settings24, icon.Symbol);
        Assert.True(icon.Filled);
    }

    [Fact]
    public void FluentIconSourceExtension_WorksInStyleSettersWithoutCreatingAVisual()
    {
        RunOnStaThread(() =>
        {
            var style = new Style(typeof(Wpf.Zen.Ui.Controls.Button));
            style.Setters.Add(
                new Setter(
                    Wpf.Zen.Ui.Controls.Button.IconProperty,
                    new FluentIconSourceExtension(SymbolRegular.Settings24).ProvideValue(null!)
                )
            );
            var button = new Wpf.Zen.Ui.Controls.Button { Style = style };

            var icon = Assert.IsType<FluentIconSource>(button.Icon);
            Assert.Equal(SymbolRegular.Settings24, icon.Symbol);
            Assert.False(icon.Filled);
        });
    }

    [Fact]
    public void IconSlots_AcceptArbitraryWpfContentWithoutCoercion()
    {
        RunOnStaThread(() =>
        {
            var content = new Border();
            var button = new Wpf.Zen.Ui.Controls.Button { Icon = content };

            Assert.Equal(typeof(object), Wpf.Zen.Ui.Controls.Button.IconProperty.PropertyType);
            Assert.Same(content, button.Icon);
            Assert.Same(content, button.ReadLocalValue(Wpf.Zen.Ui.Controls.Button.IconProperty));
        });
    }

    [Fact]
    public void FluentIcon_UsesNativeTextInheritanceWithoutLocalCopies()
    {
        RunOnStaThread(() =>
        {
            var foreground = new SolidColorBrush(Colors.Crimson);
            var icon = new FluentIcon(SymbolRegular.Home24);
            var presenter = new ContentPresenter { Content = icon };

            presenter.SetValue(TextElement.FontSizeProperty, 27D);
            presenter.SetValue(TextElement.ForegroundProperty, foreground);
            presenter.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            Assert.IsAssignableFrom<System.Windows.Controls.TextBlock>(icon);
            Assert.Equal(27D, icon.FontSize);
            Assert.Same(foreground, icon.Foreground);
            Assert.Equal(DependencyProperty.UnsetValue, icon.ReadLocalValue(TextElement.FontSizeProperty));
            Assert.Equal(DependencyProperty.UnsetValue, icon.ReadLocalValue(TextElement.ForegroundProperty));
        });
    }

    [Fact]
    public void IconPresentationScope_AffectsOnlyCompatibleIcons()
    {
        RunOnStaThread(() =>
        {
            var icon = new FluentIcon(SymbolRegular.Home24);
            var text = new System.Windows.Controls.TextBlock { Text = "Home" };
            var host = new StackPanel { Children = { icon, text } };

            host.SetValue(TextElement.FontSizeProperty, 27D);
            host.SetValue(TextElement.FontWeightProperty, FontWeights.Bold);
            var textForeground = new SolidColorBrush(Colors.Crimson);
            var iconForeground = new SolidColorBrush(Colors.CornflowerBlue);
            host.SetValue(TextElement.ForegroundProperty, textForeground);
            IconPresenter.SetIconSize(host, 12D);
            IconPresenter.SetIconWeight(host, FontWeights.Light);
            IconPresenter.SetIconForeground(host, iconForeground);
            host.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            Assert.Equal(12D, icon.FontSize);
            Assert.Equal(FontWeights.Light, icon.FontWeight);
            Assert.Same(iconForeground, icon.Foreground);
            Assert.Equal(27D, text.FontSize);
            Assert.Equal(FontWeights.Bold, text.FontWeight);
            Assert.Same(textForeground, text.Foreground);
            Assert.Equal(DependencyProperty.UnsetValue, icon.ReadLocalValue(TextElement.FontSizeProperty));
            Assert.Equal(
                DependencyProperty.UnsetValue,
                icon.ReadLocalValue(TextElement.FontWeightProperty)
            );
            Assert.Equal(
                DependencyProperty.UnsetValue,
                icon.ReadLocalValue(TextElement.ForegroundProperty)
            );
        });
    }

    [Fact]
    public void FluentIcon_IconPresentationPreservesAndRestoresNativeFontValues()
    {
        RunOnStaThread(() =>
        {
            var icon = new FluentIcon(SymbolRegular.Home24)
            {
                FontSize = 30D,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Crimson,
            };
            var host = new Border { Child = icon };

            IconPresenter.SetIconSize(host, 12D);
            IconPresenter.SetIconWeight(host, FontWeights.SemiBold);
            IconPresenter.SetIconForeground(host, Brushes.CornflowerBlue);
            host.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            Assert.Equal(12D, icon.FontSize);
            Assert.Equal(FontWeights.SemiBold, icon.FontWeight);
            Assert.Same(Brushes.CornflowerBlue, icon.Foreground);
            Assert.Equal(30D, icon.ReadLocalValue(TextElement.FontSizeProperty));
            Assert.Equal(FontWeights.Bold, icon.ReadLocalValue(TextElement.FontWeightProperty));
            Assert.Same(Brushes.Crimson, icon.ReadLocalValue(TextElement.ForegroundProperty));

            host.ClearValue(IconPresenter.IconSizeProperty);
            host.ClearValue(IconPresenter.IconWeightProperty);
            host.ClearValue(IconPresenter.IconForegroundProperty);

            Assert.Equal(30D, icon.FontSize);
            Assert.Equal(FontWeights.Bold, icon.FontWeight);
            Assert.Same(Brushes.Crimson, icon.Foreground);
            Assert.Equal(30D, icon.ReadLocalValue(TextElement.FontSizeProperty));
            Assert.Equal(FontWeights.Bold, icon.ReadLocalValue(TextElement.FontWeightProperty));
            Assert.Same(Brushes.Crimson, icon.ReadLocalValue(TextElement.ForegroundProperty));
        });
    }

    [Fact]
    public void BreadcrumbBar_IconPresentationScopeReachesSeparatorInsideItemTemplate()
    {
        RunOnStaThread(() =>
        {
            var breadcrumbBar = new BreadcrumbBar
            {
                ItemsSource = new[] { "Home", "Folder" },
                FontSize = 27D,
            };
            IconPresenter.SetIconSize(breadcrumbBar, 12D);
            IconPresenter.SetIconWeight(breadcrumbBar, FontWeights.SemiBold);
            var window = CreateHiddenWindow(breadcrumbBar);

            try
            {
                window.Show();
                breadcrumbBar.UpdateLayout();

                var separator = Assert.IsType<FluentIcon>(
                    FindVisualDescendant<FluentIcon>(
                        breadcrumbBar,
                        icon => icon.Symbol == SymbolRegular.ChevronRight24
                    )
                );

                Assert.Equal(12D, separator.FontSize);
                Assert.Equal(FontWeights.SemiBold, separator.FontWeight);
                Assert.Equal(
                    DependencyProperty.UnsetValue,
                    separator.ReadLocalValue(TextElement.FontSizeProperty)
                );
                Assert.Equal(
                    DependencyProperty.UnsetValue,
                    separator.ReadLocalValue(TextElement.FontWeightProperty)
                );
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void FluentIcon_UpdatesItsExistingTextElementWhenSymbolChanges()
    {
        RunOnStaThread(() =>
        {
            var icon = new FluentIcon(SymbolRegular.Home24);
            string initialText = icon.Text;

            icon.Symbol = SymbolRegular.Settings24;

            Assert.NotEqual(initialText, icon.Text);
            Assert.Equal(SymbolRegular.Settings24.GetString(), icon.Text);
        });
    }

    [Fact]
    public void FluentIconSource_UsesTheLibraryImplicitTemplateInOrdinaryContentPresenters()
    {
        RunOnStaThread(() =>
        {
            var iconData = new FluentIconSource(SymbolRegular.Home24, filled: true);
            var firstPresenter = new ContentPresenter { Content = iconData };
            var secondPresenter = new ContentPresenter { Content = iconData };
            var host = new StackPanel { Children = { firstPresenter, secondPresenter } };
            var window = CreateHiddenWindow(host);

            try
            {
                window.Show();
                host.UpdateLayout();

                var firstIcon = Assert.IsType<FluentIcon>(FindVisualDescendant<FluentIcon>(firstPresenter));
                var secondIcon = Assert.IsType<FluentIcon>(FindVisualDescendant<FluentIcon>(secondPresenter));

                Assert.Equal(SymbolRegular.Home24, firstIcon.Symbol);
                Assert.Equal(SymbolRegular.Home24, secondIcon.Symbol);
                Assert.True(firstIcon.Filled);
                Assert.True(secondIcon.Filled);
                Assert.NotSame(firstIcon, secondIcon);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void FluentIconSource_NearerImplicitTemplateOverridesTheLibraryDefault()
    {
        RunOnStaThread(() =>
        {
            var localTemplate = new DataTemplate(typeof(FluentIconSource))
            {
                VisualTree = new FrameworkElementFactory(typeof(Border)),
            };
            var presenter = new ContentPresenter
            {
                Content = new FluentIconSource(SymbolRegular.Home24),
            };
            presenter.Resources.Add(localTemplate.DataTemplateKey, localTemplate);
            var window = CreateHiddenWindow(presenter);

            try
            {
                window.Show();
                presenter.UpdateLayout();

                Assert.NotNull(FindVisualDescendant<Border>(presenter));
                Assert.Null(FindVisualDescendant<FluentIcon>(presenter));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void IconPresenter_PropagatesVariantToGeneratedFluentIconWithoutMutatingSource()
    {
        RunOnStaThread(() =>
        {
            var iconSource = new FluentIconSource(SymbolRegular.Home24);
            var presenter = new IconPresenter { Content = iconSource };
            var window = CreateHiddenWindow(presenter);

            try
            {
                window.Show();
                presenter.UpdateLayout();

                var generatedIcon = Assert.IsType<FluentIcon>(
                    FindVisualDescendant<FluentIcon>(presenter)
                );
                Assert.Equal(SymbolRegular.Home24, generatedIcon.Symbol);
                Assert.False(generatedIcon.Filled);

                presenter.Variant = IconVariant.Filled;

                Assert.Equal(IconVariant.Filled, generatedIcon.Variant);
                Assert.True(generatedIcon.Filled);
                Assert.Equal(SymbolRegular.Home24, iconSource.Symbol);
                Assert.False(iconSource.Filled);

                presenter.Variant = IconVariant.Default;
                Assert.False(generatedIcon.Filled);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FluentIcon_CombinesItsDefaultWithPresenterVariant(bool filled)
    {
        RunOnStaThread(() =>
        {
            var icon = new FluentIcon(SymbolRegular.Home24, filled);
            var presenter = new IconPresenter { Content = icon };
            var window = CreateHiddenWindow(presenter);

            try
            {
                window.Show();
                presenter.UpdateLayout();

                presenter.Variant = IconVariant.Filled;
                Assert.True(icon.Filled);
                Assert.Equal(filled, icon.ReadLocalValue(FluentIcon.FilledProperty));

                presenter.Variant = IconVariant.Regular;
                Assert.False(icon.Filled);
                Assert.Equal(filled, icon.ReadLocalValue(FluentIcon.FilledProperty));

                presenter.Variant = IconVariant.Default;
                Assert.Equal(filled, icon.Filled);
                Assert.Equal(filled, icon.ReadLocalValue(FluentIcon.FilledProperty));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void FluentIcon_PreservesFilledBindingDuringPresenterVariantChanges()
    {
        RunOnStaThread(() =>
        {
            var source = new Border { Tag = false };
            var icon = new FluentIcon(SymbolRegular.Home24);
            BindingOperations.SetBinding(
                icon,
                FluentIcon.FilledProperty,
                new Binding(nameof(FrameworkElement.Tag)) { Source = source }
            );

            icon.Variant = IconVariant.Filled;

            Assert.True(icon.Filled);
            Assert.True(BindingOperations.IsDataBound(icon, FluentIcon.FilledProperty));

            icon.Variant = IconVariant.Default;

            Assert.False(icon.Filled);
            Assert.True(BindingOperations.IsDataBound(icon, FluentIcon.FilledProperty));

            source.Tag = true;
            Assert.True(icon.Filled);
        });
    }

    [Fact]
    public void CustomIconFamily_CanConsumeTheSamePresenterVariantContract()
    {
        RunOnStaThread(() =>
        {
            var template = new DataTemplate(typeof(TestIconSource))
            {
                VisualTree = new FrameworkElementFactory(typeof(TestIcon)),
            };
            var presenter = new IconPresenter { Content = new TestIconSource() };
            presenter.Resources.Add(template.DataTemplateKey, template);
            var window = CreateHiddenWindow(presenter);

            try
            {
                window.Show();
                presenter.UpdateLayout();

                var icon = Assert.IsType<TestIcon>(FindVisualDescendant<TestIcon>(presenter));
                Assert.Equal(IconVariant.Default, icon.Variant);

                presenter.Variant = IconVariant.Filled;

                Assert.Equal(IconVariant.Filled, icon.Variant);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static Window CreateHiddenWindow(object content)
    {
        return new Window
        {
            Content = content,
            Width = 200D,
            Height = 80D,
            Left = -10_000D,
            Top = -10_000D,
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
        };
    }

    private static T? FindVisualDescendant<T>(DependencyObject parent, Predicate<T>? predicate = null)
        where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match && (predicate is null || predicate(match)))
            {
                return match;
            }

            if (FindVisualDescendant(child, predicate) is { } descendant)
            {
                return descendant;
            }
        }

        return null;
    }

    private static void RunOnStaThread(Action action)
    {
        WpfTestThread.Run(action);
    }

    public sealed class TestIconSource : IconSource { }

    public sealed class TestIcon : Border
    {
        public static readonly DependencyProperty VariantProperty =
            IconPresenter.VariantProperty.AddOwner(
                typeof(TestIcon),
                new FrameworkPropertyMetadata(
                    IconVariant.Default,
                    FrameworkPropertyMetadataOptions.Inherits
                )
            );

        public IconVariant Variant
        {
            get => (IconVariant)GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }
    }
}
