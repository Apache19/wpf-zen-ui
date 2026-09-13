// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.VirtualInput.Settings;
using MenuItem = System.Windows.Controls.MenuItem;
using Separator = System.Windows.Controls.Separator;

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Coordinates settings snapshots, per-profile layout selections, and their default menu presentation.
/// </summary>
public partial class SoftKeyboard
{
    private const string SettingsButtonPartName = "PART_SettingsButton";

    private readonly Dictionary<string, string> _supplementalSelections = new(StringComparer.Ordinal);
    private readonly ObservableCollection<SoftKeyboardSetting> _settingsItems = [];

    private SoftKeyboardLayoutContext? _settingsLayoutContext;
    private bool     _refreshingSettings;
    private int      _settingsVersion;

    #region DP: SettingsProvider

    /// <summary>
    /// Identifies the <see cref="SettingsProvider"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SettingsProviderProperty
        = DependencyProperty.Register(
            nameof(SettingsProvider),
            typeof(ISoftKeyboardSettingsProvider),
            typeof(SoftKeyboard),
            new PropertyMetadata(
                DefaultSoftKeyboardSettingsProvider.Default,
                (d, _) => ((SoftKeyboard)d).RefreshSettings()
            ),
            static value => value is ISoftKeyboardSettingsProvider
        );

    /// <summary>
    /// Gets or sets the settings-tree policy. Changes refresh the menu; this property does not inherit or animate.
    /// </summary>
    public ISoftKeyboardSettingsProvider SettingsProvider
    {
        get => (ISoftKeyboardSettingsProvider)GetValue(SettingsProviderProperty);
        set => SetValue(SettingsProviderProperty, value);
    }

    #endregion

    #region Read-Only DP: SettingsError

    private static readonly DependencyPropertyKey SettingsErrorPropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(SettingsError),
            typeof(Exception),
            typeof(SoftKeyboard),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Identifies the <see cref="SettingsError"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SettingsErrorProperty
        = SettingsErrorPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the last policy failure, or null after a successful refresh.
    /// Does not affect layout or inherit.
    /// </summary>
    public Exception? SettingsError => (Exception?)GetValue(SettingsErrorProperty);

    #endregion

    #region Read-Only DP: SelectedSupplementalLayout

