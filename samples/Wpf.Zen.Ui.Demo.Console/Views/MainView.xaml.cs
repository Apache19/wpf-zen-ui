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

        Application.Current.MainWindow = this;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.T)
        {
            ThemeUtilities.ChangeTheme();
        }

        /* todo 实现自定义强调色功能后再取消注释
        if (e.Key == System.Windows.Input.Key.C)
        {
            var rnd = new Random();
            var randomColor = Color.FromRgb(
                (byte)rnd.Next(256),
                (byte)rnd.Next(256),
                (byte)rnd.Next(256)
            );

            ApplicationAccentColorManager.Apply(randomColor, ApplicationThemeManager.GetAppTheme());

            ApplicationTheme current = ApplicationThemeManager.GetAppTheme();
            ApplicationTheme applicationTheme =
                ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Light
                    ? ApplicationTheme.Dark
                    : ApplicationTheme.Light;

            ApplicationThemeManager.Apply(applicationTheme, updateAccent: false);
            ApplicationThemeManager.Apply(current,          updateAccent: false);
        }
        */
    }
}