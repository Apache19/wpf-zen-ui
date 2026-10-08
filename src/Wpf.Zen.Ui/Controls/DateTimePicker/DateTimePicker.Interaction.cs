// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using NativeTextBox = System.Windows.Controls.TextBox;

namespace Wpf.Zen.Ui.Controls;

public partial class DateTimePicker
{
    private readonly DateTimePickerTimeColumn?[] _columns = new DateTimePickerTimeColumn?[3];

    private NativeTextBox?  _textBox;
    private ButtonBase?     _button;
    private ButtonBase?     _clearButton;
    private Popup?          _popup;
    private ContentControl? _calendarHost;

    private bool _updatingColumns;
    private bool _updatingEditor;
    private bool _textDirty;

    /// <summary>
    /// Commits the pending value and closes the popup. Invalid or incomplete time input disables
    /// this command.
    /// </summary>
    public static RoutedUICommand CommitCommand { get; } = new(
        "OK", nameof(CommitCommand), typeof(DateTimePicker)
    );

    /// <summary>
    /// Cancels pending changes and closes the popup.
    /// </summary>
    public static RoutedUICommand CancelCommand { get; } = new(
        "Cancel", nameof(CancelCommand), typeof(DateTimePicker)
    );

    /// <summary>
    /// Commits a local-time shortcut. The case-sensitive parameter is Year, Month, Week, Today, or
    /// Now.
    /// </summary>
    public static RoutedUICommand SelectShortcutCommand { get; } = new(
        "Select date and time", nameof(SelectShortcutCommand), typeof(DateTimePicker)
    );

    /// <summary>
    /// Installs instance commands and lifecycle handlers. Popup content explicitly targets its
    /// owner through CommandTarget.
    /// </summary>
    private void InitializeInteraction()
    {
        _calendar.AddHandler(
            Mouse.MouseUpEvent, new MouseButtonEventHandler(OnCalendarMouseUp),
            handledEventsToo: true
        );

        CommandBindings.Add(new CommandBinding(CommitCommand, OnCommitExecuted, OnCommitCanExecute));
        CommandBindings.Add(new CommandBinding(CancelCommand, OnCancelExecuted, OnCancelCanExecute));
        CommandBindings.Add(new CommandBinding(SelectShortcutCommand, OnShortcutExecuted, OnShortcutCanExecute));

        Unloaded         += OnUnloaded;
        IsEnabledChanged += OnIsEnabledChanged;
    }