    private static readonly DependencyPropertyKey SelectedSupplementalLayoutPropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(SelectedSupplementalLayout),
            typeof(string),
            typeof(SoftKeyboard),
            new PropertyMetadata(SoftKeyboardSupplementalLayouts.Automatic
            )
        );

    /// <summary>
    /// Identifies the <see cref="SelectedSupplementalLayout"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SelectedSupplementalLayoutProperty =
        SelectedSupplementalLayoutPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the remembered choice; Automatic follows the recognized input-method preference.
    /// </summary>
    /// <remarks>
    /// Use SelectSupplementalLayout to validate a change and cancel composition before replacing the letter surface.
    /// </remarks>
    public string SelectedSupplementalLayout => (string)GetValue(SelectedSupplementalLayoutProperty);

    #endregion

    /// <summary>
    /// Gets the current data-driven settings tree, usable by application-owned menu templates.
    /// </summary>
    public ReadOnlyObservableCollection<SoftKeyboardSetting> SettingsItems { get; private set; } = null!;

    /// <summary>
    /// Gets the default ContextMenu for the template's DropDownButton.Flyout.
    /// </summary>
    /// <remarks>
    /// Each keyboard owns its menu. Items have the corresponding SoftKeyboardSetting as DataContext,
    /// allowing ordinary WPF styles and implicit data templates to customize text and icons. A custom
    /// template may omit PART_SettingsButton; programmatic settings and layout selection remain available.
    /// </remarks>
    public ContextMenu SettingsMenu { get; private set; } = null!;

    private void InitializeSettings()
    {
        SettingsItems = [with(_settingsItems)];

        // The keyboard is a pointer-operated input surface. Its settings must not switch WPF's
        // TSF document or reset the editor's per-document conversion mode merely by being opened.
        SettingsMenu = new ContextMenu
        {
            Placement = PlacementMode.Top,
            Focusable = false
        };

        SettingsMenu.Opened += (_, _) => RefreshSettings();

        SettingsMenu.Closed += (_, _) =>
        {
            // ContextMenu owns a temporary focus scope. Restore only while our host remains active;
            // clicking another editor/window must never pull its focus back to this keyboard.
            _ = Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(
                    () =>
                    {
                        if (IsLoaded && IsOpen && IsWindowForeground()
                            && (System.Windows.Input.Keyboard.FocusedElement is null
                                || System.Windows.Input.Keyboard.FocusedElement is MenuItem))
                        {
                            PrepareTargetForInput();
                        }
                    }
                )
            );
        };
    }

    /// <summary>
    /// Refreshes dynamic policy data on the owning dispatcher. Reentrant policy refreshes are ignored.
    /// </summary>
    public void RefreshSettings()
    {
        VerifyAccess();

        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (_refreshingSettings || _settingsLayoutContext is null || SettingsMenu is null)
        {
            return;
        }

        _refreshingSettings = true;

        try
        {
            var context = new SoftKeyboardSettingsContext(
                _settingsLayoutContext,
                AutoShowMode,
                AutoHideMode,
                _availableLayoutOptions,
                CandidateMode
            );

            SoftKeyboardSetting[] nodes;

            object[] menuItems;
            var      version    = _settingsVersion + 1;
            var      groupScope = Guid.NewGuid().ToString("N");

            Exception? error = null;
            try
            {
                nodes = [.. SettingsProvider.CreateSettings(context)];

                if (nodes.Any(node => node is null))
                {
                    throw new InvalidOperationException("A settings node cannot be null.");
                }

                menuItems = [.. nodes.Select(node => CreateSettingsMenuItem(node, version, groupScope))];
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                error = exception;

                nodes =
                [
                    .. DefaultSoftKeyboardSettingsProvider.Default.CreateSettings(context)
                ];

                menuItems =
                [
                    .. nodes.Select(node => CreateSettingsMenuItem(node, version, groupScope))
                ];
            }

            SetValue(SettingsErrorPropertyKey, error);

            _settingsItems.Clear();

            foreach (var node in nodes)
            {
                _settingsItems.Add(node);
            }

            SettingsMenu.Items.Clear();

            _settingsVersion = version;

            foreach (var item in menuItems)
            {
                SettingsMenu.Items.Add(item);
            }
        }
        finally
        {
            _refreshingSettings = false;
        }
    }

    /// <summary>
    /// Selects an available supplemental layout and remembers it for this keyboard's active input profile.
    /// </summary>
    /// <param name="id">An identifier from the current settings context's AvailableLayouts.</param>
    /// <remarks>
    /// Selection lasts for the control's lifetime and is independent of symbol/number page navigation.
    /// Unknown choices throw; a composition that cannot be cancelled leaves the current choice intact.
    /// Application persistence can restore a choice after the relevant input profile becomes active.
    /// </remarks>
    public void SelectSupplementalLayout(string id)
    {
        VerifyAccess();

        if (_settingsLayoutContext is null
            || _availableLayoutOptions.All(option => option.Id != id))
        {
            throw new ArgumentException(
                "The supplemental layout is not available for the active input method.",
                nameof(id)
            );
        }

        var profile = GetSettingsProfileKey();

        if (_activeTextComposition is not null && !PrepareTargetForInput())
        {
            return;
        }

        CandidateInteractionVersion++;

        RunAfterActiveCompositionCancellation(
            () =>
            {
                if (profile != GetSettingsProfileKey())
                {
                    return;
                }

                _supplementalSelections[profile] = id;

                SetValue(SelectedSupplementalLayoutPropertyKey, id);
                ShowPage(SoftKeyboardPages.Letters);

                RebuildLayout();
            }
        );
    }

    private object CreateSettingsMenuItem(SoftKeyboardSetting node, int version, string groupScope)
    {
        if (node.IsSeparator)
        {
            var separator = new Separator
            {
                DataContext = node
            };

            if (node.ItemStyle is not null)
            {
                separator.Style = node.ItemStyle;
            }

            return separator;
        }

        var item = node.RadioGroup is null
            ? new MenuItem()
            : new RadioMenuItem
            {
                GroupName = groupScope + node.RadioGroup
            };

        item.DataContext = node;
        item.Focusable   = false;
        item.Header      = node.Header;
        item.IsEnabled   = node.IsEnabled;
        item.IsChecked   = node.IsChecked;

        if (node.Icon is not null)
        {
            item.Icon = new ContentPresenter
            {
                Content = node.Icon
            };
        }

        if (node.ItemStyle is not null)
        {
            item.Style = node.ItemStyle;
        }

        foreach (var child in node.Children)
        {
            item.Items.Add(CreateSettingsMenuItem(child, version, groupScope));
        }

        if (node.Apply is not null)
        {
            item.Click += (_, args) =>
            {
                if (!ReferenceEquals(args.OriginalSource, item))
                {
                    return;
                }

                args.Handled = true;

                if (version != _settingsVersion || !IsEnabled)
                {
                    return;
                }

                SettingsMenu.SetCurrentValue(ContextMenu.IsOpenProperty, false);

                node.Apply(this);

                RefreshSettings();
            };
        }

        return item;
    }

    private string GetSettingsProfileKey()
    {
        return ActiveInputMethod is { } profile
            ? $"{profile.LanguageId:X4}/{profile.ClassId:D}/{profile.ProfileId:D}"
            : System.Windows.Input.InputLanguageManager.Current.CurrentInputLanguage.Name;
    }

    private void UpdateSettingsContext(SoftKeyboardLayoutContext context)
    {
        var previous = _settingsLayoutContext;

        _settingsLayoutContext = context;

        // Native state is polled and often produces equivalent immutable profile snapshots.
        // Replacing menu containers for those notifications can discard a queued MenuItem.Click.
        if (previous is null
            || previous.Page != context.Page
            || previous.ImeState != context.ImeState
            || previous.CanToggleIme != context.CanToggleIme
            || previous.InputScope != context.InputScope
            || previous.ShiftState != context.ShiftState
            || previous.Culture.Name != context.Culture.Name
            || previous.SupplementalLayout != context.SupplementalLayout
            || previous.InputMethod?.ClassId != context.InputMethod?.ClassId
            || previous.InputMethod?.ProfileId != context.InputMethod?.ProfileId
            || previous.InputMethod?.TouchLayoutPreference != context.InputMethod?.TouchLayoutPreference)
        {
            RefreshSettings();
        }
    }

    private string ReadSupplementalSelection()
    {
        return _supplementalSelections.TryGetValue(GetSettingsProfileKey(), out var selection)
            ? selection
            : SoftKeyboardSupplementalLayouts.Automatic;
    }
}
