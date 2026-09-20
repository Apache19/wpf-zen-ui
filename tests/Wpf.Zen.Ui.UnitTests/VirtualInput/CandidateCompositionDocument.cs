// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

extern alias VirtualInputNative;

using Windows.Win32.UI.TextServices;
using Native = VirtualInputNative::Windows.Win32;

namespace Wpf.Zen.Ui.UnitTests.VirtualInput;

/// <summary>
/// Models a TSF document independently of candidate UI. Submission may leave pre-edit active.
/// </summary>
internal sealed class CandidateCompositionDocument : ITfDocumentMgr
{
    internal CandidateCompositionContext Context { get; } = new();

    /// <inheritdoc />
    public void CreateContext(uint tidOwner, uint dwFlags, object punk, out Native.UI.TextServices.ITfContext ppic, out uint pecTextStore)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public void Push(Native.UI.TextServices.ITfContext pic)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public void Pop(uint dwFlags)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public void GetTop(out Native.UI.TextServices.ITfContext ppic)
    {
        ppic = Context;
    }

    /// <inheritdoc />
    public void GetBase(out Native.UI.TextServices.ITfContext ppic)
    {
        ppic = Context;
    }

    /// <inheritdoc />
    public void EnumContexts(out Native.UI.TextServices.IEnumTfContexts ppEnum)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Supplies native composition state; unused text-store methods fail instead of simulating success.
/// </summary>
internal sealed class CandidateCompositionContext : ITfContext, ITfContextComposition
{
    internal ITfCompositionView? Current { get; set; }
    internal string Preedit { get; set; } = string.Empty;
    internal Action? OnEnumerate { get; set; }

    /// <summary>
    /// Starts or extends input. Candidate submission intentionally does not clear this buffer.
    /// </summary>
    internal void Type(string text)
    {
        Current ??= new CandidateCompositionView();
        Preedit += text;
    }

    /// <inheritdoc />
    public unsafe void RequestEditSession(uint tid, Native.UI.TextServices.ITfEditSession pes, Native.UI.TextServices.TF_CONTEXT_EDIT_CONTEXT_FLAGS dwFlags, Native.Foundation.HRESULT* phrSession)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public unsafe void InWriteSession(uint tid, Native.Foundation.BOOL* pfWriteSession)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public void GetSelection(uint ec, uint ulIndex, uint ulCount, Native.UI.TextServices.TF_SELECTION[] pSelection, out uint pcFetched)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public void SetSelection(uint ec, uint ulCount, Native.UI.TextServices.TF_SELECTION[] pSelection)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public void GetStart(uint ec, out Native.UI.TextServices.ITfRange ppStart)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public void GetEnd(uint ec, out Native.UI.TextServices.ITfRange ppEnd)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public void GetActiveView(out Native.UI.TextServices.ITfContextView ppView)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public void EnumViews(out Native.UI.TextServices.IEnumTfContextViews ppEnum)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public unsafe void GetStatus(Native.UI.TextServices.TS_STATUS* pdcs)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public unsafe void GetProperty(global::System.Guid* guidProp, out Native.UI.TextServices.ITfProperty ppProp)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public unsafe void GetAppProperty(global::System.Guid* guidProp, out Native.UI.TextServices.ITfReadOnlyProperty ppProp)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public unsafe void TrackProperties(global::System.Guid** prgProp, uint cProp, global::System.Guid** prgAppProp, uint cAppProp, out Native.UI.TextServices.ITfReadOnlyProperty ppProperty)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public void EnumProperties(out Native.UI.TextServices.IEnumTfProperties ppEnum)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public void GetDocumentMgr(out Native.UI.TextServices.ITfDocumentMgr ppDm)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public void CreateRangeBackup(uint ec, Native.UI.TextServices.ITfRange pRange, out Native.UI.TextServices.ITfRangeBackup ppBackup)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public void StartComposition(uint ecWrite, Native.UI.TextServices.ITfRange pCompositionRange, Native.UI.TextServices.ITfCompositionSink pSink, out Native.UI.TextServices.ITfComposition ppComposition)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public void EnumCompositions(out Native.UI.TextServices.IEnumITfCompositionView ppEnum)
    {
        OnEnumerate?.Invoke();
        ppEnum = new CandidateCompositionEnumerator(Current);
    }

    /// <inheritdoc />
    public void FindComposition(uint ecRead, Native.UI.TextServices.ITfRange pTestRange, out Native.UI.TextServices.IEnumITfCompositionView ppEnum)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public void TakeOwnership(uint ecWrite, Native.UI.TextServices.ITfCompositionView pComposition, Native.UI.TextServices.ITfCompositionSink pSink, out Native.UI.TextServices.ITfComposition ppComposition)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Distinguishes native transactions even when a candidate object is reused.
/// </summary>
internal sealed class CandidateCompositionView : ITfCompositionView
{
    /// <inheritdoc />
    public unsafe void GetOwnerClsid(global::System.Guid* pclsid)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public void GetRange(out Native.UI.TextServices.ITfRange ppRange)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Enumerates the context snapshot using the generated Next array/fetched-count contract.
/// </summary>
internal sealed class CandidateCompositionEnumerator(ITfCompositionView? view) : IEnumITfCompositionView
{
    private bool _read;

    /// <inheritdoc />
    public void Clone(out Native.UI.TextServices.IEnumITfCompositionView ppEnum)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public void Next(uint ulCount, Native.UI.TextServices.ITfCompositionView[] rgCompositionView, out uint pcFetched)
    {
        pcFetched = 0;
        if (!_read && view is not null && ulCount > 0)
        {
            rgCompositionView[0] = view;
            pcFetched = 1;
        }

        _read = true;
    }

    /// <inheritdoc />
    public void Reset()
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public void Skip(uint ulCount)
    {
        throw new NotSupportedException();
    }
}