    /// <summary>
    /// Releases residual capture after a left-button calendar selection so Popup can handle
    /// surrounding interaction and outside clicks.
    /// </summary>
    /// <remarks>
    /// CalendarItem captures its subtree on mouse-down; date buttons mark mouse-up as handled.
    /// This can bypass capture release in CalendarItem.OnMouseUp. Observe handled bubbling events
    /// after selection and release only this calendar's CalendarItem capture, preserving other
    /// captures.
    /// This handler is registered once on the instance calendar and is independent of template
    /// replacement.
    /// </remarks>
    private void OnCalendarMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || Mouse.Captured is not CalendarItem item)
        {
            return;
        }

        if (_calendar.IsAncestorOf(item))
        {
            // Popup's LostMouseCapture handler restores subtree capture and maintains normal
            // dismissal behavior.
            item.ReleaseMouseCapture();
        }
    }

    /// <summary>
    /// Detaches old template events, cancels the old transaction, and moves the same calendar into
    /// the new template.
    /// Public properties and commands remain usable when optional template parts are absent.
    /// </summary>
    private void DetachTemplateParts()
    {
        if (_popup is not null)
        {
            Cancel();

            _popup.Opened -= OnPopupOpened;
            _popup.Closed -= OnPopupClosed;

            if (_popup.Child is UIElement oldChild)
            {
                oldChild.PreviewKeyDown -= OnPopupPreviewKeyDown;
            }
        }

        if (_calendarHost is not null)
        {
            _calendarHost.Content = null;
        }

        if (_button is not null)
        {
            _button.Click                      -= OnDropDownButtonClick;
            _button.PreviewMouseLeftButtonDown -= OnEditorButtonMouseDown;
        }

        if (_clearButton is not null)
        {
            _clearButton.Click                      -= OnClearButtonClick;
            _clearButton.PreviewMouseLeftButtonDown -= OnEditorButtonMouseDown;
        }

        if (_textBox is not null)
        {
            _textBox.TextChanged       -= OnEditorTextChanged;
            _textBox.LostKeyboardFocus -= OnEditorLostKeyboardFocus;
        }

        foreach (var column in _columns)
        {
            if (column is not null)
            {
                column.ValueChanged         -= OnColumnValueChanged;
                column.InputValidityChanged -= OnColumnInputValidityChanged;
            }
        }

        _popup        = null;
        _calendarHost = null;
        _button       = null;
        _clearButton  = null;
        _textBox      = null;

        Array.Clear(_columns, 0, _columns.Length);
    }

    /// <summary>
    /// Releases old parts as soon as the template changes and cancels the transaction, even if the
    /// new template is null or not yet applied.
    /// </summary>
    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    {
        if (oldTemplate is not null)
        {
            Cancel();
        }

        DetachTemplateParts();

        base.OnTemplateChanged(oldTemplate, newTemplate);
    }

    /// <summary>
    /// Connects the calendar and time columns to the new template. Properties and commands remain
    /// available when parts are absent.
    /// </summary>
    public override void OnApplyTemplate()
    {
        DetachTemplateParts();

        base.OnApplyTemplate();

        _textBox      = GetTemplateChild("PART_TextBox") as NativeTextBox;
        _button       = GetTemplateChild("PART_Button") as ButtonBase;
        _clearButton  = GetTemplateChild("PART_ClearButton") as ButtonBase;
        _popup        = GetTemplateChild("PART_Popup") as Popup;
        _calendarHost = GetTemplateChild("PART_CalendarHost") as ContentControl;
        _columns[0]   = GetTemplateChild("PART_Hour") as DateTimePickerTimeColumn;
        _columns[1]   = GetTemplateChild("PART_Minute") as DateTimePickerTimeColumn;
        _columns[2]   = GetTemplateChild("PART_Second") as DateTimePickerTimeColumn;

        if (_textBox is not null)
        {
            _textBox.TextChanged       += OnEditorTextChanged;
            _textBox.LostKeyboardFocus += OnEditorLostKeyboardFocus;
        }

        if (_button is not null)
        {
            _button.Click                      += OnDropDownButtonClick;
            _button.PreviewMouseLeftButtonDown += OnEditorButtonMouseDown;
        }

        if (_clearButton is not null)
        {
            _clearButton.Click                      += OnClearButtonClick;
            _clearButton.PreviewMouseLeftButtonDown += OnEditorButtonMouseDown;
        }

        if (_calendarHost is not null)
        {
            _calendarHost.Content = _calendar;
        }

        for (var index = 0; index < _columns.Length; index++)
        {
            var column = _columns[index];
            if (column is not null)
            {
                column.SetCurrentValue(DateTimePickerTimeColumn.MaximumProperty, index == 0 ? 23 : 59);
                column.ValueChanged         += OnColumnValueChanged;
                column.InputValidityChanged += OnColumnInputValidityChanged;
            }
        }

        if (_popup is not null)
        {
            _popup.Opened += OnPopupOpened;
            _popup.Closed += OnPopupClosed;

            PopupDismissal.SetExcludedElement(_popup, _button);

            if (_popup.Child is UIElement child)
            {
                child.PreviewKeyDown += OnPopupPreviewKeyDown;
            }
        }

        UpdateEditorText();
        SynchronizeCalendar();
        SynchronizeTimeColumns();
        UpdatePopupState();
        UpdateInputError();
    }

    /// <summary>
    /// Confirms the popup transaction. Incomplete input or a disallowed date returns false and
    /// retains the editing state.
    /// </summary>
    /// <returns>True when the pending value is committed and the popup closes.</returns>
    public bool Commit()
    {
        if (!CanCommit())
        {
            return false;
        }

        var value = PendingDateTime!.Value;
        SetCurrentValue(SelectedDateTimeProperty, value);

        UpdateFormattedText();
        CloseAndRestoreFocus();

        return true;
    }

    /// <summary>
    /// Discards the current popup edits, preserving the committed value and its binding.
    /// </summary>
    public void Cancel()
    {
        if (IsDropDownOpen)
        {
            Debug.WriteLine(
                $"[DateTimePicker.Transaction] CANCEL editing; pending={PendingDateTime:O}."
            );

            SetCurrentValue(IsDropDownOpenProperty, false);
        }
    }

    private bool CanCommit()
    {
        if (!IsDropDownOpen || !PendingDateTime.HasValue || !IsDateAllowed(PendingDateTime.Value))
        {
            return false;
        }

        foreach (var column in _columns)
        {
            if (column is not null && !column.HasValidInput)
            {
                return false;
            }
        }

        return true;
    }

    private void OnCommitCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = IsEnabled && CanCommit();
        e.Handled    = true;
    }

    private void OnCommitExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        Commit();
        e.Handled = true;
    }

    private void OnCancelCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = IsDropDownOpen;
        e.Handled    = true;
    }

    private void OnCancelExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        CloseAndRestoreFocus();
        e.Handled = true;
    }

    private void OnShortcutCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        var value = GetShortcutValue(e.Parameter as string);
        e.CanExecute = IsEnabled && IsDropDownOpen && value.HasValue && IsDateAllowed(value.Value);
        e.Handled    = true;
    }

    private void OnShortcutExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        var value = GetShortcutValue(e.Parameter as string);
        // Reevaluate and validate to account for date rollover or range changes between command
        // query and execution.
        if (IsDropDownOpen && value.HasValue && IsDateAllowed(value.Value))
        {
            SetCurrentValue(SelectedDateTimeProperty, value.Value);
            UpdateFormattedText();
            CloseAndRestoreFocus();
        }

        e.Handled = true;
    }

    private DateTime? GetShortcutValue(string? shortcut)
    {
        var now = DateTime.Now;

        switch (shortcut)
        {
            case "Year":
                return new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Local);

            case "Month":
                return new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Local);

            case "Week":
            {
                var days = ((int)now.DayOfWeek - (int)FirstDayOfWeek + 7) % 7;
                return now.Date.AddDays(-days);
            }

            case "Today":
                return now.Date;

            case "Now":
                return now;

            default:
                return null;
        }
    }

    private void SynchronizeTimeColumns()
    {
        if (_updatingColumns || PendingDateTime is not DateTime value)
        {
            return;
        }

        _updatingColumns = true;

        try
        {
            int[] values =
            {
                value.Hour, value.Minute, value.Second
            };

            for (var index = 0; index < values.Length; index++)
            {
                var column = _columns[index];
                if (column is not null)
                {
                    column.SetCurrentValue(DateTimePickerTimeColumn.ValueProperty, values[index]);
                    column.RefreshText();
                }
            }
        }
        finally
        {
            _updatingColumns = false;
        }

        UpdateInputError();
    }

    /// <summary>
    /// Invalid time-column drafts do not change field values, so input validity is synchronized
    /// separately.
    /// </summary>
    private void OnColumnInputValidityChanged(object? sender, EventArgs e)
    {
        if (!_updatingColumns)
        {
            UpdateInputError();
        }
    }

    private void OnColumnValueChanged(object sender, RoutedPropertyChangedEventArgs<int> e)
    {
        if (_updatingColumns || !IsDropDownOpen || PendingDateTime is not DateTime value)
        {
            return;
        }

        var column = sender as DateTimePickerTimeColumn;
        var index  = Array.IndexOf(_columns, column);

        long ticksPerUnit;

        switch (index)
        {
            case 0:
                ticksPerUnit = TimeSpan.TicksPerHour;
                break;

            case 1:
                ticksPerUnit = TimeSpan.TicksPerMinute;
                break;

            case 2:
                ticksPerUnit = TimeSpan.TicksPerSecond;
                break;

            default:
                return;
        }

        var ticks   = value.Ticks + (e.NewValue - e.OldValue) * ticksPerUnit;
        var updated = new DateTime(ticks, value.Kind);

        // This column has already synchronized locally. Preserve unfinished input in the other
        // columns.
        SetValue(PendingDateTimePropertyKey, updated);
        UpdateInputError();

        CommandManager.InvalidateRequerySuggested();
    }

    private void UpdateEditorText()
    {
        if (_textBox is null)
        {
            return;
        }

        _updatingEditor = true;

        try
        {
            var text = Text;
            if (string.IsNullOrEmpty(text))
            {
                _textBox.SetCurrentValue(NativeTextBox.TextProperty, string.Empty);
            }
            else
            {
                _textBox.SetCurrentValue(NativeTextBox.TextProperty, text);
            }

            _textDirty = false;
        }
        finally
        {
            _updatingEditor = false;
        }
    }

    private void OnEditorTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingEditor || _textBox is null)
        {
            return;
        }

        _updatingText = true;

        try
        {
            SetCurrentValue(TextProperty, _textBox.Text);
            _textDirty = true;
        }
        finally
        {
            _updatingText = false;
        }
    }

    /// <summary>
    /// Commits draft text on focus loss. Validation failure restores committed text, or empty text
    /// when the committed value is null.
    /// While the popup is open, retain the draft until popup confirmation determines the final
    /// text.
    /// </summary>
    private void OnEditorLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!IsDropDownOpen && _textDirty)
        {
            _textDirty = !CommitText();
            if (_textDirty)
            {
                UpdateFormattedText();
            }
        }
    }

    /// <summary>
    /// Supports Enter to commit, Escape to restore, and F4 or Alt+Down to open the picker from the
    /// main editor.
    /// </summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        var key    = e.Key == Key.System ? e.SystemKey : e.Key;
        var toggle = key == Key.F4 || (key == Key.Down && Keyboard.Modifiers == ModifierKeys.Alt);

        if (toggle)
        {
            if (IsDropDownOpen)
            {
                CloseAndRestoreFocus();
            }
            else
            {
                OpenFromEditor();
            }

            e.Handled = true;
        }
        else if (!IsDropDownOpen && key == Key.Enter)
        {
            _textDirty = !CommitText();
            e.Handled  = true;
        }
        else if (!IsDropDownOpen && key == Key.Escape)
        {
            UpdateFormattedText();
            e.Handled = true;
        }
    }

    private void OnPopupPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape || (e.Key == Key.System && e.SystemKey == Key.Up))
        {
            CloseAndRestoreFocus();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            // Enter drills down in calendar year/month modes; buttons retain their own Enter
            // command behavior.
            if ((_calendar.IsKeyboardFocusWithin && _calendar.DisplayMode != CalendarMode.Month)
                || Keyboard.FocusedElement is ButtonBase)
            {
                return;
            }

            Commit();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Focuses the editor on button press before ButtonBase handles the bubbling event.
    /// Leaves the event unhandled to preserve button capture, click, and touch promotion behavior.
    /// </summary>
    private void OnEditorButtonMouseDown(object sender, MouseButtonEventArgs e)
    {
        _textBox?.Focus();
    }

    /// <summary>
    /// Clears the committed value and invalid drafts, ends the pending transaction, and preserves
    /// caller bindings.
    /// </summary>
    private void OnClearButtonClick(object sender, RoutedEventArgs e)
    {
        if (!IsEnabled || !IsClearButtonEnabled)
        {
            return;
        }

        Cancel();
        SetCurrentValue(SelectedDateTimeProperty, null);
        UpdateFormattedText();

        _textBox?.Focus();
        e.Handled = true;
    }

    private void OnDropDownButtonClick(object sender, RoutedEventArgs e)
    {
        if (IsDropDownOpen)
        {
            CloseAndRestoreFocus();
        }
        else
        {
            OpenFromEditor();
        }
    }

    private void OpenFromEditor()
    {
        if (!IsEnabled)
        {
            return;
        }

        // Apply valid drafts first; retain invalid drafts so the user can choose a replacement in
        // the popup.
        // Opening does not report text errors and therefore does not invoke the ThrowException
        // policy.
        if (_textDirty)
        {
            var culture  = GetCulture();
            var text     = Text;
            var parsed   = DateTimePickerTextParser.TryParse(text, DateTimeFormat, culture, out var value);
            var canApply = string.IsNullOrWhiteSpace(text) || (parsed && IsDateAllowed(value));
            if (canApply)
            {
                CommitText();
            }
        }

        SetCurrentValue(IsDropDownOpenProperty, true);
    }

    /// <summary>
    /// Transfers focus from the control itself to its text editor while preserving child focus
    /// behavior.
    /// </summary>
    protected override void OnGotFocus(RoutedEventArgs e)
    {
        base.OnGotFocus(e);

        if (ReferenceEquals(e.OriginalSource, this))
        {
            ApplyTemplate();
            _textBox?.Focus();
        }
    }

    /// <summary>
    /// Focuses the text editor when the template background is clicked. Clicks handled by text
    /// boxes or buttons retain their behavior.
    /// </summary>
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        if (!e.Handled && !IsDropDownOpen)
        {
            Focus();
        }
    }

    private void UpdatePopupState()
    {
        if (_popup is not null)
        {
            _popup.SetCurrentValue(Popup.IsOpenProperty, IsDropDownOpen && IsEnabled);
        }
    }

    private void OnPopupOpened(object? sender, EventArgs e)
    {
        if (!IsDropDownOpen)
        {
            SetCurrentValue(IsDropDownOpenProperty, true);
        }

        _calendar.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        CalendarOpened?.Invoke(this, EventArgs.Empty);
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        // Closed may arrive at the end of this dispatch; preserve any new transaction that has
        // already reopened.
        if (_popup?.IsOpen != true)
        {
            SetCurrentValue(IsDropDownOpenProperty, false);
        }

        CalendarClosed?.Invoke(this, EventArgs.Empty);
    }

    private void CloseAndRestoreFocus()
    {
        SetCurrentValue(IsDropDownOpenProperty, false);
        _textBox?.Focus();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Cancel();
    }

    private void OnIsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!(bool)e.NewValue)
        {
            Cancel();
        }

        CoerceValue(IsDropDownOpenProperty);
    }

    /// <summary>
    /// Provides UI Automation patterns for the complete text value and expand/collapse state.
    /// </summary>
    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new DateTimePickerAutomationPeer(this);
    }
}
