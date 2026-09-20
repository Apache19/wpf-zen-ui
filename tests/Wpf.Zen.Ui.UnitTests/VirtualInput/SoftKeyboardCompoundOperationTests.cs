// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Wpf.Zen.Ui.VirtualInput.Candidates;
using Wpf.Zen.Ui.VirtualInput.Controls;
using Wpf.Zen.Ui.VirtualInput.Infrastructure;
using Wpf.Zen.Ui.VirtualInput.Input;

namespace Wpf.Zen.Ui.UnitTests.VirtualInput;

/// <summary>
/// Exercises ownership through whole provider operations, including callbacks that ignore tokens.
/// </summary>
public class SoftKeyboardCompoundOperationTests
{
    /// <summary>
    /// Cancellation and profile transitions reject a pending command's input, snapshots and errors.
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task PendingCandidate_CannotContinueInObsoleteLifetime(bool changeProfile, bool fail)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var source = new CandidateSource();
            scope.Keyboard.CandidateSource = source;
            source.Publish("original");
            var resume = new TaskCompletionSource<bool>();
            bool? sent = null;
            source.Commit = async token =>
            {
                await resume.Task;
                sent = source.Host!.TrySendKey(Key.D1);
                source.Publish("obsolete");
                if (fail)
                {
                    throw new InvalidOperationException("obsolete error");
                }

                return SoftKeyboardCandidateOperationResult.Success;
            };
            using var cancellation = new CancellationTokenSource();
            var commit = scope.Keyboard.CommitCandidateAsync(scope.Keyboard.Candidates[0], cancellation.Token);
            Assert.False(commit.IsCompleted);
            if (changeProfile)
            {
                BeginProfileChange(scope.Keyboard);
            }
            else
            {
                cancellation.Cancel();
            }

