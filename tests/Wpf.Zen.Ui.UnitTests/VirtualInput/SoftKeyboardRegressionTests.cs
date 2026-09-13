// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NSubstitute;
using NSubstitute.Extensions;
using Wpf.Zen.Ui.VirtualInput.Candidates;
using Wpf.Zen.Ui.VirtualInput.Controls;
using Wpf.Zen.Ui.VirtualInput.Input;
using Wpf.Zen.Ui.VirtualInput.InputMethods;
using Wpf.Zen.Ui.VirtualInput.Layouts;
using Wpf.Zen.Ui.VirtualInput.Layouts.Providers;

namespace Wpf.Zen.Ui.UnitTests.VirtualInput;

/// <summary>
/// Exercises asynchronous source transitions, alternate routing, resource cleanup, and template animation behavior.
/// </summary>
public class SoftKeyboardRegressionTests
{
    /// <summary>
    /// Reentrant source and target transitions invalidate the whole synchronous activation transaction.
    /// </summary>
    [Theory]
    [InlineData("source")]
    [InlineData("close")]
    [InlineData("target")]
    [InlineData("unload")]
    public Task Activation_RejectsReentrantContextChange(string transition)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var keyboard = scope.Keyboard;
            keyboard.InputMethodSource = CreateSource(scope.Method, out var session);
            var next = session;
            session.TryActivate(scope.Method).Returns(_ =>
            {
                switch (transition)
                {
                    case "source":
                        keyboard.InputMethodSource = CreateSource(scope.Method, out next);
                        session.DidNotReceive().Dispose();
                        break;
                    case "close":
                        keyboard.IsOpen = false;
                        break;
                    case "target":
                        keyboard.Target = scope.OtherEditor;
                        break;
                    case "unload":
                        scope.Panel.Children.Remove(keyboard);
                        keyboard.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(transition));
                }

                return true;
            });

            Assert.False(keyboard.TryActivateInputMethod(scope.Method));
            next.DidNotReceive().TrySetState(Arg.Any<InputMethodState>());
            if (transition == "source")
            {
                session.Received(1).Dispose();
                await scope.DrainAsync();
                Assert.Same(scope.Method, keyboard.ActiveInputMethod);
            }
        });
    }

    /// <summary>
    /// Reentrant activation is rejected without interrupting the operation already owning native state.
    /// </summary>
    [Fact]
    public Task Activation_RejectsNestedMutation()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            scope.Keyboard.InputMethodSource = CreateSource(scope.Method, out var session);
            session.TryActivate(scope.Method).Returns(_ =>
            {
                Assert.False(scope.Keyboard.TryActivateInputMethod(scope.Method));
                Assert.False(scope.Keyboard.RefreshInputMethods());
                return true;
            });

            Assert.True(scope.Keyboard.TryActivateInputMethod(scope.Method));
            session.Received(1).TryActivate(scope.Method);
            session.Received(1).TrySetState(InputMethodState.On);
        });
    }

    /// <summary>
    /// Activation requires a successful state write and rejects context changes inside that write.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Activation_RequiresCurrentSuccessfulStateWrite(bool close)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            scope.Keyboard.InputMethodSource = CreateSource(scope.Method, out var session);
            session.TrySetState(InputMethodState.On).Returns(_ =>
            {
                if (close)
                {
                    scope.Keyboard.IsOpen = false;
                }

                return close;
            });

            Assert.False(scope.Keyboard.TryActivateInputMethod(scope.Method));
        });
    }

    /// <summary>
    /// A current activation completes all three source operations synchronously.
    /// </summary>
    [Fact]
    public Task Activation_CompletesCurrentSuccessfulOperation()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            scope.Keyboard.InputMethodSource = CreateSource(scope.Method, out var session);
            Assert.True(scope.Keyboard.TryActivateInputMethod(scope.Method));
            session.Received(1).TrySetState(InputMethodState.On);
        });
    }

    /// <summary>
    /// Catalog bindings receive complete immutable snapshots and retain stable older values.
    /// </summary>
    [Fact]
    public Task InputMethodCatalog_ReplacesSnapshotAtomically()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            scope.Keyboard.InputMethodSource = CreateSource(scope.Method, out var session);
            var previous = scope.Keyboard.InputMethods;
            var observer = new TextBlock();
            observer.SetBinding(
                FrameworkElement.TagProperty,
                new System.Windows.Data.Binding(nameof(SoftKeyboard.InputMethods)) { Source = scope.Keyboard });
            var fresh = new SoftKeyboardInputMethod(Guid.NewGuid(), Guid.NewGuid(), 0x0409, "fresh", true);
            session.GetSnapshot().Returns(new SoftKeyboardInputMethodSnapshot([fresh], fresh));
            Assert.True(scope.Keyboard.RefreshInputMethods());
            Assert.Same(scope.Method, Assert.Single(previous));
            Assert.Same(fresh, Assert.Single(scope.Keyboard.InputMethods));
            Assert.Same(scope.Keyboard.InputMethods, observer.Tag);
        });
    }

    /// <summary>
    /// A failure is observable and a later successful snapshot clears it.
    /// </summary>
    [Fact]
    public Task InputMethodCatalog_ReportsFailureAndRecovers()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            scope.Keyboard.InputMethodSource = CreateSource(scope.Method, out var session);
            var error = new InvalidOperationException("catalog failed");
            session.GetSnapshot().Returns(_ => throw error);
            Assert.False(scope.Keyboard.RefreshInputMethods());
            Assert.Same(error, scope.Keyboard.InputMethodError);
            session.Configure().GetSnapshot().Returns(new SoftKeyboardInputMethodSnapshot([scope.Method], scope.Method));
            Assert.True(scope.Keyboard.RefreshInputMethods());
            Assert.Null(scope.Keyboard.InputMethodError);
        });
    }

    /// <summary>
    /// Source creation may reenter; its returned session must not attach to the replacement source.
    /// </summary>
    [Fact]
    public Task InputMethodCreation_RejectsReentrantReplacement()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var replacement = CreateSource(scope.Method, out var current);
            var source = CreateSource(scope.Method, out var obsolete);
