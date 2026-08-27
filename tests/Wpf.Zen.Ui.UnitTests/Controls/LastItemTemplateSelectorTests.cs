// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using UiLastItemTemplateSelector = Wpf.Zen.Ui.Controls.LastItemTemplateSelector;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.UnitTests.Controls;

public class LastItemTemplateSelectorTests
{
    [Fact]
    public void SelectTemplate_UsesContainerPositionForRepeatedItems()
    {
        WpfTestThread.Run(
            static () =>
            {
                var repeatedItem = new object();
                var normalTemplate = new DataTemplate();
                var lastItemTemplate = new DataTemplate();
                var selector = new UiLastItemTemplateSelector
                {
                    NormalTemplate = normalTemplate,
                    LastItemTemplate = lastItemTemplate,
                };
                var itemsControl = new ItemsControl
                {
                    Height = 100,
                    ItemTemplateSelector = selector,
                    Width = 100,
                };
                itemsControl.Items.Add(repeatedItem);
                itemsControl.Items.Add(repeatedItem);

                var host = new Grid();
                host.Children.Add(itemsControl);
                host.Measure(new Size(100, 100));
                host.Arrange(new Rect(0, 0, 100, 100));
                host.UpdateLayout();

                DependencyObject firstContainer = Assert.IsType<ContentPresenter>(
                    itemsControl.ItemContainerGenerator.ContainerFromIndex(0)
                );
                DependencyObject lastContainer = Assert.IsType<ContentPresenter>(
                    itemsControl.ItemContainerGenerator.ContainerFromIndex(1)
                );

                Assert.Same(normalTemplate, selector.SelectTemplate(repeatedItem, firstContainer));
                Assert.Same(lastItemTemplate, selector.SelectTemplate(repeatedItem, lastContainer));
            }
        );
    }

    [Fact]
    public void SelectTemplate_WithoutOwningItemsControl_UsesNormalTemplate()
    {
        WpfTestThread.Run(
            static () =>
            {
                var normalTemplate = new DataTemplate();
                var selector = new UiLastItemTemplateSelector
                {
                    NormalTemplate = normalTemplate,
                    LastItemTemplate = new DataTemplate(),
                };

                Assert.Same(
                    normalTemplate,
                    selector.SelectTemplate(new object(), new ContentPresenter())
                );
            }
        );
    }
}
