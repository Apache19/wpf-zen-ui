// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Resolves layout snapshots and publishes recoverable layout diagnostics.
/// </summary>
public partial class SoftKeyboard
{
    private bool _isRebuildingLayout;
    private bool _layoutDirty = true;
    private bool _hasBuiltLayout;
    private bool _explicitLayoutRefresh;
    private bool _inputMethodLayoutPending;

    private static readonly DependencyPropertyKey LayoutErrorPropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(LayoutError),
            typeof(Exception),
            typeof(SoftKeyboard),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Identifies the <see cref="LayoutError"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty LayoutErrorProperty
        = LayoutErrorPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the last layout failure, or null after successful resolution.
    /// </summary>
    /// <remarks>
    /// Default is null. A provider exception or an unhandled page records the reason before a
    /// built-in fallback is displayed. This diagnostic does not inherit or affect layout itself
    /// and cannot be animated. Returning null to delegate a supported built-in page is ordinary
    /// resolution, not an error.
    /// </remarks>
    public Exception? LayoutError => (Exception?)GetValue(LayoutErrorProperty);

    /// <summary>
    /// Requests a new immutable layout snapshot after application-owned data changes.
    /// </summary>
    /// <remarks>
    /// Call on the owning dispatcher. This cancels held-key interactions before replacing their
    /// data. Providers must not call this while constructing a layout; recursive construction is
    /// ignored. Invisible controls can explicitly build their layout and available choices
    /// with this method. Requests during an input-method operation wait for its coherent publication.
    /// </remarks>
    public void RefreshLayout()
    {
        VerifyAccess();
        if (_isRebuildingLayout)
        {
            return;
        }

        _explicitLayoutRefresh = true;
        RebuildLayout();
    }

    /// <summary>
    /// Invalidates the layout, coalescing invisible-state changes and native-operation requests.
    /// </summary>
    /// <param name="publishInputMethodState">
    /// True while publishing a complete observed snapshot under the input-method operation gate.
    /// </param>
    private void RebuildLayout(bool publishInputMethodState = false)
    {
        if (_isRebuildingLayout)
        {
            return;
        }

        _layoutDirty = true;
        EnsureLayout(publishInputMethodState);
    }

    /// <summary>
    /// Coalesces unloaded or invisible changes until display, preserving explicit refresh requests.
    /// </summary>
    private void EnsureLayout(bool publishInputMethodState = false)
    {
        if (!_layoutDirty || _isRebuildingLayout
            || ((!IsLoaded || !IsVisible) && !_explicitLayoutRefresh))
        {
            return;
        }

        if (!publishInputMethodState
            && (IsInputMethodOperationActive || (_inputObservation.IsObserving && _hasBuiltLayout)))
        {
            // First presentation can show the last committed context while native discovery settles.
            // Later refreshes keep the existing surface until all observed fields are published.
            _inputMethodLayoutPending = true;
            return;
        }

        BuildLayoutCore();
    }

    /// <summary>
    /// Resolves and publishes one requested snapshot; kept outside the invisible-state fast path.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void BuildLayoutCore()
    {
        _layoutDirty           = false;
        _explicitLayoutRefresh = false;
        _isRebuildingLayout    = true;

#if DEBUG
        using var performance = new SoftKeyboardPerformanceScope("RebuildLayout", this);
#endif
        try
        {
            CancelKeyInteraction();

            var adapter = ActiveTarget is null ? null : GetAdapter(ActiveTarget);

            var inputScope = ActiveTarget is not null && adapter is not null
                ? adapter.GetInputScope(ActiveTarget)
                : InputScopeNameValue.Default;

            var culture = _observedInputCulture
                ?? InputLanguageManager.Current.CurrentInputLanguage;

            var supplementalSelection = ReadSupplementalSelection();

            SetValue(SelectedSupplementalLayoutPropertyKey, supplementalSelection);

            var extensionContext = new SoftKeyboardLayoutContext(
                culture,
                inputScope,
                Page,
                ShiftState,
                ActiveInputMethod,
                supplementalLayout: supplementalSelection,
                inputModeButton: InputModeButton
            );

            ResolveExtensions(extensionContext);

            var supplementalLayout = ResolveSupplementalLayout(supplementalSelection);
            var context = new SoftKeyboardLayoutContext(
                culture,
                inputScope,
                Page,
                ShiftState,
                ActiveInputMethod,
                supplementalLayout: supplementalLayout.Id,
                inputModeButton: InputModeButton
            );

            SoftKeyboardLayout? layout = null;
            Exception? error = null;

            if (LayoutProvider is not null)
            {
                try
                {
                    layout = LayoutProvider.CreateLayout(context);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    error = exception;
                }
            }

            if (layout is null && supplementalLayout.Id != SoftKeyboardSupplementalLayouts.Standard)
            {
                try
                {
                    layout = supplementalLayout.LayoutProvider.CreateLayout(context);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    ReportExtensionError(exception);
                }
            }

            if (layout is null && _observedLayoutProvider is not null)
            {
                try
                {
                    layout = _observedLayoutProvider.CreateLayout(context);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    ReportExtensionError(exception);
                }
            }

            foreach (var extension in _activeExtensions)
            {
                if (layout is not null || extension.LayoutProvider is null)
                {
                    continue;
                }

                try
                {
                    layout = extension.LayoutProvider.CreateLayout(context);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    ReportExtensionError(exception);
                }
            }

            layout ??= DefaultSoftKeyboardLayoutProvider.Default.CreateLayout(context);

            if (layout is null)
            {
                error ??= new InvalidOperationException($"No layout provider handles page '{Page}'.");

                // Normalize navigation state with the displayed fallback. The callback may request
                // rebuilding, but the guard prevents a recursive provider call during publication.
                SetValue(PagePropertyKey, SoftKeyboardPages.Automatic);

                context = new SoftKeyboardLayoutContext(
                    culture,
                    inputScope,
                    Page,
                    ShiftState,
                    ActiveInputMethod,
                    supplementalLayout: SoftKeyboardSupplementalLayouts.Standard,
                    inputModeButton: InputModeButton
                );

                layout = DefaultSoftKeyboardLayoutProvider.Default.CreateLayout(context)!;
            }

            _hasBuiltLayout = true;

            SetValue(CurrentLayoutPropertyKey, layout);
            SetValue(LayoutErrorPropertyKey, error);
        }
        finally
        {
            _isRebuildingLayout = false;
        }
    }
}
