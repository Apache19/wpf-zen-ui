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
    /// Default is null. A provider exception or an unhandled page records the reason before a built-in
    /// fallback is displayed. This diagnostic does not inherit or affect layout itself and cannot be
    /// animated. Returning null to delegate a supported built-in page is ordinary resolution, not an error.
    /// </remarks>
    public Exception? LayoutError => (Exception?)GetValue(LayoutErrorProperty);

    /// <summary>
    /// Requests a new immutable layout snapshot after application-owned data changes.
    /// </summary>
    /// <remarks>
    /// Call on the owning dispatcher. This cancels held-key interactions before replacing their data.
    /// Providers must not call this while constructing a layout; recursive construction is ignored.
    /// </remarks>
    public void RefreshLayout()
    {
        VerifyAccess();
        RebuildLayout();
    }

    private void RebuildLayout()
    {
        if (_isRebuildingLayout)
        {
            return;
        }

        _isRebuildingLayout = true;

        try
        {
            CancelKeyInteraction();

            var adapter = ActiveTarget is null ? null : GetAdapter(ActiveTarget);

            var inputScope = ActiveTarget is not null && adapter is not null
                ? adapter.GetInputScope(ActiveTarget)
                : InputScopeNameValue.Default;

            var culture = InputLanguageManager.Current.CurrentInputLanguage;
            var supplementalSelection = ReadSupplementalSelection();

            SetValue(SelectedSupplementalLayoutPropertyKey, supplementalSelection);

            var settingsContext = new SoftKeyboardLayoutContext(
                culture,
                inputScope,
                Page,
                ShiftState,
                ImeState,
                ActiveInputMethod,
                supplementalLayout: supplementalSelection
            );

            ResolveExtensions(settingsContext);

            var supplementalLayout = ResolveSupplementalLayout(supplementalSelection);
            var context = new SoftKeyboardLayoutContext(
                culture,
                inputScope,
                Page,
                ShiftState,
                ImeState,
                ActiveInputMethod,
                supplementalLayout: supplementalLayout.Id
            );

            SoftKeyboardLayout? layout = null;
            Exception?          error  = null;

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
                    ImeState,
                    ActiveInputMethod,
                    supplementalLayout: SoftKeyboardSupplementalLayouts.Standard
                );

                layout = DefaultSoftKeyboardLayoutProvider.Default.CreateLayout(context)!;
            }

            SetValue(CurrentLayoutPropertyKey, layout);
            SetValue(LayoutErrorPropertyKey, error);

            UpdateSettingsContext(settingsContext);
        }
        finally
        {
            _isRebuildingLayout = false;
        }
    }
}
