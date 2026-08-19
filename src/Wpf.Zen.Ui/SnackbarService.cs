// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui;

/// <summary>
/// Presents snackbar notifications through the explicit <see cref="SnackbarPresenter"/> attached
/// to this service instance.
/// </summary>
/// <remarks>
/// Register the service with the lifetime of its presentation scope and call <see cref="Attach"/>
/// once. Binding the presenter directly supports non-Window WPF roots and prevents independent
/// windows or plug-ins from combining their queues and destinations.
/// </remarks>
public class SnackbarService : ISnackbarService
{
    private SnackbarPresenter? _presenter;
    private Snackbar? _snackbar;

    /// <inheritdoc />
    public TimeSpan DefaultTimeOut { get; set; } = TimeSpan.FromSeconds(5);

    /// <inheritdoc />
    public void Attach(SnackbarPresenter presenter)
    {
        if (presenter is null)
        {
            throw new ArgumentNullException(nameof(presenter));
        }

        if (_presenter is not null)
        {
            if (ReferenceEquals(_presenter, presenter))
            {
                return;
            }

            throw new InvalidOperationException(
                "A snackbar service cannot change its presenter after attachment. "
                    + "Create one service instance per presentation scope."
            );
        }

        _presenter = presenter;
        _snackbar = new Snackbar(presenter);
    }

    /// <inheritdoc />
    public void Show(
        string title,
        string message,
        ControlAppearance appearance,
        object? icon,
        TimeSpan timeout
    )
    {
        Snackbar snackbar = GetSnackbarForPresentation();
        snackbar.SetCurrentValue(Snackbar.TitleProperty, title);
        snackbar.SetCurrentValue(System.Windows.Controls.ContentControl.ContentProperty, message);
        snackbar.SetCurrentValue(Snackbar.AppearanceProperty, appearance);
        snackbar.SetCurrentValue(IconAssist.IconProperty, icon);
        snackbar.SetCurrentValue(
            Snackbar.TimeoutProperty,
            timeout == TimeSpan.Zero ? DefaultTimeOut : timeout
        );

        snackbar.Show(true);
    }

    private Snackbar GetSnackbarForPresentation()
    {
        if (_presenter is null)
        {
            throw new InvalidOperationException(
                "The snackbar service has not been attached to a SnackbarPresenter."
            );
        }

        if (!_presenter.Dispatcher.CheckAccess())
        {
            throw new InvalidOperationException(
                "Snackbars must be shown on their presenter's dispatcher."
            );
        }

        if (!_presenter.IsLoaded)
        {
            throw new InvalidOperationException(
                "Snackbars can only be shown after their SnackbarPresenter has loaded."
            );
        }

        return _snackbar!;
    }
}
