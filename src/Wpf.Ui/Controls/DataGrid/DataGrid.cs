// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Specialized;
using System.Windows.Controls;
using System.Windows.Data;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// A DataGrid control that displays data in rows and columns and allows
/// for the entering and editing of data.
/// </summary>
[StyleTypedProperty(Property = nameof(CheckBoxColumnElementStyle), StyleTargetType = typeof(CheckBox))]
[StyleTypedProperty(Property = nameof(CheckBoxColumnEditingElementStyle), StyleTargetType = typeof(CheckBox))]
[StyleTypedProperty(Property = nameof(ComboBoxColumnElementStyle), StyleTargetType = typeof(ComboBox))]
[StyleTypedProperty(Property = nameof(ComboBoxColumnEditingElementStyle), StyleTargetType = typeof(ComboBox))]
[StyleTypedProperty(Property = nameof(TextColumnElementStyle), StyleTargetType = typeof(TextBlock))]
[StyleTypedProperty(Property = nameof(TextColumnEditingElementStyle), StyleTargetType = typeof(TextBox))]
public class DataGrid : System.Windows.Controls.DataGrid
{
    private readonly Dictionary<DataGridColumn, Dictionary<DependencyProperty, BindingBase>>
        _columnStyleBindings = new();
    private readonly Dictionary<DataGridColumn, Dictionary<DependencyProperty, ColumnStyleObserver>>
        _columnStyleObservers = new();
    private bool _isReconcilingColumnStyles;

    /// <summary>
    /// Identifies the <see cref="IsSelectionIndicatorVisible"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsSelectionIndicatorVisibleProperty =
        SelectionIndicator.IsSelectionIndicatorVisibleProperty.AddOwner(
            typeof(DataGrid),
            new FrameworkPropertyMetadata(
                true,
                FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender
            )
        );

    /// <summary>Identifies the <see cref="CheckBoxColumnElementStyle"/> dependency property.</summary>
    public static readonly DependencyProperty CheckBoxColumnElementStyleProperty =
        DependencyProperty.Register(
            nameof(CheckBoxColumnElementStyle),
            typeof(Style),
            typeof(DataGrid),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>Identifies the <see cref="CheckBoxColumnEditingElementStyle"/> dependency property.</summary>
    public static readonly DependencyProperty CheckBoxColumnEditingElementStyleProperty =
        DependencyProperty.Register(
            nameof(CheckBoxColumnEditingElementStyle),
            typeof(Style),
            typeof(DataGrid),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>Identifies the <see cref="ComboBoxColumnElementStyle"/> dependency property.</summary>
    public static readonly DependencyProperty ComboBoxColumnElementStyleProperty =
        DependencyProperty.Register(
            nameof(ComboBoxColumnElementStyle),
            typeof(Style),
            typeof(DataGrid),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>Identifies the <see cref="ComboBoxColumnEditingElementStyle"/> dependency property.</summary>
    public static readonly DependencyProperty ComboBoxColumnEditingElementStyleProperty =
        DependencyProperty.Register(
            nameof(ComboBoxColumnEditingElementStyle),
            typeof(Style),
            typeof(DataGrid),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>Identifies the <see cref="TextColumnElementStyle"/> dependency property.</summary>
    public static readonly DependencyProperty TextColumnElementStyleProperty = DependencyProperty.Register(
        nameof(TextColumnElementStyle),
        typeof(Style),
        typeof(DataGrid),
        new FrameworkPropertyMetadata(null)
    );

    /// <summary>Identifies the <see cref="TextColumnEditingElementStyle"/> dependency property.</summary>
    public static readonly DependencyProperty TextColumnEditingElementStyleProperty =
        DependencyProperty.Register(
            nameof(TextColumnEditingElementStyle),
            typeof(Style),
            typeof(DataGrid),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>
    /// Initializes static members of the <see cref="DataGrid"/> class.
    /// </summary>
    static DataGrid()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(DataGrid),
            new FrameworkPropertyMetadata(typeof(DataGrid))
        );
    }

    /// <summary>
    /// Gets or sets a value indicating whether the selected row displays an accent-colored indicator at its leading edge.
    /// </summary>
    public bool IsSelectionIndicatorVisible
    {
        get => (bool)GetValue(IsSelectionIndicatorVisibleProperty);
        set => SetValue(IsSelectionIndicatorVisibleProperty, value);
    }

    /// <summary>
    /// Gets or sets the style which is applied to all checkbox column in the DataGrid
    /// </summary>
    public Style? CheckBoxColumnElementStyle
    {
        get => (Style?)GetValue(CheckBoxColumnElementStyleProperty);
        set => SetValue(CheckBoxColumnElementStyleProperty, value);
    }

    /// <summary>
    /// Gets or sets the style for all the column checkboxes in the DataGrid
    /// </summary>
    public Style? CheckBoxColumnEditingElementStyle
    {
        get => (Style?)GetValue(CheckBoxColumnEditingElementStyleProperty);
        set => SetValue(CheckBoxColumnEditingElementStyleProperty, value);
    }

    /// <summary>
    /// Gets or sets the style which is applied to all combobox column in the DataGrid
    /// </summary>
    public Style? ComboBoxColumnElementStyle
    {
        get => (Style?)GetValue(ComboBoxColumnElementStyleProperty);
        set => SetValue(ComboBoxColumnElementStyleProperty, value);
    }

    /// <summary>
    /// Gets or sets the style for all the column comboboxes in the DataGrid
    /// </summary>
    public Style? ComboBoxColumnEditingElementStyle
    {
        get => (Style?)GetValue(ComboBoxColumnEditingElementStyleProperty);
        set => SetValue(ComboBoxColumnEditingElementStyleProperty, value);
    }

    /// <summary>
    /// Gets or sets the style which is applied to all textbox column in the DataGrid
    /// </summary>
    public Style? TextColumnElementStyle
    {
        get => (Style?)GetValue(TextColumnElementStyleProperty);
        set => SetValue(TextColumnElementStyleProperty, value);
    }

    /// <summary>
    /// Gets or sets the style for all the column textboxes in the DataGrid
    /// </summary>
    public Style? TextColumnEditingElementStyle
    {
        get => (Style?)GetValue(TextColumnEditingElementStyleProperty);
        set => SetValue(TextColumnEditingElementStyleProperty, value);
    }

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);

        Columns.CollectionChanged += ColumnsOnCollectionChanged;
        ReconcileColumnElementStyles();
    }

    private void ColumnsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ReconcileColumnElementStyles();
    }

