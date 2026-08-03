// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;

namespace Wpf.Zen.Ui.Demo.Console;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Debug.WriteLine("Args: " + string.Join(", ", args));

        if (Application.Current is null)
        {
            System.Console.WriteLine("Application.Current is null.");
        }

        ThemeManager.Initialize();

        try
        {
            _ = new Views.SimpleView().ShowDialog();
            _ = new Views.MainView().ShowDialog();
        }
        catch (Exception ex)
        {
            System.Console.WriteLine(ex);
            Thread.Sleep(10000);

            throw;
        }
    }
}