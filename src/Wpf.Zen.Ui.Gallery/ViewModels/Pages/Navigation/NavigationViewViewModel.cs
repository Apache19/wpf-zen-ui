// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace Wpf.Zen.Ui.Gallery.ViewModels.Pages.Navigation;

/// <summary>
/// Provides the NavigationView gallery page and both forms of page-owned header contribution.
/// </summary>
public partial class NavigationViewViewModel : ViewModel
{
    private string _headerActionText = "Show page message";
    private string? _headerActionMessage;
    private string _headerSubtitle = "PageHeaderContent and PageHeaderItems share this header layout.";
    private int _headerActionCount;

    /// <summary>
    /// Gets the label shown by the page-owned button in the gallery header.
    /// </summary>
    public string HeaderActionText
    {
        get => _headerActionText;
        private set => SetProperty(ref _headerActionText, value);
    }

    /// <summary>
    /// Gets the message displayed by the InfoBar in the page body.
    /// </summary>
    /// <remarks>
    /// The initial null value keeps the InfoBar collapsed until the header action is invoked.
    /// </remarks>
    public string? HeaderActionMessage
    {
        get => _headerActionMessage;
        private set => SetProperty(ref _headerActionMessage, value);
    }

    /// <summary>
    /// Gets the subtitle contributed below the breadcrumb by the page's header collection.
    /// </summary>
    public string HeaderSubtitle
    {
        get => _headerSubtitle;
        private set => SetProperty(ref _headerSubtitle, value);
    }

    /// <summary>
    /// Gets the command bound by the page-owned button in the gallery header.
    /// </summary>
    public ICommand HeaderActionCommand { get; }

    /// <summary>
    /// Initializes the header action for this gallery page.
    /// </summary>
    public NavigationViewViewModel()
    {
        HeaderActionCommand = new RelayCommand(ExecuteHeaderAction);
    }

    /// <summary>
    /// Publishes a new page message when the contributed header button is clicked.
    /// </summary>
    private void ExecuteHeaderAction()
    {
        _headerActionCount++;
        HeaderActionMessage = $"The page header action was clicked {_headerActionCount} time(s).";
        HeaderSubtitle = $"Page action executed {_headerActionCount} time(s). Both header mechanisms use the page model.";
    }
}