    private void ReconcileColumnElementStyles()
    {
        // A DataGridColumn is not in the visual tree and therefore cannot inherit styles from its
        // owning grid. Bindings supply that missing inheritance. Remove only bindings installed by
        // this grid before a column is moved elsewhere; otherwise the detached column keeps this
        // DataGrid alive and continues to use its styles in the new owner.
        _isReconcilingColumnStyles = true;
        try
        {
            foreach (
                DataGridColumn trackedColumn in _columnStyleObservers
                    .Keys.Concat(_columnStyleBindings.Keys)
                    .Distinct()
                    .ToArray()
            )
            {
                if (!Columns.Contains(trackedColumn))
                {
                    RemoveColumnElementStyleTracking(trackedColumn);
                }
            }

            foreach (DataGridColumn singleColumn in Columns)
            {
                UpdateSingleColumn(singleColumn);
            }
        }
        finally
        {
            _isReconcilingColumnStyles = false;
        }
    }

    private void UpdateSingleColumn(DataGridColumn dataGridColumn)
    {
        switch (dataGridColumn)
        {
            case DataGridCheckBoxColumn checkBoxColumn:
                EnsureColumnElementStyleBinding(
                    checkBoxColumn,
                    DataGridBoundColumn.ElementStyleProperty,
                    CheckBoxColumnElementStyleProperty
                );
                EnsureColumnElementStyleBinding(
                    checkBoxColumn,
                    DataGridBoundColumn.EditingElementStyleProperty,
                    CheckBoxColumnEditingElementStyleProperty
                );

                break;

            case DataGridComboBoxColumn comboBoxColumn:
                EnsureColumnElementStyleBinding(
                    comboBoxColumn,
                    DataGridBoundColumn.ElementStyleProperty,
                    ComboBoxColumnElementStyleProperty
                );
                EnsureColumnElementStyleBinding(
                    comboBoxColumn,
                    DataGridBoundColumn.EditingElementStyleProperty,
                    ComboBoxColumnEditingElementStyleProperty
                );

                break;

            case DataGridTextColumn textBoxColumn:
                EnsureColumnElementStyleBinding(
                    textBoxColumn,
                    DataGridBoundColumn.ElementStyleProperty,
                    TextColumnElementStyleProperty
                );
                EnsureColumnElementStyleBinding(
                    textBoxColumn,
                    DataGridBoundColumn.EditingElementStyleProperty,
                    TextColumnEditingElementStyleProperty
                );

                break;
        }
    }

