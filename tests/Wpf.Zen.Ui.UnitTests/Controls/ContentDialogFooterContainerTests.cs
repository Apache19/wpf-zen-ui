// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls;

public partial class ContentDialogTests
{
    /// <summary>
    /// Verifies that custom templates can omit Style while explicit template styles and null
    /// retain their normal precedence over the container's automatic resource reference.
    /// </summary>
    /// <param name="styleAttribute">The explicit Style declaration, or an empty string.</param>
    [Theory]
    [InlineData("")]
    [InlineData("Style=\"{StaticResource ExplicitFooterStyle}\"")]
    [InlineData("Style=\"{x:Null}\"")]
    public void Footer_CustomTemplateHandlesStyleLookup(string styleAttribute)
    {
        Run(scope =>
        {
            var root = Assert.IsType<Grid>(scope.Window.Content);
            var implicitStyle = new Style(typeof(ContentDialogFooterContainer));
            implicitStyle.Setters.Add(new Setter(Border.PaddingProperty, new Thickness(9D)));
            root.Resources.Add(typeof(ContentDialogFooterContainer), implicitStyle);
            var control = new Control();
            control.Template = Assert.IsType<ControlTemplate>(XamlReader.Parse($$"""
                <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                 xmlns:ui="http://schemas.thezendev.com/wpf/2027"
                                 TargetType="{x:Type Control}">
                    <ControlTemplate.Resources>
                        <Style x:Key="ExplicitFooterStyle"
                               TargetType="{x:Type ui:ContentDialogFooterContainer}">
                            <Setter Property="Padding" Value="17" />
                        </Style>
                    </ControlTemplate.Resources>
                    <ui:ContentDialogFooterContainer x:Name="Footer" {{styleAttribute}} />
                </ControlTemplate>
                """));
            root.Children.Add(control);
            control.ApplyTemplate();
            Drain();
            var footer = Assert.IsType<ContentDialogFooterContainer>(
                control.Template.FindName("Footer", control)
            );

            if (styleAttribute.Length == 0)
            {
                Assert.Same(implicitStyle, footer.Style);
                Assert.Equal(new Thickness(9D), footer.Padding);
                var replacement = new Style(typeof(ContentDialogFooterContainer));
                replacement.Setters.Add(new Setter(Border.PaddingProperty, new Thickness(12D)));
                root.Resources[typeof(ContentDialogFooterContainer)] = replacement;
                Drain();
                Assert.Same(replacement, footer.Style);
                Assert.Equal(new Thickness(12D), footer.Padding);
            }
            else if (styleAttribute.Contains("x:Null"))
            {
                Assert.Null(footer.Style);
            }
            else
            {
                Assert.Same(control.Template.Resources["ExplicitFooterStyle"], footer.Style);
                Assert.Equal(new Thickness(17D), footer.Padding);
            }

            Assert.Equal(new Thickness(0D, 1D, 0D, 0D), footer.BorderThickness);
        });
    }

    /// <summary>
    /// Verifies that footer style lookup crosses the dialog template boundary while the
    /// default theme continues to supply properties omitted by an application-level style.
    /// </summary>
    [Fact]
    public void Footer_StyleLookupReachesResourcesOutsideDialog()
    {
        Run(scope =>
        {
            var style = new Style(typeof(ContentDialogFooterContainer));
            style.Setters.Add(new Setter(Border.PaddingProperty, new Thickness(9D)));
            // The scope merges Generic.xaml into this root. A nearer theme dictionary would
            // otherwise shadow a Window-level style through normal WPF resource precedence.
            var root = Assert.IsType<Grid>(scope.Window.Content);
            root.Resources.Add(typeof(ContentDialogFooterContainer), style);
            var dialog = scope.CreateDialog();
            dialog.Footer = "Actions";

            var task = scope.Host.ShowAsync(dialog);
            Drain();
            dialog.UpdateLayout();

            var footer = Assert.IsType<ContentDialogFooterContainer>(
                dialog.Template.FindName("footerBorder", dialog)
            );
            Assert.Same(style, footer.Style);
            Assert.Equal(new Thickness(9D), footer.Padding);
            Assert.Equal(new Thickness(0D, 1D, 0D, 0D), footer.BorderThickness);
            Assert.NotNull(footer.Background);

            dialog.Close();
            Pump(task);
        });
    }

    /// <summary>
    /// Verifies that a dialog-scoped style overrides footer chrome without duplicating theme
    /// defaults, and that replacing the style restores properties supplied by the theme.
    /// </summary>
    [Fact]
    public void Footer_ImplicitStyleOverridesChromeAndRetainsThemeDefaults()
    {
        Run(scope =>
        {
            var dialog = scope.CreateDialog();
            var content = new System.Windows.Controls.Button { Content = "Save" };
            dialog.Footer = content;
            var style = Assert.IsType<Style>(XamlReader.Parse("""
                <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                       xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                       xmlns:ui="http://schemas.thezendev.com/wpf/2027"
                       TargetType="{x:Type ui:ContentDialogFooterContainer}">
                    <Setter Property="Background" Value="Transparent" />
                    <Setter Property="BorderThickness" Value="0" />
                </Style>
                """));
            dialog.Resources.Add(typeof(ContentDialogFooterContainer), style);

            var task = scope.Host.ShowAsync(dialog);
            Drain();
            dialog.UpdateLayout();

            var footer = Assert.IsType<ContentDialogFooterContainer>(
                dialog.Template.FindName("footerBorder", dialog)
            );
            Assert.Same(style, footer.Style);
            Assert.Null(style.BasedOn);
            Assert.Equal(Colors.Transparent, Assert.IsType<SolidColorBrush>(footer.Background).Color);
            Assert.Equal(new Thickness(0D), footer.BorderThickness);
            Assert.Equal(new Thickness(24D, 20D, 24D, 20D), footer.Padding);
            Assert.Equal(
                BaseValueSource.DefaultStyle,
                DependencyPropertyHelper.GetValueSource(footer, Border.PaddingProperty).BaseValueSource
            );
            Assert.Same(dialog, content.Parent);

            var spacingStyle = new Style(typeof(ContentDialogFooterContainer));
            spacingStyle.Setters.Add(new Setter(Border.PaddingProperty, new Thickness(12D)));
            dialog.Resources[typeof(ContentDialogFooterContainer)] = spacingStyle;
            Drain();
            dialog.UpdateLayout();

            Assert.Equal(new Thickness(12D), footer.Padding);
            Assert.Equal(new Thickness(0D, 1D, 0D, 0D), footer.BorderThickness);
            Assert.NotNull(footer.Background);
            Assert.NotNull(footer.BorderBrush);

            dialog.Footer = null;
            dialog.UpdateLayout();
            Assert.Equal(Visibility.Collapsed, footer.Visibility);
            Assert.Null(content.Parent);

            dialog.Close();
            Pump(task);
        });
    }
}
