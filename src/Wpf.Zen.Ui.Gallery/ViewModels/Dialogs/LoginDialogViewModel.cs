// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Gallery.Models;

namespace Wpf.Zen.Ui.Gallery.ViewModels.Dialogs;

/// <summary>
/// Owns input, validation and completion for the local login demonstration.
/// No credentials are transmitted, persisted or verified against GitHub.
/// </summary>
public partial class LoginDialogViewModel : ObservableObject
{
    /// <summary>Raised after successful demo validation; the presentation owner closes the view.</summary>
    public event Action<LoginDialogResult>? SignInCompleted;

    /// <summary>Gets or sets the demo account name, initially empty.</summary>
    [ObservableProperty]
    public partial string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the demo password. This sample uses a bindable string for demonstration only,
    /// not production credential handling; the service clears it after every presentation.
    /// </summary>
    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    /// <summary>Gets the latest validation message, initially empty.</summary>
    [ObservableProperty]
    public partial string ErrorMessage { get; private set; } = string.Empty;

    [RelayCommand]
    private void SignIn()
    {
        ErrorMessage = string.Empty;

        string userName = UserName.Trim();
        if (userName.Length == 0 || Password.Length == 0)
        {
            ErrorMessage = "Enter a username and password.";
            return;
        }

        // Fixed, publicly displayed credentials keep this UI example distinct from real authentication.
        if (userName != "octocat" || Password != "demo")
        {
            ErrorMessage = "Use the demo account: octocat / demo.";
            return;
        }

        ErrorMessage = string.Empty;
        Password = string.Empty;
        SignInCompleted?.Invoke(new LoginDialogResult(userName));
    }
}
