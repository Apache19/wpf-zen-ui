// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Zen.Ui.Controls;
using Dialog = Wpf.Zen.Ui.Controls.ContentDialog;
using TextBox = System.Windows.Controls.TextBox;

namespace Wpf.Zen.Ui.UnitTests.Controls;

public partial class ContentDialogTests
{
    [Fact]
    public void Preparation_RejectsEveryReentrantShowAndReleasesReservation() => Run(scope =>
    {
        var host = CreatePreparationHost(scope);
        var nested = new Dialog();
        host.TemplateApplied = () =>
        {
            // A rejected nested call must not release the outer call's preparation guard.
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var rejected = host.ShowAsync(nested);
                Assert.True(rejected.IsFaulted);
                Assert.Throws<InvalidOperationException>(() => rejected.GetAwaiter().GetResult());
                Assert.False(nested.IsOpen);
            }
        };

        var dialog = new Dialog();
        var task = host.ShowAsync(dialog);
        Drain();
        Assert.Same(dialog, host.CurrentDialog);
        dialog.Close("accepted");
        Pump(task);
        Assert.Equal("accepted", task.Result);
        Assert.Null(dialog.Parent);
        Assert.True(((Border)host.Content).IsEnabled);
        var next = host.ShowAsync(nested);
        Drain();
        nested.Close();
        Pump(next);
    });

    [Theory]
    [InlineData("throw")]
    [InlineData("hide")]
    [InlineData("parent")]
    [InlineData("cancel")]
    public void Preparation_RevalidatesAfterCallbacksAndAllowsRetry(string action) => Run(scope =>
    {
        var host = CreatePreparationHost(scope);
        var dialog = new Dialog();
        var parent = new Border();
        using var cancellation = new CancellationTokenSource();
        host.TemplateApplied = () =>
        {
            switch (action)
            {
                case "throw":
                    throw new InvalidOperationException("Template failed.");
                case "hide":
                    host.Visibility = Visibility.Collapsed;
                    break;
                case "parent":
                    parent.Child = dialog;
                    break;
                case "cancel":
                    cancellation.Cancel();
                    break;
            }
        };

        var rejected = host.ShowAsync(dialog, cancellation.Token);
        Assert.True(rejected.IsCompleted);
        if (action == "cancel")
        {
            Assert.True(rejected.IsCanceled);
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => rejected.GetAwaiter().GetResult());
        }

        Assert.False(dialog.IsOpen);
        Assert.Null(host.CurrentDialog);
        Assert.True(((Border)host.Content).IsEnabled);
        parent.Child = null;
        host.Visibility = Visibility.Visible;
        Drain();
        var retry = host.ShowAsync(dialog);
        Drain();
        Assert.Same(dialog, host.CurrentDialog);
        dialog.Close();
        Pump(retry);
    });

    [Fact]
    public void SiblingHosts_KeepFocusCommandsAndCleanupIndependent() => Run(scope =>
    {
        var firstBackground = new TextBox();
        scope.Host.Content = firstBackground;
        var secondBackground = new TextBox();
        var otherHost = new ContentDialogHost { IsAnimationEnabled = false, Content = secondBackground };
        ((Panel)scope.Window.Content).Children.Add(otherHost);
        Drain();
        var firstEditor = new TextBox();
        var first = new Dialog { Content = firstEditor };
        var firstTask = scope.Host.ShowAsync(first);
        Drain();
        Assert.Same(firstEditor, Keyboard.FocusedElement);
        var secondEditor = new TextBox();
        var second = new Dialog { Content = secondEditor };
        var secondTask = otherHost.ShowAsync(second);
        Drain();
        Assert.Same(secondEditor, Keyboard.FocusedElement);
        Assert.True(firstEditor.Focus());
        var command = new RoutedCommand();
        int calls = 0;
        secondEditor.CommandBindings.Add(new CommandBinding(
            command,
            (_, _) => calls++,
            (_, e) => e.CanExecute = true));
        // An explicit target in another host stays usable even while the first dialog has focus.
        Assert.True(command.CanExecute(null, secondEditor));
        command.Execute(null, secondEditor);
        Assert.Equal(1, calls);
        Assert.False(firstBackground.Focus());
        Assert.False(secondBackground.Focus());
        Assert.True(secondEditor.Focus());
        first.Close();
        Pump(firstTask);
        Assert.Same(secondEditor, Keyboard.FocusedElement);
        Assert.True(firstBackground.IsEnabled);
        Assert.False(secondBackground.IsEnabled);
        Assert.False(secondTask.IsCompleted);
        second.Close();
        Pump(secondTask);
        Assert.True(secondBackground.IsEnabled);
    });

    [Theory]
    [InlineData("window", false)]
    [InlineData("root", false)]
    [InlineData("host", false)]
    [InlineData("window", true)]
    public void AncestorCommands_AreBlockedOnlyWhileDialogOwnsFocus(string targetKind, bool usePopup) => Run(scope =>
    {
        var root = (Panel)scope.Window.Content;
        var independent = new TextBox();
        root.Children.Add(independent);
        var target = targetKind switch
        {
            "window" => (UIElement)scope.Window,
            "root" => root,
            _ => scope.Host,
        };
        int calls = 0;
        var command = new RoutedCommand();
        target.CommandBindings.Add(new CommandBinding(
            command,
            (_, _) => calls++,
            (_, e) => e.CanExecute = true));
        var editor = new TextBox();
        var dialog = new Dialog { Content = editor };
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        var popupEditor = new TextBox();
        var popup = new System.Windows.Controls.Primitives.Popup
        {
            PlacementTarget = editor,
            Child = popupEditor,
            StaysOpen = true,
        };
        try
        {
            if (usePopup)
            {
                popup.IsOpen = true;
                Drain();
                Assert.True(popupEditor.Focus());
            }
            else
            {
                Assert.Same(editor, Keyboard.FocusedElement);
            }

            Assert.False(command.CanExecute(null, target));
            // Execute can be called without CanExecute; both routed phases need protection.
            command.Execute(null, target);
            Assert.Equal(0, calls);
            popup.IsOpen = false;
            Assert.True(independent.Focus());
            if (targetKind != "host")
            {
                Assert.True(command.CanExecute(null, target));
                command.Execute(null, target);
                Assert.Equal(1, calls);
            }
            else
            {
                // The active host itself remains covered regardless of outside focus.
                Assert.False(command.CanExecute(null, target));
            }

            dialog.Close();
            Pump(task);
            int before = calls;
            Assert.True(command.CanExecute(null, target));
            command.Execute(null, target);
            Assert.Equal(before + 1, calls);
        }
        finally
        {
            popup.IsOpen = false;
        }
    });

    [Fact]
    public void IndependentCommandTarget_RemainsUsableWhileDialogHasFocus() => Run(scope =>
    {
        var independent = new TextBox();
        ((Panel)scope.Window.Content).Children.Add(independent);
        var command = new RoutedCommand();
        int calls = 0;
        independent.CommandBindings.Add(new CommandBinding(
            command,
            (_, _) => calls++,
            (_, e) => e.CanExecute = true));
        var editor = new TextBox();
        var dialog = new Dialog { Content = editor };
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        Assert.Same(editor, Keyboard.FocusedElement);
        Assert.True(command.CanExecute(null, independent));
        command.Execute(null, independent);
        Assert.Equal(1, calls);
        dialog.Close();
        Pump(task);
    });

    private static PreparationHost CreatePreparationHost(Scope scope)
    {
        var host = new PreparationHost { IsAnimationEnabled = false, Content = new Border() };
        scope.Background.Child = host;
        Drain();
        // Force ShowAsync to realize a template while the host is already loaded and visible.
        host.Template = CreateHostTemplateWithDurations("0:0:0", "0:0:0");
        return host;
    }

    private sealed class PreparationHost : ContentDialogHost
    {
        internal Action? TemplateApplied { get; set; }

        /// <inheritdoc />
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            var callback = TemplateApplied;
            TemplateApplied = null;
            callback?.Invoke();
        }
    }
}
