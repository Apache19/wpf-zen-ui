// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using UiSnackbar = Wpf.Zen.Ui.Controls.Snackbar;
using UiTextBox = Wpf.Zen.Ui.Controls.TextBox;
using UiThumbRate = Wpf.Zen.Ui.Controls.ThumbRate;

namespace Wpf.Zen.Ui.UnitTests.Controls;

public class TemplateButtonPartTests
{
    [Fact]
    public void Snackbar_ClosePart_InvokesHide()
    {
        WpfTestThread.Run(() =>
        {
            var snackbar = new RecordingSnackbar
            {
                Template = CreateTemplate(typeof(UiSnackbar), typeof(Button), "PART_CloseButton"),
            };

            Assert.True(snackbar.ApplyTemplate());
            RaiseClick(FindButton(snackbar, "PART_CloseButton"));

            Assert.Equal(1, snackbar.HideCount);
        });
    }

    [Fact]
    public void TextBox_ClearPart_ClearsText()
    {
        WpfTestThread.Run(() =>
        {
            var textBox = new UiTextBox
            {
                Text = "value",
                Template = CreateTemplate(typeof(UiTextBox), typeof(Button), "PART_ClearButton"),
            };

            Assert.True(textBox.ApplyTemplate());
            RaiseClick(FindButton(textBox, "PART_ClearButton"));

            Assert.Equal(string.Empty, textBox.Text);
        });
    }

    [Theory]
    [InlineData("PART_LikeButton", Wpf.Zen.Ui.Controls.ThumbRateState.Liked)]
    [InlineData("PART_DislikeButton", Wpf.Zen.Ui.Controls.ThumbRateState.Disliked)]
    public void ThumbRate_TemplateButton_TogglesRepresentedState(
        string partName,
        Wpf.Zen.Ui.Controls.ThumbRateState expectedState
    )
    {
        WpfTestThread.Run(() =>
        {
            var thumbRate = new UiThumbRate
            {
                Template = CreateTemplate(typeof(UiThumbRate), typeof(Button), partName),
            };

            Assert.True(thumbRate.ApplyTemplate());
            var button = FindButton(thumbRate, partName);

            RaiseClick(button);
            Assert.Equal(expectedState, thumbRate.State);

            RaiseClick(button);
            Assert.Equal(Wpf.Zen.Ui.Controls.ThumbRateState.None, thumbRate.State);
        });
    }

    [Fact]
    public void ReplacingTemplate_DetachesOldButtonImmediately()
    {
        WpfTestThread.Run(() =>
        {
            var controls = new (Control Control, string PartName, Action AssertUnchanged)[]
            {
                CreateSnackbarCase(),
                CreateTextBoxCase(),
                CreateThumbRateCase(),
            };

            foreach ((Control control, string partName, Action assertUnchanged) in controls)
            {
                Assert.True(control.ApplyTemplate());
                ButtonBase oldButton = FindButton(control, partName);

                control.Template = CreateTemplate(control.GetType(), typeof(Border), "ReplacementRoot");
                RaiseClick(oldButton);

                assertUnchanged();
            }
        });
    }

    private static (Control Control, string PartName, Action AssertUnchanged) CreateSnackbarCase()
    {
        var control = new RecordingSnackbar
        {
            Template = CreateTemplate(typeof(UiSnackbar), typeof(Button), "PART_CloseButton"),
        };
        return (control, "PART_CloseButton", () => Assert.Equal(0, control.HideCount));
    }

    private static (Control Control, string PartName, Action AssertUnchanged) CreateTextBoxCase()
    {
        var control = new UiTextBox
        {
            Text = "value",
            Template = CreateTemplate(typeof(UiTextBox), typeof(Button), "PART_ClearButton"),
        };
        return (control, "PART_ClearButton", () => Assert.Equal("value", control.Text));
    }

    private static (Control Control, string PartName, Action AssertUnchanged) CreateThumbRateCase()
    {
        var control = new UiThumbRate
        {
            Template = CreateTemplate(typeof(UiThumbRate), typeof(Button), "PART_LikeButton"),
        };
        return (
            control,
            "PART_LikeButton",
            () => Assert.Equal(Wpf.Zen.Ui.Controls.ThumbRateState.None, control.State)
        );
    }

    private static ButtonBase FindButton(Control control, string partName)
    {
        return Assert.IsAssignableFrom<ButtonBase>(control.Template.FindName(partName, control));
    }

    private static void RaiseClick(ButtonBase button)
    {
        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
    }

    private static ControlTemplate CreateTemplate(Type targetType, Type rootType, string name)
    {
#pragma warning disable CS0618 // FrameworkElementFactory is sufficient for template-contract tests.
        var root = new FrameworkElementFactory(rootType) { Name = name };
        return new ControlTemplate(targetType) { VisualTree = root };
#pragma warning restore CS0618
    }

    private sealed class RecordingSnackbar() : UiSnackbar(new Wpf.Zen.Ui.Controls.SnackbarPresenter())
    {
        public int HideCount { get; private set; }

        protected override void Hide()
        {
            HideCount++;
        }
    }
}
