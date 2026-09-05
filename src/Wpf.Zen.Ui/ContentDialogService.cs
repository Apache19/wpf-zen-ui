// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui;

/// <summary>
/// Presents <see cref="ContentDialog"/> instances through the explicit
/// <see cref="ContentDialogHost"/> attached to this service instance.
/// </summary>
/// <remarks>
/// Create one service per presentation scope and call <see cref="Attach"/> once after constructing
/// its visual tree. Binding directly to the host avoids assuming that the tree is owned by a WPF
/// Window while still preventing a later scope from redirecting an already injected service.
/// </remarks>
public class ContentDialogService : IContentDialogService
{
    private ContentDialogHost? _dialogHost;

    static ContentDialogService()
    {
        LibraryInitializer.EnsureInitialized();
    }

    /// <inheritdoc />
    public void Attach(ContentDialogHost dialogHost)
    {
        if (dialogHost is null)
        {
            throw new ArgumentNullException(nameof(dialogHost));
        }

        if (_dialogHost is not null)
        {
            if (ReferenceEquals(_dialogHost, dialogHost))
            {
                return;
            }

            throw new InvalidOperationException(
                "A content dialog service cannot change its host after attachment. "
                    + "Create one service instance per presentation scope."
            );
        }

        _dialogHost = dialogHost;
    }

    /// <inheritdoc />
    public Task<object?> ShowAsync(
        ContentDialog dialog,
        CancellationToken cancellationToken
    )
    {
        if (dialog is null)
        {
            throw new ArgumentNullException(nameof(dialog));
        }

        ContentDialogHost dialogHost = GetHostForPresentation();

        return dialogHost.ShowAsync(dialog, cancellationToken);
    }

    private ContentDialogHost GetHostForPresentation()
    {
        if (_dialogHost is null)
        {
            throw new InvalidOperationException(
                "The content dialog service has not been attached to a ContentDialogHost."
            );
        }

        if (!_dialogHost.Dispatcher.CheckAccess())
        {
            throw new InvalidOperationException(
                "Content dialogs must be shown on their host's dispatcher."
            );
        }

        if (!_dialogHost.IsLoaded)
        {
            throw new InvalidOperationException(
                "Content dialogs can only be shown after their ContentDialogHost has loaded."
            );
        }

        return _dialogHost;
    }
}