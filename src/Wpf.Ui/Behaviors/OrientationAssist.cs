// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Media.Media3D;

namespace Wpf.Ui.Behaviors;

/// <summary>
/// Provides attached behavior for detecting the nearest parent layout orientation.
/// </summary>
public static class OrientationAssist
{
    /// <summary>
    /// Identifies the <c>AutoOrientation</c> attached property.
    /// </summary>
    public static readonly DependencyProperty AutoOrientationProperty =
        DependencyProperty.RegisterAttached(
            "AutoOrientation",
            typeof(bool),
            typeof(OrientationAssist),
            new PropertyMetadata(false, OnAutoOrientationChanged)
        );

    private static readonly DependencyPropertyKey DetectedOrientationPropertyKey =
        DependencyProperty.RegisterAttachedReadOnly(
            "DetectedOrientation",
            typeof(Orientation),
            typeof(OrientationAssist),
            new PropertyMetadata(Orientation.Vertical)
        );

    /// <summary>
    /// Identifies the read-only <c>DetectedOrientation</c> attached property.
    /// </summary>
    public static readonly DependencyProperty DetectedOrientationProperty =
        DetectedOrientationPropertyKey.DependencyProperty;

    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached(
            "State",
            typeof(OrientationDetectionState),
            typeof(OrientationAssist),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Gets whether the element automatically detects its nearest parent orientation.
    /// </summary>
    /// <param name="element">
    /// <see cref="FrameworkElement"/> to read <see cref="AutoOrientationProperty"/> from.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if automatic orientation detection is enabled; otherwise <see langword="false"/>.
    /// </returns>
    [AttachedPropertyBrowsableForType(typeof(FrameworkElement))]
    public static bool GetAutoOrientation(FrameworkElement element)
    {
        return (bool)element.GetValue(AutoOrientationProperty);
    }

    /// <summary>
    /// Sets whether the element automatically detects its nearest parent orientation.
    /// </summary>
    /// <param name="element">
    /// <see cref="FrameworkElement"/> to set <see cref="AutoOrientationProperty"/> on.
    /// </param>
    /// <param name="value">AutoOrientation property value.</param>
    public static void SetAutoOrientation(FrameworkElement element, bool value)
    {
        element.SetValue(AutoOrientationProperty, value);
    }

    /// <summary>
    /// Gets the nearest detected parent orientation. Defaults to <see cref="Orientation.Vertical"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="FrameworkElement"/> to read <see cref="DetectedOrientationProperty"/> from.
    /// </param>
    /// <returns>The detected parent orientation.</returns>
    [AttachedPropertyBrowsableForType(typeof(FrameworkElement))]
    public static Orientation GetDetectedOrientation(FrameworkElement element)
    {
        return (Orientation)element.GetValue(DetectedOrientationProperty);
    }

    /// <summary>
    /// Re-runs parent orientation detection for an element with <see cref="AutoOrientationProperty"/> enabled.
    /// </summary>
    /// <param name="element">The target element.</param>
    public static void RefreshOrientation(FrameworkElement element)
    {
        if (element.GetValue(StateProperty) is OrientationDetectionState state)
        {
            state.Refresh();
        }
    }

    private static void OnAutoOrientationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            var state = (OrientationDetectionState?)element.GetValue(StateProperty);
            if (state == null)
            {
                state = new OrientationDetectionState(element);
                element.SetValue(StateProperty, state);
            }

            state.Start();
            return;
        }

        if (element.GetValue(StateProperty) is OrientationDetectionState existingState)
        {
            existingState.Stop();
            element.ClearValue(StateProperty);
        }
    }

    private static void SetDetectedOrientation(FrameworkElement element, Orientation value)
    {
        element.SetValue(DetectedOrientationPropertyKey, value);
    }

    private sealed class OrientationDetectionState
    {
        private readonly FrameworkElement    _element;
        private          DependencyObject?   _orientationSource;
        private          PropertyDescriptor? _orientationProperty;
        private          bool                _isStarted;

        public OrientationDetectionState(FrameworkElement element)
        {
            _element = element;
        }

        public void Start()
        {
            if (_isStarted)
            {
                return;
            }

            _element.Loaded   += OnLoaded;
            _element.Unloaded += OnUnloaded;
            _isStarted        =  true;

            Refresh();
        }

        public void Stop()
        {
            if (!_isStarted)
            {
                return;
            }

            _element.Loaded   -= OnLoaded;
            _element.Unloaded -= OnUnloaded;

            DetachOrientationSource();
            SetDetectedOrientation(_element, Orientation.Vertical);

            _isStarted = false;
        }

        public void Refresh()
        {
            DetachOrientationSource();

            var match = FindNearestOrientationSource(_element);
            if (match == null)
            {
                SetDetectedOrientation(_element, Orientation.Vertical);
                return;
            }

            _orientationSource   = match.Source;
            _orientationProperty = match.Property;

            _orientationProperty.AddValueChanged(_orientationSource, OnSourceOrientationChanged);

            UpdateDetectedOrientation();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            DetachOrientationSource();
        }

        private void OnSourceOrientationChanged(object? sender, EventArgs e)
        {
            UpdateDetectedOrientation();
        }

        private void UpdateDetectedOrientation()
        {
            if (_orientationSource is null ||
                _orientationProperty?.GetValue(_orientationSource) is not Orientation orientation)
            {
                SetDetectedOrientation(_element, Orientation.Vertical);
                return;
            }

            SetDetectedOrientation(_element, orientation);
        }

        private void DetachOrientationSource()
        {
            if (_orientationSource is not null && _orientationProperty is not null)
            {
                _orientationProperty.RemoveValueChanged(
                    _orientationSource,
                    OnSourceOrientationChanged
                );
            }

            _orientationSource   = null;
            _orientationProperty = null;
        }
    }

    private sealed record OrientationSource(DependencyObject Source, PropertyDescriptor Property);

    private static OrientationSource? FindNearestOrientationSource(DependencyObject element)
    {
        var current = GetParent(element);

        while (current is not null)
        {
            var orientationProperty = GetOrientationProperty(current);
            if (orientationProperty is not null)
            {
                return new OrientationSource(current, orientationProperty);
            }

            current = GetParent(current);
        }

        return null;
    }

    private static PropertyDescriptor? GetOrientationProperty(DependencyObject element)
    {
        var property = TypeDescriptor.GetProperties(element)["Orientation"];

        return property?.PropertyType == typeof(Orientation) ? property : null;
    }

    private static DependencyObject? GetParent(DependencyObject element)
    {
        var logicalParent = LogicalTreeHelper.GetParent(element);
        if (logicalParent is not null)
        {
            return logicalParent;
        }

        return element is Visual or Visual3D ? VisualTreeHelper.GetParent(element) : null;
    }
}
