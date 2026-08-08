// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Data;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls.Menu;

public class RadioMenuItemTests
{
    [Fact]
    public void IsCheckable_IsAlwaysTrue()
    {
        RunOnStaThread(
            () =>
            {
                var item = new RadioMenuItem();

                item.IsCheckable = false;

                Assert.True(item.IsCheckable);
            }
        );
    }

    [Fact]
    public void IsChecked_UnchecksSibling_WhenGroupNameIsEmpty()
    {
        RunOnStaThread(
            () =>
            {
                var parent = new MenuItem();
                var first = new RadioMenuItem();
                var second = new RadioMenuItem();

                parent.Items.Add(first);
                parent.Items.Add(second);

                first.IsChecked = true;
                second.IsChecked = true;

                Assert.False(first.IsChecked);
                Assert.True(second.IsChecked);
            }
        );
    }

    [Fact]
    public void IsChecked_UnchecksNamedGroupPeer_InSamePresentationRoot()
    {
        RunOnStaThread(
            () =>
            {
                var groupName = Guid.NewGuid().ToString("N");
                var first = new RadioMenuItem { GroupName = groupName };
                var second = new RadioMenuItem { GroupName = groupName };
                var firstMenu = new System.Windows.Controls.Menu();
                var secondMenu = new System.Windows.Controls.Menu();
                var panel = new StackPanel();
                var window = new Window
                {
                    Content = panel,
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None,
                    Width = 1d,
                    Height = 1d,
                };

                firstMenu.Items.Add(first);
                secondMenu.Items.Add(second);
                panel.Children.Add(firstMenu);
                panel.Children.Add(secondMenu);

                try
                {
                    window.Show();

                    first.IsChecked = true;
                    second.IsChecked = true;

                    Assert.False(first.IsChecked);
                    Assert.True(second.IsChecked);
                }
                finally
                {
                    window.Close();
                }
            }
        );
    }

    [Fact]
    public void OnClick_DoesNotUncheckAlreadyCheckedItem()
    {
        RunOnStaThread(
            () =>
            {
                var item = new TestRadioMenuItem { IsChecked = true };

                item.InvokeClick();

                Assert.True(item.IsChecked);
            }
        );
    }

    [Fact]
    public void UncheckingSibling_PreservesIsCheckedBinding()
    {
        RunOnStaThread(
            () =>
            {
                var source = new RadioMenuItemState { First = true };
                var parent = new MenuItem();
                var first = new RadioMenuItem();
                var second = new RadioMenuItem();

                first.SetBinding(
                    MenuItem.IsCheckedProperty,
                    new Binding(nameof(RadioMenuItemState.First)) { Source = source, Mode = BindingMode.TwoWay }
                );

                parent.Items.Add(first);
                parent.Items.Add(second);

                second.IsChecked = true;

                Assert.False(first.IsChecked);
                Assert.False(source.First);
                Assert.NotNull(BindingOperations.GetBindingExpression(first, MenuItem.IsCheckedProperty));
            }
        );
    }

    private sealed class TestRadioMenuItem : RadioMenuItem
    {
        public void InvokeClick()
        {
            OnClick();
        }
    }

    private sealed class RadioMenuItemState : INotifyPropertyChanged
    {
        private bool _first;

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool First
        {
            get => _first;
            set
            {
                if (_first == value)
                {
                    return;
                }

                _first = value;
                OnPropertyChanged();
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    private static void RunOnStaThread(Action action)
    {
        WpfTestThread.Run(action);
    }
}
