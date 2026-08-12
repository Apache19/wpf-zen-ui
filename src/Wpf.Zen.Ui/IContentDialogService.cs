// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui;

/// <summary>
/// Presents content dialogs through exactly one explicitly attached <see cref="ContentDialogHost"/>.
/// </summary>
/// <remarks>
/// The service lifetime must match the lifetime of the UI scope that owns the host. The host can
/// belong to a Window, an ElementHost tree, an HwndSource, or an independently composed plug-in.
/// </remarks>
public interface IContentDialogService
{
    /// <summary>
    /// Attaches the service to its immutable presentation host.
    /// </summary>
    /// <param name="dialogHost">The only host through which this service may present dialogs.</param>
    /// <exception cref="InvalidOperationException">
    /// The service was already attached to a different host.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="dialogHost"/> is <see langword="null"/>.</exception>
    void Attach(ContentDialogHost dialogHost);

    /// <summary>Asynchronously shows <paramref name="dialog"/> through the attached host.</summary>
    /// <param name="dialog">The dialog to display.</param>
    /// <param name="cancellationToken">Cancels the dialog operation.</param>
    /// <returns>A task whose result identifies the button used to close the dialog.</returns>
    Task<ContentDialogResult> ShowAsync(
        ContentDialog dialog,
        CancellationToken cancellationToken
    );
}
