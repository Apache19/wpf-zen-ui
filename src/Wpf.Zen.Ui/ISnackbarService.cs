// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui;

/// <summary>Provides snackbar notifications through one explicitly attached presenter.</summary>
/// <remarks>
/// The service lifetime must match the owning presentation scope so independent visual trees never
/// share a presenter, queue, or current notification.
/// </remarks>
public interface ISnackbarService
{
    /// <summary>
    /// Gets or sets the fallback duration used when a show request supplies <see cref="TimeSpan.Zero"/>.
    /// </summary>
    TimeSpan DefaultTimeOut { get; set; }

    /// <summary>Attaches the service to its immutable presentation host.</summary>
    /// <param name="presenter">The only presenter through which this service may show notifications.</param>
    /// <exception cref="InvalidOperationException">The service was already attached to another presenter.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="presenter"/> is <see langword="null"/>.</exception>
    void Attach(SnackbarPresenter presenter);

    /// <summary>
    /// Shows a notification, replacing the notification currently displayed by the attached presenter.
    /// </summary>
    /// <param name="title">The notification title.</param>
    /// <param name="message">The notification message.</param>
    /// <param name="appearance">The semantic visual appearance.</param>
    /// <param name="icon">An optional icon displayed beside the text.</param>
    /// <param name="timeout">The display duration, or zero to use <see cref="DefaultTimeOut"/>.</param>
    void Show(
        string title,
        string message,
        ControlAppearance appearance,
        object? icon,
        TimeSpan timeout
    );
}
