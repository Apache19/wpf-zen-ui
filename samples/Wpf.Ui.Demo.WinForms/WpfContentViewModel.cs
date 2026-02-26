// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Wpf.Ui.Controls;

namespace Wpf.Ui.Demo.WinForms;

public class WpfContentViewModel : INotifyPropertyChanged
{
    private ObservableCollection<object> _menuItems;
    private ObservableCollection<object> _footerMenuItems;

    public ObservableCollection<object> MenuItems
    {
        get => _menuItems;
        set
        {
            _menuItems = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<object> FooterMenuItems
    {
        get => _footerMenuItems;
        set
        {
            _footerMenuItems = value;
            OnPropertyChanged();
        }
    }

    public WpfContentViewModel()
    {
        MenuItems = new ObservableCollection<object>
        {
            new NavigationViewItem("Home", SymbolRegular.Home24, null),
            new NavigationViewItem("Settings", SymbolRegular.Settings24, null),
        };

        FooterMenuItems = new ObservableCollection<object>
        {
            new NavigationViewItem("About", SymbolRegular.Info24, null),
        };
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
