// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Threading;
using Wpf.Ui.Appearance;
using Xunit;
using Forms = System.Windows.Forms;

namespace Wpf.Ui.WinFormsHostTests;

/// <summary>
/// Exercises theme resource lookup through a real WinForms <see cref="ElementHost"/> rather than constructing an
/// artificial WPF state that a supported host would never use.
/// </summary>
public class ElementHostThemeTests
{
    /// <summary>
    /// Verifies that ThemeManager supplies the Application resource scope absent from WinForms and that native WPF
    /// controls inside a navigated page resolve the implicit styles placed in that scope.
    /// </summary>
    [Fact]
    public void DarkTheme_ReachesNativeControlsInsideFrame()
    {
        RunOnStaThread(() =>
        {
            EnsureWindowsDirectoryEnvironment();
            Assert.Null(Application.Current);

            var textBox = new TextBox { Text = "Text" };
            var comboBox = new ComboBox { ItemsSource = new[] { "Item" }, SelectedIndex = 0 };
            var page = new Page
            {
                Content = new StackPanel { Children = { textBox, comboBox } },
            };
            var frame = new Frame();
            frame.Navigate(page);

            var root = new Grid();
            Theme.SetApplyToRootVisual(root, true);
            root.Children.Add(frame);

            ThemeManager.Configure(
                UiThemeMode.Dark,
                AccentColorSource.Default,
                enableNativeControlStyling: true
            );

            using var form = new Forms.Form
            {
                ShowInTaskbar = false,
                StartPosition = Forms.FormStartPosition.Manual,
                Location = new System.Drawing.Point(-10_000, -10_000),
                Size = new System.Drawing.Size(320, 240),
            };
            using var host = new ElementHost { Dock = Forms.DockStyle.Fill, Child = root };
            form.Controls.Add(host);

            try
            {
                form.Show();
                Forms.Application.DoEvents();
                DrainDispatcher();

                Application application = Assert.IsType<Application>(Application.Current);
                Style textBoxStyle = Assert.IsType<Style>(application.TryFindResource(typeof(TextBox)));
                Style comboBoxStyle = Assert.IsType<Style>(application.TryFindResource(typeof(ComboBox)));

                // Frame navigation creates a separate content boundary. Matching the effective implicit styles to
                // Application.Resources proves that lookup crossed both that boundary and the ElementHost boundary;
                // merely checking that the controls rendered would allow WPF's light system styles to pass.
                Assert.Same(textBoxStyle, textBox.Style);
                Assert.Same(comboBoxStyle, comboBox.Style);
                Assert.True(textBox.OverridesDefaultStyle);
                Assert.True(comboBox.OverridesDefaultStyle);
                Assert.Equal(ApplicationTheme.Dark, ThemeManager.CurrentTheme);
            }
            finally
            {
                form.Close();
                Forms.Application.DoEvents();
                DrainDispatcher();
            }
        });
    }

    private static void DrainDispatcher() =>
        Dispatcher.CurrentDispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);

    private static void EnsureWindowsDirectoryEnvironment()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("windir")))
        {
            // WPF's font cache reads the lower-case Win32 environment name. Some test runners preserve only
            // SystemRoot, unlike a normally launched WinForms process, so reproduce the real host environment
            // before constructing the first WPF text control.
            Environment.SetEnvironmentVariable(
                "windir",
                Environment.GetEnvironmentVariable("SystemRoot")
                    ?? Environment.GetEnvironmentVariable("windir", EnvironmentVariableTarget.Machine)
            );
        }
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
}
