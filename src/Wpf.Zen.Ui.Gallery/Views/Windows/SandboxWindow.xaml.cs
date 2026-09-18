// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Interop;
using Wpf.Zen.Ui.Gallery.Diagnostics;
using Wpf.Zen.Ui.Gallery.ViewModels.Windows;

namespace Wpf.Zen.Ui.Gallery.Views.Windows;

/// <summary>
/// Hosts manual experiments for native input-language and IME window messages.
/// </summary>
public sealed partial class SandboxWindow : IDisposable
{
    private const int MaximumRecordCount = 2000;

    private ImeWindowMessageMonitor? _messageMonitor;

    /// <summary>
    /// Initializes the sandbox window and its input-method diagnostic surface.
    /// </summary>
    /// <param name="viewModel">The view model created by the gallery host.</param>
    public SandboxWindow(SandboxWindowViewModel viewModel)
    {
        if (viewModel is null)
        {
            throw new ArgumentNullException(nameof(viewModel));
        }

        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;
        Loaded += OnLoaded;
    }

    /// <summary>
    /// Gets the native-message records displayed by the diagnostic log.
    /// </summary>
    public ObservableCollection<string> MessageRecords { get; } = [];

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeMessageMonitor();
        GC.SuppressFinalize(this);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is not HwndSource source)
        {
            return;
        }

        _messageMonitor = new ImeWindowMessageMonitor(source);
        _messageMonitor.RecordCaptured += OnRecordCaptured;
        _messageMonitor.CaptureSnapshot();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _ = ImeProbeTextBox.Focus();
        Keyboard.Focus(ImeProbeTextBox);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        SourceInitialized -= OnSourceInitialized;
        Closed -= OnClosed;
        Loaded -= OnLoaded;

        DisposeMessageMonitor();
    }

    private void OnRecordCaptured(object? sender, string record)
    {
        MessageRecords.Add(record);
        while (MessageRecords.Count > MaximumRecordCount)
        {
            MessageRecords.RemoveAt(0);
        }

        if (AutoScrollToggle.IsChecked == true)
        {
            MessageLogList.ScrollIntoView(record);
        }
    }

    private void OnCaptureToggleClick(object sender, RoutedEventArgs e)
    {
        if (_messageMonitor is not null)
        {
            _messageMonitor.IsEnabled = CaptureToggle.IsChecked == true;
        }
    }

    private void OnCaptureSnapshotClick(object sender, RoutedEventArgs e)
    {
        _messageMonitor?.CaptureSnapshot();
        _ = ImeProbeTextBox.Focus();
        Keyboard.Focus(ImeProbeTextBox);
    }

    private void OnCopyLogClick(object sender, RoutedEventArgs e)
    {
        if (MessageRecords.Count > 0)
        {
            Clipboard.SetText(string.Join(Environment.NewLine, MessageRecords));
        }
    }

    private void OnClearLogClick(object sender, RoutedEventArgs e)
    {
        MessageRecords.Clear();
        _ = ImeProbeTextBox.Focus();
        Keyboard.Focus(ImeProbeTextBox);
    }

    private void DisposeMessageMonitor()
    {
        if (_messageMonitor is null)
        {
            return;
        }

        _messageMonitor.RecordCaptured -= OnRecordCaptured;
        _messageMonitor.Dispose();
        _messageMonitor = null;
    }
}
