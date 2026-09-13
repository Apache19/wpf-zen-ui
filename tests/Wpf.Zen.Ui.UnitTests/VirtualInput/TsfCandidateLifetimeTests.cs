// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using NSubstitute;
using Wpf.Zen.Ui.VirtualInput.Candidates;
using Wpf.Zen.Ui.VirtualInput.Controls;
using Wpf.Zen.Ui.VirtualInput.Extensibility;
using Wpf.Zen.Ui.VirtualInput.Extensions;
using Wpf.Zen.Ui.VirtualInput.Input;
using Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

namespace Wpf.Zen.Ui.UnitTests.VirtualInput;

/// <summary>
/// Drives real TSF sink callbacks against managed COM-contract doubles to exercise candidate lifetime races.
/// </summary>
public class TsfCandidateLifetimeTests
{
    /// <summary>
    /// A number-key commit may be acknowledged by element end, while caller cancellation still rejects the operation.
    /// </summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public Task ExtensionAcknowledgement_AllowsElementEndButHonorsCallerCancellation(int cancellation, bool wrapToken)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var extension = Substitute.For<ISoftKeyboardTsfCandidateBehavior>();
            var provider = Substitute.For<ISoftKeyboardExtensionProvider>();
            provider.GetExtensions(Arg.Any<SoftKeyboardExtensionContext>()).Returns(
                new[] { new SoftKeyboardExtension(candidateBehavior: extension) }
            );
            scope.Keyboard.ExtensionProvider = provider;
            using var extensionCancellation = new CancellationTokenSource();
            extension.CommitAsync(
                Arg.Any<SoftKeyboardTsfCandidateContext>(), Arg.Any<SoftKeyboardCandidate>(), Arg.Any<CancellationToken>()
            ).Returns(async call =>
            {
                var context = call.Arg<SoftKeyboardTsfCandidateContext>();
                Assert.True(context.TrySendKey(Key.D1));
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                    call.Arg<CancellationToken>(), extensionCancellation.Token
                );
                return await context.WaitForSnapshotChangeAsync(
                    TimeSpan.FromSeconds(2), wrapToken ? linked.Token : extensionCancellation.Token
                )
                    ? SoftKeyboardCandidateOperationResult.Success
                    : SoftKeyboardCandidateOperationResult.NotHandled;
            });
            using var native = new CandidateHarness(scope.Keyboard);
            using var callerCancellation = new CancellationTokenSource();
            native.Begin(1);
            var operation = native.Session.TryCommitAsync(
                scope.Keyboard, scope.Keyboard.Candidates[0], callerCancellation.Token
            );
            Assert.False(operation.IsCompleted);
            if (cancellation == 1)
            {
                callerCancellation.Cancel();
            }
            else if (cancellation == 2)
            {
                extensionCancellation.Cancel();
            }

            native.End(1);
            if (cancellation == 1)
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await operation);
            }
            else if (cancellation == 2)
            {
                Assert.False((await operation).Succeeded);
                Assert.Empty(native.First.Selections);
                Assert.Equal(0, native.First.Finalizations);
            }
            else
            {
                Assert.True((await operation).Succeeded);
            }
        });
    }

    /// <summary>
    /// Late extension completion must neither select a later element nor overwrite its presentation diagnostics.
    /// </summary>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public Task PendingOperation_ExpiresWithOriginalElement(bool movePage, bool replace, bool fail)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var extension = Substitute.For<ISoftKeyboardTsfCandidateBehavior>();
            var provider = Substitute.For<ISoftKeyboardExtensionProvider>();
            provider.GetExtensions(Arg.Any<SoftKeyboardExtensionContext>()).Returns(
                new[] { new SoftKeyboardExtension(candidateBehavior: extension) }
            );
            scope.Keyboard.ExtensionProvider = provider;
            var pending = new TaskCompletionSource<SoftKeyboardCandidateOperationResult>();
            CancellationToken operationToken = default;
            SoftKeyboardTsfCandidateContext? operationContext = null;
            extension.CommitAsync(
                Arg.Any<SoftKeyboardTsfCandidateContext>(), Arg.Any<SoftKeyboardCandidate>(), Arg.Any<CancellationToken>()
            ).Returns(async call =>
            {
                operationContext = call.Arg<SoftKeyboardTsfCandidateContext>();
                operationToken = call.Arg<CancellationToken>();
                return await pending.Task;
            });
            extension.MovePageAsync(
                Arg.Any<SoftKeyboardTsfCandidateContext>(), Arg.Any<int>(), Arg.Any<CancellationToken>()
            ).Returns(async call =>
            {
                operationContext = call.Arg<SoftKeyboardTsfCandidateContext>();
                operationToken = call.Arg<CancellationToken>();
                return await pending.Task;
            });
            using var native = new CandidateHarness(scope.Keyboard);
            native.Begin(1);
            var operation = movePage
                ? native.Session.TryMovePageAsync(scope.Keyboard, 1, CancellationToken.None)
                : native.Session.TryCommitAsync(scope.Keyboard, scope.Keyboard.Candidates[0], CancellationToken.None);
            Assert.False(operation.IsCompleted);
            Assert.NotNull(operationContext);

            native.End(1);
            if (replace)
            {
                native.Begin(2);
            }

            Assert.False(operationToken.IsCancellationRequested);
            Assert.Throws<OperationCanceledException>(() => operationContext.TrySendKey(Key.D1));
            if (fail)
            {
                pending.SetException(new InvalidOperationException("late extension failure"));
            }
            else
            {
                pending.SetResult(SoftKeyboardCandidateOperationResult.NotHandled);
            }

            Assert.False((await operation).Succeeded);
            Assert.Empty(native.First.Selections);
            Assert.Empty(native.Second.Selections);
            Assert.Equal(0, native.First.Finalizations);
            Assert.Equal(0, native.Second.Finalizations);
            Assert.Null(scope.Keyboard.CandidateError);
            Assert.Equal(
                replace ? SoftKeyboardCandidatePresentationState.Embedded : SoftKeyboardCandidatePresentationState.None,
                scope.Keyboard.CandidatePresentationState
            );
        });
    }

    /// <summary>
    /// Caller cancellation prevents a late extension failure from changing a still-active native element.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CallerCancellation_RejectsLateExtensionFailure(bool movePage)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var extension = Substitute.For<ISoftKeyboardTsfCandidateBehavior>();
            var provider = Substitute.For<ISoftKeyboardExtensionProvider>();
            provider.GetExtensions(Arg.Any<SoftKeyboardExtensionContext>()).Returns(
                new[] { new SoftKeyboardExtension(candidateBehavior: extension) });
            scope.Keyboard.ExtensionProvider = provider;
            var pending = new TaskCompletionSource<SoftKeyboardCandidateOperationResult>();
            extension.CommitAsync(
                Arg.Any<SoftKeyboardTsfCandidateContext>(), Arg.Any<SoftKeyboardCandidate>(), Arg.Any<CancellationToken>()
            ).Returns(async _ => await pending.Task);
            extension.MovePageAsync(
                Arg.Any<SoftKeyboardTsfCandidateContext>(), Arg.Any<int>(), Arg.Any<CancellationToken>()
            ).Returns(async _ => await pending.Task);
            using var native = new CandidateHarness(scope.Keyboard);
            using var cancellation = new CancellationTokenSource();
            native.Begin(1);
            var operation = movePage
                ? native.Session.TryMovePageAsync(scope.Keyboard, 1, cancellation.Token)
                : native.Session.TryCommitAsync(scope.Keyboard, scope.Keyboard.Candidates[0], cancellation.Token);
            cancellation.Cancel();
            pending.SetException(new InvalidOperationException("late failure"));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await operation);
            Assert.Null(scope.Keyboard.CandidateError);
            Assert.Empty(native.First.Selections);
            Assert.Equal(0, native.First.Finalizations);
            Assert.Equal(SoftKeyboardCandidatePresentationState.Embedded, scope.Keyboard.CandidatePresentationState);
        });
    }

    /// <summary>
    /// Synchronous sink reentrancy during selection must not finalize even when the same COM object and id are reused.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Selection_RevalidatesElementGeneration(bool reuseElement)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            using var native = new CandidateHarness(scope.Keyboard);
            native.Begin(1);
            native.First.OnSelect = () =>
            {
                native.End(1);
                native.Begin(reuseElement ? 1U : 2U);
            };
            var result = await native.Session.TryCommitAsync(
                scope.Keyboard, scope.Keyboard.Candidates[0], CancellationToken.None
            );
            Assert.False(result.Succeeded);
            Assert.Single(native.First.Selections);
            Assert.Equal(0, native.First.Finalizations);
            Assert.Equal(0, native.Second.Finalizations);
            Assert.Equal(SoftKeyboardCandidatePresentationState.Embedded, scope.Keyboard.CandidatePresentationState);
        });
    }

    /// <summary>
    /// Current elements still allow paging and committing, including normal EndUIElement during finalization.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task NativeOperation_CompletesForCurrentElement(bool movePage)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            using var native = new CandidateHarness(scope.Keyboard);
            native.Begin(1);
            native.First.OnFinalize = () => native.End(1);
            var result = movePage
                ? await native.Session.TryMovePageAsync(scope.Keyboard, 1, CancellationToken.None)
                : await native.Session.TryCommitAsync(scope.Keyboard, scope.Keyboard.Candidates[0], CancellationToken.None);
            Assert.True(result.Succeeded);
            Assert.Equal(movePage ? 1U : 0U, Assert.Single(native.First.Selections));
            Assert.Equal(movePage ? 0 : 1, native.First.Finalizations);
        });
    }

    /// <summary>
    /// Replaces only the COM transport, leaving registration, sink routing, page parsing, and operation checks intact.
    /// </summary>
    private sealed class CandidateHarness : IDisposable
    {
        private readonly ISoftKeyboardTsfUIElementSink _sink;

        internal CandidateHarness(SoftKeyboard keyboard)
        {
            var document = new object();
            First = new CandidateElement(document, "first");
            Second = new CandidateElement(document, "second");
            Session = new TsfCandidateSession(Dispatcher.CurrentDispatcher);
            SetField("_reflectionUnavailable", true);
            SetField("_threadManager", new ThreadManager(document));
            SetField("_elementManager", new ElementManager(First, Second));
            Session.Register(keyboard);
            _sink = Session;
        }

        internal TsfCandidateSession Session { get; }
        internal CandidateElement First { get; }
        internal CandidateElement Second { get; }

        internal void Begin(uint id)
        {
            var show = 1;
            Assert.Equal(0, _sink.BeginUIElement(id, ref show));
            Assert.Equal(0, show);
        }

        internal void End(uint id)
        {
            Assert.Equal(0, _sink.EndUIElement(id));
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Session.Dispose();
        }

        private void SetField(string name, object value)
        {
            typeof(TsfCandidateSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(Session, value);
        }
    }

    private sealed class ThreadManager(object document) : ITfThreadMgr
    {
        /// <inheritdoc />
        public int Activate(out uint clientId)
        {
            clientId = 1;
            return 0;
        }

        /// <inheritdoc />
        public int Deactivate()
        {
            return 0;
        }

        /// <inheritdoc />
        public int CreateDocumentMgr(out object? documentManager)
        {
            documentManager = document;
            return 0;
        }

        /// <inheritdoc />
        public int EnumDocumentMgrs(out object? enumerator)
        {
            enumerator = null;
            return -1;
        }

        /// <inheritdoc />
        public int GetFocus(out object? documentManager)
        {
            documentManager = document;
            return 0;
        }
    }

    private sealed class ElementManager(CandidateElement first, CandidateElement second) : ITfUIElementMgr
    {
        /// <inheritdoc />
        public int BeginUIElement(object element, ref int show, out uint elementId)
        {
            elementId = 1;
            return 0;
        }

        /// <inheritdoc />
        public int UpdateUIElement(uint elementId)
        {
            return 0;
        }

        /// <inheritdoc />
        public int EndUIElement(uint elementId)
        {
            return 0;
        }

        /// <inheritdoc />
        public int GetUIElement(uint elementId, out object? element)
        {
            element = elementId == 1 ? first : second;
            return 0;
        }

        /// <inheritdoc />
        public int EnumUIElements(out object? enumerator)
        {
            enumerator = null;
            return -1;
        }
    }

    private sealed class CandidateElement(object document, string name)
        : ITfCandidateListUIElement, ITfCandidateListUIElementBehavior
    {
        private uint _selection;

        internal List<uint> Selections { get; } = [];
        internal int Finalizations { get; private set; }
        internal Action? OnSelect { get; set; }
        internal Action? OnFinalize { get; set; }

        /// <inheritdoc />
        public int GetDescription(out string? description)
        {
            description = name;
            return 0;
        }

        /// <inheritdoc />
        public int GetGuid(out Guid guid)
        {
            guid = Guid.Empty;
            return 0;
        }

        /// <inheritdoc />
        public int Show(int show)
        {
            return 0;
        }

        /// <inheritdoc />
        public int IsShown(out int show)
        {
            show = 0;
            return 0;
        }

        /// <inheritdoc />
        public int GetUpdatedFlags(out uint flags)
        {
            flags = 0;
            return 0;
        }

        /// <inheritdoc />
        public int GetDocumentMgr(out object? documentManager)
        {
            documentManager = document;
            return 0;
        }

        /// <inheritdoc />
        public int GetCount(out uint count)
        {
            count = 2;
            return 0;
        }

        /// <inheritdoc />
        public int GetSelection(out uint index)
        {
            index = _selection;
            return 0;
        }

        /// <inheritdoc />
        public int GetString(uint index, out string? text)
        {
            text = name + index;
            return 0;
        }

        /// <inheritdoc />
        public int GetPageIndex(IntPtr indices, uint size, out uint pageCount)
        {
            pageCount = 2;
            if (size < pageCount)
            {
                return -1;
            }

            Marshal.WriteInt32(indices, 0, 0);
            Marshal.WriteInt32(indices, sizeof(uint), 1);
            return 0;
        }

        /// <inheritdoc />
        public int SetPageIndex(IntPtr indices, uint pageCount)
        {
            return 0;
        }

        /// <inheritdoc />
        public int GetCurrentPage(out uint page)
        {
            page = _selection;
            return 0;
        }

        /// <inheritdoc />
        public int SetSelection(uint index)
        {
            _selection = index;
            Selections.Add(index);
            OnSelect?.Invoke();
            return 0;
        }

        /// <inheritdoc />
        public int FinalizeCandidate()
        {
            Finalizations++;
            OnFinalize?.Invoke();
            return 0;
        }

        /// <inheritdoc />
        public int Abort()
        {
            return 0;
        }
    }
}
