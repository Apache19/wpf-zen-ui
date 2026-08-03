// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.ViewModels.Windows;
using Wpf.Zen.Ui.Gallery.Views.Pages.Samples;

namespace Wpf.Zen.Ui.Gallery.Views.Windows;

public partial class SandboxWindow
{
    public SandboxWindowViewModel ViewModel { get; init; }

    public SandboxWindow(SandboxWindowViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();

        MyTestNavigationView.Loaded += (sender, args) =>
        {
            var menuItems = new ObservableCollection<object>()
            {
                new NavigationItem("Home", SymbolRegular.Home24, typeof(SamplePage1)),
            };

            MyTestNavigationView.SetCurrentValue(
                NavigationView.MenuItemsSourceProperty,
                menuItems
            );

            var configurationBasedLogic = true;

            if (configurationBasedLogic)
            {
                menuItems.Add(new NavigationItem("Test", SymbolRegular.Home24, typeof(SamplePage2)));
            }
        };
    }

    private void OnAutoSuggestBoxTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        Debug.WriteLine(
            $"OnAutoSuggestBoxTextChanged: {sender.Text} (ViewModel.AutoSuggestBoxText: {ViewModel.AutoSuggestBoxText})"
        );
    }
}
