// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Invokes NavigationView's strongly typed routed handlers without reflection.
/// </summary>
/// <remarks>
/// RoutedEventArgs otherwise falls back to Delegate.DynamicInvoke for custom delegate types, which
/// wraps application exceptions in TargetInvocationException and changes their observable type.
/// This internal event-data type keeps the public RoutedEventArgs contract while providing WPF's
/// intended strongly typed invocation path.
/// </remarks>
internal sealed class NavigationViewRoutedEventArgs : RoutedEventArgs
{
    /// <summary>Initializes a new instance of the <see cref="NavigationViewRoutedEventArgs"/> class.</summary>
    internal NavigationViewRoutedEventArgs(RoutedEvent routedEvent, NavigationView source)
        : base(routedEvent, source)
    {
    }

    /// <inheritdoc />
    protected override void InvokeEventHandler(Delegate genericHandler, object genericTarget)
    {
        ((TypedEventHandler<NavigationView, RoutedEventArgs>)genericHandler)(
            (NavigationView)genericTarget,
            this
        );
    }
}
