// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Presents <see cref="Snackbar"/> notifications by managing a display queue.
/// </summary>
/// <example>
/// <code lang="xml">
/// &lt;ui:SnackbarPresenter x:Name="RootSnackbarPresenter" /&gt;
/// </code>
/// </example>
public sealed class SnackbarPresenter : System.Windows.Controls.ContentPresenter
{
    /// <summary>
    /// Gets the currently displayed <see cref="Snackbar"/>, or <see langword="null"/> if none is shown.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "WpfAnalyzers.DependencyProperty",
        "WPF0012:CLR property type should match registered type",
        Justification = "seems harmless"
    )]
    public new Snackbar? Content
    {
        get => (Snackbar?)GetValue(ContentProperty);
        private set => SetValue(ContentProperty, value);
    }

    public SnackbarPresenter()
    {
        Unloaded += static (sender, _) =>
        {
            var self = (SnackbarPresenter)sender;
            self.OnUnloaded();
        };
    }

    private Queue<Snackbar> Queue { get; } = new();
    private CancellationTokenSource CancellationTokenSource { get; set; } = new();

    private void OnUnloaded()
    {
        if (CancellationTokenSource.IsCancellationRequested)
        {
            return;
        }

        ImmediatelyHideCurrent();
        ResetCancellationTokenSource();
    }

    private void ImmediatelyHideCurrent()
    {
        if (Content is null)
        {
            return;
        }

        CancellationTokenSource.Cancel();
        ImmediatelyHideSnackbar(Content);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "WpfAnalyzers.DependencyProperty",
        "WPF0041:Set mutable dependency properties using SetCurrentValue",
        Justification = "SetCurrentValue(ContentProperty, ...) will not work"
    )]
    private void ImmediatelyHideSnackbar(Snackbar snackbar)
    {
        snackbar.SetCurrentValue(Snackbar.IsShownProperty, false);
        Content = null;
    }

    private void ResetCancellationTokenSource()
    {
        CancellationTokenSource.Dispose();
        CancellationTokenSource = new CancellationTokenSource();
    }

    /// <summary>
    /// Adds a <see cref="Snackbar"/> to the display queue. If no snackbar is currently shown, begins processing the queue.
    /// </summary>
    /// <param name="snackbar">The snackbar to enqueue.</param>
    public void AddToQueue(Snackbar snackbar)
    {
        Queue.Enqueue(snackbar);

        if (Content is null)
        {
            _ = ShowQueuedSnackbarsAsync(); // TODO: Fix detached process
        }
    }

    /// <summary>
    /// Immediately displays a <see cref="Snackbar"/>, hiding any currently shown snackbar first.
    /// </summary>
    /// <param name="snackbar">The snackbar to display immediately.</param>
    public async Task ImmediatelyDisplay(Snackbar snackbar)
    {
        await HideCurrent();
        await ShowSnackbar(snackbar);

        await ShowQueuedSnackbarsAsync();
    }

    /// <summary>
    /// Hides the currently displayed <see cref="Snackbar"/>, if any.
    /// </summary>
    public async Task HideCurrent()
    {
        if (Content is null)
        {
            return;
        }

        CancellationTokenSource.Cancel();
        await HideSnackbar(Content);
        ResetCancellationTokenSource();
    }

    private async Task ShowQueuedSnackbarsAsync()
    {
        while (Queue.Count > 0 && !CancellationTokenSource.IsCancellationRequested)
        {
            Snackbar snackbar = Queue.Dequeue();

            await ShowSnackbar(snackbar);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "WpfAnalyzers.DependencyProperty",
        "WPF0041:Set mutable dependency properties using SetCurrentValue",
        Justification = "SetCurrentValue(ContentProperty, ...) will not work"
    )]
    private async Task ShowSnackbar(Snackbar snackbar)
    {
        Content = snackbar;

        snackbar.SetCurrentValue(Snackbar.IsShownProperty, true);

        try
        {
            await Task.Delay(snackbar.Timeout, CancellationTokenSource.Token);
        }
        catch
        {
            return;
        }

        await HideSnackbar(snackbar);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "WpfAnalyzers.DependencyProperty",
        "WPF0041:Set mutable dependency properties using SetCurrentValue",
        Justification = "SetCurrentValue(ContentProperty, ...) will not work"
    )]
    private async Task HideSnackbar(Snackbar snackbar)
    {
        snackbar.SetCurrentValue(Snackbar.IsShownProperty, false);

        await Task.Delay(300);

        Content = null;
    }

    ~SnackbarPresenter()
    {
        if (!CancellationTokenSource.IsCancellationRequested)
        {
            CancellationTokenSource.Cancel();
        }

        CancellationTokenSource.Dispose();
    }
}
