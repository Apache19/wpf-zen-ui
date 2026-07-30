// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using Wpf.Ui.Markup;

namespace Wpf.Ui.UnitTests.Controls;

public class IconContentTests
{
    [Fact]
    public void SymbolExtension_ReturnsReusableSymbolData()
    {
        var extension = new SymbolExtension(SymbolRegular.Settings24);

        Assert.Equal(SymbolRegular.Settings24, extension.ProvideValue(null!));
    }

    [Fact]
    public void SymbolExtension_WorksInStyleSettersWithoutCreatingAVisual()
    {
        RunOnStaThread(() =>
        {
            var style = new Style(typeof(Wpf.Ui.Controls.Button));
            style.Setters.Add(
                new Setter(
                    Wpf.Ui.Controls.Button.IconProperty,
                    new SymbolExtension(SymbolRegular.Settings24).ProvideValue(null!)
                )
            );
            var button = new Wpf.Ui.Controls.Button { Style = style };

            Assert.Equal(SymbolRegular.Settings24, Assert.IsType<SymbolRegular>(button.Icon));
        });
    }

    [Fact]
    public void IconSlots_AcceptArbitraryWpfContentWithoutCoercion()
    {
        RunOnStaThread(() =>
        {
            var content = new Border();
            var button = new Wpf.Ui.Controls.Button { Icon = content };

            Assert.Equal(typeof(object), Wpf.Ui.Controls.Button.IconProperty.PropertyType);
            Assert.Same(content, button.Icon);
            Assert.Same(content, button.ReadLocalValue(Wpf.Ui.Controls.Button.IconProperty));
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
    public void SymbolRegular_UsesTheLibraryImplicitTemplateInOrdinaryContentPresenters()
    {
        RunOnStaThread(() =>
        {
            var firstPresenter = new ContentPresenter { Content = SymbolRegular.Home24 };
            var secondPresenter = new ContentPresenter { Content = SymbolRegular.Home24 };
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
                Assert.NotSame(firstIcon, secondIcon);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void SymbolRegular_NearerImplicitTemplateOverridesTheLibraryDefault()
    {
        RunOnStaThread(() =>
        {
            var localTemplate = new DataTemplate(typeof(SymbolRegular))
            {
                VisualTree = new FrameworkElementFactory(typeof(Border)),
            };
            var presenter = new ContentPresenter { Content = SymbolRegular.Home24 };
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
                    new Uri("/Wpf.Ui;component/Themes/Generic.xaml", UriKind.Relative)
                ),
            };
            var item = new NavigationViewItem
            {
                Icon = SymbolRegular.Home24,
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
                Assert.Equal(SymbolRegular.Home24, Assert.IsType<SymbolRegular>(item.Icon));
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
