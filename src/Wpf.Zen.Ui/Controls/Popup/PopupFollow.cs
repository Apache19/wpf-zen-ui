// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Reflection;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Windows.Win32;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Keeps an open Popup positioned relative to its moving placement target.
/// </summary>
/// <remarks>
/// <para>
/// Set IsEnabled on a Popup to follow window movement, scrolling, layout and render transforms.
/// PlacementTarget is the anchor; when omitted, WPF uses the Popup's containing visual.
/// Absolute and mouse placement modes retain their screen-relative semantics.
/// A disconnected target is ignored until it reconnects. Clipping or scrolling the target out of
/// view does not close the Popup; visibility and dismissal remain the owner's responsibility.
/// </para>
/// <para>
/// Tracking runs only while open. A render-frame geometry check covers animated transforms and
/// native host movement that do not raise WPF layout events. Unchanged geometry does not invoke
/// placement. WPF's internal Reposition method preserves bindings, custom placement, screen-edge
/// fitting, alignment, placement gaps, animation, capture and detached shadow synchronization.
/// If that method is unavailable in a future WPF version, following is disabled with a diagnostic.
/// </para>
/// </remarks>
public static class PopupFollow
{
#pragma warning disable WPF0061 // Document the behavior and its lifetime at the accessors.

    /// <summary>
    /// Identifies the opt-in attached property for target following. The default is false.
    /// </summary>
    public static readonly DependencyProperty IsEnabledProperty
        = DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(PopupFollow),
            new PropertyMetadata(false, OnIsEnabledChanged)
        );

    /// <summary>
    /// Gets whether the specified Popup follows its placement target while open.
    /// </summary>
    /// <param name="element">The Popup to inspect.</param>
    /// <returns>True when following is enabled; otherwise false.</returns>
    [AttachedPropertyBrowsableForType(typeof(Popup))]
    public static bool GetIsEnabled(DependencyObject element)
    {
        return (bool)element.GetValue(IsEnabledProperty);
    }

    /// <summary>
    /// Enables or disables target following, including on an already open Popup.
    /// </summary>
    /// <param name="element">The Popup to configure. Other objects only store the value.</param>
    /// <param name="value">
    /// True to follow; false to release tracking without closing the Popup.
    /// </param>
    public static void SetIsEnabled(DependencyObject element, bool value)
    {
        element.SetValue(IsEnabledProperty, value);
    }