            resume.SetResult(true);
            Assert.False(await commit);
            Assert.False(sent);
            Assert.Equal("original", Assert.Single(scope.Keyboard.Candidates).Text);
            Assert.Null(scope.Keyboard.CandidateError);
            Assert.Equal(0, source.Releases);
        });
    }

    /// <summary>
    /// Focus preparation may synchronously replace the snapshot before the source receives input.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CandidatePreparation_RejectsChangedSnapshot(bool movePage)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var source = new CandidateSource();
            scope.Keyboard.CandidateSource = source;
            source.Publish("original");
            source.Refresh = () => source.Publish("replacement");
            var result = movePage
                ? await scope.Keyboard.MoveCandidatePageAsync(1, TestContext.Current.CancellationToken)
                : await scope.Keyboard.CommitCandidateAsync(
                    scope.Keyboard.Candidates[0], TestContext.Current.CancellationToken);
            Assert.False(result);
            Assert.Equal(0, source.InputCalls);
            Assert.Equal("replacement", Assert.Single(scope.Keyboard.Candidates).Text);
        });
    }

    /// <summary>
    /// Snapshot changes caused by the command itself acknowledge successful input.
    /// </summary>
    [Fact]
    public Task CandidateCompletion_AcceptsItsOwnSnapshotChange()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var source = new CandidateSource();
            scope.Keyboard.CandidateSource = source;
            source.Publish("original");
            source.Commit = async token =>
            {
                await Task.Yield();
                source.Publish("updated");
                return SoftKeyboardCandidateOperationResult.Success;
            };
            Assert.True(await scope.Keyboard.CommitCandidateAsync(
                scope.Keyboard.Candidates[0], TestContext.Current.CancellationToken));
            Assert.Equal("updated", Assert.Single(scope.Keyboard.Candidates).Text);
        });
    }

    /// <summary>
    /// A retained provider continuation cannot publish after its invoking operation has completed.
    /// </summary>
    [Fact]
    public Task CandidateCompletion_ExpiresDetachedContinuation()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var source = new CandidateSource();
            scope.Keyboard.CandidateSource = source;
            source.Publish("original");
            var resume = new TaskCompletionSource<bool>();
            Task? detached = null;
            async Task PublishLater()
            {
                await resume.Task;
                Assert.False(source.Host!.TrySendKey(Key.D1));
                source.Publish("late");
            }

            source.Commit = token =>
            {
                detached = PublishLater();
                return Task.FromResult(SoftKeyboardCandidateOperationResult.Success);
            };
            Assert.True(await scope.Keyboard.CommitCandidateAsync(
                scope.Keyboard.Candidates[0], TestContext.Current.CancellationToken));
            resume.SetResult(true);
            Assert.NotNull(detached);
            await detached;
            Assert.Equal("original", Assert.Single(scope.Keyboard.Candidates).Text);
        });
    }

    /// <summary>
    /// An action cannot inject text if its final focus preparation changes profile ownership.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActionInput_RechecksOwnershipAfterPreparation(bool asynchronous)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var source = new CandidateSource();
            scope.Keyboard.CandidateSource = source;
            bool? sent = null;
            var action = new KeyAction(async context =>
            {
                source.Refresh = () => BeginProfileChange(scope.Keyboard);
                sent = asynchronous
                    ? await context.TrySendTextAsync("obsolete")
                    : context.TrySendText("obsolete");
            });
            var execute = typeof(SoftKeyboard).GetMethod(
                "ExecuteCustomActionAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
            await (Task)execute.Invoke(scope.Keyboard, [action, null])!;
            Assert.False(sent);
            Assert.Empty(scope.Input.Text);
            Assert.Null(scope.Keyboard.ExtensionError);
        });
    }

    /// <summary>
    /// Input through a custom source must retain the candidate snapshot supplied to its request.
    /// </summary>
    [Fact]
    public Task PendingCandidate_RejectsInputAfterSnapshotChange()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var source = new CandidateSource();
            scope.Keyboard.CandidateSource = source;
            source.Publish("original");
            var resume = new TaskCompletionSource<bool>();
            bool? sent = null;
            source.Commit = async token =>
            {
                await resume.Task;
                sent = source.Host!.TrySendKey(Key.D1);
                return SoftKeyboardCandidateOperationResult.NotHandled;
            };
            var commit = scope.Keyboard.CommitCandidateAsync(
                scope.Keyboard.Candidates[0], TestContext.Current.CancellationToken);
            source.Publish("replacement");
            resume.SetResult(true);
            Assert.False(await commit);
            Assert.False(sent);
            Assert.Equal("replacement", Assert.Single(scope.Keyboard.Candidates).Text);
        });
    }

    /// <summary>
    /// A worker publication queued before completion is accepted only while its ownership survives.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task QueuedCandidateSnapshot_RevalidatesAfterNormalCompletion(bool cancel)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var source = new CandidateSource();
            scope.Keyboard.CandidateSource = source;
            source.Publish("original");
            using var cancellation = new CancellationTokenSource();
            source.Commit = token =>
            {
                // Keep the dispatcher occupied until the worker has queued its publication.
#pragma warning disable xUnit1031 // This worker only posts to the dispatcher and cannot await it.
                Task.Run(() => source.Publish("queued"), token).GetAwaiter().GetResult();
#pragma warning restore xUnit1031
                return Task.FromResult(SoftKeyboardCandidateOperationResult.Success);
            };
            Assert.True(await scope.Keyboard.CommitCandidateAsync(
                scope.Keyboard.Candidates[0], cancellation.Token));
            Assert.Equal("original", Assert.Single(scope.Keyboard.Candidates).Text);
            if (cancel)
            {
                cancellation.Cancel();
            }

            await scope.DrainAsync();
            Assert.Equal(cancel ? "original" : "queued", Assert.Single(scope.Keyboard.Candidates).Text);
        });
    }

    /// <summary>
    /// Deferred mode changes retain their original profile and composition across idle dispatch.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public Task CompositionCancellation_RejectsExpiredDeferredCommand(int transition)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var composition = new TextComposition(InputManager.Current, scope.Editor, "preedit");
            void Observe(RoutedEvent routedEvent)
            {
                scope.Keyboard.OnSessionTextComposition(
                    new TextCompositionEventArgs(Keyboard.PrimaryDevice, composition)
                    {
                        RoutedEvent = routedEvent,
                        Source = scope.Editor,
                    });
            }

            Observe(TextCompositionManager.PreviewTextInputStartEvent);
            var executed = false;
            var defer = typeof(SoftKeyboard).GetMethod(
                "RunAfterActiveCompositionCancellation", BindingFlags.Instance | BindingFlags.NonPublic)!;
            defer.Invoke(scope.Keyboard, [new Action(() => executed = true)]);
            Assert.False(executed);
            Observe(TextCompositionManager.PreviewTextInputEvent);
            if (transition == 1)
            {
                BeginProfileChange(scope.Keyboard);
            }
            else if (transition == 2)
            {
                Observe(TextCompositionManager.PreviewTextInputStartEvent);
                Observe(TextCompositionManager.PreviewTextInputEvent);
            }

            await scope.DrainAsync();
            Assert.Equal(transition == 0, executed);
        });
    }

    /// <summary>
    /// Starts an activation generation before scheduled cleanup cancels existing provider tokens.
    /// </summary>
    private static void BeginProfileChange(SoftKeyboard keyboard)
    {
        keyboard.OnSessionInputMethodChanged(new InputMethodChange(
            InputMethodChangeKind.Profile,
            new InputProcessorProfileChange(2, 0x0409, Guid.Empty, Guid.Empty, Guid.Empty,
                new IntPtr(0x0409), true)));
    }

    private sealed class KeyAction(Func<SoftKeyboardActionContext, Task> execute) : IAsyncSoftKeyboardKeyAction
    {
        /// <inheritdoc />
        public Task ExecuteAsync(SoftKeyboardActionContext context, CancellationToken cancellationToken)
        {
            return execute(context);
        }
    }

    /// <summary>
    /// Keeps one host alive while tests suspend input or reenter through refresh callbacks.
    /// </summary>
    private sealed class CandidateSource : ISoftKeyboardCandidateSource, ISoftKeyboardCandidateSession
    {
        internal SoftKeyboardCandidateHost? Host { get; private set; }
        internal Func<CancellationToken, Task<SoftKeyboardCandidateOperationResult>>? Commit { get; set; }
        internal Action? Refresh { get; set; }
        internal int InputCalls { get; private set; }
        internal int Releases { get; private set; }

        internal void Publish(string text)
        {
            Host!.Publish(new SoftKeyboardCandidateSnapshot(
                [new SoftKeyboardCandidate(0, text)], SoftKeyboardCandidatePresentationState.Embedded,
                1, 2, 0));
        }

        /// <inheritdoc />
        public ISoftKeyboardCandidateSession CreateSession(SoftKeyboardCandidateHost host)
        {
            Host = host;
            return this;
        }

        /// <inheritdoc />
        public Task RefreshAsync(CancellationToken cancellationToken)
        {
            Refresh?.Invoke();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<SoftKeyboardCandidateOperationResult> CommitAsync(
            SoftKeyboardCandidate candidate, CancellationToken cancellationToken)
        {
            InputCalls++;
            return Commit?.Invoke(cancellationToken)
                ?? Task.FromResult(SoftKeyboardCandidateOperationResult.Success);
        }

        /// <inheritdoc />
        public Task<SoftKeyboardCandidateOperationResult> MovePageAsync(
            int delta, CancellationToken cancellationToken)
        {
            InputCalls++;
            return Task.FromResult(SoftKeyboardCandidateOperationResult.Success);
        }

        /// <inheritdoc />
        public void ReleasePresentation()
        {
            Releases++;
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}
