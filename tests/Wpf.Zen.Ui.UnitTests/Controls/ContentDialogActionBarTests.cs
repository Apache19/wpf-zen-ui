// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using Wpf.Zen.Ui.Controls;
using NativeButton = System.Windows.Controls.Button;

namespace Wpf.Zen.Ui.UnitTests.Controls;

public class ContentDialogActionBarTests
{
    [Fact]
    public void ItemsControlContract_SupportsDirectElementsAndDataDrivenContainers()
    {
        WpfTestThread.Run(() =>
        {
            var directAction = new NativeButton { Content = "Direct" };
            var actions = CreateStyledActions();
            actions.Items.Add(directAction);
            actions.Items.Add("Generated");

            using var scope = new WindowScope(actions);

            Assert.Same(directAction, actions.ItemContainerGenerator.ContainerFromIndex(0));
            var generatedContainer = Assert.IsType<ContentPresenter>(
                actions.ItemContainerGenerator.ContainerFromIndex(1)
            );
            Assert.Equal("Generated", generatedContainer.Content);
            Assert.True(actions.IsItemItsOwnContainer(directAction));
            Assert.False(actions.IsItemItsOwnContainer("Generated"));
        });
    }

    [Fact]
    public void ItemsSourceAndItemTemplate_UseStandardItemsControlGeneration()
    {
        WpfTestThread.Run(() =>
        {
            var source = new ObservableCollection<ActionModel>
            {
                new("Save"),
                new("Cancel"),
            };
            var actions = CreateStyledActions();
            actions.ItemsSource = source;
            actions.ItemTemplate = (DataTemplate)XamlReader.Parse(
                """
                <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                    <Button Content="{Binding Label}" />
                </DataTemplate>
                """
            );

            using var scope = new WindowScope(actions);
            var first = Assert.IsType<ContentPresenter>(
                actions.ItemContainerGenerator.ContainerFromIndex(0)
            );
            var firstButton = Assert.IsType<NativeButton>(VisualTreeHelper.GetChild(first, 0));
            Assert.Equal("Save", firstButton.Content);

            source.Add(new ActionModel("Retry"));
            scope.UpdateLayout();

            Assert.Equal(3, actions.Items.Count);
            var added = Assert.IsType<ContentPresenter>(
                actions.ItemContainerGenerator.ContainerFromIndex(2)
            );
            var addedButton = Assert.IsType<NativeButton>(VisualTreeHelper.GetChild(added, 0));
            Assert.Equal("Retry", addedButton.Content);
        });
    }

    [Fact]
    public void DefaultPanel_ForwardsLayoutPropertiesAndStretchesIncompleteRows()
    {
        WpfTestThread.Run(() =>
        {
            var actions = CreateStyledActions();
            for (var index = 0; index < 4; index++)
            {
                actions.Items.Add(new Border { Height = 32D });
            }

            using var scope = new WindowScope(actions);
            var panel = Assert.IsType<AdaptiveUniformGrid>(FindVisualDescendant<AdaptiveUniformGrid>(actions));

            Assert.Equal(3, panel.Columns);
            Assert.Equal(8D, panel.ColumnSpacing);
            Assert.Equal(8D, panel.RowSpacing);
            Assert.True(panel.IsRowStretchEnabled);

            var firstWidth = ((Border)actions.Items[0]).ActualWidth;
            var finalWidth = ((Border)actions.Items[3]).ActualWidth;
            Assert.True(finalWidth > firstWidth * 2D);

            actions.Columns = 2;
            actions.ColumnSpacing = 12D;
            actions.RowSpacing = 6D;
            actions.IsRowStretchEnabled = false;
            scope.UpdateLayout();

            Assert.Equal(2, panel.Columns);
            Assert.Equal(12D, panel.ColumnSpacing);
            Assert.Equal(6D, panel.RowSpacing);
            Assert.False(panel.IsRowStretchEnabled);
        });
    }

    private static ContentDialogActionBar CreateStyledActions()
    {
        var resources = new ResourceDictionary { Source = CreateResourceUri() };
        return new ContentDialogActionBar
        {
            Style = Assert.IsType<Style>(resources[typeof(ContentDialogActionBar)]),
        };
    }

    private static T? FindVisualDescendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                return match;
            }

            if (FindVisualDescendant<T>(child) is { } descendant)
            {
                return descendant;
            }
        }

        return null;
    }

    private static Uri CreateResourceUri()
    {
        RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);

        return System.IO.Packaging.PackUriHelper.Create(
            new Uri("application:///", UriKind.Absolute),
            new Uri("/Wpf.Zen.Ui;component/Themes/Generic.xaml", UriKind.Relative)
        );
    }

    private sealed record ActionModel(string Label);

    private sealed class WindowScope : IDisposable
    {
        private readonly Window _window;

        public WindowScope(ContentDialogActionBar actions)
        {
            _window = new Window
            {
                Content = actions,
                Width = 360D,
                Height = 180D,
                Left = -10000D,
                Top = -10000D,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
            };
            _window.Show();
            UpdateLayout();
        }

        public void UpdateLayout()
        {
            _window.UpdateLayout();
        }

        public void Dispose()
        {
            _window.Close();
        }
    }
}
