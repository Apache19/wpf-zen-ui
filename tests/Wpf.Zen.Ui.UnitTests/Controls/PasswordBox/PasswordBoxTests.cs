// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using UiPasswordBox = Wpf.Zen.Ui.Controls.PasswordBox;

namespace Wpf.Zen.Ui.UnitTests.Controls;

public class PasswordBoxTests
{
    [Fact]
    public void HiddenTextInput_UpdatesPasswordAndMasksText()
    {
        RunOnStaThread(
            () =>
            {
                var passwordBox = new UiPasswordBox();

                passwordBox.Text = "secret";

                Assert.Equal("secret", passwordBox.Password);
                Assert.Equal(Mask(passwordBox), passwordBox.Text);
            }
        );
    }

    [Fact]
    public void HiddenSelectionReplacement_UpdatesPasswordAndMasksText()
    {
        RunOnStaThread(
            () =>
            {
                var passwordBox = new UiPasswordBox
                {
                    Password = "abcd",
                };

                passwordBox.Select(1, 2);
                passwordBox.SelectedText = "XY";

                Assert.Equal("aXYd", passwordBox.Password);
                Assert.Equal(Mask(passwordBox), passwordBox.Text);
                Assert.Equal(3, passwordBox.CaretIndex);
            }
        );
    }

    [Fact]
    public void RevealedTextInput_UpdatesPasswordWithoutMaskingText()
    {
        RunOnStaThread(
            () =>
            {
                var passwordBox = new UiPasswordBox
                {
                    Password = "old",
                };

                ClickTemplateButton(passwordBox, "PART_RevealButton");

                passwordBox.Text = "new-password";

                Assert.True(passwordBox.IsPasswordRevealed);
                Assert.Equal("new-password", passwordBox.Password);
                Assert.Equal("new-password", passwordBox.Text);
            }
        );
    }

    [Fact]
    public void RevealButtonClick_TogglesBetweenPlainTextAndMask()
    {
        RunOnStaThread(
            () =>
            {
                var passwordBox = new UiPasswordBox
                {
                    Password = "secret",
                };

                ClickTemplateButton(passwordBox, "PART_RevealButton");

                Assert.True(passwordBox.IsPasswordRevealed);
                Assert.Equal("secret", passwordBox.Text);

                ClickTemplateButton(passwordBox, "PART_RevealButton");

                Assert.False(passwordBox.IsPasswordRevealed);
                Assert.Equal(Mask(passwordBox), passwordBox.Text);
            }
        );
    }

    [Fact]
    public void RevealButtonDisabled_MasksRevealedPasswordAndBlocksButtonClick()
    {
        RunOnStaThread(
            () =>
            {
                var passwordBox = new UiPasswordBox
                {
                    Password = "secret",
                };

                ClickTemplateButton(passwordBox, "PART_RevealButton");
                passwordBox.RevealButtonEnabled = false;

                Assert.False(passwordBox.IsPasswordRevealed);
                Assert.Equal(Mask(passwordBox), passwordBox.Text);

                ClickTemplateButton(passwordBox, "PART_RevealButton");

                Assert.False(passwordBox.IsPasswordRevealed);
                Assert.Equal(Mask(passwordBox), passwordBox.Text);
            }
        );
    }

    [Fact]
    public void DirectRevealPropertySet_DoesNotReveal_WhenRevealButtonDisabled()
    {
        RunOnStaThread(
            () =>
            {
                var passwordBox = new UiPasswordBox
                {
                    Password = "secret",
                    RevealButtonEnabled = false,
                };

                passwordBox.SetCurrentValue(UiPasswordBox.IsPasswordRevealedProperty, true);

                Assert.False(passwordBox.IsPasswordRevealed);
                Assert.Equal(Mask(passwordBox), passwordBox.Text);
            }
        );
    }

    [Fact]
    public void DisablingControl_MasksRevealedPasswordAndBlocksRevealButtonClick()
    {
        RunOnStaThread(
            () =>
            {
                var passwordBox = new UiPasswordBox
                {
                    Password = "secret",
                };

                ClickTemplateButton(passwordBox, "PART_RevealButton");
                passwordBox.IsEnabled = false;

                Assert.False(passwordBox.IsPasswordRevealed);
                Assert.Equal(Mask(passwordBox), passwordBox.Text);

                ClickTemplateButton(passwordBox, "PART_RevealButton");

                Assert.False(passwordBox.IsPasswordRevealed);
                Assert.Equal(Mask(passwordBox), passwordBox.Text);
            }
        );
    }

    [Fact]
    public void ClearButtonClick_ClearsPassword_WhenEditable()
    {
        RunOnStaThread(
            () =>
            {
                var passwordBox = new UiPasswordBox
                {
                    Password = "secret",
                };

                ClickTemplateButton(passwordBox, "PART_ClearButton");

                Assert.Equal(string.Empty, passwordBox.Password);
                Assert.Equal(string.Empty, passwordBox.Text);
            }
        );
    }

