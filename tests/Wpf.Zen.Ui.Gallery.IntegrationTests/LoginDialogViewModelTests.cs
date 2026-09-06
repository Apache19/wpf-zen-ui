// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Gallery.Models;
using Wpf.Zen.Ui.Gallery.ViewModels.Dialogs;

namespace Wpf.Zen.Ui.Gallery.IntegrationTests;

/// <summary>Checks the custom dialog's validation without creating a WPF presentation.</summary>
public sealed class LoginDialogViewModelTests
{
    /// <summary>Invalid or incomplete demo input must not complete the dialog.</summary>
    [Theory]
    [InlineData("", "")]
    [InlineData("octocat", "")]
    [InlineData(" ", "demo")]
    [InlineData("octocat", "wrong")]
    public void InvalidInputKeepsDialogOpen(string userName, string password)
    {
        var viewModel = new LoginDialogViewModel { UserName = userName, Password = password };
        bool completed = false;
        viewModel.SignInCompleted += _ => completed = true;

        viewModel.SignInCommand.Execute(null);

        Assert.False(completed);
        Assert.NotEmpty(viewModel.ErrorMessage);
    }

    /// <summary>A corrected demo account returns only its name and clears transient validation and password state.</summary>
    [Fact]
    public void ValidInputCompletesWithAccountOnly()
    {
        var viewModel = new LoginDialogViewModel();
        LoginDialogResult? result = null;
        viewModel.SignInCompleted += value => result = value;
        viewModel.SignInCommand.Execute(null);
        viewModel.UserName = " octocat ";
        viewModel.Password = "demo";

        viewModel.SignInCommand.Execute(null);

        Assert.Equal(new LoginDialogResult("octocat"), result);
        Assert.Empty(viewModel.ErrorMessage);
        Assert.Empty(viewModel.Password);
    }
}
