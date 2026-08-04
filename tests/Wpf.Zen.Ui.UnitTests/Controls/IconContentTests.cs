// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.ExceptionServices;
using System.Threading;
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
    public void FluentSystemIconExtension_ReturnsReusableIconData()
    {
        var extension = new FluentSystemIconExtension(SymbolRegular.Settings24, filled: true);

        var icon = Assert.IsType<FluentSystemIcon>(extension.ProvideValue(null!));
        Assert.Equal(SymbolRegular.Settings24, icon.Symbol);
        Assert.True(icon.Filled);
    }

    [Fact]
    public void FluentSystemIconExtension_WorksInStyleSettersWithoutCreatingAVisual()
    {
        RunOnStaThread(() =>
        {
            var style = new Style(typeof(Wpf.Zen.Ui.Controls.Button));
            style.Setters.Add(
                new Setter(
                    Wpf.Zen.Ui.Controls.Button.IconProperty,
                    new FluentSystemIconExtension(SymbolRegular.Settings24).ProvideValue(null!)
                )
            );
            var button = new Wpf.Zen.Ui.Controls.Button { Style = style };

            var icon = Assert.IsType<FluentSystemIcon>(button.Icon);
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
    public void SymbolIcon_UsesNativeTextInheritanceWithoutLocalCopies()
    {
        RunOnStaThread(() =>
        {
            var foreground = new SolidColorBrush(Colors.Crimson);
            var icon = new SymbolIcon(SymbolRegular.Home24);
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
    public void SymbolIcon_UpdatesItsExistingTextElementWhenSymbolChanges()
    {
        RunOnStaThread(() =>
        {
            var icon = new SymbolIcon(SymbolRegular.Home24);
            string initialText = icon.Text;

            icon.Symbol = SymbolRegular.Settings24;

            Assert.NotEqual(initialText, icon.Text);
            Assert.Equal(SymbolRegular.Settings24.GetString(), icon.Text);
        });
    }

    [Fact]
    public void FluentSystemIcon_UsesTheLibraryImplicitTemplateInOrdinaryContentPresenters()
    {
        RunOnStaThread(() =>
        {
            var iconData = new FluentSystemIcon(SymbolRegular.Home24, filled: true);
            var firstPresenter = new ContentPresenter { Content = iconData };
            var secondPresenter = new ContentPresenter { Content = iconData };
            var host = new StackPanel { Children = { firstPresenter, secondPresenter } };
            var window = CreateHiddenWindow(host);

            try
            {
                window.Show();
                host.UpdateLayout();

                var firstIcon = Assert.IsType<SymbolIcon>(FindVisualDescendant<SymbolIcon>(firstPresenter));
                var secondIcon = Assert.IsType<SymbolIcon>(FindVisualDescendant<SymbolIcon>(secondPresenter));

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
    public void FluentSystemIcon_NearerImplicitTemplateOverridesTheLibraryDefault()
    {
        RunOnStaThread(() =>
        {
            var localTemplate = new DataTemplate(typeof(FluentSystemIcon))
            {
                VisualTree = new FrameworkElementFactory(typeof(Border)),
            };
            var presenter = new ContentPresenter
            {
                Content = new FluentSystemIcon(SymbolRegular.Home24),
            };
            presenter.Resources.Add(localTemplate.DataTemplateKey, localTemplate);
            var window = CreateHiddenWindow(presenter);

            try
            {
                window.Show();
                presenter.UpdateLayout();

                Assert.NotNull(FindVisualDescendant<Border>(presenter));
                Assert.Null(FindVisualDescendant<SymbolIcon>(presenter));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void NavigationSymbolData_CreatesAVisualPerPresenterAndTracksFilledState()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary
            {
                Source = System.IO.Packaging.PackUriHelper.Create(
                    new Uri("application:///", UriKind.Absolute),
                    new Uri("/Wpf.Zen.Ui;component/Themes/Generic.xaml", UriKind.Relative)
                ),
            };
            var item = new NavigationViewItem
            {
                Icon = new FluentSystemIcon(SymbolRegular.Home24),
                Style = Assert.IsType<Style>(resources[typeof(NavigationViewItem)]),
            };
            var window = CreateHiddenWindow(item);

            try
            {
                window.Show();
                item.UpdateLayout();

                var generatedIcon = Assert.IsType<SymbolIcon>(
                    FindVisualDescendant<SymbolIcon>(
                        item,
                        candidate => candidate.Symbol == SymbolRegular.Home24
                    )
                );
                Assert.Equal(SymbolRegular.Home24, generatedIcon.Symbol);
                Assert.False(generatedIcon.Filled);

                NavigationViewItemTemplateBehavior.SetIsIconFilled(item, true);
                Assert.True(NavigationViewItemTemplateBehavior.GetIsIconFilled(generatedIcon));
                Assert.True(generatedIcon.Filled);
                var iconData = Assert.IsType<FluentSystemIcon>(item.Icon);
                Assert.Equal(SymbolRegular.Home24, iconData.Symbol);
                Assert.False(iconData.Filled);
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
    public void NavigationSymbolVisual_CombinesExplicitAndNavigationFilledState(bool filled)
    {
        RunOnStaThread(() =>
        {
            var icon = new SymbolIcon(SymbolRegular.Home24, filled);
            var item = new NavigationViewItem { Icon = icon };

            NavigationViewItemTemplateBehavior.SetIsIconFilled(item, !filled);

            Assert.True(icon.Filled);
            Assert.Equal(filled, icon.ReadLocalValue(SymbolIcon.FilledProperty));

            NavigationViewItemTemplateBehavior.SetIsIconFilled(item, false);

            Assert.Equal(filled, icon.Filled);
            Assert.Equal(filled, icon.ReadLocalValue(SymbolIcon.FilledProperty));
        });
    }

    [Fact]
    public void NavigationSymbolVisual_PreservesFilledBindingDuringNavigationStateChanges()
    {
        RunOnStaThread(() =>
        {
            var source = new Border { Tag = false };
            var icon = new SymbolIcon(SymbolRegular.Home24);
            BindingOperations.SetBinding(
                icon,
                SymbolIcon.FilledProperty,
                new Binding(nameof(FrameworkElement.Tag)) { Source = source }
            );
            var item = new NavigationViewItem { Icon = icon };

            NavigationViewItemTemplateBehavior.SetIsIconFilled(item, true);

            Assert.True(icon.Filled);
            Assert.True(BindingOperations.IsDataBound(icon, SymbolIcon.FilledProperty));

            NavigationViewItemTemplateBehavior.SetIsIconFilled(item, false);

            Assert.False(icon.Filled);
            Assert.True(BindingOperations.IsDataBound(icon, SymbolIcon.FilledProperty));

            source.Tag = true;
            Assert.True(icon.Filled);
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
