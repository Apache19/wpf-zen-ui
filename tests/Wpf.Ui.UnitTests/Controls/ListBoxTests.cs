// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Input;
using NativeListBoxItem = System.Windows.Controls.ListBoxItem;
using UiListBox = Wpf.Ui.Controls.ListBox;
using UiListBoxItem = Wpf.Ui.Controls.ListBoxItem;
using UiListBoxItemDoubleClickEventArgs = Wpf.Ui.Controls.ListBoxItemDoubleClickEventArgs;

namespace Wpf.Ui.UnitTests.Controls;

public class ListBoxTests
{
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
