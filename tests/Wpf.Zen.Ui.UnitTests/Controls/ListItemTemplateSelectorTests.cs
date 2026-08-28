// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Media;
using UiListItemTemplateSelector = Wpf.Zen.Ui.Controls.ListItemTemplateSelector;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.UnitTests.Controls;

public class ListItemTemplateSelectorTests
{
    [Fact]
    public void SelectTemplate_AppliesSpecificPositionalRulesAndSupportsRepeatedItems()
    {
        WpfTestThread.Run(
            static () =>
            {
                var repeatedItem = new object();
                var normalTemplate = new DataTemplate();
                var firstItemTemplate = new DataTemplate();
                var lastItemTemplate = new DataTemplate();
                var singleItemTemplate = new DataTemplate();
                var evenItemTemplate = new DataTemplate();
                var oddItemTemplate = new DataTemplate();
                var selector = new UiListItemTemplateSelector
                {
                    DefaultTemplate = normalTemplate,
                    FirstItemTemplate = firstItemTemplate,
                    LastItemTemplate = lastItemTemplate,
                    SingleItemTemplate = singleItemTemplate,
                    EvenItemTemplate = evenItemTemplate,
                    OddItemTemplate = oddItemTemplate,
                };
                var itemsControl = new ItemsControl
                {
                    Height = 100,
                    ItemTemplateSelector = selector,
                    Width = 100,
                };
                itemsControl.Items.Add(repeatedItem);
                itemsControl.Items.Add(repeatedItem);
                itemsControl.Items.Add(repeatedItem);
                itemsControl.Items.Add(repeatedItem);

                Arrange(itemsControl);

                Assert.Same(
                    firstItemTemplate,
                    SelectTemplate(selector, itemsControl, repeatedItem, 0)
                );
                Assert.Same(
                    oddItemTemplate,
                    SelectTemplate(selector, itemsControl, repeatedItem, 1)
                );
                Assert.Same(
                    evenItemTemplate,
                    SelectTemplate(selector, itemsControl, repeatedItem, 2)
                );
                Assert.Same(
                    lastItemTemplate,
                    SelectTemplate(selector, itemsControl, repeatedItem, 3)
                );

                itemsControl.Items.Clear();
                itemsControl.Items.Add(repeatedItem);
                Arrange(itemsControl);

                Assert.Same(
                    singleItemTemplate,
                    SelectTemplate(selector, itemsControl, repeatedItem, 0)
                );
            }
        );
    }

    [Fact]
    public void SelectTemplate_FallsThroughUnsetSpecificRules()
    {
        WpfTestThread.Run(
            static () =>
            {
                var normalTemplate = new DataTemplate();
                var evenItemTemplate = new DataTemplate();
                var oddItemTemplate = new DataTemplate();
                var selector = new UiListItemTemplateSelector
                {
                    DefaultTemplate = normalTemplate,
                    EvenItemTemplate = evenItemTemplate,
                    OddItemTemplate = oddItemTemplate,
                };
                var itemsControl = new ItemsControl
                {
                    ItemTemplateSelector = selector,
                    Height = 100,
                    Width = 100,
                };
                itemsControl.Items.Add(new object());
                itemsControl.Items.Add(new object());

                Arrange(itemsControl);

                Assert.Same(
                    evenItemTemplate,
                    SelectTemplate(selector, itemsControl, itemsControl.Items[0], 0)
                );
                Assert.Same(
                    oddItemTemplate,
                    SelectTemplate(selector, itemsControl, itemsControl.Items[1], 1)
                );
            }
        );
    }

