// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Infrastructure;
using Wpf.Zen.Ui.VirtualInput.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls;

/// <summary>
/// Verifies observation contracts and collection while the owning dispatcher remains alive.
/// </summary>
public class DependencyPropertyObservationTests
{
    /// <summary>
    /// Initial transfer, duplicate registration, removal and collection clearing are silent.
    /// </summary>
    [Fact]
    public void SubscriptionLifecycle_NotifiesOnlySourceChanges()
    {
        WpfTestThread.Run(() =>
        {
            var source = new Border { CornerRadius = new CornerRadius(2) };
            var subscriptions = new DependencyPropertySubscriptions();
            var calls = 0;
            EventHandler callback = (sender, _) =>
            {
                Assert.Same(source, sender);
                calls++;
            };

            subscriptions.Add(source, Border.CornerRadiusProperty, callback);
            subscriptions.Add(source, Border.CornerRadiusProperty, callback);
            Assert.Equal(0, calls);
            source.CornerRadius = new CornerRadius(3);
            Assert.Equal(1, calls);

            subscriptions.Remove(source, Border.CornerRadiusProperty, callback);
            subscriptions.Remove(source, Border.CornerRadiusProperty, callback);
            source.CornerRadius = new CornerRadius(4);
            Assert.Equal(1, calls);

            subscriptions.Add(source, Border.CornerRadiusProperty, callback);
            source.CornerRadius = new CornerRadius(5);
            Assert.Equal(2, calls);
            subscriptions.Clear();
            subscriptions.Clear();
            source.CornerRadius = new CornerRadius(6);
            Assert.Equal(2, calls);
        });
    }

    /// <summary>
    /// Handlers may stop their own observation during notification without recursive callbacks.
    /// </summary>
    [Fact]
    public void CallbackCanClearSubscriptions()
    {
        WpfTestThread.Run(() =>
        {
            var source = new Border();
            var subscriptions = new DependencyPropertySubscriptions();
            var calls = 0;
            subscriptions.Add(source, Border.CornerRadiusProperty, (_, _) =>
            {
                calls++;
                subscriptions.Clear();
            });
            source.CornerRadius = new CornerRadius(1);
            source.CornerRadius = new CornerRadius(2);
            Assert.Equal(1, calls);
        });
    }

    /// <summary>
    /// Observation and detachment preserve the consumer's source binding and subsequent transfers.
    /// </summary>
    [Fact]
    public void ObservationPreservesSourceBinding()
    {
        WpfTestThread.Run(() =>
        {
            var upstream = new Border();
            var source = new Border();
            var binding = new Binding
            {
                Source = upstream,
                Path = new PropertyPath(Border.CornerRadiusProperty),
            };
            BindingOperations.SetBinding(source, Border.CornerRadiusProperty, binding);
            var calls = 0;
            var observer = new DependencyPropertyObserver(
                source, Border.CornerRadiusProperty, (_, _) => calls++);
            upstream.CornerRadius = new CornerRadius(4);
            Assert.Equal(1, calls);
            observer.Detach();
            Assert.Equal(1, calls);
            Assert.Same(binding, BindingOperations.GetBindingBase(source, Border.CornerRadiusProperty));
            upstream.CornerRadius = new CornerRadius(8);
            Assert.Equal(new CornerRadius(8), source.CornerRadius);
            Assert.Equal(1, calls);
        });
    }

    /// <summary>
    /// An externally retained source must not keep the observer or its callback target alive.
    /// </summary>
    [Fact]
    public void LiveSourceDoesNotRetainObserverOrListener()
    {
        WpfTestThread.Run(() =>
        {
            var source = new Border();
            var references = CreateListener(source);
            AssertCollected(references);
            source.CornerRadius = new CornerRadius(5);
            GC.KeepAlive(source);
        });
    }

    /// <summary>
    /// Retaining a detached observer must not retain its former source or callback target.
    /// </summary>
    [Fact]
    public void DetachedObserverReleasesSourceAndListener()
    {
        WpfTestThread.Run(() =>
        {
            var (observer, references) = CreateDetachedObserver();
            AssertCollected(references);
            observer.Detach();
            GC.KeepAlive(observer);
        });
    }

    /// <summary>
    /// Controls discarded before Loaded/Opened must be collectible without explicit teardown.
    /// </summary>
    [Theory]
    [InlineData("ToggleSwitch")]
    [InlineData("Popup")]
    [InlineData("ContextMenu")]
    [InlineData("ToolTip")]
    [InlineData("FocusPresenter")]
    [InlineData("DataGrid")]
    [InlineData("KeyboardTemplate")]
    public void UnloadedOwnerCanBeCollected(string scenario)
    {
        WpfTestThread.Run(() =>
        {
            var references = CreateUnloadedOwner(scenario);
            AssertCollected(references);
        });
    }

