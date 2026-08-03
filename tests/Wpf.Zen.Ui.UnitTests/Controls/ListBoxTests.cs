// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Input;
using NativeListBoxItem = System.Windows.Controls.ListBoxItem;
using UiListBox = Wpf.Zen.Ui.Controls.ListBox;
using UiListBoxItem = Wpf.Zen.Ui.Controls.ListBoxItem;
using UiListBoxItemDoubleClickEventArgs = Wpf.Zen.Ui.Controls.ListBoxItemDoubleClickEventArgs;
using UiListItemContainerStyleSelector = Wpf.Zen.Ui.Controls.ListItemContainerStyleSelector;

namespace Wpf.Zen.Ui.UnitTests.Controls;

public class ListBoxTests
{
    [Fact]
    public void ListItemContainerStyleSelector_ReevaluatesSurvivingListBoxContainersAfterCollectionChanges()
    {
        RunOnStaThread(static () =>
        {
            var items = new ObservableCollection<string> { "A", "B" };
            var listBox = new UiListBox
            {
                Height = 200,
                ItemContainerStyleSelector = new UiListItemContainerStyleSelector
                {
                    FirstItemStyle = CreateTaggedListBoxItemStyle("first"),
                    LastItemStyle = CreateTaggedListBoxItemStyle("last"),
                    OddItemStyle = CreateTaggedListBoxItemStyle("odd"),
                    SingleItemStyle = CreateTaggedListBoxItemStyle("single"),
                },
                ItemsSource = items,
                Width = 300,
            };

            Arrange(listBox);
            UiListBoxItem secondContainer = GetListBoxItem(listBox, 1);
            Assert.Equal("last", secondContainer.Tag);

            items.Add("C");
            listBox.UpdateLayout();

            Assert.Same(secondContainer, GetListBoxItem(listBox, 1));
            Assert.Equal("odd", secondContainer.Tag);
            Assert.Equal("last", GetListBoxItem(listBox, 2).Tag);

            items.RemoveAt(0);
            listBox.UpdateLayout();

            Assert.Same(secondContainer, GetListBoxItem(listBox, 0));
            Assert.Equal("first", secondContainer.Tag);

            items.RemoveAt(1);
            listBox.UpdateLayout();

            Assert.Equal("single", GetListBoxItem(listBox, 0).Tag);
        });
    }

    [Fact]
    public void ListBox_GeneratesFluentContainersAndAcceptsNativeContainers()
    {
        RunOnStaThread(static () =>
        {
            var listBox = new TestListBox();

            Assert.IsType<UiListBoxItem>(listBox.CreateContainer());
            Assert.True(listBox.IsOwnContainer(new NativeListBoxItem()));
            Assert.True(listBox.IsOwnContainer(new UiListBoxItem()));
            Assert.False(listBox.IsOwnContainer("data item"));
        });
    }

    [Fact]
    public void ItemDoubleClick_RaisesItemEventAndPassesItemToCommand()
    {
        RunOnStaThread(static () =>
        {
            const string item = "activated item";
            var command = new RecordingCommand();
            var listBox = new TestListBox { ItemDoubleClickCommand = command };
            var container = new UiListBoxItem();
            UiListBoxItemDoubleClickEventArgs? receivedArgs = null;
            listBox.ItemDoubleClick += (_, args) => receivedArgs = args;

            listBox.InvokeItemDoubleClick(item, container, 0, MouseButton.Left);

            Assert.NotNull(receivedArgs);
            Assert.Same(item, receivedArgs.Item);
            Assert.Same(container, receivedArgs.Container);
            Assert.Equal(0, receivedArgs.Index);
            Assert.Equal(MouseButton.Left, receivedArgs.ChangedButton);
            Assert.Same(item, command.Parameter);
            Assert.Equal(1, command.ExecutionCount);
        });
    }

    [Fact]
    public void ItemDoubleClick_DoesNotExecuteDisabledCommand()
    {
        RunOnStaThread(static () =>
        {
            var command = new RecordingCommand { IsEnabled = false };
            var listBox = new TestListBox { ItemDoubleClickCommand = command };
            var container = new UiListBoxItem();

            listBox.InvokeItemDoubleClick("item", container, 0, MouseButton.Right);

            Assert.Equal(0, command.ExecutionCount);
        });
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

    private static void Arrange(FrameworkElement element)
    {
        // Theme styles are resolved only after a control enters a logical tree. A lightweight host
        // exercises the same container generation path as application markup without showing a window.
        var host = new Grid();
        host.Children.Add(element);
        host.Measure(new Size(element.Width, element.Height));
        host.Arrange(new Rect(0, 0, element.Width, element.Height));
        host.UpdateLayout();
    }

    private static UiListBoxItem GetListBoxItem(UiListBox listBox, int index) =>
        Assert.IsType<UiListBoxItem>(listBox.ItemContainerGenerator.ContainerFromIndex(index));

    private static Style CreateTaggedListBoxItemStyle(string tag)
    {
        var style = new Style(typeof(UiListBoxItem));
        style.Setters.Add(new Setter(FrameworkElement.TagProperty, tag));
        return style;
    }

    private sealed class TestListBox : UiListBox
    {
        public DependencyObject CreateContainer() => GetContainerForItemOverride();

        public bool IsOwnContainer(object item) => IsItemItsOwnContainerOverride(item);

        public void InvokeItemDoubleClick(
            object item,
            NativeListBoxItem container,
            int index,
            MouseButton changedButton
        )
        {
            OnItemDoubleClick(item, container, index, changedButton);
        }
    }

    private sealed class RecordingCommand : ICommand
    {
        public bool IsEnabled { get; set; } = true;

        public int ExecutionCount { get; private set; }

        public object? Parameter { get; private set; }

        event EventHandler? ICommand.CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => IsEnabled;

        public void Execute(object? parameter)
        {
            ExecutionCount++;
            Parameter = parameter;
        }
    }
}