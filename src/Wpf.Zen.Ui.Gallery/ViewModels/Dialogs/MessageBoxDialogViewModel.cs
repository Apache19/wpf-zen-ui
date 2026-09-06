// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.Models;

namespace Wpf.Zen.Ui.Gallery.ViewModels.Dialogs;

/// <summary>
/// Provides immutable binding values for one presentation of the reusable message-box dialog.
/// </summary>
/// <remarks>
/// Action roles are assigned here instead of being accepted as arbitrary command parameters from
/// callers. Consequently the service always returns one of the documented, strongly typed results.
/// </remarks>
public sealed class MessageBoxDialogViewModel
{
    /// <summary>Initializes a new instance of the <see cref="MessageBoxDialogViewModel"/> class.</summary>
    /// <param name="options">The immutable request used to compose this presentation.</param>
    public MessageBoxDialogViewModel(MessageBoxDialogOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.Title))
        {
            throw new ArgumentException("A message-box title is required.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.Message))
        {
            throw new ArgumentException("A message-box message is required.", nameof(options));
        }

        Title         = options.Title;
        Message       = options.Message;
        Icon          = options.Icon ?? SymbolRegular.Empty;
        HasIcon       = options.Icon.HasValue;
        IsDismissible = options.IsDismissible;

        var actions = new List<MessageBoxDialogActionViewModel>(3)
        {
            CreateAction(options.PrimaryButton, MessageBoxDialogResult.Primary),
        };

        if (options.SecondaryButton is { } secondaryButton)
        {
            actions.Add(CreateAction(secondaryButton, MessageBoxDialogResult.Secondary));
        }

        if (options.CloseButton is { } closeButton)
        {
            actions.Add(CreateAction(closeButton, MessageBoxDialogResult.Close, isCancel: true));
        }

        Actions = actions.AsReadOnly();
    }

    /// <summary>Gets the heading displayed by the dialog.</summary>
    public string Title { get; }

    /// <summary>Gets the message displayed in the dialog body.</summary>
    public string Message { get; }

    /// <summary>Gets the Fluent icon displayed beside the message.</summary>
    public SymbolRegular Icon { get; }

    /// <summary>Gets a value indicating whether the icon slot is visible.</summary>
    public bool HasIcon { get; }

    /// <summary>
    /// Gets a value indicating whether the independent top-right dismissal surface is available.
    /// </summary>
    public bool IsDismissible { get; }

    /// <summary>Gets the ordered footer actions displayed by the dialog.</summary>
    public IReadOnlyList<MessageBoxDialogActionViewModel> Actions { get; }

    private static MessageBoxDialogActionViewModel CreateAction(
        MessageBoxDialogButtonOptions options,
        MessageBoxDialogResult result,
        bool isCancel = false)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.Text))
        {
            throw new ArgumentException("A message-box action requires text.", nameof(options));
        }

        return new MessageBoxDialogActionViewModel(
            options.Text,
            options.Appearance,
            options.IsDefault,
            isCancel,
            result
        );
    }
}