    /// <summary>
    /// A replaced focus target stops driving geometry, including after explicit detachment.
    /// </summary>
    [Fact]
    public void FocusTargetReplacementDetachesOldSource()
    {
        WpfTestThread.Run(() =>
        {
            var first = new Border { CornerRadius = new CornerRadius(1) };
            var second = new Border { CornerRadius = new CornerRadius(2) };
            var presenter = new FocusVisualPresenter();
            presenter.SetAdornedElement(first);
            presenter.SetAdornedElement(second);
            first.CornerRadius = new CornerRadius(3);
            Assert.Equal(new CornerRadius(2), presenter.CornerRadius);
            second.CornerRadius = new CornerRadius(4);
            Assert.Equal(new CornerRadius(4), presenter.CornerRadius);
            presenter.SetAdornedElement(null);
            var detachedRadius = presenter.CornerRadius;
            second.CornerRadius = new CornerRadius(5);
            Assert.Equal(detachedRadius, presenter.CornerRadius);
        });
    }

    /// <summary>
    /// Closed popups are collectible while their former placement target remains in a live window.
    /// </summary>
    [Fact]
    public void ReopenedPopupCanBeCollectedAfterClose()
    {
        WpfTestThread.Run(() =>
        {
            var target = new Border();
            var window = new Window
            {
                Content = target, Width = 160, Height = 100, ShowInTaskbar = false,
                ShowActivated = false,
            };
            try
            {
                window.Show();
                window.UpdateLayout();
                var references = CreateClosedPopup(target);
                AssertCollected(references);
                GC.KeepAlive(target);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Exercises repeated open/close subscriptions without disabling the attached behaviors.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] CreateClosedPopup(Border target)
    {
        var child = new Border { Width = 40, Height = 20 };
        var popup = new Popup
        {
            PlacementTarget = target, Child = child, PopupAnimation = PopupAnimation.None,
            AllowsTransparency = true,
        };
        PopupShadow.SetIsEnabled(popup, true);
        PopupSlideAnimator.SetIsEnabled(popup, true);
        PopupPlacementTracker.SetIsEnabled(popup, true);
        try
        {
            for (var cycle = 0; cycle < 3; cycle++)
            {
                popup.IsOpen = true;
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                popup.HorizontalOffset = cycle + 1;
                popup.IsOpen = false;
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.Equal(PopupPlacementDirection.None, PopupPlacementTracker.GetActualPlacement(popup));
            }
        }
        finally
        {
            popup.IsOpen = false;
        }

        return [new WeakReference(popup), new WeakReference(child)];
    }

    /// <summary>
    /// Creates a callback cycle outside the collecting stack frame to avoid JIT local lifetimes.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] CreateListener(Border source)
    {
        var listener = new Listener();
        listener.Observer = new DependencyPropertyObserver(
            source, Border.CornerRadiusProperty, listener.OnChanged);
        return [new WeakReference(listener), new WeakReference(listener.Observer)];
    }

    /// <summary>
    /// Keeps only the detached observer strongly rooted for the release-contract test.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (DependencyPropertyObserver, WeakReference[]) CreateDetachedObserver()
    {
        var source = new Border();
        var listener = new Listener();
        var observer = new DependencyPropertyObserver(
            source, Border.CornerRadiusProperty, listener.OnChanged);
        observer.Detach();
        return (observer, [new WeakReference(source), new WeakReference(listener)]);
    }

    /// <summary>
    /// Exercises production attachment paths without relying on a Loaded/Unloaded pair.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] CreateUnloadedOwner(string scenario)
    {
        if (scenario == "ToggleSwitch")
        {
            return [new WeakReference(new ToggleSwitch())];
        }

        if (scenario == "KeyboardTemplate")
        {
            var template = (ControlTemplate)XamlReader.Parse(
                "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " +
                "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>" +
                "<Popup x:Name='PART_AlternatePopup'/></ControlTemplate>");
            var key = new SoftKeyboardKeyControl { Template = template };
            Assert.True(key.ApplyTemplate());
            var popup = Assert.IsType<Popup>(template.FindName("PART_AlternatePopup", key));
            Assert.False(key.IsLoaded);
            return [new WeakReference(key), new WeakReference(popup)];
        }

        if (scenario == "FocusPresenter")
        {
            var source = new Border();
            var presenter = new FocusVisualPresenter();
            presenter.SetAdornedElement(source);
            return [new WeakReference(source), new WeakReference(presenter)];
        }

        if (scenario == "DataGrid")
        {
            var grid = new Wpf.Zen.Ui.Controls.DataGrid();
            var column = new DataGridTextColumn();
            grid.Columns.Add(column);
            return [new WeakReference(grid), new WeakReference(column)];
        }

        DependencyObject owner = scenario switch
        {
            "Popup" => new Popup { Child = new Border() },
            "ContextMenu" => new System.Windows.Controls.ContextMenu(),
            "ToolTip" => new System.Windows.Controls.ToolTip(),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
        PopupShadow.SetIsEnabled(owner, true);
        PopupSlideAnimator.SetIsEnabled(owner, true);
        return [new WeakReference(owner)];
    }

    /// <summary>
    /// Flushes pending binding work before collection, keeping the dispatcher alive throughout.
    /// </summary>
    private static void AssertCollected(WeakReference[] references)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        Assert.All(references, reference => Assert.False(reference.IsAlive));
    }

    /// <summary>
    /// Models a controller owning its observer while the observer retains the controller callback.
    /// </summary>
    private sealed class Listener
    {
        internal DependencyPropertyObserver? Observer { get; set; }

        internal void OnChanged(object? sender, EventArgs args)
        {
            GC.KeepAlive(this);
        }
    }
}