#pragma warning disable IDISP004 // Configure ownership transfer from the substitute factory.
            source.Configure().CreateSession().Returns(_ =>
            {
                scope.Keyboard.InputMethodSource = replacement;
                Assert.Empty(scope.Keyboard.InputMethods);
                return obsolete;
            });
#pragma warning restore IDISP004
            scope.Keyboard.InputMethodSource = source;
            obsolete.Received(1).Dispose();
            obsolete.DidNotReceive().GetSnapshot();
            await scope.DrainAsync();
            Assert.Same(scope.Method, Assert.Single(scope.Keyboard.InputMethods));
            current.DidNotReceive().Dispose();
        });
    }

    /// <summary>
    /// Reentrant candidate registration releases the detached source and permits fresh registration.
    /// </summary>
    [Fact]
    public Task CandidateCreation_RejectsReentrantReplacement()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var source = Substitute.For<ISoftKeyboardCandidateSource>();
            var replacement = Substitute.For<ISoftKeyboardCandidateSource>();
            var obsolete = Substitute.For<ISoftKeyboardCandidateSession>();
            var current = Substitute.For<ISoftKeyboardCandidateSession>();
            current.RefreshAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
#pragma warning disable IDISP004 // Configure sessions whose lifetime belongs to the keyboard.
            replacement.CreateSession(Arg.Any<SoftKeyboardCandidateHost>()).Returns(current);
            source.CreateSession(Arg.Any<SoftKeyboardCandidateHost>()).Returns(_ =>
            {
                scope.Keyboard.CandidateSource = replacement;
                return obsolete;
            });
