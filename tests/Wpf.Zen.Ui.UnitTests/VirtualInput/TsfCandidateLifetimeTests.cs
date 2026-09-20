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
using Wpf.Zen.Ui.VirtualInput.Extensions.InputMethods;
using Wpf.Zen.Ui.VirtualInput.Input;
using Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

namespace Wpf.Zen.Ui.UnitTests.VirtualInput;

/// <summary>
/// Drives real TSF sink callbacks against managed COM-contract doubles to exercise candidate
/// lifetime races.
/// </summary>
public class TsfCandidateLifetimeTests
{
    /// <summary>
    /// Neither discovery path may embed an unknown service based on optional COM capabilities.
    /// Missing, undecided, empty, and failing extension policies all preserve native candidates.
    /// </summary>
    /// <param name="enumerated">Whether discovery starts through enumeration.</param>
    /// <param name="policy">The extension configuration under test.</param>
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    [InlineData(false, 3)]
    [InlineData(true, 3)]
    [InlineData(false, 4)]
    [InlineData(true, 4)]
    public Task Candidates_RequireExplicitExtensionAdmission(bool enumerated, int policy)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            if (policy == 1)
            {
                scope.Keyboard.ExtensionProvider = DefaultSoftKeyboardExtensionProvider.Default;
            }
            else if (policy >= 2)
            {
                var extension = Substitute.For<ISoftKeyboardTsfCandidateBehavior>();
                extension.GetPresentation(Arg.Any<SoftKeyboardTsfCandidateContext>()).Returns(_ =>
                {
                    if (policy == 4)
                    {
                        throw new InvalidOperationException("Candidate policy failed.");
                    }

                    return SoftKeyboardTsfCandidatePresentation.Default;
                });
                var provider = Substitute.For<ISoftKeyboardExtensionProvider>();
                provider.GetExtensions(Arg.Any<SoftKeyboardExtensionContext>()).Returns(
                    policy == 2 ? [] : new[] { new SoftKeyboardExtension(candidateBehavior: extension) });
                scope.Keyboard.ExtensionProvider = provider;
            }

            using var native = new CandidateHarness(scope.Keyboard);
            native.Manager.ElementFactory = () => native.First;
            var sink = (ISoftKeyboardTsfUIElementSink)native.Session;
            if (!enumerated)
            {
                var show = 0;
                Assert.Equal(0, sink.BeginUIElement(1, ref show));
                Assert.Equal(1, show);
            }

            for (var update = 0; update < 3; update++)
            {
                sink.UpdateUIElement(1);
                native.Session.Refresh(scope.Keyboard);
                Assert.Equal(SoftKeyboardCandidatePresentationState.NativeFallback,
                    scope.Keyboard.CandidatePresentationState);
                Assert.Empty(scope.Keyboard.Candidates);
                Assert.Equal(0, native.First.ContentReads);
                Assert.DoesNotContain(0, native.First.ShowCalls);
            }

