// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls;

public class ToolBarTests
{
    [Fact]
    public void RadioButtonStyle_ShowsIndicatorForCheckedDefaultOrNamedGroup()
    {
        WpfTestThread.Run(
            () =>
            {
                var resources = new ResourceDictionary { Source = CreateToolBarResourceUri() };
                var radioButton = new RadioButton
                {
                    DataContext = new RadioButton { GroupName = "data-context-group" },
                    IsChecked = true,
                    Style = Assert.IsType<Style>(resources[ToolBar.RadioButtonStyleKey]),
                };

                Assert.True(radioButton.ApplyTemplate());

                var indicator = Assert.IsType<Border>(
                    radioButton.Template.FindName("radioIndicator", radioButton)
                );

                Assert.Equal(Visibility.Visible, indicator.Visibility);

                radioButton.GroupName = "editor-mode";

                Assert.Equal(Visibility.Visible, indicator.Visibility);

                radioButton.IsChecked = false;

                Assert.Equal(Visibility.Collapsed, indicator.Visibility);

                radioButton.IsChecked = null;

                Assert.Equal(Visibility.Collapsed, indicator.Visibility);

                radioButton.GroupName = string.Empty;
                radioButton.IsChecked = true;

                Assert.Equal(Visibility.Visible, indicator.Visibility);
            }
        );
    }

    [Fact]
    public void ToggleButtonStyle_HidesRadioIndicator()
    {
        WpfTestThread.Run(
            () =>
            {
                var resources = new ResourceDictionary { Source = CreateToolBarResourceUri() };
                var toggleButton = new System.Windows.Controls.Primitives.ToggleButton
                {
                    IsChecked = true,
                    Style = Assert.IsType<Style>(resources[ToolBar.ToggleButtonStyleKey]),
                };

                Assert.True(toggleButton.ApplyTemplate());

                var indicator = Assert.IsType<Border>(
                    toggleButton.Template.FindName("radioIndicator", toggleButton)
                );

                Assert.Equal(Visibility.Collapsed, indicator.Visibility);
            }
        );
    }

    private static Uri CreateToolBarResourceUri()
    {
        RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);

        return System.IO.Packaging.PackUriHelper.Create(
            new Uri("application:///", UriKind.Absolute),
            new Uri("/Wpf.Zen.Ui;component/Controls/ToolBar/ToolBar.xaml", UriKind.Relative)
        );
    }
}
