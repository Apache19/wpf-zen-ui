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
        var extension = new FluentIconSourceExtension(FluentSymbolRegular.Settings24, filled: true);

        var icon = Assert.IsType<FluentIconSource>(extension.ProvideValue(null!));
        Assert.Equal(FluentSymbolRegular.Settings24, icon.Symbol);
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
                    new FluentIconSourceExtension(FluentSymbolRegular.Settings24).ProvideValue(null!)
                )
            );
            var button = new Wpf.Zen.Ui.Controls.Button { Style = style };

            var icon = Assert.IsType<FluentIconSource>(button.Icon);
            Assert.Equal(FluentSymbolRegular.Settings24, icon.Symbol);
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
            Assert.Same(
                content,
                button.ReadLocalValue(Wpf.Zen.Ui.Controls.Button.IconProperty)
            );
        });
    }

    [Fact]
    public void IconMargin_IsLocalToTheOwningControl()
    {
        RunOnStaThread(() =>
        {
            var parent = new Wpf.Zen.Ui.Controls.Button();
            var child = new Wpf.Zen.Ui.Controls.Button();
            parent.Content = child;

            var margin = new Thickness(1D, 2D, 3D, 4D);
            parent.IconMargin = margin;

            Assert.Equal(margin, parent.IconMargin);

            // Slot layout belongs to the templated control and must not leak into the control's
            // content. The nested button therefore keeps the value its own style supplies and
            // never acquires a local one.
            Assert.NotEqual(margin, child.IconMargin);
            Assert.Same(
                DependencyProperty.UnsetValue,
                child.ReadLocalValue(Wpf.Zen.Ui.Controls.Button.IconMarginProperty)
            );
            Assert.Equal(
                default(Thickness),
                Wpf.Zen.Ui.Controls.Button.IconMarginProperty.DefaultMetadata.DefaultValue
            );
        });
    }

    /// <summary>
    /// The icon slot is a per-control contract, not something every WPF control opts into. Native
    /// control styles therefore carry no icon host at all; applications that want an icon use the
    /// library control (for example <see cref="Wpf.Zen.Ui.Controls.Button"/>) instead.
    /// </summary>
    [Fact]
    public void NativeButtonTemplate_HasNoIconSlot()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary
            {
                Source = new Uri(
                    "pack://application:,,,/Wpf.Zen.Ui;component/Controls/Button/NativeButton.xaml",
                    UriKind.Absolute
                ),
            };
            var button = new System.Windows.Controls.Button
            {
                Content = "Native",
                Style = Assert.IsType<Style>(resources[typeof(System.Windows.Controls.Button)]),
            };

            Assert.True(button.ApplyTemplate());

            Assert.Null(button.Template.FindName("iconPresenter", button));
            Assert.Null(typeof(System.Windows.Controls.Button).GetProperty("Icon"));
            Assert.Null(typeof(System.Windows.Controls.Button).GetProperty("IconMargin"));
        });
    }

    [Fact]
    public void FontIcon_UsesNativeTextInheritanceWithoutLocalCopies()
    {
        RunOnStaThread(() =>
        {
            var foreground = new SolidColorBrush(Colors.Crimson);
            var icon = new FontIcon(new FluentIconSource(FluentSymbolRegular.Home24));
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
            var icon = new FontIcon(new FluentIconSource(FluentSymbolRegular.Home24));
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
    public void FontIcon_IconPresentationPreservesAndRestoresNativeFontValues()
    {
        RunOnStaThread(() =>
        {
            var icon = new FontIcon(new FluentIconSource(FluentSymbolRegular.Home24))
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

                var separator = Assert.IsType<FontIcon>(
                    FindVisualDescendant<FontIcon>(
                        breadcrumbBar,
                        icon => icon.Source is FluentIconSource { Symbol: FluentSymbolRegular.ChevronRight24 }
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
    public void FontIcon_UpdatesItsExistingTextElementWhenSymbolChanges()
    {
        RunOnStaThread(() =>
        {
            var icon = new FontIcon(new FluentIconSource(FluentSymbolRegular.Home24));
            string initialText = icon.Text;

            icon.Source = new FluentIconSource(FluentSymbolRegular.Settings24);

            Assert.NotEqual(initialText, icon.Text);
            Assert.Equal(char.ConvertFromUtf32((int)FluentSymbolRegular.Settings24), icon.Text);
        });
    }

    [Fact]
    public void FluentIconSource_UsesTheLibraryImplicitTemplateInOrdinaryContentPresenters()
    {
        RunOnStaThread(() =>
        {
            var iconData = new FluentIconSource(FluentSymbolRegular.Home24, filled: true);
            var firstPresenter = new ContentPresenter { Content = iconData };
            var secondPresenter = new ContentPresenter { Content = iconData };
            var host = new StackPanel { Children = { firstPresenter, secondPresenter } };
            var window = CreateHiddenWindow(host);

            try
            {
                window.Show();
                host.UpdateLayout();

                var firstIcon = Assert.IsType<FontIcon>(FindVisualDescendant<FontIcon>(firstPresenter));
                var secondIcon = Assert.IsType<FontIcon>(FindVisualDescendant<FontIcon>(secondPresenter));

                Assert.Equal(char.ConvertFromUtf32((int)FluentSymbolRegular.Home24), firstIcon.Source!.WithVariant(IconVariant.Regular).Glyph);
                Assert.Equal(char.ConvertFromUtf32((int)FluentSymbolRegular.Home24), secondIcon.Source!.WithVariant(IconVariant.Regular).Glyph);
                Assert.Equal(char.ConvertFromUtf32((int)FluentSymbolFilled.Home24), firstIcon.Text);
                Assert.Equal(char.ConvertFromUtf32((int)FluentSymbolFilled.Home24), secondIcon.Text);
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
                Content = new FluentIconSource(FluentSymbolRegular.Home24),
            };
            presenter.Resources.Add(localTemplate.DataTemplateKey, localTemplate);
            var window = CreateHiddenWindow(presenter);

            try
            {
                window.Show();
                presenter.UpdateLayout();

                Assert.NotNull(FindVisualDescendant<Border>(presenter));
                Assert.Null(FindVisualDescendant<FontIcon>(presenter));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void IconPresenter_PropagatesVariantToGeneratedFontIconWithoutMutatingSource()
    {
        RunOnStaThread(() =>
        {
            var iconSource = new FluentIconSource(FluentSymbolRegular.Home24);
            var presenter = new IconPresenter { Content = iconSource };
            var window = CreateHiddenWindow(presenter);

            try
            {
                window.Show();
                presenter.UpdateLayout();

                var generatedIcon = Assert.IsType<FontIcon>(
                    FindVisualDescendant<FontIcon>(presenter)
                );
                Assert.Equal(char.ConvertFromUtf32((int)FluentSymbolRegular.Home24), generatedIcon.Source!.WithVariant(IconVariant.Regular).Glyph);
                Assert.Equal(char.ConvertFromUtf32((int)FluentSymbolRegular.Home24), generatedIcon.Text);

                presenter.Variant = IconVariant.Filled;

                Assert.Equal(IconVariant.Filled, generatedIcon.Variant);
                Assert.Equal(char.ConvertFromUtf32((int)FluentSymbolFilled.Home24), generatedIcon.Text);
                Assert.Equal(FluentSymbolRegular.Home24, iconSource.Symbol);
                Assert.False(iconSource.Filled);

                presenter.Variant = IconVariant.Default;
                Assert.Equal(char.ConvertFromUtf32((int)FluentSymbolRegular.Home24), generatedIcon.Text);
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
    public void FontIcon_CombinesItsDefaultWithPresenterVariant(bool filled)
    {
        RunOnStaThread(() =>
        {
            var icon = new FontIcon(new FluentIconSource(FluentSymbolRegular.Home24, filled));
            var presenter = new IconPresenter { Content = icon };
            var window = CreateHiddenWindow(presenter);

            try
            {
                window.Show();
                presenter.UpdateLayout();

                presenter.Variant = IconVariant.Filled;
                Assert.Equal(char.ConvertFromUtf32((int)FluentSymbolFilled.Home24), icon.Text);
                Assert.Equal(filled, ((FluentIconSource)icon.Source!).Filled);

                presenter.Variant = IconVariant.Regular;
                Assert.Equal(char.ConvertFromUtf32((int)FluentSymbolRegular.Home24), icon.Text);
                Assert.Equal(filled, ((FluentIconSource)icon.Source!).Filled);

                presenter.Variant = IconVariant.Default;
                Assert.Equal(icon.Source!.Glyph, icon.Text);
                Assert.Equal(filled, ((FluentIconSource)icon.Source!).Filled);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void FontIcon_PreservesSourceBindingDuringPresenterVariantChanges()
    {
        RunOnStaThread(() =>
        {
            var source = new Border { Tag = new FluentIconSource(FluentSymbolRegular.Home24) };
            var icon = new FontIcon(new FluentIconSource(FluentSymbolRegular.Home24));
            BindingOperations.SetBinding(
                icon,
                FontIcon.SourceProperty,
                new Binding(nameof(FrameworkElement.Tag)) { Source = source }
            );

            icon.Variant = IconVariant.Filled;

            Assert.Equal(char.ConvertFromUtf32((int)FluentSymbolFilled.Home24), icon.Text);
            Assert.True(BindingOperations.IsDataBound(icon, FontIcon.SourceProperty));

            icon.Variant = IconVariant.Default;

            Assert.Equal(char.ConvertFromUtf32((int)FluentSymbolRegular.Home24), icon.Text);
            Assert.True(BindingOperations.IsDataBound(icon, FontIcon.SourceProperty));

            source.Tag = new FluentIconSource(FluentSymbolRegular.Home24, true);
            Assert.Equal(char.ConvertFromUtf32((int)FluentSymbolFilled.Home24), icon.Text);
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

    /// <summary>
    /// A new font family uses the library's base template and all hosts without a custom visual.
    /// Its optional resource key is available to external subclasses as well as library sources.
    /// </summary>
    [Fact]
    public void ExternalFontSource_UsesBaseTemplateAndScopedFontsAcrossAllHosts()
    {
        RunOnStaThread(() =>
        {
            var source = new TestFontSource();
            var presenter = new ContentPresenter { Content = source };
            var span = new FontIconSpan(source);
            var text = new System.Windows.Controls.TextBlock();
            text.Inlines.Add(span);
            var badge = new Badge { Icon = source };
            var host = new StackPanel { Children = { presenter, text, badge } };
            var scopedFont = new FontFamily("Segoe UI Symbol");
            host.Resources[TestFontSource.ResourceKey] = scopedFont;
            var window = CreateHiddenWindow(host);

            try
            {
                window.Show();
                host.UpdateLayout();
                var icon = Assert.IsType<FontIcon>(FindVisualDescendant<FontIcon>(presenter));
                Assert.Same(source, icon.Source);
                Assert.Equal(source.Glyph, icon.Text);
                Assert.Equal(source.Glyph, span.Text);
                Assert.Same(scopedFont, icon.FontFamily);
                Assert.Same(scopedFont, span.FontFamily);
                Assert.True(badge.DesiredSize.Width > 0);

                // An unsupported presentation request must leave a third-party source usable.
                IconPresenter.SetVariant(host, IconVariant.Filled);
                Assert.Equal(source.Glyph, icon.Text);
                Assert.Equal(source.Glyph, span.Text);
                Assert.Same(source, icon.Source);

                host.Resources.Remove(TestFontSource.ResourceKey);
                Assert.Same(source.FontFamily, icon.FontFamily);
                Assert.Same(source.FontFamily, span.FontFamily);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Clearing and replacing Source must release old resource expressions and update inline text.
    /// Separate hosts may select different variants of the same immutable source.
    /// </summary>
    [Fact]
    public void SourceReplacement_ClearsOldFontAndKeepsSharedPresentationsIndependent()
    {
        RunOnStaThread(() =>
        {
            var source = new FluentIconSource(FluentSymbolRegular.Home24);
            var icon = new FontIcon(source) { Variant = IconVariant.Filled };
            var span = new FontIconSpan(source);
            Assert.Equal(char.ConvertFromUtf32((int)FluentSymbolFilled.Home24), icon.Text);
            Assert.Equal(char.ConvertFromUtf32((int)FluentSymbolRegular.Home24), span.Text);
            Assert.NotEqual(icon.FontFamily, span.FontFamily);
            Assert.False(source.Filled);

            var custom = new TestFontSource();
            icon.Source = custom;
            span.Source = custom;
            Assert.Equal(custom.Glyph, icon.Text);
            Assert.Equal(custom.Glyph, span.Text);
            Assert.Same(custom.FontFamily, icon.FontFamily);
            Assert.Same(custom.FontFamily, span.FontFamily);

            var inheritedFont = new FontFamily("Arial");
            var text = new System.Windows.Controls.TextBlock { FontFamily = inheritedFont };
            text.Inlines.Add(span);
            var host = new StackPanel { Children = { icon, text } };
            host.SetValue(TextElement.FontFamilyProperty, inheritedFont);
            icon.Source = null;
            span.Source = null;
            Assert.Empty(icon.Text);
            Assert.Empty(span.Text);
            Assert.Same(inheritedFont, icon.FontFamily);
            Assert.Same(inheritedFont, span.FontFamily);

            icon.Source = new FluentIconSource(FluentSymbolRegular.Empty, true);
            span.Source = icon.Source;
            Assert.Empty(icon.Text);
            Assert.Empty(span.Text);
        });
    }

    /// <summary>
    /// Minimal external family: only glyph and font are required, resource overrides are optional.
    /// </summary>
    private sealed class TestFontSource : FontIconSource
    {
        internal static readonly object ResourceKey = new object();

        public override string Glyph => "★";

        public override FontFamily FontFamily { get; } = new FontFamily("Segoe UI");

        public override object FontFamilyResourceKey => ResourceKey;
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