    [Fact]
    public void ClearButtonClick_DoesNotChangePassword_WhenClearButtonDisabled()
    {
        RunOnStaThread(
            () =>
            {
                var passwordBox = new UiPasswordBox
                {
                    Password = "secret",
                    ClearButtonEnabled = false,
                };

                ClickTemplateButton(passwordBox, "PART_ClearButton");

                Assert.Equal("secret", passwordBox.Password);
                Assert.Equal(Mask(passwordBox), passwordBox.Text);
            }
        );
    }

    [Fact]
    public void ClearButtonClick_DoesNotChangePassword_WhenReadOnly()
    {
        RunOnStaThread(
            () =>
            {
                var passwordBox = new UiPasswordBox
                {
                    Password = "secret",
                    IsReadOnly = true,
                };

                ClickTemplateButton(passwordBox, "PART_ClearButton");

                Assert.Equal("secret", passwordBox.Password);
                Assert.Equal(Mask(passwordBox), passwordBox.Text);
            }
        );
    }

    [Fact]
    public void ClearButtonClick_DoesNotChangePassword_WhenDisabled()
    {
        RunOnStaThread(
            () =>
            {
                var passwordBox = new UiPasswordBox
                {
                    Password = "secret",
                    IsEnabled = false,
                };

                ClickTemplateButton(passwordBox, "PART_ClearButton");

                Assert.Equal("secret", passwordBox.Password);
                Assert.Equal(Mask(passwordBox), passwordBox.Text);
            }
        );
    }

    [Fact]
    public void PasswordCharChange_RemasksTextAndPreservesSelection()
    {
        RunOnStaThread(
            () =>
            {
                var passwordBox = new UiPasswordBox
                {
                    Password = "secret",
                };
                passwordBox.Select(1, 3);

                passwordBox.PasswordChar = '#';

                Assert.Equal("######", passwordBox.Text);
                Assert.Equal(1, passwordBox.SelectionStart);
                Assert.Equal(3, passwordBox.SelectionLength);
            }
        );
    }

    [Fact]
    public void CopyAndCutCommands_AreDisabled()
    {
        RunOnStaThread(
            () =>
            {
                var passwordBox = new UiPasswordBox
                {
                    Password = "secret",
                };

                passwordBox.SelectAll();

                Assert.False(ApplicationCommands.Copy.CanExecute(null, passwordBox));
                Assert.False(ApplicationCommands.Cut.CanExecute(null, passwordBox));
            }
        );
    }

    [Fact]
    public void PasswordChanged_RaisesOnceForHiddenInput()
    {
        RunOnStaThread(
            () =>
            {
                var passwordBox = new UiPasswordBox();
                var eventCount = 0;
                passwordBox.PasswordChanged += (_, _) => eventCount++;

                passwordBox.Text = "secret";

                Assert.Equal("secret", passwordBox.Password);
                Assert.Equal(1, eventCount);
            }
        );
    }

    [Fact]
    public void ReplacingTemplate_DetachesPreviousRevealButtonImmediately()
    {
        RunOnStaThread(() =>
        {
            var passwordBox = new UiPasswordBox
            {
                Password = "secret",
                Template = CreateTemplate("PART_RevealButton"),
            };

            Assert.True(passwordBox.ApplyTemplate());
            var oldButton = Assert.IsAssignableFrom<ButtonBase>(
                passwordBox.Template.FindName("PART_RevealButton", passwordBox)
            );

            passwordBox.Template = CreateTemplate("ReplacementRoot");
            oldButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, oldButton));

            Assert.False(passwordBox.IsPasswordRevealed);
            Assert.Equal(Mask(passwordBox), passwordBox.Text);
        });
    }

    private static void ClickTemplateButton(UiPasswordBox passwordBox, string partName)
    {
        if (passwordBox.Template?.FindName(partName, passwordBox) is not ButtonBase)
        {
            passwordBox.Template = CreateTemplate(partName);
            Assert.True(passwordBox.ApplyTemplate());
        }

        var button = Assert.IsAssignableFrom<ButtonBase>(
            passwordBox.Template.FindName(partName, passwordBox)
        );
        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
    }

    private static ControlTemplate CreateTemplate(string partName)
    {
#pragma warning disable CS0618 // FrameworkElementFactory is sufficient for template-contract tests.
        var root = new FrameworkElementFactory(typeof(Button)) { Name = partName };
        return new ControlTemplate(typeof(UiPasswordBox)) { VisualTree = root };
#pragma warning restore CS0618
    }

    private static string Mask(UiPasswordBox passwordBox)
    {
        return new string(passwordBox.PasswordChar, passwordBox.Password.Length);
    }

    private static void RunOnStaThread(Action action)
    {
        WpfTestThread.Run(action);
    }
}
