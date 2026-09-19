// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Wpf.Zen.Ui.VirtualInput.Candidates;
using Wpf.Zen.Ui.VirtualInput.Controls;
using Wpf.Zen.Ui.VirtualInput.Infrastructure;
using Wpf.Zen.Ui.VirtualInput.Input;
using Wpf.Zen.Ui.VirtualInput.InputMethods;

namespace Wpf.Zen.Ui.UnitTests.VirtualInput;

public class SoftKeyboardAsyncSourceTests
{
    /// <summary>
    /// An activation retains the old button until the new observation window is committed.
    /// </summary>
    [Fact]
    public void InputMethodNotification_PublishesOnlyAfterActivationSettles()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var backend = new SettlingInputMethodBackend();
            var keyboard = CreateKeyboard();
            keyboard.InputMethodSource = new WindowsSoftKeyboardInputMethodSource(backend);
            keyboard.IsOpen = true;
            var editor = new TextBox();
            var panel = new StackPanel();
            panel.Children.Add(editor);
            panel.Children.Add(keyboard);
            keyboard.Target = editor;
            var window = new Window { Content = panel, ShowInTaskbar = false };
            var publishedCaps = new List<object>();
            var descriptor = DependencyPropertyDescriptor.FromProperty(
                SoftKeyboard.InputModeButtonProperty,
                typeof(SoftKeyboard)
            );

            void RecordButton(object? sender, EventArgs args)
            {
                if (keyboard.InputModeButton is { } button)
                {
                    publishedCaps.Add(button.Content);
                }
            }

