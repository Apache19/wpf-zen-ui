// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Threading;
using Wpf.Zen.Ui.VirtualInput.Infrastructure;

namespace Wpf.Zen.Ui.UnitTests.VirtualInput;

/// <summary>
/// Verifies ownership transitions independently from any particular asynchronous provider.
/// </summary>
public class SoftKeyboardOperationSlotTests
{
    /// <summary>
    /// Replacement revokes publication while allowing the old execution to finish using its token.
    /// </summary>
    [Fact]
    public void Replacement_CancelsWithoutDisposingRunningOperation()
    {
        var slot = new SoftKeyboardOperationSlot();
        using var previous = slot.Start(TestContext.Current.CancellationToken, replace: true, Fail);
        using var current = slot.Start(TestContext.Current.CancellationToken, replace: true, Fail);
        Assert.NotNull(previous);
        Assert.NotNull(current);
        Assert.False(previous.IsCurrent);
        Assert.True(previous.Token.IsCancellationRequested);
        // WaitHandle throws if the token's source was disposed while its operation is still running.
        Assert.True(previous.Token.WaitHandle.WaitOne(0));
#pragma warning disable IDISP017 // Complete the old operation before checking the replacement.
        previous.Dispose();
#pragma warning restore IDISP017
        Assert.True(current.IsCurrent);
    }

    /// <summary>
    /// A second input request cannot interrupt an in-progress candidate command.
    /// </summary>
    [Fact]
    public void ConcurrentInput_IsRejectedWithoutCancelingItsOwner()
    {
        var slot = new SoftKeyboardOperationSlot();
        using var current = slot.Start(TestContext.Current.CancellationToken, replace: false, Fail);
        using var rejected = slot.Start(TestContext.Current.CancellationToken, replace: false, Fail);
        Assert.Null(rejected);
        Assert.NotNull(current);
        Assert.True(current.IsCurrent);
        Assert.False(current.Token.IsCancellationRequested);
    }

    /// <summary>
    /// Caller cancellation invalidates publication and permits a subsequent input request.
    /// </summary>
    [Fact]
    public void CallerCancellation_RevokesPublicationEvenIfProviderIgnoresToken()
    {
        using var cancellation = new CancellationTokenSource();
        var slot = new SoftKeyboardOperationSlot();
        using var operation = slot.Start(cancellation.Token, replace: false, Fail);
        Assert.NotNull(operation);
        cancellation.Cancel();
        Assert.False(operation.IsCurrent);
        using var next = slot.Start(TestContext.Current.CancellationToken, replace: false, Fail);
        Assert.NotNull(next);
        Assert.True(next.IsCurrent);
    }

    /// <summary>
    /// A request that was already canceled cannot displace valid work.
    /// </summary>
    [Fact]
    public void AlreadyCanceledRequest_DoesNotReplaceActiveWork()
    {
        var slot = new SoftKeyboardOperationSlot();
        using var current = slot.Start(TestContext.Current.CancellationToken, replace: true, Fail);
        using var rejected = slot.Start(new CancellationToken(true), replace: true, Fail);
        Assert.Null(rejected);
        Assert.NotNull(current);
        Assert.True(current.IsCurrent);
    }

    /// <summary>
    /// Cancellation callbacks can replace ownership without old cleanup detaching the new operation.
    /// </summary>
    [Fact]
    public void Replacement_ReentrantCancellationPreservesTheNewestOwner()
    {
        var slot = new SoftKeyboardOperationSlot();
        using var previous = slot.Start(TestContext.Current.CancellationToken, replace: true, Fail);
        Assert.NotNull(previous);
        SoftKeyboardOperationSlot.Operation? newest = null;
#pragma warning disable IDISP003 // The cancellation callback executes once; the created operation is disposed below.
        using var registration = previous.Token.Register(() => newest = slot.Start(TestContext.Current.CancellationToken, replace: true, Fail));
#pragma warning restore IDISP003
        using var superseded = slot.Start(TestContext.Current.CancellationToken, replace: true, Fail);
        using (newest)
        {
            Assert.NotNull(newest);
            Assert.NotNull(superseded);
            Assert.False(superseded.IsCurrent);
            superseded.Dispose();
            Assert.True(newest.IsCurrent);
        }
    }

    /// <summary>
    /// A throwing cancellation callback cannot retain ownership or suppress other notifications.
    /// </summary>
    [Fact]
    public void CancellationCallbackFailure_DoesNotRetainOwnershipOrSkipOtherCallbacks()
    {
        var slot = new SoftKeyboardOperationSlot();
        using var current = slot.Start(TestContext.Current.CancellationToken, replace: true, Fail);
        Assert.NotNull(current);
        bool notified = false;
        using var first = current.Token.Register(() => notified = true);
        using var second = current.Token.Register(() => throw new InvalidOperationException("callback"));
        Exception? error = null;
        slot.Cancel(value => error = value);
        Assert.IsType<AggregateException>(error);
        Assert.True(notified);
        Assert.False(current.IsCurrent);
        using var next = slot.Start(TestContext.Current.CancellationToken, replace: false, Fail);
        Assert.NotNull(next);
        Assert.True(next.IsCurrent);
    }

    /// <summary>
    /// Related operations detach together so old cancellation callbacks cannot lose their replacements.
    /// </summary>
    [Fact]
    public void GroupCancellation_DoesNotCancelWorkStartedByAnOldCallback()
    {
        var first = new SoftKeyboardOperationSlot();
        var second = new SoftKeyboardOperationSlot();
        using var oldFirst = first.Start(TestContext.Current.CancellationToken, replace: true, Fail);
        using var oldSecond = second.Start(TestContext.Current.CancellationToken, replace: true, Fail);
        Assert.NotNull(oldFirst);
        Assert.NotNull(oldSecond);
        SoftKeyboardOperationSlot.Operation? replacement = null;
#pragma warning disable IDISP003 // The single cancellation callback creates one operation, disposed below.
        using var registration = oldFirst.Token.Register(() =>
            replacement = second.Start(TestContext.Current.CancellationToken, replace: true, Fail));
#pragma warning restore IDISP003
        SoftKeyboardOperationSlot.CancelTogether(first, second, Fail);
        using (replacement)
        {
            Assert.False(oldFirst.IsCurrent);
            Assert.False(oldSecond.IsCurrent);
            Assert.NotNull(replacement);
            Assert.True(replacement.IsCurrent);
        }
    }

    private static void Fail(Exception exception)
    {
        Assert.Fail(exception.ToString());
    }
}