#pragma warning restore IDISP004
            scope.Keyboard.CandidateSource = source;
            obsolete.Received(1).Dispose();
            await obsolete.DidNotReceive().RefreshAsync(Arg.Any<CancellationToken>());
            await scope.Keyboard.RefreshCandidatesAsync();
            current.DidNotReceive().Dispose();
            Assert.Null(scope.Keyboard.CandidateError);
        });
    }

    /// <summary>
    /// Cleanup callbacks may replace sources, but new registration waits until old cleanup returns.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SourceCleanup_DefersReentrantRegistration(bool candidate)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            if (candidate)
            {
                var source = Substitute.For<ISoftKeyboardCandidateSource>();
                var replacement = Substitute.For<ISoftKeyboardCandidateSource>();
                var obsolete = Substitute.For<ISoftKeyboardCandidateSession>();
                var current = Substitute.For<ISoftKeyboardCandidateSession>();
                obsolete.RefreshAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
                current.RefreshAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
#pragma warning disable IDISP004 // The control owns factory results and cleanup.
                source.CreateSession(Arg.Any<SoftKeyboardCandidateHost>()).Returns(obsolete);
                replacement.CreateSession(Arg.Any<SoftKeyboardCandidateHost>()).Returns(current);
#pragma warning restore IDISP004
                scope.Keyboard.CandidateSource = source;
                obsolete.When(value => value.Dispose()).Do(_ =>
                {
                    scope.Keyboard.CandidateSource = replacement;
#pragma warning disable IDISP004 // Verifies that no registration occurred inside the old cleanup callback.
                    replacement.DidNotReceive().CreateSession(Arg.Any<SoftKeyboardCandidateHost>());
#pragma warning restore IDISP004
                });
                scope.Keyboard.CandidateSource = Substitute.For<ISoftKeyboardCandidateSource>();
                await scope.Keyboard.RefreshCandidatesAsync();
                current.DidNotReceive().Dispose();
                Assert.Null(scope.Keyboard.CandidateError);
            }
            else
            {
                scope.Keyboard.InputMethodSource = CreateSource(scope.Method, out var obsolete);
                var replacement = CreateSource(scope.Method, out var current);
                obsolete.When(value => value.Dispose()).Do(_ =>
                {
                    scope.Keyboard.InputMethodSource = replacement;
#pragma warning disable IDISP004 // Verifies that no source opened inside the old cleanup callback.
                    replacement.DidNotReceive().CreateSession();
#pragma warning restore IDISP004
                });
                scope.Keyboard.InputMethodSource = CreateSource(scope.Method, out _);
                Assert.True(scope.Keyboard.RefreshInputMethods());
                current.DidNotReceive().Dispose();
                Assert.Null(scope.Keyboard.InputMethodError);
            }
        });
    }

    /// <summary>
    /// Cleanup must dispose once even when presentation release fails, preserving both failures when necessary.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CandidateCleanup_DisposesAfterReleaseFailure(bool disposeThrows)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var source = Substitute.For<ISoftKeyboardCandidateSource>();
            var session = Substitute.For<ISoftKeyboardCandidateSession>();
            var releaseError = new InvalidOperationException("release failed");
            var disposeError = new InvalidOperationException("dispose failed");
#pragma warning disable IDISP004 // Configures a substitute; the keyboard owns and disposes the returned session.
            source.CreateSession(Arg.Any<SoftKeyboardCandidateHost>()).Returns(session);