    private void EnsureColumnElementStyleBinding(
        DataGridColumn column,
        DependencyProperty targetProperty,
        DependencyProperty sourceProperty
    )
    {
        EnsureColumnStylePropertyTracking(column, targetProperty);

        // A local style or user-authored binding always wins. SetBinding is only used for an empty
        // slot so adding a column never overwrites an explicit per-column customization.
        if (column.ReadLocalValue(targetProperty) != DependencyProperty.UnsetValue)
        {
            return;
        }

        var binding = new Binding { Path = new PropertyPath(sourceProperty), Source = this };
        _ = BindingOperations.SetBinding(column, targetProperty, binding);

        if (!_columnStyleBindings.TryGetValue(column, out var bindings))
        {
            bindings = new Dictionary<DependencyProperty, BindingBase>();
            _columnStyleBindings.Add(column, bindings);
        }

        bindings[targetProperty] = binding;
    }

    private void EnsureColumnStylePropertyTracking(
        DataGridColumn column,
        DependencyProperty targetProperty
    )
    {
        if (!_columnStyleObservers.TryGetValue(column, out var observers))
        {
            observers = new Dictionary<DependencyProperty, ColumnStyleObserver>();
            _columnStyleObservers.Add(column, observers);
        }

        if (observers.ContainsKey(targetProperty))
        {
            return;
        }

        // DataGridColumn is not in the visual/logical tree, so ClearValue cannot naturally reveal
        // an inherited grid-level style. A binding observer supplies change notification without
        // DependencyPropertyDescriptor's global strong reference to the column. Explicit values and
        // bindings remain untouched because EnsureColumnElementStyleBinding checks precedence first.
        observers.Add(targetProperty, new ColumnStyleObserver(this, column, targetProperty));
    }

    private void OnColumnStylePropertyChanged(object? sender, EventArgs e)
    {
        if (
            _isReconcilingColumnStyles
            || sender is not DataGridColumn column
            || !Columns.Contains(column)
        )
        {
            return;
        }

        _isReconcilingColumnStyles = true;
        try
        {
            UpdateSingleColumn(column);
        }
        finally
        {
            _isReconcilingColumnStyles = false;
        }
    }

    private void RemoveColumnElementStyleTracking(DataGridColumn column)
    {
        RemoveColumnElementStyleBindings(column);

        if (!_columnStyleObservers.TryGetValue(column, out var observers))
        {
            return;
        }

        _columnStyleObservers.Remove(column);
        foreach (ColumnStyleObserver observer in observers.Values)
        {
            observer.Detach();
        }
    }

    private void RemoveColumnElementStyleBindings(DataGridColumn column)
    {
        // .NET Framework does not provide Dictionary.Remove(TKey, out TValue). Retrieve the
        // bindings first so this cleanup path remains available to every target framework.
        if (!_columnStyleBindings.TryGetValue(column, out var bindings))
        {
            return;
        }

        _columnStyleBindings.Remove(column);

        foreach (KeyValuePair<DependencyProperty, BindingBase> bindingEntry in bindings)
        {
            // The application may have replaced our binding after the column was added. Compare the
            // exact BindingBase instance so removal never clears that newer user-owned value.
            if (
                ReferenceEquals(
                    BindingOperations.GetBindingBase(column, bindingEntry.Key),
                    bindingEntry.Value
                )
            )
            {
                BindingOperations.ClearBinding(column, bindingEntry.Key);
            }
        }
    }

    /// <summary>
    /// Observes a column dependency property through WPF's binding engine. Unlike
    /// DependencyPropertyDescriptor, the observer is owned by the grid and does not register the
    /// column in a process-wide descriptor table, so an abandoned grid remains collectible.
    /// </summary>
    private sealed class ColumnStyleObserver : DependencyObject
    {
        private static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            "Value",
            typeof(object),
            typeof(ColumnStyleObserver),
            new PropertyMetadata(null, OnValueChanged)
        );

        private readonly DataGrid _owner;
        private readonly DataGridColumn _column;

        internal ColumnStyleObserver(
            DataGrid owner,
            DataGridColumn column,
            DependencyProperty targetProperty
        )
        {
            _owner = owner;
            _column = column;
            _ = BindingOperations.SetBinding(
                this,
                ValueProperty,
                new Binding
                {
                    Mode = BindingMode.OneWay,
                    Path = new PropertyPath(targetProperty),
                    Source = column,
                }
            );
        }

        internal void Detach()
        {
            BindingOperations.ClearBinding(this, ValueProperty);
        }

        private static void OnValueChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e
        )
        {
            var observer = (ColumnStyleObserver)d;
            observer._owner.OnColumnStylePropertyChanged(observer._column, EventArgs.Empty);
        }
    }
}
