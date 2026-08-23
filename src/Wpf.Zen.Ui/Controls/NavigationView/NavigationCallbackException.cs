// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Adds navigation context to an exception thrown by an <see cref="INavigationAware"/> callback.
/// </summary>
/// <remarks>
/// WPF commits object navigation on a dispatcher operation after the original navigation method
/// has returned. Consequently a callback failure cannot retain the caller's stack. This exception
/// records the component, displayed content, semantic target, and lifecycle stage so the failure
/// remains attributable when it reaches the dispatcher exception boundary.
/// </remarks>
public sealed class NavigationCallbackException : Exception
{
    internal NavigationCallbackException(
        NavigationCallbackStage stage,
        object content,
        INavigationAware callbackTarget,
        NavigationTarget? navigationTarget,
        Exception innerException
    )
        : base(CreateMessage(stage, content, callbackTarget, navigationTarget), innerException)
    {
        Stage = stage;
        Content = content;
        CallbackTarget = callbackTarget;
        NavigationTarget = navigationTarget;
    }

    /// <summary>Gets the lifecycle stage whose callback failed.</summary>
    public NavigationCallbackStage Stage { get; }

    /// <summary>Gets the page or content object being transitioned.</summary>
    public object Content { get; }

    /// <summary>Gets the view or view model whose callback failed.</summary>
    public INavigationAware CallbackTarget { get; }

    /// <summary>
    /// Gets the semantic occurrence associated with <see cref="Content"/>, or
    /// <see langword="null"/> for content replacement that has no navigation target.
    /// </summary>
    public NavigationTarget? NavigationTarget { get; }

    private static string CreateMessage(
        NavigationCallbackStage stage,
        object content,
        INavigationAware callbackTarget,
        NavigationTarget? navigationTarget
    )
    {
        string targetDescription = navigationTarget is null
            ? "an untracked content replacement"
            : $"navigation target '{navigationTarget}'";

        return $"The {stage} navigation callback on '{callbackTarget.GetType().FullName}' failed "
            + $"for content '{content.GetType().FullName}' and {targetDescription}.";
    }
}