            Assert.Empty(native.First.Selections);
            Assert.Equal(0, native.First.Finalizations);
            sink.EndUIElement(1);
        });
    }

    /// <summary>
    /// WeChat retains its native candidate surface through both sink callbacks and enumeration,
    /// even when it advertises the optional integrated-candidate interface.
    /// </summary>
    /// <param name="enumerated">Whether discovery starts through enumeration.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task WeChatCandidates_KeepNativePresentation(bool enumerated)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope(KnownTextServices.WeChat);
            await scope.DrainAsync();
            scope.Keyboard.ExtensionProvider = DefaultSoftKeyboardExtensionProvider.Default;
            using var native = new CandidateHarness(scope.Keyboard);
            native.Manager.ElementFactory = () => native.First;
            var sink = (ISoftKeyboardTsfUIElementSink)native.Session;
            if (!enumerated)
            {
                var show = 0;
                Assert.Equal(0, sink.BeginUIElement(1, ref show));
                Assert.Equal(1, show);
            }

            for (var update = 0; update < 3; update++)
            {
                sink.UpdateUIElement(1);
                native.Session.Refresh(scope.Keyboard);
                Assert.Equal(SoftKeyboardCandidatePresentationState.NativeFallback,
                    scope.Keyboard.CandidatePresentationState);
                Assert.Empty(scope.Keyboard.Candidates);
                Assert.Equal(0, native.First.ContentReads);
                Assert.DoesNotContain(0, native.First.ShowCalls);
            }

            var result = await native.Session.TryMovePageAsync(
                scope.Keyboard, 1, CancellationToken.None);
            Assert.False(result.Handled);
            Assert.Empty(native.First.Selections);
            Assert.Equal(0, native.First.Finalizations);
            sink.EndUIElement(1);
        });
    }

    /// <summary>
    /// An accepted Finalize can schedule a native edit session. Preserve that transaction until
    /// its callback commits the selected word; forced termination here would commit raw pre-edit.
    /// </summary>
    /// <param name="endUi">Whether the TIP ends candidate UI before its edit session runs.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Finalize_DoesNotInterruptDeferredNativeCommit(bool endUi)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = CreateEmbeddedScope();
            await scope.DrainAsync();
            using var native = new CandidateHarness(scope.Keyboard);
            var composition = native.Document.Context;
            composition.Type("hf");
            native.Begin(1);
            var committed = string.Empty;
            native.First.OnFinalize = () =>
            {
                if (endUi)
                {
                    native.End(1);
                }

                scope.Keyboard.Dispatcher.BeginInvoke(new Action(() =>
                {
                    Assert.Equal("hf", composition.Preedit);
                    committed = "恢复";
                    composition.Current = null;
                    composition.Preedit = string.Empty;
                    if (!endUi)
                    {
                        native.End(1);
                    }
                }));
            };

            var result = await native.Session.TryCommitAsync(
                scope.Keyboard, scope.Keyboard.Candidates[0], CancellationToken.None);
            Assert.True(result.Succeeded);
            Assert.Empty(committed);
            Assert.Equal("hf", composition.Preedit);
            if (!endUi)
            {
                Assert.NotEmpty(scope.Keyboard.Candidates);
            }

            await scope.DrainAsync();
            Assert.Equal("恢复", committed);
            Assert.Equal(1, native.First.Finalizations);
            Assert.Empty(scope.Keyboard.Candidates);
            composition.Type("cs");
            Assert.Equal("cs", composition.Preedit);
        });
    }

    /// <summary>
    /// Native Finalize can already end the composition. Verification then permits UI cleanup.
    /// </summary>
    [Fact]
    public Task Finalize_AlreadyEndedCompositionPermitsCleanup()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = CreateEmbeddedScope();
            await scope.DrainAsync();
            using var native = new CandidateHarness(scope.Keyboard);
            native.Document.Context.Type("hf");
            native.Begin(1);
            native.First.OnFinalize = () => native.Document.Context.Current = null;

            Assert.True((await native.Session.TryCommitAsync(
                scope.Keyboard, scope.Keyboard.Candidates[0], CancellationToken.None)).Succeeded);
            Assert.Empty(scope.Keyboard.Candidates);
        });
    }

    /// <summary>
    /// A new composition must retain its candidates even when its WPF Start has not arrived yet.
    /// </summary>
    [Fact]
    public Task Finalize_PreservesReplacementNativeComposition()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = CreateEmbeddedScope();
            await scope.DrainAsync();
            using var native = new CandidateHarness(scope.Keyboard);
            var composition = native.Document.Context;
            composition.Type("hf");
            native.Begin(1);
            var replacement = new CandidateCompositionView();
            native.First.OnFinalize = () =>
            {
                composition.Current = replacement;
                composition.Preedit = "cs";
            };

            Assert.True((await native.Session.TryCommitAsync(
                scope.Keyboard, scope.Keyboard.Candidates[0], CancellationToken.None)).Succeeded);
            Assert.Same(replacement, composition.Current);
            Assert.Equal("cs", composition.Preedit);
            Assert.NotEmpty(scope.Keyboard.Candidates);
        });
    }

    /// <summary>
    /// An unobservable native state cannot confirm completion, even after Finalize returned S_OK.
    /// </summary>
    [Fact]
    public Task Finalize_EnumerationFailureDoesNotRetireCandidates()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = CreateEmbeddedScope();
            await scope.DrainAsync();
            using var native = new CandidateHarness(scope.Keyboard);
            native.Document.Context.Type("hf");
            native.Begin(1);
            native.First.OnFinalize = () => native.Document.Context.OnEnumerate = () =>
            {
                throw new COMException("Enumeration unavailable.");
            };

            var result = await native.Session.TryCommitAsync(
                scope.Keyboard, scope.Keyboard.Candidates[0], CancellationToken.None);
            Assert.True(result.Handled);
            Assert.False(result.Succeeded);
            Assert.NotEmpty(scope.Keyboard.Candidates);
        });
    }

    /// <summary>
    /// A focus change inside Finalize preserves the previous editor's native transaction.
    /// </summary>
    [Fact]
    public Task Finalize_FocusedDocumentChangePreservesNativeTransaction()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = CreateEmbeddedScope();
            await scope.DrainAsync();
            using var native = new CandidateHarness(scope.Keyboard);
            native.Document.Context.Type("hf");
            native.Begin(1);
            native.First.OnFinalize = () => typeof(TsfCandidateSession)
                .GetField("_threadManager", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(native.Session, new ThreadManager(new CandidateCompositionDocument()));

            Assert.True((await native.Session.TryCommitAsync(
                scope.Keyboard, scope.Keyboard.Candidates[0], CancellationToken.None)).Succeeded);
            Assert.Equal("hf", native.Document.Context.Preedit);
        });
    }

    /// <summary>
    /// Verified native completion closes presentation even if a TIP retains the same callable
    /// element, strings, and manager membership, whether or not it sends EndUIElement.
    /// </summary>
    /// <param name="enumerated">Whether discovery happened without a Begin callback.</param>
    /// <param name="notifyEnd">Whether Finalize synchronously sends an End callback.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public Task Finalize_RetiresCandidateAndRejectsRediscovery(bool enumerated, bool notifyEnd)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = CreateEmbeddedScope();
            await scope.DrainAsync();
            using var native = new CandidateHarness(scope.Keyboard);
            native.Manager.ElementFactory = () => native.First;
            if (notifyEnd)
            {
                native.First.OnFinalize = () => native.End(1);
            }

            if (enumerated)
            {
                native.Session.Refresh(scope.Keyboard);
            }
            else
            {
                native.Begin(1);
            }

            var candidate = Assert.Single(scope.Keyboard.Candidates);
            var result = await native.Session.TryCommitAsync(scope.Keyboard, candidate, CancellationToken.None);
            Assert.True(result.Succeeded);
            Assert.Empty(scope.Keyboard.Candidates);
            Assert.False(scope.Keyboard.HasCandidates);
            Assert.Equal(0U, scope.Keyboard.CandidatePageNumber);
            Assert.Equal(0U, scope.Keyboard.CandidatePageCount);
            Assert.Null(scope.Keyboard.SelectedCandidateIndex);
            Assert.False(scope.Keyboard.CanGoToNextCandidatePage);
            Assert.Equal(SoftKeyboardCandidatePresentationState.None, scope.Keyboard.CandidatePresentationState);
            Assert.DoesNotContain(1, native.First.ShowCalls);

            // A stale Update, unrelated End, timer callback, or public refresh must not resurrect
            // the finalized element. Repeated clicks must not finalize the same list again.
            ((ISoftKeyboardTsfUIElementSink)native.Session).UpdateUIElement(1);
            native.End(42);
            typeof(TsfCandidateSession).GetMethod(
                "OnCandidatePollTimerTick", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(native.Session, [null, EventArgs.Empty]);
            native.Session.Refresh(scope.Keyboard);
            await scope.DrainAsync();
            Assert.Empty(scope.Keyboard.Candidates);
            Assert.False((await native.Session.TryCommitAsync(
                scope.Keyboard, candidate, CancellationToken.None)).Succeeded);
            Assert.Equal(1, native.First.Finalizations);
            Assert.False(((DispatcherTimer)typeof(TsfCandidateSession)
                .GetField("_candidatePollTimer", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(native.Session)!).IsEnabled);
        });
    }

    /// <summary>
    /// A later composition or an explicit Begin can reuse the exact COM element and candidate text.
    /// Suppression is scoped to a candidate lifetime rather than to its string values.
    /// </summary>
    /// <param name="begin">Whether TSF supplies a fresh Begin instead of only a WPF start.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task FinalizedElement_CanBeReusedByNextComposition(bool begin)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = CreateEmbeddedScope();
            await scope.DrainAsync();
            using var native = new CandidateHarness(scope.Keyboard);
            native.Manager.ElementFactory = () => native.First;
            native.Begin(1);
            Assert.True((await native.Session.TryCommitAsync(
                scope.Keyboard, scope.Keyboard.Candidates[0], CancellationToken.None)).Succeeded);

            if (begin)
            {
                native.Begin(1);
            }
            else
            {
                ObserveCompositionStart(scope);
                native.Session.Refresh(scope.Keyboard);
            }

            Assert.Equal("first0", Assert.Single(scope.Keyboard.Candidates).Text);
            Assert.True((await native.Session.TryCommitAsync(
                scope.Keyboard, scope.Keyboard.Candidates[0], CancellationToken.None)).Succeeded);
            Assert.Equal(2, native.First.Finalizations);
        });
    }

    /// <summary>
    /// Finalize can synchronously begin another candidate list or composition. Its success only
    /// closes the captured generation and cannot erase that replacement, including COM/id reuse.
    /// </summary>
    /// <param name="replacement">A different element, reused element, or new composition.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public Task Finalize_ReentrantReplacementKeepsNewCandidates(int replacement)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = CreateEmbeddedScope();
            await scope.DrainAsync();
            using var native = new CandidateHarness(scope.Keyboard);
            native.Begin(1);
            native.First.OnFinalize = () =>
            {
                if (replacement == 2)
                {
                    ObserveCompositionStart(scope);
                }
                else
                {
                    native.End(1);
                    native.Begin(replacement == 0 ? 2U : 1U);
                }
            };

            Assert.True((await native.Session.TryCommitAsync(
                scope.Keyboard, scope.Keyboard.Candidates[0], CancellationToken.None)).Succeeded);
            Assert.Equal(replacement == 0 ? "second0" : "first0",
                Assert.Single(scope.Keyboard.Candidates).Text);
            Assert.Equal(SoftKeyboardCandidatePresentationState.Embedded, scope.Keyboard.CandidatePresentationState);
        });
    }

    /// <summary>
    /// A failing or noncommittal HRESULT must not discard a list that remains available for retry.
    /// </summary>
    /// <param name="result">The result returned by the native finalization method.</param>
    [Theory]
    [InlineData(unchecked((int)0x80004005))]
    [InlineData(1)]
    public Task FinalizeWithoutSuccess_KeepsCandidates(int result)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = CreateEmbeddedScope();
            await scope.DrainAsync();
            using var native = new CandidateHarness(scope.Keyboard);
            native.Begin(1);
            native.First.FinalizeResult = result;
            Assert.False((await native.Session.TryCommitAsync(
                scope.Keyboard, scope.Keyboard.Candidates[0], CancellationToken.None)).Succeeded);
            Assert.NotEmpty(scope.Keyboard.Candidates);
        });
    }

    /// <summary>
    /// Reproduces the composition start observed independently of a native candidate callback.
    /// </summary>
    private static void ObserveCompositionStart(SoftKeyboardRegressionScope scope)
    {
        var composition = new TextComposition(InputManager.Current, scope.Editor, string.Empty);
        scope.Keyboard.OnSessionTextComposition(new TextCompositionEventArgs(Keyboard.PrimaryDevice, composition)
        {
            RoutedEvent = TextCompositionManager.PreviewTextInputStartEvent,
            Source = scope.Editor,
        });
    }

    /// <summary>
    /// Extension acknowledgement has the same cleanup boundary as native Finalize, but a late
    /// acknowledgement cannot retire a replacement list that appeared while the extension awaited.
    /// </summary>
    /// <param name="replace">Whether a new list starts before acknowledgement completes.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ExtensionCommitSuccess_RetiresOnlyItsOwnElement(bool replace)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = CreateEmbeddedScope();
            await scope.DrainAsync();
            var extension = Substitute.For<ISoftKeyboardTsfCandidateBehavior>();
            extension.GetPresentation(Arg.Any<SoftKeyboardTsfCandidateContext>()).Returns(
                SoftKeyboardTsfCandidatePresentation.Embedded);
            var provider = Substitute.For<ISoftKeyboardExtensionProvider>();
            provider.GetExtensions(Arg.Any<SoftKeyboardExtensionContext>()).Returns(
                new[] { new SoftKeyboardExtension(candidateBehavior: extension) });
            scope.Keyboard.ExtensionProvider = provider;
            var pending = new TaskCompletionSource<SoftKeyboardCandidateOperationResult>();
            extension.CommitAsync(
                Arg.Any<SoftKeyboardTsfCandidateContext>(), Arg.Any<SoftKeyboardCandidate>(),
                Arg.Any<CancellationToken>()
            ).Returns(async _ => await pending.Task);
            using var native = new CandidateHarness(scope.Keyboard);
            native.Begin(1);
            var operation = native.Session.TryCommitAsync(
                scope.Keyboard, scope.Keyboard.Candidates[0], CancellationToken.None);
            Assert.False(operation.IsCompleted);
            if (replace)
            {
                native.End(1);
                native.Begin(2);
            }

            pending.SetResult(SoftKeyboardCandidateOperationResult.Success);
            Assert.True((await operation).Succeeded);
            Assert.Equal(0, native.First.Finalizations);
            Assert.Equal(0, native.Second.Finalizations);
            if (replace)
            {
                Assert.Equal("second0", Assert.Single(scope.Keyboard.Candidates).Text);
            }
            else
            {
                Assert.Empty(scope.Keyboard.Candidates);
                native.Manager.ElementFactory = () => native.First;
                native.Session.Refresh(scope.Keyboard);
                Assert.Empty(scope.Keyboard.Candidates);
            }
        });
    }

    /// <summary>
    /// A due poll or deferred End reconciliation replaces stale candidates before queued input.
    /// </summary>
    /// <param name="poll">Whether to trigger polling instead of an End notification.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CandidateReconciliation_PrecedesPendingInput(bool poll)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = CreateEmbeddedScope();
            await scope.DrainAsync();
            using var native = new CandidateHarness(scope.Keyboard);
            native.Manager.ElementFactory = () => native.First;
            native.Session.Refresh(scope.Keyboard);
            Assert.Equal("first0", Assert.Single(scope.Keyboard.Candidates).Text);

            var timer = (DispatcherTimer)typeof(TsfCandidateSession)
                .GetField("_candidatePollTimer", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(native.Session)!;
            timer.Stop();
            native.Manager.ElementFactory = () => native.Second;

            var input = Dispatcher.CurrentDispatcher.InvokeAsync(
                () => Assert.Equal("second0", Assert.Single(scope.Keyboard.Candidates).Text),
                DispatcherPriority.Input);

            if (poll)
            {
                // Make exactly one poll due without depending on wall-clock timing.
                timer.Interval = TimeSpan.Zero;
                timer.Tick += (_, _) => timer.Stop();
                timer.Start();
            }
            else
            {
                native.End(42);
            }

            Assert.Equal("first0", Assert.Single(scope.Keyboard.Candidates).Text);
            await input;
        });
    }

    /// <summary>
    /// A synchronous TIP callback may replace an element while its old snapshot is being read.
    /// Neither old data nor an old COM failure may overwrite the replacement's presentation.
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task SnapshotRead_ReentrantReplacementKeepsNewCandidates(bool readString, bool fail)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = CreateEmbeddedScope();
            await scope.DrainAsync();
            using var native = new CandidateHarness(scope.Keyboard);
            Action replace = () =>
            {
                native.End(1);
                native.Begin(2);
                if (fail)
                {
                    throw new COMException("The old element ended.");
                }
            };
            if (readString)
            {
                native.First.OnString = replace;
            }
            else
            {
                native.First.OnCount = replace;
            }

            native.Begin(1);
            Assert.Equal("second0", Assert.Single(scope.Keyboard.Candidates).Text);
            Assert.Equal(SoftKeyboardCandidatePresentationState.Embedded,
                scope.Keyboard.CandidatePresentationState);
        });
    }

    /// <summary>
    /// S_FALSE leaves the output index undefined and must publish an unselected candidate list.
    /// </summary>
    [Fact]
    public Task CandidateWithoutSelection_DoesNotPublishTheOutputIndex()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = CreateEmbeddedScope();
            await scope.DrainAsync();
            using var native = new CandidateHarness(scope.Keyboard);
            native.First.SelectionResult = 1;
            native.Begin(1);
            Assert.NotEmpty(scope.Keyboard.Candidates);
            Assert.Null(scope.Keyboard.SelectedCandidateIndex);
        });
    }

    /// <summary>
    /// An enumerated element may remain callable after ending in the same document. Neither
    /// polling nor an unrelated End notification may confuse COM object lifetime with membership.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task EnumeratedCandidate_RemovalReleasesTheOldSnapshot(bool notifyEnd)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = CreateEmbeddedScope();
            await scope.DrainAsync();
            using var native = new CandidateHarness(scope.Keyboard);
            native.Manager.ElementFactory = () => native.First;
            native.Session.Refresh(scope.Keyboard);
            Assert.Equal("first0", Assert.Single(scope.Keyboard.Candidates).Text);
            native.End(42);
            await scope.DrainAsync();
            Assert.Equal("first0", Assert.Single(scope.Keyboard.Candidates).Text);

            var oldCandidate = scope.Keyboard.Candidates[0];
            native.Manager.ElementFactory = () => native.Second;
            if (notifyEnd)
            {
                native.End(42);
                await scope.DrainAsync();
            }
            else
            {
                var result = await native.Session.TryCommitAsync(
                    scope.Keyboard, oldCandidate, CancellationToken.None);
                Assert.False(result.Succeeded);
                native.Session.Refresh(scope.Keyboard);
            }

            Assert.Empty(native.First.Selections);
            Assert.Equal(0, native.First.Finalizations);
            Assert.DoesNotContain(1, native.First.ShowCalls);
            Assert.Equal("second0", Assert.Single(scope.Keyboard.Candidates).Text);
            native.Manager.ElementFactory = () => null;
            native.Session.Refresh(scope.Keyboard);
            Assert.Empty(scope.Keyboard.Candidates);
        });
    }

    /// <summary>
    /// A TIP may expose its interfaces before initializing strings; Begin negotiates, Update reads.
    /// </summary>
    [Fact]
    public Task Begin_DefersCandidateContentsUntilUpdate()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = CreateEmbeddedScope();
            await scope.DrainAsync();
            using var native = new CandidateHarness(scope.Keyboard);
            var sink = (ISoftKeyboardTsfUIElementSink)native.Session;
            var show = 1;
            sink.BeginUIElement(1, ref show);
            Assert.Equal(0, show);
            Assert.Equal(0, native.First.ContentReads);
            Assert.Empty(scope.Keyboard.Candidates);
            sink.UpdateUIElement(1);
            Assert.True(native.First.ContentReads > 0);
            Assert.NotEmpty(scope.Keyboard.Candidates);
        });
    }

    /// <summary>
    /// QueryInterface success alone does not make a candidate usable: an unimplemented document
    /// method must keep native UI visible for any provider.
    /// </summary>
    [Fact]
    public Task UnimplementedCandidateDocument_UsesNativePresentation()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = CreateEmbeddedScope();
            await scope.DrainAsync();
            using var native = new CandidateHarness(scope.Keyboard);
            native.First.DocumentResult = unchecked((int)0x80004001);
            var show = 0;
            ((ISoftKeyboardTsfUIElementSink)native.Session).BeginUIElement(1, ref show);
            Assert.Equal(1, show);
            Assert.Equal(0, native.First.ContentReads);
            Assert.Equal(SoftKeyboardCandidatePresentationState.NativeFallback,
                scope.Keyboard.CandidatePresentationState);
        });
    }

    /// <summary>
    /// Without explicit extension policy, a candidate that only implements the basic UI-less
    /// interfaces keeps its native window.
    /// </summary>
    [Fact]
    public Task BasicCandidateInterfaces_UseNativePresentation()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            using var native = new CandidateHarness(scope.Keyboard);
            native.Manager.ElementFactory = () => new CandidateElement(native.Document, "basic");
            var show = 0;

            ((ISoftKeyboardTsfUIElementSink)native.Session).BeginUIElement(1, ref show);

            Assert.Equal(1, show);
            Assert.Empty(scope.Keyboard.Candidates);
            Assert.Equal(
                SoftKeyboardCandidatePresentationState.NativeFallback,
                scope.Keyboard.CandidatePresentationState
            );
        });
    }

    /// <summary>
    /// Explicit extension policy can identify a known candidate implementation that does not
    /// expose the optional integrated-candidate interface.
    /// </summary>
    [Fact]
    public Task CandidateExtension_CanEmbedKnownBasicImplementation()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = CreateEmbeddedScope();
            await scope.DrainAsync();
            var behavior = Substitute.For<ISoftKeyboardTsfCandidateBehavior>();
            behavior.GetPresentation(Arg.Any<SoftKeyboardTsfCandidateContext>()).Returns(
                SoftKeyboardTsfCandidatePresentation.Embedded
            );
            var provider = Substitute.For<ISoftKeyboardExtensionProvider>();
            provider.GetExtensions(Arg.Any<SoftKeyboardExtensionContext>()).Returns(
                [new SoftKeyboardExtension(candidateBehavior: behavior)]
            );
            scope.Keyboard.ExtensionProvider = provider;
            using var native = new CandidateHarness(scope.Keyboard);
            native.Manager.ElementFactory = () => new CandidateElement(native.Document, "basic");
            var show = 1;
            var sink = (ISoftKeyboardTsfUIElementSink)native.Session;

            sink.BeginUIElement(1, ref show);
            sink.UpdateUIElement(1);

            Assert.Equal(0, show);
            Assert.NotEmpty(scope.Keyboard.Candidates);
            Assert.Equal(
                SoftKeyboardCandidatePresentationState.Embedded,
                scope.Keyboard.CandidatePresentationState
            );
        });
    }

    /// <summary>
    /// Rejected native UI elements must release their RCW references before the next composition,
    /// through both discovery paths. A native IUnknown avoids the CLR unwrapping a managed COM
    /// double.
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public Task RejectedNativeElement_ReleasesProbeBeforeReturning(bool enumerate, bool candidateInterface)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = CreateEmbeddedScope();
            await scope.DrainAsync();
            using var native = new CandidateHarness(scope.Keyboard);
            using var element = new NativeUnknown(candidateInterface);
            native.Manager.ElementFactory = element.Acquire;
            for (var cycle = 0; cycle < 3; cycle++)
            {
                if (enumerate)
                {
                    native.Session.Refresh(scope.Keyboard);
                }
                else
                {
                    var show = 0;
                    ((ISoftKeyboardTsfUIElementSink)native.Session).BeginUIElement(1, ref show);
                    Assert.Equal(1, show);
                }

                Assert.Equal(1, element.References);
            }
        });
    }

    /// <summary>
    /// A keyboard registered before the first editor must leave TSF activation policy to WPF.
    /// </summary>
    [Fact]
    public void ConnectionBeforeFirstEditor_DoesNotActivateAThreadManager()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                Assert.Null(WpfTextServicesAccessor.TryGetThreadManager());
                using var session = new TsfCandidateSession(Dispatcher.CurrentDispatcher);
                session.EnsureConnected();
                Assert.Null(typeof(TsfCandidateSession)
                    .GetField("_threadManager", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session));
                Assert.Null(WpfTextServicesAccessor.TryGetThreadManager());
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "The isolated TSF dispatcher did not exit.");
        Assert.Null(failure);
    }

    /// <summary>
    /// A number-key commit may be acknowledged by element end, while caller cancellation still
    /// rejects the operation.
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
            using var scope = CreateEmbeddedScope();
            await scope.DrainAsync();
            var extension = Substitute.For<ISoftKeyboardTsfCandidateBehavior>();
            extension.GetPresentation(Arg.Any<SoftKeyboardTsfCandidateContext>()).Returns(
                SoftKeyboardTsfCandidatePresentation.Embedded);
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
    /// Late extension completion must neither select a later element nor overwrite its
    /// presentation diagnostics.
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
            using var scope = CreateEmbeddedScope();
            await scope.DrainAsync();
            var extension = Substitute.For<ISoftKeyboardTsfCandidateBehavior>();
            extension.GetPresentation(Arg.Any<SoftKeyboardTsfCandidateContext>()).Returns(
                SoftKeyboardTsfCandidatePresentation.Embedded);
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
    /// Caller cancellation prevents a late extension failure from changing a still-active native
    /// element.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CallerCancellation_RejectsLateExtensionFailure(bool movePage)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = CreateEmbeddedScope();
            await scope.DrainAsync();
            var extension = Substitute.For<ISoftKeyboardTsfCandidateBehavior>();
            extension.GetPresentation(Arg.Any<SoftKeyboardTsfCandidateContext>()).Returns(
                SoftKeyboardTsfCandidatePresentation.Embedded);
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
    /// Synchronous sink reentrancy during selection must not finalize even when the same COM
    /// object and id are reused.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Selection_RevalidatesElementGeneration(bool reuseElement)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = CreateEmbeddedScope();
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
    /// Current elements still allow paging and committing, including normal EndUIElement during
    /// finalization.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task NativeOperation_CompletesForCurrentElement(bool movePage)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = CreateEmbeddedScope();
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
    /// Grants explicit admission through the Windows extension for candidate lifecycle tests.
    /// </summary>
    private static SoftKeyboardRegressionScope CreateEmbeddedScope()
    {
        var scope = new SoftKeyboardRegressionScope(KnownTextServices.MicrosoftPinyin);
        scope.Keyboard.ExtensionProvider = DefaultSoftKeyboardExtensionProvider.Default;
        return scope;
    }

    /// <summary>
    /// Replaces only the COM transport, leaving registration, sink routing, page parsing, and
    /// operation checks intact.
    /// </summary>
    private sealed class CandidateHarness : IDisposable
    {
        private readonly ISoftKeyboardTsfUIElementSink _sink;

        internal CandidateHarness(SoftKeyboard keyboard)
        {
            Document = new CandidateCompositionDocument();
            First = new IntegratedCandidateElement(Document, "first");
            Second = new IntegratedCandidateElement(Document, "second");
            Session = new TsfCandidateSession(Dispatcher.CurrentDispatcher);
            SetField("_reflectionUnavailable", true);
            SetField("_threadManager", new ThreadManager(Document));
            Manager = new ElementManager(First, Second);
            SetField("_elementManager", Manager);
            Session.Register(keyboard);
            _sink = Session;
        }

        internal TsfCandidateSession Session { get; }
        internal ElementManager Manager { get; }
        internal CandidateElement First { get; }
        internal CandidateElement Second { get; }
        internal CandidateCompositionDocument Document { get; }

        internal void Begin(uint id)
        {
            var show = 1;
            Assert.Equal(0, _sink.BeginUIElement(id, ref show));
            Assert.Equal(0, show);
            Assert.Equal(0, _sink.UpdateUIElement(id));
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
        internal Func<object?>? ElementFactory { get; set; }
        internal int Acquisitions { get; private set; }
        internal int Enumerations { get; private set; }

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
            Acquisitions++;
            element = ElementFactory is { } factory ? factory() : elementId == 1 ? first : second;
            return 0;
        }

        /// <inheritdoc />
        public int EnumUIElements(out object? enumerator)
        {
            Enumerations++;
            enumerator = ElementFactory is { } factory ? new SingleElementEnumerator(factory) : null;
            return enumerator is null ? -1 : 0;
        }
    }

    private sealed class SingleElementEnumerator(Func<object?> factory) : IEnumTfUIElements
    {
        private bool _fetched;

        /// <inheritdoc />
        public int Clone(out IEnumTfUIElements enumerator)
        {
            enumerator = new SingleElementEnumerator(factory);
            return 0;
        }

        /// <inheritdoc />
        public int Next(uint count, out object? element, out uint fetched)
        {
            element = _fetched ? null : factory();
            fetched = _fetched ? 0U : 1U;
            _fetched = true;
            return fetched == 0 ? 1 : 0;
        }

        /// <inheritdoc />
        public int Reset() { _fetched = false; return 0; }

        /// <inheritdoc />
        public int Skip(uint count) { _fetched = true; return 0; }
    }

    /// <summary>
    /// Owns an unmanaged vtable with observable reference balance. Optional candidate interfaces
    /// return E_NOTIMPL for the document query, exercising rejection after successful
    /// QueryInterface.
    /// The initial reference keeps its storage alive until test teardown.
    /// </summary>
    private sealed class NativeUnknown : IDisposable
    {
        private static readonly List<NativeUnknown> FailedTestRoots = [];
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int QueryInterfaceCallback(IntPtr self, ref Guid iid, out IntPtr result);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint ReferenceCallback(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DocumentCallback(IntPtr self, out IntPtr document);

        private readonly QueryInterfaceCallback _query;
        private readonly ReferenceCallback _addRef;
        private readonly ReferenceCallback _release;
        private readonly DocumentCallback _document;
        private readonly bool _candidateInterface;
        private readonly IntPtr _vtable = Marshal.AllocCoTaskMem(18 * IntPtr.Size);
        private readonly IntPtr _instance = Marshal.AllocCoTaskMem(IntPtr.Size);
        internal int References { get; private set; } = 1;

        internal NativeUnknown(bool candidateInterface)
        {
            _candidateInterface = candidateInterface;
            _query = QueryInterface;
            _addRef = _ => (uint)++References;
            _release = _ => (uint)--References;
            _document = (IntPtr _, out IntPtr document) =>
            {
                document = IntPtr.Zero;
                return unchecked((int)0x80004001);
            };
            for (var slot = 0; slot < 18; slot++) Marshal.WriteIntPtr(_vtable, slot * IntPtr.Size, IntPtr.Zero);
            Marshal.WriteIntPtr(_vtable, 0, Marshal.GetFunctionPointerForDelegate(_query));
            Marshal.WriteIntPtr(_vtable, IntPtr.Size, Marshal.GetFunctionPointerForDelegate(_addRef));
            Marshal.WriteIntPtr(_vtable, 2 * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(_release));
            Marshal.WriteIntPtr(_vtable, 8 * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(_document));
            Marshal.WriteIntPtr(_instance, _vtable);
        }

        internal object Acquire() => Marshal.GetObjectForIUnknown(_instance);

        private int QueryInterface(IntPtr self, ref Guid iid, out IntPtr result)
        {
            var supported = iid == new Guid("00000000-0000-0000-C000-000000000046")
                || (_candidateInterface && (iid == typeof(ITfCandidateListUIElement).GUID
                    || iid == typeof(ITfCandidateListUIElementBehavior).GUID));
            result = supported ? self : IntPtr.Zero;
            if (result == IntPtr.Zero) return unchecked((int)0x80004002);
            References++;
            return 0;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (References == 1)
            {
                Marshal.FreeCoTaskMem(_instance);
                Marshal.FreeCoTaskMem(_vtable);
            }
            else
            {
                // A failed balance assertion must not leave callbacks pointing into collected
                // delegates when the leaked RCW eventually finalizes.
                FailedTestRoots.Add(this);
            }

            GC.KeepAlive(_query);
            GC.KeepAlive(_addRef);
            GC.KeepAlive(_release);
            GC.KeepAlive(_document);
        }
    }

    private class CandidateElement(object document, string name)
        : ITfCandidateListUIElement, ITfCandidateListUIElementBehavior
    {
        private uint _selection;

        internal List<uint> Selections { get; } = [];
        internal List<int> ShowCalls { get; } = [];
        internal int Finalizations { get; private set; }
        internal Action? OnSelect { get; set; }
        internal Action? OnFinalize { get; set; }
        internal Action? OnCount { get; set; }
        internal Action? OnString { get; set; }
        internal int DocumentResult { get; set; }
        internal int SelectionResult { get; set; }
        internal int FinalizeResult { get; set; }
        internal int ContentReads { get; private set; }

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
            ShowCalls.Add(show);
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
            documentManager = DocumentResult < 0 ? null : document;
            return DocumentResult;
        }

        /// <inheritdoc />
        public int GetCount(out uint count)
        {
            ContentReads++;
            count = 2;
            OnCount?.Invoke();
            return 0;
        }

        /// <inheritdoc />
        public int GetSelection(out uint index)
        {
            index = _selection;
            return SelectionResult;
        }

        /// <inheritdoc />
        public int GetString(uint index, out string? text)
        {
            text = name + index;
            OnString?.Invoke();
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
            return FinalizeResult;
        }

        /// <inheritdoc />
        public int Abort()
        {
            return 0;
        }
    }

    private sealed class IntegratedCandidateElement(object document, string name)
        : CandidateElement(document, name), ITfIntegratableCandidateListUIElementCapability
    {
    }
}
