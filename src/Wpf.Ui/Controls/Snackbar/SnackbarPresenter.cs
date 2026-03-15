// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace

using System.Windows.Threading;

namespace Wpf.Ui.Controls;

// todo： 下一步重构
//   Snackbar 自己管理自己的显示时间，时间到则自动退出。
//   容器可容纳多个 Snackbar，设置上限数量，从下到上以列表的方式排列。
//   新 Snackbar 进入的同时强制达到上限的最旧 Snackbar 退出。
//   容器则可使用堆叠的方式，新的出现则会将堆叠往上挤。

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
        Unloaded += static (sender, _) => ((SnackbarPresenter)sender).OnUnloaded();
    }

    private Queue<Snackbar> Queue { get; } = new();
    private DispatcherTimer? _timer;
    private int _hideGeneration;

    private void OnUnloaded()
    {
        StopTimer();

        _hideGeneration++;

        if (Content is { } snackbar)
        {
            ImmediatelyHideSnackbar(snackbar);
        }

        Queue.Clear();
    }

    private void StopTimer()
    {
        if (_timer is null)
        {
            return;
        }

        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _timer = null;
    }

    private void StartTimer(TimeSpan interval)
    {
        StopTimer();
        _timer = new DispatcherTimer { Interval = interval };
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        StopTimer();

        var gen = ++_hideGeneration;

        if (Content is { } snackbar)
        {
            await HideSnackbar(snackbar);
        }

        if (_hideGeneration == gen)
        {
            ShowNext();
        }
    }

    private void ShowNext()
    {
        if (Queue.Count > 0)
        {
            ShowSnackbar(Queue.Dequeue());
        }
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

    /// <summary>
    /// Adds a <see cref="Snackbar"/> to the display queue. If no snackbar is currently shown, begins processing the queue.
    /// </summary>
    /// <param name="snackbar">The snackbar to enqueue.</param>
    public void AddToQueue(Snackbar snackbar)
    {
        Queue.Enqueue(snackbar);
        if (Content is null && _timer is null)
        {
            ShowNext();
        }
    }

    /// <summary>
    /// Immediately displays a <see cref="Snackbar"/>, hiding any currently shown snackbar first.
    /// </summary>
    /// <param name="snackbar">The snackbar to display immediately.</param>
    public async Task ImmediatelyDisplay(Snackbar snackbar)
    {
        await HideCurrent();
        ShowSnackbar(snackbar);
    }

    /// <summary>
    /// Hides the currently displayed <see cref="Snackbar"/>, if any.
    /// </summary>
    public async Task HideCurrent()
    {
        StopTimer();
        _hideGeneration++;
        if (Content is { } snackbar)
        {
            await HideSnackbar(snackbar);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "WpfAnalyzers.DependencyProperty",
        "WPF0041:Set mutable dependency properties using SetCurrentValue",
        Justification = "SetCurrentValue(ContentProperty, ...) will not work"
    )]
    private void ShowSnackbar(Snackbar snackbar)
    {
        Content = snackbar;
        snackbar.SetCurrentValue(Snackbar.IsShownProperty, true);
        StartTimer(snackbar.Timeout);
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
        if (Content == snackbar)
        {
            Content = null;
        }
    }
}