            descriptor.AddValueChanged(keyboard, RecordButton);
            try
            {
                window.Show();
                Assert.True(editor.Focus());
                PumpUntil(() => Equals(keyboard.InputModeButton?.Content, "A"));

                Assert.True(keyboard.TryActivateInputMethod(backend.Next));
                Assert.NotEqual(backend.Next.ClassId, keyboard.ActiveInputMethod?.ClassId);
                Assert.Equal("direct:A", keyboard.InputModeButton?.Id);
                Assert.Equal("A", keyboard.InputModeButton?.Content);

                publishedCaps.Clear();
                keyboard.OnSessionInputMethodStateInvalidated();
                Assert.NotEqual(backend.Next.ClassId, keyboard.ActiveInputMethod?.ClassId);
                Assert.Equal("A", keyboard.InputModeButton?.Content);
                backend.CompleteActivation();
                keyboard.OnSessionInputMethodStateInvalidated();
                PumpUntil(() => Equals(keyboard.InputModeButton?.Content, "英"));
                Assert.Equal(backend.Next.ClassId, keyboard.ActiveInputMethod?.ClassId);
                Assert.Equal("direct:英", keyboard.InputModeButton?.Id);
                Assert.Equal(new object[] { "英" }, publishedCaps);

            }
            finally
            {
                descriptor.RemoveValueChanged(keyboard, RecordButton);
                window.Close();
            }
        });
    }

    [Fact]
    public void CandidateRefresh_RejectsLateSnapshotFromSupersededRequest()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var source = new DeferredCandidateSource();
            var keyboard = CreateKeyboard();
            keyboard.CandidateSource = source;
            keyboard.IsOpen = true;
            var editor = new TextBox();
            var panel = new StackPanel();
            panel.Children.Add(editor);
            panel.Children.Add(keyboard);
            keyboard.Target = editor;
            var window = new Window { Content = panel, ShowInTaskbar = false };

            try
            {
                window.Show();
                Assert.True(editor.Focus());
                PumpUntil(() => keyboard.ActiveInputMethod is not null);
                PumpDispatcher();
                PumpUntil(() => source.RefreshRequests.Count > 0);
                DeferredCandidateRequest previous = source.RefreshRequests.Last();

                Task refresh = keyboard.RefreshCandidatesAsync();
                DeferredCandidateRequest current = source.RefreshRequests.Last();
                Assert.NotSame(previous, current);
                Assert.True(previous.CancellationToken.IsCancellationRequested);

                current.Complete("current");
                PumpUntil(() => refresh.IsCompleted && keyboard.Candidates.Count == 1);
                Assert.Equal("current", keyboard.Candidates[0].Text);

                previous.Complete("obsolete");
                PumpDispatcher();
                Assert.Equal("current", Assert.Single(keyboard.Candidates).Text);
                Assert.Null(keyboard.CandidateError);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public Task CandidateSourceReplacement_CancelsCommitAndExpiresOldHost()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            var source = new DeferredCandidateSource();
            var keyboard = CreateKeyboard();
            keyboard.CandidateSource = source;
            keyboard.IsOpen = true;
            var editor = new TextBox();
            var panel = new StackPanel();
            panel.Children.Add(editor);
            panel.Children.Add(keyboard);
            keyboard.Target = editor;
            var window = new Window { Content = panel, ShowInTaskbar = false };

            try
            {
                window.Show();
                Assert.True(editor.Focus());
                PumpUntil(() => keyboard.ActiveInputMethod is not null);
                PumpDispatcher();
                PumpUntil(() => source.RefreshRequests.Count > 0);
                source.RefreshRequests.Last().Complete("candidate");
                PumpUntil(() => keyboard.Candidates.Count == 1);

                Task<bool> commit = keyboard.CommitCandidateAsync(keyboard.Candidates[0]);
                PumpUntil(() => source.CommitRequests.Count == 1);
                DeferredCandidateOperation operation = source.CommitRequests[0];

                var replacement = new DeferredCandidateSource();
                keyboard.CandidateSource = replacement;
                Assert.True(operation.CancellationToken.IsCancellationRequested);
                Assert.True(source.SessionDisposed);

                operation.Complete(SoftKeyboardCandidateOperationResult.Success);
                PumpUntil(() => commit.IsCompleted);
                Assert.False(await commit);
                Assert.Empty(keyboard.Candidates);
                Assert.Null(keyboard.CandidateError);

                source.PublishOutsideRefresh("obsolete");
                PumpDispatcher();
                Assert.Empty(keyboard.Candidates);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void CandidateFailures_ArePublishedButCancellationIsNotAnError()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = CreateKeyboard();
            keyboard.CandidateSource = new ThrowingCandidateSource();
            Task refresh = keyboard.RefreshCandidatesAsync();
            PumpUntil(() => refresh.IsCompleted);
            Assert.IsType<InvalidOperationException>(keyboard.CandidateError);

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            keyboard.CandidateSource = new CanceledCandidateSource();
            Task canceled = keyboard.RefreshCandidatesAsync(cancellation.Token);
            PumpUntil(() => canceled.IsCompleted);
            Assert.Null(keyboard.CandidateError);
        });
    }

    /// <summary>
    /// A canceled provider cannot report late success or overwrite the current candidate error.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CandidateCancellation_RejectsLateCompletion(bool fails)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var source = new DeferredCandidateSource();
            scope.Keyboard.CandidateSource = source;
            source.RefreshRequests.Last().Complete("candidate");
            PumpUntil(() => scope.Keyboard.Candidates.Count == 1);

            using var cancellation = new CancellationTokenSource();
            var commit = scope.Keyboard.CommitCandidateAsync(scope.Keyboard.Candidates[0], cancellation.Token);
            var pending = Assert.Single(source.CommitRequests);
            cancellation.Cancel();
            if (fails)
            {
                pending.Fail(new InvalidOperationException("late error"));
            }
            else
            {
                pending.Complete(SoftKeyboardCandidateOperationResult.Success);
            }

            Assert.False(await commit);
            Assert.Null(scope.Keyboard.CandidateError);
            Assert.Single(scope.Keyboard.Candidates);
        });
    }

    /// <summary>
    /// A canceled refresh cannot publish a snapshot even if no replacement refresh has started.
    /// </summary>
    [Fact]
    public Task CandidateRefresh_CancellationRevokesPublication()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var source = new DeferredCandidateSource();
            scope.Keyboard.CandidateSource = source;
            using var cancellation = new CancellationTokenSource();
            var refresh = scope.Keyboard.RefreshCandidatesAsync(cancellation.Token);
            var pending = source.RefreshRequests.Last();
            cancellation.Cancel();
            pending.Complete("obsolete");
            await refresh;
            await scope.DrainAsync();
            Assert.Empty(scope.Keyboard.Candidates);
            Assert.Null(scope.Keyboard.CandidateError);
        });
    }

    private static SoftKeyboard CreateKeyboard()
    {
        return new SoftKeyboard(
            new SoftKeyboardPlatformServices(
                new AcceptingInputBackend(),
                new NoOpInputPaneBackend(),
                new EmptyInputMethodBackend(),
                new BackendSoftKeyboardCandidateSource(new EmptyCandidateBackend())
            )
        );
    }

    private static SoftKeyboardInputMethod CreateInputMethod(string name)
    {
        return new SoftKeyboardInputMethod(Guid.NewGuid(), Guid.NewGuid(), 0x0409, name, true);
    }

    private static void PumpUntil(Func<bool> condition)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition() && stopwatch.Elapsed < TimeSpan.FromSeconds(3))
        {
            PumpDispatcher();
        }

        Assert.True(condition(), "The asynchronous WPF operation did not finish in time.");
    }

    private static void PumpDispatcher()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(10),
            DispatcherPriority.ApplicationIdle,
            (_, _) => frame.Continue = false,
            Dispatcher.CurrentDispatcher
        );
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
    }

    private sealed class DeferredCandidateSource : ISoftKeyboardCandidateSource
    {
        private SoftKeyboardCandidateHost? _host;

        internal List<DeferredCandidateRequest> RefreshRequests { get; } = [];

        internal List<DeferredCandidateOperation> CommitRequests { get; } = [];

        internal bool SessionDisposed { get; private set; }

        public ISoftKeyboardCandidateSession CreateSession(SoftKeyboardCandidateHost host)
        {
            _host = host;
            return new Session(this, host);
        }

        internal void PublishOutsideRefresh(string text)
        {
            _host!.Publish(CreateSnapshot(text));
        }

        private static SoftKeyboardCandidateSnapshot CreateSnapshot(string text)
        {
            return new SoftKeyboardCandidateSnapshot(
                [new SoftKeyboardCandidate(0, text)],
                SoftKeyboardCandidatePresentationState.Embedded,
                1,
                1,
                0
            );
        }

        private sealed class Session(DeferredCandidateSource owner, SoftKeyboardCandidateHost host)
            : ISoftKeyboardCandidateSession
        {
            public async Task RefreshAsync(CancellationToken cancellationToken)
            {
                var request = new DeferredCandidateRequest(cancellationToken);
                owner.RefreshRequests.Add(request);
                string value = await request.Task.ConfigureAwait(false);
                host.Publish(CreateSnapshot(value));
            }

            public Task<SoftKeyboardCandidateOperationResult> CommitAsync(
                SoftKeyboardCandidate candidate,
                CancellationToken cancellationToken)
            {
                var request = new DeferredCandidateOperation(cancellationToken);
                owner.CommitRequests.Add(request);
                return request.Task;
            }

            public Task<SoftKeyboardCandidateOperationResult> MovePageAsync(
                int delta,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(SoftKeyboardCandidateOperationResult.NotHandled);
            }

            public void ReleasePresentation()
            {
            }

            public void Dispose()
            {
                owner.SessionDisposed = true;
            }
        }
    }

    private sealed class DeferredCandidateRequest(CancellationToken cancellationToken)
    {
        private readonly TaskCompletionSource<string> _completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        internal CancellationToken CancellationToken { get; } = cancellationToken;

        internal Task<string> Task => _completion.Task;

        internal void Complete(string value)
        {
            _completion.TrySetResult(value);
        }
    }

    private sealed class DeferredCandidateOperation(CancellationToken cancellationToken)
    {
        private readonly TaskCompletionSource<SoftKeyboardCandidateOperationResult> _completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        internal CancellationToken CancellationToken { get; } = cancellationToken;

        internal Task<SoftKeyboardCandidateOperationResult> Task => _completion.Task;

        internal void Fail(Exception exception)
        {
            _completion.TrySetException(exception);
        }

        internal void Complete(SoftKeyboardCandidateOperationResult result)
        {
            _completion.TrySetResult(result);
        }
    }

    private sealed class ThrowingCandidateSource : ISoftKeyboardCandidateSource
    {
        public ISoftKeyboardCandidateSession CreateSession(SoftKeyboardCandidateHost host)
        {
            return new ThrowingCandidateSession();
        }
    }

    private class ThrowingCandidateSession : ISoftKeyboardCandidateSession
    {
        public virtual Task RefreshAsync(CancellationToken cancellationToken)
        {
            return Task.FromException(new InvalidOperationException("Candidate failure."));
        }

        public Task<SoftKeyboardCandidateOperationResult> CommitAsync(
            SoftKeyboardCandidate candidate,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(SoftKeyboardCandidateOperationResult.NotHandled);
        }

        public Task<SoftKeyboardCandidateOperationResult> MovePageAsync(
            int delta,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(SoftKeyboardCandidateOperationResult.NotHandled);
        }

        public void ReleasePresentation()
        {
        }

        public virtual void Dispose()
        {
        }
    }

    private sealed class CanceledCandidateSource : ISoftKeyboardCandidateSource
    {
        public ISoftKeyboardCandidateSession CreateSession(SoftKeyboardCandidateHost host)
        {
            return new CanceledCandidateSession();
        }
    }

    private sealed class CanceledCandidateSession : ThrowingCandidateSession
    {
        public override Task RefreshAsync(CancellationToken cancellationToken)
        {
            return Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }

    private sealed class AcceptingInputBackend : ISoftKeyboardInputBackend
    {
        public bool SendVirtualKey(Key key, ModifierKeys modifiers)
        {
            return true;
        }

        public bool SendUnicode(string text)
        {
            return true;
        }

        public bool IsHostForeground(IntPtr host)
        {
            return true;
        }
    }

    private sealed class NoOpInputPaneBackend : ISoftKeyboardInputPaneBackend
    {
        public SoftKeyboardSystemKeyboardSuppressionState TryHide(IntPtr host)
        {
            return SoftKeyboardSystemKeyboardSuppressionState.Unavailable;
        }

        public bool TryShow(IntPtr host)
        {
            return false;
        }
    }

    private sealed class EmptyInputMethodBackend : ISoftKeyboardInputMethodBackend
    {
        private readonly SoftKeyboardInputMethod _inputMethod = CreateInputMethod("Platform IME");

        public void Refresh(
            ObservableCollection<SoftKeyboardInputMethod> destination,
            out SoftKeyboardInputMethod? active)
        {
            destination.Clear();
            destination.Add(_inputMethod);
            active = _inputMethod;
        }

        public bool TryActivate(SoftKeyboardInputMethod profile)
        {
            return false;
        }

    }

    private sealed class SettlingInputMethodBackend :
        ISoftKeyboardInputMethodBackend,
        ISoftKeyboardInputModeButtonBackend
    {
        private readonly SoftKeyboardInputMethod _initial = new(
            Guid.NewGuid(), Guid.NewGuid(), 0x0409, "Initial", true
        );

        private SoftKeyboardInputMethod _active;
        private bool _native;

        internal SettlingInputMethodBackend()
        {
            _active = _initial;
            Next = new SoftKeyboardInputMethod(
                Guid.NewGuid(), Guid.NewGuid(), 0x0409, "Next", false
            );
        }

        internal SoftKeyboardInputMethod Next { get; }

        internal void CompleteActivation()
        {
            _native = false;
        }

        public void Refresh(
            ObservableCollection<SoftKeyboardInputMethod> destination,
            out SoftKeyboardInputMethod? active)
        {
            destination.Clear();
            destination.Add(Copy(_initial));
            destination.Add(Copy(Next));
            active = destination.Single(item => item.ClassId == _active.ClassId);
        }

        public bool TryActivate(SoftKeyboardInputMethod profile)
        {
            _active = profile;
            _native = true;
            return true;
        }

        public SoftKeyboardInputModeButton GetInputModeButton(
            SoftKeyboardInputMethod inputMethod)
        {
            var cap = _native ? "中" : inputMethod.ClassId == _initial.ClassId ? "A" : "英";
            return new SoftKeyboardInputModeButton(
                (_native ? "native:" : "direct:") + cap,
                cap,
                _native ? "Native input" : "Direct input"
            );
        }

        public SoftKeyboardInputModeCommandResult TryInvokeInputModeButton(
            SoftKeyboardInputMethod inputMethod, System.Windows.Rect screenBounds)
        {
            return SoftKeyboardInputModeCommandResult.Unavailable;
        }

        private SoftKeyboardInputMethod Copy(SoftKeyboardInputMethod inputMethod)
        {
            return new SoftKeyboardInputMethod(
                inputMethod.ClassId,
                inputMethod.ProfileId,
                inputMethod.LanguageId,
                inputMethod.DisplayName,
                inputMethod.ClassId == _active.ClassId
            );
        }
    }

    private sealed class EmptyCandidateBackend : ISoftKeyboardCandidateBackend
    {
        public void Register(SoftKeyboard keyboard)
        {
        }

        public void Unregister(SoftKeyboard keyboard)
        {
        }

        public void Refresh(SoftKeyboard keyboard)
        {
        }

        public void ReleasePresentation(SoftKeyboard keyboard)
        {
        }

        public bool TryMovePage(SoftKeyboard keyboard, int delta)
        {
            return false;
        }

        public bool TryCommit(SoftKeyboard keyboard, uint index)
        {
            return false;
        }
    }
}
