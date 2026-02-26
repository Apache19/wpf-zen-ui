// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System;
using System.Windows;
using System.Windows.Forms;
using Wpf.Ui.Appearance;

namespace Wpf.Ui.Demo.WinForms;

/// <summary>
/// Initializes a new instance of the <see cref="Form1"/> class.
/// Sets up the form and hosts a WPF user control within the Windows Forms environment.
/// </summary>
public partial class Form1 : Form
{
    public Form1()
    {
        InitializeComponent();

        elementHost1.Child = new WpfContent();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        // WeakEventManager<ThemeManager, ThemeChangedEventArgs>.AddHandler(
        //     ThemeManager.Instance,
        //     nameof(ThemeManager.Changed),
        //     ThemeChanged
        // );
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        // WeakEventManager<ThemeManager, ThemeChangedEventArgs>.RemoveHandler(
        //     ThemeManager.Instance,
        //     nameof(ThemeManager.Changed),
        //     ThemeChanged
        // );

        base.OnFormClosed(e);
    }

    private void ThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        ThemeManager.ApplyThemeToElement(
            elementHost1.HostContainer,
            e.AppliedTheme,
            ThemeManager.UseSystemAccentColor
        );
    }
}