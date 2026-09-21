// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Wpf.Zen.Ui.UnitTests.VirtualInput;

/// <summary>
/// Verifies that literal text cannot enter an unfinished or replacement composition.
/// </summary>
public class SoftKeyboardTextBoundaryTests
{
    /// <summary>
    /// Escape acceptance is not acknowledgement, and a late completion must not replay rejected
    /// text.
    /// </summary>
    [Fact]
    public Task IgnoredEscape_DoesNotInjectTextLater()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var composition = StartComposition(scope);
            Assert.False(await scope.Keyboard.SendActionTextAsync("★"));
            Assert.Empty(scope.Input.Text);
            composition.Complete();
            await scope.DrainAsync();
            Assert.Empty(scope.Input.Text);
        });
    }

    /// <summary>
    /// Only the original input session can consume an acknowledged cancellation.
    /// </summary>
    [Theory]
    [InlineData("complete")]
    [InlineData("target")]
    [InlineData("reopen")]
    [InlineData("composition")]
    [InlineData("cancel")]
    public Task CancellationAcknowledgement_RevalidatesOwnership(string transition)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var composition = StartComposition(scope);
            using var cancellation = new CancellationTokenSource();
            var pending = scope.Keyboard.SendActionTextAsync("★", cancellation.Token);
            Assert.False(pending.IsCompleted);
            Assert.Empty(scope.Input.Text);
            composition.Complete();

            switch (transition)
            {
                case "target":
                    scope.Keyboard.Target = scope.OtherEditor;
                    scope.OtherEditor.Focus();
                    scope.Keyboard.Target = scope.Editor;
                    scope.Editor.Focus();
                    break;
                case "reopen":
                    scope.Keyboard.Visibility = Visibility.Collapsed;
                    scope.Keyboard.Visibility = Visibility.Visible;
                    break;
                case "composition":
                    StartComposition(scope).Complete();
                    break;
                case "cancel":
                    cancellation.Cancel();
                    break;
            }

            if (transition == "cancel")
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            }
            else
            {
                Assert.Equal(transition == "complete", await pending);
            }

            Assert.Equal(transition == "complete" ? ["★"] : Array.Empty<string>(), scope.Input.Text);
        });
    }

    /// <summary>
    /// Synchronous callers report no text delivery while composition cancellation is pending.
    /// </summary>
    [Fact]
    public Task SynchronousText_DoesNotClaimQueuedCancellationAsDelivery()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var composition = StartComposition(scope);
            Assert.False(scope.Keyboard.SendActionText("★"));
            composition.Complete();
            await scope.DrainAsync();
            Assert.Empty(scope.Input.Text);
            Assert.True(scope.Keyboard.SendActionText("☆"));
            Assert.Equal(["☆"], scope.Input.Text);
        });
    }

    /// <summary>
    /// Starts a WPF composition through the same events used by the keyboard's observer.
    /// </summary>
    private static TextComposition StartComposition(SoftKeyboardRegressionScope scope)
    {
        var composition = new TextComposition(InputManager.Current, scope.Editor, string.Empty,
            TextCompositionAutoComplete.Off);
        TextCompositionManager.StartComposition(composition);
        return composition;
    }
}