    [Fact]
    public void Selector_ReevaluatesSurvivingContainersAfterCollectionChanges()
    {
        WpfTestThread.Run(
            static () =>
            {
                var items = new ObservableCollection<string> { "A", "B", "C" };
                var selector = new UiListItemTemplateSelector
                {
                    DefaultTemplate = CreateTaggedTemplate("normal"),
                    FirstItemTemplate = CreateTaggedTemplate("first"),
                    LastItemTemplate = CreateTaggedTemplate("last"),
                    SingleItemTemplate = CreateTaggedTemplate("single"),
                    EvenItemTemplate = CreateTaggedTemplate("even"),
                    OddItemTemplate = CreateTaggedTemplate("odd"),
                };
                var itemsControl = new ItemsControl
                {
                    Height = 200,
                    ItemsSource = items,
                    ItemTemplateSelector = selector,
                    Width = 200,
                };

                Arrange(itemsControl);
                ContentPresenter originalFirst = GetContainer(itemsControl, 0);
                ContentPresenter originalLast = GetContainer(itemsControl, 2);
                Assert.Equal("first", GetTemplateTag(originalFirst));
                Assert.Equal("last", GetTemplateTag(originalLast));

                items.Insert(0, "New");
                Arrange(itemsControl);

                Assert.Same(originalFirst, GetContainer(itemsControl, 1));
                Assert.Equal("odd", GetTemplateTag(originalFirst));
                Assert.Same(originalLast, GetContainer(itemsControl, 3));
                Assert.Equal("last", GetTemplateTag(originalLast));

                items.RemoveAt(0);
                items.RemoveAt(0);
                items.RemoveAt(0);
                Arrange(itemsControl);

                Assert.Same(originalLast, GetContainer(itemsControl, 0));
                Assert.Equal("single", GetTemplateTag(originalLast));
            }
        );
    }

    [Fact]
    public void Selector_ReevaluatesContentControlContainersAfterCollectionChanges()
    {
        WpfTestThread.Run(
            static () =>
            {
                var items = new ObservableCollection<string> { "A", "B" };
                var selector = new UiListItemTemplateSelector
                {
                    DefaultTemplate = CreateTaggedTemplate("normal"),
                    FirstItemTemplate = CreateTaggedTemplate("first"),
                    LastItemTemplate = CreateTaggedTemplate("last"),
                    OddItemTemplate = CreateTaggedTemplate("odd"),
                };
                var listBox = new System.Windows.Controls.ListBox
                {
                    Height = 200,
                    ItemsSource = items,
                    ItemTemplateSelector = selector,
                    Width = 200,
                };

                Arrange(listBox);
                var originalFirst = Assert.IsType<System.Windows.Controls.ListBoxItem>(
                    listBox.ItemContainerGenerator.ContainerFromIndex(0)
                );
                Assert.Equal("first", FindTemplateTag(originalFirst));

                items.Insert(0, "New");
                Arrange(listBox);

                Assert.Same(
                    originalFirst,
                    listBox.ItemContainerGenerator.ContainerFromIndex(1)
                );
                Assert.Equal("odd", FindTemplateTag(originalFirst));
            }
        );
    }

    [Fact]
    public void SelectTemplate_WithoutOwningItemsControl_UsesDefaultTemplate()
    {
        WpfTestThread.Run(
            static () =>
            {
                var normalTemplate = new DataTemplate();
                var selector = new UiListItemTemplateSelector
                {
                    DefaultTemplate = normalTemplate,
                    LastItemTemplate = new DataTemplate(),
                };

                Assert.Same(
                    normalTemplate,
                    selector.SelectTemplate(new object(), new ContentPresenter())
                );
            }
        );
    }

    private static DataTemplate? SelectTemplate(
        UiListItemTemplateSelector selector,
        ItemsControl owner,
        object item,
        int index
    ) => selector.SelectTemplate(item, GetContainer(owner, index));

    private static ContentPresenter GetContainer(ItemsControl owner, int index) =>
        Assert.IsType<ContentPresenter>(owner.ItemContainerGenerator.ContainerFromIndex(index));

    private static DataTemplate CreateTaggedTemplate(string tag)
    {
#pragma warning disable CS0618 // FrameworkElementFactory is sufficient for small test-only templates.
        var root = new FrameworkElementFactory(typeof(Border));
#pragma warning restore CS0618
        root.SetValue(FrameworkElement.TagProperty, tag);
        return new DataTemplate { VisualTree = root };
    }

    private static string? GetTemplateTag(ContentPresenter presenter)
    {
        presenter.ApplyTemplate();
        return FindTemplateTag(presenter);
    }

    private static string? FindTemplateTag(DependencyObject parent)
    {
        if (parent is FrameworkElement { Tag: string tag })
        {
            return tag;
        }

        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            if (FindTemplateTag(VisualTreeHelper.GetChild(parent, index)) is { } descendantTag)
            {
                return descendantTag;
            }
        }

        return null;
    }

    private static void Arrange(FrameworkElement element)
    {
        FrameworkElement host;
        if (element.Parent is FrameworkElement existingHost)
        {
            host = existingHost;
        }
        else
        {
            var newHost = new Grid();
            newHost.Children.Add(element);
            host = newHost;
        }

        host.Measure(new Size(200, 200));
        host.Arrange(new Rect(0, 0, 200, 200));
        host.UpdateLayout();
    }
}
