// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui;

/// <summary>Describes the outcome of a navigation request.</summary>
public enum NavigationResultStatus
{
    /// <summary>The host rejected or had no state change for the request.</summary>
    Rejected,

    /// <summary>The destination was successfully entered.</summary>
    Succeeded,

    /// <summary>No navigation item matched a requested id or tag.</summary>
    TargetNotFound,

    /// <summary>No live named navigation scope matched an explicit routing request.</summary>
    HostNotFound,
}

/// <summary>Contains the status returned by a navigation operation.</summary>
public readonly struct NavigationResult
{
    /// <summary>Initializes a new instance of the <see cref="NavigationResult"/> struct.</summary>
    /// <param name="status">The completed operation status.</param>
    public NavigationResult(NavigationResultStatus status)
    {
        Status = status;
    }

    /// <summary>Gets the completed operation status.</summary>
    public NavigationResultStatus Status { get; }

    /// <summary>Gets a value indicating whether navigation was performed successfully.</summary>
    public bool Succeeded => Status == NavigationResultStatus.Succeeded;

    /// <summary>Gets a successful navigation result.</summary>
    public static NavigationResult Success => new(NavigationResultStatus.Succeeded);

    /// <summary>Gets a result indicating that the host did not navigate.</summary>
    public static NavigationResult Rejected => new(NavigationResultStatus.Rejected);

    /// <summary>Gets a result indicating that a target id or tag was not registered.</summary>
    public static NavigationResult TargetNotFound => new(NavigationResultStatus.TargetNotFound);

    /// <summary>Gets a result indicating that an explicitly named host is not available.</summary>
    public static NavigationResult HostNotFound => new(NavigationResultStatus.HostNotFound);
}
