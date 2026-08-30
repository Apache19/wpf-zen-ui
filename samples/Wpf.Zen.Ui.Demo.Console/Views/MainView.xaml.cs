// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;
using Wpf.Zen.Ui.Demo.Console.Utilities;
using Wpf.Zen.Ui.Demo.Console.Views.Pages;

namespace Wpf.Zen.Ui.Demo.Console.Views;

public partial class MainView
{
    public MainView()
    {
        DataContext = this;

        InitializeComponent();

        Loaded += (_, _) => RootNavigation.Navigate(typeof(DashboardPage));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.T)
        {
            ThemeUtilities.ChangeTheme();
        }
    }
}