#pragma warning restore IDISP004
            session.RefreshAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            session.When(value => value.ReleasePresentation()).Do(_ => throw releaseError);
            if (disposeThrows)
            {
                session.When(value => value.Dispose()).Do(_ => throw disposeError);
            }

            scope.Keyboard.CandidateSource = source;
            await scope.Keyboard.RefreshCandidatesAsync();
            scope.Keyboard.IsOpen = false;
            session.Received(1).Dispose();
            if (disposeThrows)
            {
                var error = Assert.IsType<AggregateException>(scope.Keyboard.CandidateError);
                Assert.Equal([releaseError, disposeError], error.InnerExceptions);
            }
            else
            {
                Assert.Same(releaseError, scope.Keyboard.CandidateError);
            }
        });
    }

    /// <summary>
    /// An action implementing only the asynchronous alternate interface receives the selected value
    /// without Unicode fallback.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task AlternateRequest_InvokesAsyncOnlyAction(bool failAfterClosing)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var action = Substitute.For<IAsyncSoftKeyboardAlternateKeyAction>();
            var pending = new TaskCompletionSource<bool>();
            action.ExecuteAlternateAsync(
                Arg.Any<SoftKeyboardActionContext>(), "alt", Arg.Any<CancellationToken>()
            ).Returns(pending.Task);
            var key = new SoftKeyboardKey(
                SoftKeyboardKeyKind.Custom, "a", "a", alternates: ["alt"], action: action
            );
            scope.Keyboard.LayoutProvider = new SoftKeyboardLayoutRule(
                _ => true, _ => new SoftKeyboardLayout([key])
            );
            scope.Window.UpdateLayout();
            var cap = Assert.Single(FindDescendants<SoftKeyboardKeyControl>(scope.Keyboard));
            cap.RaiseEvent(new RoutedEventArgs(SoftKeyboardKeyControl.InteractionStartedEvent, cap));
            cap.RaiseEvent(new SoftKeyboardInputRequestedEventArgs(
                SoftKeyboardKeyControl.InputRequestedEvent, cap, key, "alt"
            ));

            Assert.Single(action.ReceivedCalls(), call => call.GetMethodInfo().Name == "ExecuteAlternateAsync");
            Assert.Empty(scope.Input.Text);
            if (failAfterClosing)
            {
                scope.Keyboard.IsOpen = false;
                pending.SetException(new InvalidOperationException("late action failure"));
            }
            else
            {
                pending.SetResult(true);
            }

            await scope.DrainAsync();
            Assert.Null(scope.Keyboard.ExtensionError);
        });
    }

    /// <summary>
    /// The menu consumes the current snapshot immediately, including an initially empty catalog.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task InputMethodMenu_ReadsFreshCatalog(bool initiallyEmpty)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var source = CreateSource(scope.Method, out var session);
            if (initiallyEmpty)
            {
                session.GetSnapshot().Returns(new SoftKeyboardInputMethodSnapshot([], null));
            }

            scope.Keyboard.InputMethodSource = source;
            var fresh = new SoftKeyboardInputMethod(Guid.NewGuid(), Guid.NewGuid(), 0x0409, "fresh", true);
            session.GetSnapshot().Returns(new SoftKeyboardInputMethodSnapshot([fresh], fresh));
            scope.Keyboard.ShowInputMethodMenu();

            var menu = Assert.IsType<ContextMenu>(GetMenu(scope.Keyboard));
            Assert.True(menu.IsOpen);
            var item = Assert.IsType<MenuItem>(Assert.Single(menu.Items.Cast<object>()));
            Assert.Same(fresh, item.CommandParameter);
        });
    }

    /// <summary>
    /// Native reentrancy during catalog discovery cannot reopen a closed or replaced menu request.
    /// </summary>
    [Theory]
    [InlineData("close")]
    [InlineData("source")]
    [InlineData("newer")]
    public Task InputMethodMenu_RejectsReentrantOpen(string transition)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            scope.Keyboard.InputMethodSource = CreateSource(scope.Method, out var session);
            session.GetSnapshot().Returns(call =>
            {
                if (transition == "close")
                {
                    scope.Keyboard.IsOpen = false;
                }
                else if (transition == "source")
                {
                    scope.Keyboard.InputMethodSource = CreateSource(scope.Method, out _);
                }
                else
                {
                    scope.Keyboard.ShowInputMethodMenu();
                }

                return new SoftKeyboardInputMethodSnapshot([scope.Method], scope.Method);
            });

            scope.Keyboard.ShowInputMethodMenu();
            Assert.NotEqual(true, GetMenu(scope.Keyboard)?.IsOpen);
            // Stop provoking reentrancy before queued observation runs.
            session.Configure().GetSnapshot().Returns(new SoftKeyboardInputMethodSnapshot([scope.Method], scope.Method));
        });
    }

    /// <summary>
    /// Opening animation relinquishes opacity so disabling works regardless of transition order or reopening.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task OpeningAnimation_PreservesDisabledOpacity(bool disableBeforeOpening)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            scope.Keyboard.IsOpen = false;
            scope.Keyboard.IsEnabled = !disableBeforeOpening;
            scope.Keyboard.IsOpen = true;
            await Task.Delay(250);
            scope.Keyboard.IsEnabled = false;
            var root = Assert.IsType<Border>(scope.Keyboard.Template.FindName("KeyboardRoot", scope.Keyboard));
            Assert.Equal(0.55, root.Opacity, 3);
            scope.Keyboard.IsOpen = false;
            scope.Keyboard.IsOpen = true;
            await Task.Delay(250);
            Assert.Equal(0.55, root.Opacity, 3);
            scope.Keyboard.IsEnabled = true;
            Assert.Equal(1, root.Opacity, 3);
        });
    }

    /// <summary>
    /// Creates an immediate source whose callbacks can simulate native reentrancy.
    /// </summary>
    private static ISoftKeyboardInputMethodSource CreateSource(
        SoftKeyboardInputMethod method,
        out ISoftKeyboardInputMethodSession session)
    {
        var source = Substitute.For<ISoftKeyboardInputMethodSource>();
        session = Substitute.For<ISoftKeyboardInputMethodSession>();
#pragma warning disable IDISP004 // Configures a substitute; the keyboard owns and disposes the returned session.
        source.CreateSession().Returns(session);
#pragma warning restore IDISP004
        session.GetSnapshot().Returns(new SoftKeyboardInputMethodSnapshot([method], method));
        session.GetState().Returns(InputMethodState.On);
        session.TrySetState(Arg.Any<InputMethodState>()).Returns(true);
        session.TryActivate(Arg.Any<SoftKeyboardInputMethod>()).Returns(true);
        return source;
    }

    private static ContextMenu? GetMenu(SoftKeyboard keyboard)
    {
        return (ContextMenu?)typeof(SoftKeyboard).GetField(
            "_inputMethodMenu", BindingFlags.Instance | BindingFlags.NonPublic
        )!.GetValue(keyboard);
    }

    private static IEnumerable<T> FindDescendants<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T result)
            {
                yield return result;
            }

            foreach (var descendant in FindDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }
}
