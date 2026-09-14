// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Controls;
using NSubstitute;
using Wpf.Zen.Ui.VirtualInput.Controls;
using Wpf.Zen.Ui.VirtualInput.Input;
using Wpf.Zen.Ui.VirtualInput.InputMethods;

namespace Wpf.Zen.Ui.UnitTests.VirtualInput;

/// <summary>
/// Verifies language/profile presentation while preserving each input method's command identity.
/// </summary>
public class SoftKeyboardInputMethodMenuTests
{
    /// <summary>
    /// Two profiles in one language remain separate choices with two-line accessible headers.
    /// </summary>
    [Fact]
    public Task SameLanguageProfiles_KeepSeparateTwoLineMenuItems()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var first = new SoftKeyboardInputMethod(Guid.NewGuid(), Guid.NewGuid(), 0x0804,
                "微软拼音", true);
            var second = new SoftKeyboardInputMethod(Guid.NewGuid(), Guid.NewGuid(), 0x0804,
                "小狼毫", false);
            var session = Substitute.For<ISoftKeyboardInputMethodSession>();
            session.GetSnapshot().Returns(new SoftKeyboardInputMethodSnapshot([first, second], first));
            var source = Substitute.For<ISoftKeyboardInputMethodSource>();
            source.CreateSession().Returns(session);
            scope.Keyboard.InputMethodSource = source;
            await scope.DrainAsync();
            scope.Keyboard.ShowInputMethodMenu();
            var menu = (ContextMenu)typeof(SoftKeyboard)
                .GetField("_inputMethodMenu", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(scope.Keyboard)!;
            Assert.True(menu.IsOpen);
            Assert.Equal(2, menu.Items.Count);
            foreach (var item in menu.Items.Cast<MenuItem>())
            {
                var method = Assert.IsType<SoftKeyboardInputMethod>(item.CommandParameter);
                var header = Assert.IsType<StackPanel>(item.Header);
                Assert.Equal(2, header.Children.Count);
                Assert.Equal(method.LanguageDisplayName, Assert.IsType<TextBlock>(header.Children[0]).Text);
                Assert.Equal(method.DisplayName, Assert.IsType<TextBlock>(header.Children[1]).Text);
                Assert.Equal($"{method.LanguageDisplayName}, {method.DisplayName}",
                    AutomationProperties.GetName(item));
                Assert.Equal(method.IsActive, item.IsChecked);
            }

            Assert.Same(first, ((MenuItem)menu.Items[0]).CommandParameter);
            Assert.Same(second, ((MenuItem)menu.Items[1]).CommandParameter);
            menu.IsOpen = false;
        });
    }
}
