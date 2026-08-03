// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using NativeDataGrid = System.Windows.Controls.DataGrid;
using NativeGridViewColumn = System.Windows.Controls.GridViewColumn;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Marks the last visible column header so a header template can remove its trailing resize handle.
/// </summary>
/// <remarks>
/// <para>
/// WPF exposes documented resize template parts on <see cref="GridViewColumnHeader"/> and
/// <see cref="DataGridColumnHeader"/>, but neither header exposes a styleable "last column"
/// property. This behavior supplies that missing structural state without replacing WPF's native
/// header presenters or intercepting drag input.
/// </para>
/// <para>
/// The Fluent templates use <see cref="IsLastColumnHeaderProperty"/> to make the final trailing
/// gripper non-hit-testable. Interior boundaries retain their native resize and double-click
/// behavior. Applications that replace the header templates can bind or trigger on the same state.
/// </para>
/// </remarks>
public static class ColumnHeaderResizeBehavior
{
    /// <summary>
    /// Identifies the <c>IsEnabled</c> attached dependency property.
    /// </summary>
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(ColumnHeaderResizeBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged)
    );

    private static readonly DependencyPropertyKey IsLastColumnHeaderPropertyKey =
        DependencyProperty.RegisterAttachedReadOnly(
            "IsLastColumnHeader",
            typeof(bool),
            typeof(ColumnHeaderResizeBehavior),
            new PropertyMetadata(false)
        );

    /// <summary>
    /// Identifies the read-only <c>IsLastColumnHeader</c> attached dependency property.
    /// </summary>
    public static readonly DependencyProperty IsLastColumnHeaderProperty =
        IsLastColumnHeaderPropertyKey.DependencyProperty;

    private static readonly DependencyProperty CoordinatorProperty = DependencyProperty.RegisterAttached(
        "Coordinator",
        typeof(Coordinator),
        typeof(ColumnHeaderResizeBehavior),
        new PropertyMetadata(null)
    );

    /// <summary>
    /// Gets whether <paramref name="element"/> publishes last-column state to its generated headers.
    /// </summary>
    /// <param name="element">A GridView or DataGrid column-header presenter.</param>
    /// <returns><see langword="true"/> when last-column tracking is enabled.</returns>
    public static bool GetIsEnabled(DependencyObject element)
    {
        return (bool)element.GetValue(IsEnabledProperty);
    }

    /// <summary>
    /// Sets whether <paramref name="element"/> publishes last-column state to its generated headers.
    /// </summary>
    /// <param name="element">A GridView or DataGrid column-header presenter.</param>
    /// <param name="value"><see langword="true"/> to enable tracking.</param>
    public static void SetIsEnabled(DependencyObject element, bool value)
    {
        element.SetValue(IsEnabledProperty, value);
    }

    /// <summary>
    /// Gets whether <paramref name="element"/> represents the last visible data column.
    /// </summary>
    /// <param name="element">A generated column header.</param>
    /// <returns><see langword="true"/> for the last visible data-column header.</returns>
    public static bool GetIsLastColumnHeader(DependencyObject element)
    {
        return (bool)element.GetValue(IsLastColumnHeaderProperty);
    }

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not GridViewHeaderRowPresenter && d is not DataGridColumnHeadersPresenter)
        {
            throw new InvalidOperationException(
                $"{nameof(ColumnHeaderResizeBehavior)}.{nameof(IsEnabledProperty)} can only be set on a GridViewHeaderRowPresenter or DataGridColumnHeadersPresenter."
            );
        }

        if (d.GetValue(CoordinatorProperty) is Coordinator previousCoordinator)
        {
            previousCoordinator.Detach();
            d.ClearValue(CoordinatorProperty);
        }

        if ((bool)e.NewValue)
        {
            var coordinator = new Coordinator((FrameworkElement)d);
            d.SetCurrentValue(CoordinatorProperty, coordinator);
        }
    }

    /// <summary>
    /// Keeps structural state synchronized with WPF's generated and virtualized header containers.
    /// </summary>
    private sealed class Coordinator
    {
        private readonly FrameworkElement _presenter;
        private DependencyObject? _lastHeader;
        private bool _isActive;

        internal Coordinator(FrameworkElement presenter)
        {
            _presenter = presenter;
            _presenter.Loaded += OnLoaded;
            _presenter.Unloaded += OnUnloaded;

            if (_presenter.IsLoaded)
            {
                Activate();
            }
        }

        internal void Detach()
        {
            _presenter.Loaded -= OnLoaded;
            _presenter.Unloaded -= OnUnloaded;
            Deactivate();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Activate();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Deactivate();
        }

        private void Activate()
        {
            if (_isActive)
            {
                return;
            }

            _isActive = true;

            // Header containers are regenerated by template application, column virtualization,
            // visibility changes, and display-index changes. LayoutUpdated is the one public WPF
            // signal covering all those paths. The handler only walks the small header subtree and
            // writes a dependency property when the last container identity actually changes.
            _presenter.LayoutUpdated += OnLayoutUpdated;
            UpdateLastHeader();
        }

        private void Deactivate()
        {
            if (!_isActive)
            {
                return;
            }

            _isActive = false;
            _presenter.LayoutUpdated -= OnLayoutUpdated;
            SetLastHeader(null);
        }

        private void OnLayoutUpdated(object? sender, EventArgs e)
        {
            UpdateLastHeader();
        }

        private void UpdateLastHeader()
        {
            DependencyObject? lastHeader = _presenter switch
            {
                GridViewHeaderRowPresenter gridViewPresenter => FindLastGridViewHeader(
                    gridViewPresenter
                ),
                DataGridColumnHeadersPresenter dataGridPresenter => FindLastDataGridHeader(
                    dataGridPresenter
                ),
                _ => null,
            };

            SetLastHeader(lastHeader);
        }

        private void SetLastHeader(DependencyObject? header)
        {
            if (ReferenceEquals(_lastHeader, header))
            {
                return;
            }

            if (_lastHeader is not null)
            {
                _lastHeader.ClearValue(IsLastColumnHeaderPropertyKey);
            }

            _lastHeader = header;
            if (_lastHeader is not null)
            {
                _lastHeader.SetValue(IsLastColumnHeaderPropertyKey, true);
            }
        }

        private static GridViewColumnHeader? FindLastGridViewHeader(
            GridViewHeaderRowPresenter presenter
        )
        {
            if (presenter.Columns is not { Count: > 0 } columns)
            {
                return null;
            }

            NativeGridViewColumn lastColumn = columns[columns.Count - 1];
            return FindVisualDescendants<GridViewColumnHeader>(presenter).FirstOrDefault(header =>
                header.Role == GridViewColumnHeaderRole.Normal
                && ReferenceEquals(header.Column, lastColumn)
            );
        }

        private static DataGridColumnHeader? FindLastDataGridHeader(
            DataGridColumnHeadersPresenter presenter
        )
        {
            NativeDataGrid? dataGrid = FindVisualAncestor<NativeDataGrid>(presenter);
            if (dataGrid is null)
            {
                return null;
            }

            DataGridColumn? lastColumn = null;
            foreach (DataGridColumn column in dataGrid.Columns)
            {
                if (
                    column.Visibility == Visibility.Visible
                    && (lastColumn is null || column.DisplayIndex > lastColumn.DisplayIndex)
                )
                {
                    lastColumn = column;
                }
            }

            if (lastColumn is null)
            {
                return null;
            }

            return FindVisualDescendants<DataGridColumnHeader>(presenter).FirstOrDefault(header =>
                ReferenceEquals(header.Column, lastColumn)
            );
        }

        private static T? FindVisualAncestor<T>(DependencyObject element)
            where T : DependencyObject
        {
            for (
                DependencyObject? current = VisualTreeHelper.GetParent(element);
                current is not null;
                current = VisualTreeHelper.GetParent(current)
            )
            {
                if (current is T result)
                {
                    return result;
                }
            }

            return null;
        }

        private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject parent)
            where T : DependencyObject
        {
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, index);
                if (child is T result)
                {
                    yield return result;
                }

                foreach (T descendant in FindVisualDescendants<T>(child))
                {
                    yield return descendant;
                }
            }
        }
    }
}