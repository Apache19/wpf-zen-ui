// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32.UI.TextServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

/// <summary>
/// Holds the candidate's original TSF document and checks native completion without changing its
/// pre-edit. Finalize may finish asynchronously; observing a live composition must not end it.
/// All operations and disposal belong to the TSF Dispatcher.
/// </summary>
internal sealed class TsfCandidateDocument : IDisposable
{
    private readonly object                _document;
    private readonly ITfContext            _context;
    private readonly ITfContextComposition _compositions;

    /// <summary>
    /// Gets whether this operation has released its native acquisitions.
    /// </summary>
    private bool IsDisposed { get; set; }

    /// <summary>
    /// Takes ownership of the document and base-context acquisitions supplied by Capture.
    /// </summary>
    private TsfCandidateDocument(object document, ITfContext context)
    {
        _document     = document;
        _context      = context;
        _compositions = (ITfContextComposition)context;
    }

    /// <summary>
    /// Acquires the editor's base context, matching WPF FrameworkTextComposition's query path.
    /// </summary>
    internal static TsfCandidateDocument Capture(ITfCandidateListUIElementBehavior candidate)
    {
        object? document = null;
        ITfContext? context = null;
        try
        {
            Marshal.ThrowExceptionForHR(candidate.GetDocumentMgr(out document));
            if (document is not ITfDocumentMgr manager)
            {
                throw new InvalidOperationException("The candidate has no accessible TSF document.");
            }

            manager.GetBase(out context);
            return new TsfCandidateDocument(document, context);
        }
        catch
        {
            Release(context);
            Release(document);
            throw;
        }
    }

    /// <summary>
    /// Revalidates the original document without taking ownership of WPF's thread manager.
    /// </summary>
    internal bool MatchesFocus(ITfThreadMgr manager)
    {
        object? focused = null;
        try
        {
            return manager.GetFocus(out focused) >= 0
                && TsfCandidateSession.HaveSameComIdentity(_document, focused);
        }
        finally
        {
            Release(focused);
        }
    }

    /// <summary>
    /// Confirms that the context has no native composition. A live or replacement composition
    /// keeps its UI and continues through normal TSF notifications; it is never force-terminated.
    /// </summary>
    internal bool IsCompositionCompleted()
    {
        IEnumITfCompositionView? enumerator = null;
        var views = new ITfCompositionView[1];
        try
        {
            _compositions.EnumCompositions(out enumerator);

            enumerator.Next(1, views, out var fetched);

            var completed = fetched == 0 && views[0] is null;

            Debug.WriteLine(
                $"[SoftKeyboard.Candidates.Tsf] COMPOSITION observed after submission; completed={completed}."
            );

            return completed;
        }
        finally
        {
            Release(views[0]);
            Release(enumerator);
        }
    }

    /// <summary>
    /// Balances one COM out-parameter acquisition, preserving other owners of shared WPF objects.
    /// </summary>
    private static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.ReleaseComObject(value);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        Release(_context);
        Release(_document);
    }
}
