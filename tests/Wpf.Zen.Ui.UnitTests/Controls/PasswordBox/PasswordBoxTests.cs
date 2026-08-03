// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.ExceptionServices;
using System.Threading;
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

                ExecuteTemplateCommand(passwordBox, "reveal");

                passwordBox.Text = "new-password";

                Assert.True(passwordBox.IsPasswordRevealed);
                Assert.Equal("new-password", passwordBox.Password);
                Assert.Equal("new-password", passwordBox.Text);
            }
        );
    }

    [Fact]
    public void RevealCommand_TogglesBetweenPlainTextAndMask()
    {
        RunOnStaThread(
            () =>
            {
                var passwordBox = new UiPasswordBox
                {
                    Password = "secret",
                };

                ExecuteTemplateCommand(passwordBox, "reveal");

                Assert.True(passwordBox.IsPasswordRevealed);
                Assert.Equal("secret", passwordBox.Text);

                ExecuteTemplateCommand(passwordBox, "reveal");

                Assert.False(passwordBox.IsPasswordRevealed);
                Assert.Equal(Mask(passwordBox), passwordBox.Text);
            }
        );
    }

    [Fact]
    public void RevealButtonDisabled_MasksRevealedPasswordAndBlocksRevealCommand()
    {
        RunOnStaThread(
            () =>
            {
                var passwordBox = new UiPasswordBox
                {
                    Password = "secret",
                };

                ExecuteTemplateCommand(passwordBox, "reveal");
                passwordBox.RevealButtonEnabled = false;

                Assert.False(passwordBox.IsPasswordRevealed);
                Assert.Equal(Mask(passwordBox), passwordBox.Text);

                ExecuteTemplateCommand(passwordBox, "reveal");

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
    public void DisablingControl_MasksRevealedPasswordAndBlocksRevealCommand()
    {
        RunOnStaThread(
            () =>
            {
                var passwordBox = new UiPasswordBox
                {
                    Password = "secret",
                };

                ExecuteTemplateCommand(passwordBox, "reveal");
                passwordBox.IsEnabled = false;

                Assert.False(passwordBox.IsPasswordRevealed);
                Assert.Equal(Mask(passwordBox), passwordBox.Text);

                ExecuteTemplateCommand(passwordBox, "reveal");

                Assert.False(passwordBox.IsPasswordRevealed);
                Assert.Equal(Mask(passwordBox), passwordBox.Text);
            }
        );
    }

    [Fact]
    public void ClearCommand_ClearsPassword_WhenEditable()
    {
        RunOnStaThread(
            () =>
            {
                var passwordBox = new UiPasswordBox
                {
                    Password = "secret",
                };

                ExecuteTemplateCommand(passwordBox, "clear");

                Assert.Equal(string.Empty, passwordBox.Password);
                Assert.Equal(string.Empty, passwordBox.Text);
            }
        );
    }

    [Fact]
    public void ClearCommand_DoesNotChangePassword_WhenClearButtonDisabled()
    {
        RunOnStaThread(
            () =>
            {
                var passwordBox = new UiPasswordBox
                {
                    Password = "secret",
                    ClearButtonEnabled = false,
                };

                ExecuteTemplateCommand(passwordBox, "clear");

                Assert.Equal("secret", passwordBox.Password);
                Assert.Equal(Mask(passwordBox), passwordBox.Text);
            }
        );
    }

    [Fact]
    public void ClearCommand_DoesNotChangePassword_WhenReadOnly()
    {
        RunOnStaThread(
            () =>
            {
                var passwordBox = new UiPasswordBox
                {
                    Password = "secret",
                    IsReadOnly = true,
                };

                ExecuteTemplateCommand(passwordBox, "clear");

                Assert.Equal("secret", passwordBox.Password);
                Assert.Equal(Mask(passwordBox), passwordBox.Text);
            }
        );
    }

    [Fact]
    public void ClearCommand_DoesNotChangePassword_WhenDisabled()
    {
        RunOnStaThread(
            () =>
            {
                var passwordBox = new UiPasswordBox
                {
                    Password = "secret",
                    IsEnabled = false,
                };

                ExecuteTemplateCommand(passwordBox, "clear");

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

    private static void ExecuteTemplateCommand(UiPasswordBox passwordBox, string parameter)
    {
        passwordBox.TemplateButtonCommand.Execute(parameter);
    }

    private static string Mask(UiPasswordBox passwordBox)
    {
        return new string(passwordBox.PasswordChar, passwordBox.Password.Length);
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(
            () =>
            {
                try
                {
                    action();
                }
                catch (Exception caughtException)
                {
                    exception = caughtException;
                }
            }
        );

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }
}