#pragma warning restore WPF0061

    private static readonly DependencyProperty ControllerProperty
        = DependencyProperty.RegisterAttached(
            "Controller",
            typeof(FollowController),
            typeof(PopupFollow),
            new PropertyMetadata(null)
        );

    // Popup.cs: Reposition checks IsOpen and HWND lifetime before entering UpdatePosition.
    // Calling that entry point leaves every placement input and its value source intact.
    private static readonly Action<Popup>? Reposition = CreateRepositionCallback();

    /// <summary>
    /// Resolves the internal entry point once and preserves exceptions from custom placement code.
    /// </summary>
    private static Action<Popup>? CreateRepositionCallback()
    {
        try
        {
            var method = typeof(Popup).GetMethod(
                "Reposition",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null
            );

            return (Action<Popup>?)method?.CreateDelegate(typeof(Action<Popup>));
        }
        catch (Exception exception) when (
            exception is MemberAccessException
                or ArgumentException
                or System.Security.SecurityException)
        {
            // Restricted reflection or a changed runtime signature disables only this behavior.
            return null;
        }
    }

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Popup popup)
        {
            return;
        }

        if (popup.GetValue(ControllerProperty) is FollowController previous)
        {
            previous.Detach();
            popup.ClearValue(ControllerProperty);
        }

        if ((bool)e.NewValue)
        {
            if (Reposition is null)
            {
                Trace.TraceWarning("[Popup.Follow] REPOSITION unavailable; trackingEnabled=False.");
                return;
            }

            var controller = new FollowController(popup);
            popup.SetValue(ControllerProperty, controller);
            controller.Attach();
        }
    }

    /// <summary>
    /// Owns open-lifetime observation and compares target geometry before requesting placement.
    /// </summary>
    private sealed class FollowController
    {
        private readonly Popup _popup;

        private Observation?                                 _observation;
        private DispatcherOperation?                         _pending;
        private Visual?                                      _target;
        private (Point Origin, Point X, Point Y, Size Size)? _geometry;
        private bool                                         _updating;

        /// <summary>
        /// Associates tracking state with one Popup without starting global observation.
        /// </summary>
        public FollowController(Popup popup)
        {
            _popup = popup;
        }

        /// <summary>
        /// Observes lifecycle events and also supports enabling an already open Popup.
        /// </summary>
        public void Attach()
        {
            _popup.Opened   += OnOpened;
            _popup.Closed   += OnClosed;
            _popup.Loaded   += OnLoaded;
            _popup.Unloaded += OnUnloaded;

            Start();
        }

        /// <summary>
        /// Releases lifecycle handlers, pending work and open-lifetime observation.
        /// </summary>
        public void Detach()
        {
            _popup.Opened   -= OnOpened;
            _popup.Closed   -= OnClosed;
            _popup.Loaded   -= OnLoaded;
            _popup.Unloaded -= OnUnloaded;

            Stop();
        }

        private void OnOpened(object? sender, EventArgs e)
        {
            Start();
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            Stop();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Start();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Stop();
        }

        /// <summary>
        /// Starts a single observer, including for standalone Popups that never raise Loaded.
        /// </summary>
        private void Start()
        {
            if (!_popup.IsOpen || _observation is not null)
            {
                return;
            }

            _observation = new Observation(this);
            QueueUpdate();
        }

        /// <summary>
        /// Cancels work before releasing the anchor so reopening starts with fresh geometry.
        /// </summary>
        private void Stop()
        {
            _observation?.Detach();
            _observation = null;
            _pending?.Abort();
            _pending  = null;
            _target   = null;
            _geometry = null;
        }

        /// <summary>
        /// Coalesces layout and HWND notifications after the current WPF placement/layout pass.
        /// </summary>
        public void QueueUpdate()
        {
            if (_observation is null || _pending is not null || !_popup.IsOpen)
            {
                return;
            }

            _pending = _popup.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                _pending = null;
                Update();
            }));
        }

        /// <summary>
        /// Samples an affine screen basis and target size, including changes without layout events.
        /// </summary>
        public void Update()
        {
            if (_observation is null || !_popup.IsOpen || _updating)
            {
                return;
            }

            // Match Popup.GetTarget, including a visual parent separated by a Visual3D node.
            var parent = _popup.PlacementTarget ?? VisualTreeHelper.GetParent(_popup);
            while (parent is not null && parent is not Visual)
            {
                parent = VisualTreeHelper.GetParent(parent);
            }

            var target        = parent as Visual;
            var source        = target is null ? null : PresentationSource.FromVisual(target) as HwndSource;
            var sourceChanged = _observation.SetSource(source);

            if (source is null || target is null || _popup.Placement is PlacementMode.Absolute
                or PlacementMode.AbsolutePoint or PlacementMode.Mouse or PlacementMode.MousePoint)
            {
                _geometry = null;
                _target   = null;
                return;
            }

            (Point Origin, Point X, Point Y, Size Size) geometry;
            try
            {
                geometry = (target.PointToScreen(default), target.PointToScreen(new Point(1, 0)),
                            target.PointToScreen(new Point(0, 1)), (target as UIElement)?.RenderSize ?? default);
            }
            catch (InvalidOperationException)
            {
                // Visual/source teardown can briefly leave the screen transform unavailable.
                _geometry = null;
                return;
            }

            if (!sourceChanged && ReferenceEquals(_target, target) && _geometry == geometry)
            {
                return;
            }

            _target   = target;
            _geometry = geometry;
            _updating = true;
            try
            {
                Reposition!(_popup);
            }
            finally
            {
                _updating = false;
            }
        }

        /// <summary>
        /// Keeps global rendering and native source events from retaining an abandoned Popup.
        /// </summary>
        private sealed class Observation
        {
            private readonly WeakReference<FollowController> _owner;
            private          WeakReference<HwndSource>?      _source;

            /// <summary>
            /// Subscribes through a weak owner reference for the current open interval.
            /// </summary>
            public Observation(FollowController owner)
            {
                _owner                      =  new WeakReference<FollowController>(owner);
                CompositionTarget.Rendering += OnRendering;
                owner._popup.LayoutUpdated  += OnLayoutUpdated;
            }

            /// <summary>
            /// Moves the native hook when the target changes presentation sources.
            /// </summary>
            public bool SetSource(HwndSource? source)
            {
                HwndSource? previous = null;
                _source?.TryGetTarget(out previous);

                if (ReferenceEquals(source, previous))
                {
                    return false;
                }

                previous?.RemoveHook(OnWindowMessage);

                // Rendering must not retain a source's visual tree, which may contain the Popup.
                _source = source is null ? null : new WeakReference<HwndSource>(source);
                source?.AddHook(OnWindowMessage);

                return true;
            }

            /// <summary>
            /// Removes global and native subscriptions even after the owner has been collected.
            /// </summary>
            public void Detach()
            {
                CompositionTarget.Rendering -= OnRendering;

                if (_owner.TryGetTarget(out var owner))
                {
                    owner._popup.LayoutUpdated -= OnLayoutUpdated;
                }

                SetSource(null);
            }

            private void OnRendering(object? sender, EventArgs e)
            {
                if (_owner.TryGetTarget(out var owner))
                {
                    owner.Update();
                }
                else
                {
                    Detach();
                }
            }

            private void OnLayoutUpdated(object? sender, EventArgs e)
            {
                if (_owner.TryGetTarget(out var owner))
                {
                    owner.QueueUpdate();
                }
                else
                {
                    Detach();
                }
            }

            private IntPtr OnWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
            {
                if ((uint)msg is PInvoke.WM_WINDOWPOSCHANGED or PInvoke.WM_DPICHANGED)
                {
                    // Preserve native message dispatch; shadows and nested Popups observe their
                    // own HWND movement through the existing library hooks after repositioning.
                    OnLayoutUpdated(this, EventArgs.Empty);
                }

                return IntPtr.Zero;
            }
        }
    }
}