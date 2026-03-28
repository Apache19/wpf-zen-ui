// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Automation.Peers;
using System.Windows.Controls;
using Wpf.Ui.AutomationPeers;
using Wpf.Ui.Input;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Dialogue displayed inside the application covering its internals, displaying some content.
/// </summary>
/// <example>
/// <code lang="xml">
/// &lt;ContentDialogHost x:Name="RootContentDialogHost" Grid.Row="0" /&gt;
/// </code>
/// <code lang="csharp">
/// var contentDialog = new ContentDialog(RootContentDialogHost);
///
/// contentDialog.SetCurrentValue(ContentDialog.TitleProperty, "Hello World");
/// contentDialog.SetCurrentValue(ContentControl.ContentProperty, "This is a message");
/// contentDialog.SetCurrentValue(ContentDialog.CloseButtonTextProperty, "Close this dialog");
///
/// await contentDialog.ShowAsync(cancellationToken);
/// </code>
/// <code lang="csharp">
/// var contentDialogService = new ContentDialogService();
/// contentDialogService.SetDialogHost(RootContentDialogHost);
///
/// await _contentDialogService.ShowSimpleDialogAsync(
///     new SimpleContentDialogCreateOptions()
///         {
///             Title = "The cake?",
///             Content = "IS A LIE!",
///             PrimaryButtonText = "Save",
///             SecondaryButtonText = "Don't Save",
///             CloseButtonText = "Cancel"
///         }
///     );
/// </code>
/// </example>
public partial class ContentDialog : ContentControl
{
    static ContentDialog()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ContentDialog),
            new FrameworkPropertyMetadata(typeof(ContentDialog))
        );
    }

    /// <summary>
    /// Gets command triggered after clicking the button in the template.
    /// </summary>
    public IRelayCommand TemplateButtonCommand => (IRelayCommand)GetValue(TemplateButtonCommandProperty);

    /// <summary>
    /// Occurs after the dialog is opened.
    /// </summary>
    public event TypedEventHandler<ContentDialog, RoutedEventArgs> Opened
    {
        add => AddHandler(OpenedEvent, value);
        remove => RemoveHandler(OpenedEvent, value);
    }

    /// <summary>
    /// Occurs after the dialog starts to close, but before it is closed and before the <see cref="Closed"/> event occurs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This event allows cancellation of the close operation by setting
    /// <see cref="ContentDialogClosingEventArgs.Cancel"/> to <see langword="true"/>.
    /// </para>
    /// <para>
    /// <strong>Important:</strong> The Closing event is only raised for explicit close operations initiated via the
    /// <see cref="Hide"/> method. It is <em>not</em> raised when the dialog is passively removed from the visual tree,
    /// such as when:
    /// </para>
    /// <list type="bullet">
    /// <item>Another dialog replaces this one</item>
    /// <item>The host control or window is disposed</item>
    /// </list>
    /// </remarks>
    public event TypedEventHandler<ContentDialog, ContentDialogClosingEventArgs> Closing
    {
        add => AddHandler(ClosingEvent, value);
        remove => RemoveHandler(ClosingEvent, value);
    }

    /// <summary>
    /// Occurs after the dialog is closed.
    /// </summary>
    public event TypedEventHandler<ContentDialog, ContentDialogClosedEventArgs> Closed
    {
        add => AddHandler(ClosedEvent, value);
        remove => RemoveHandler(ClosedEvent, value);
    }

    /// <summary>
    /// Occurs after the <see cref="ContentDialogButton"/> has been tapped.
    /// </summary>
    public event TypedEventHandler<ContentDialog, ContentDialogButtonClickEventArgs> ButtonClicked
    {
        add => AddHandler(ButtonClickedEvent, value);
        remove => RemoveHandler(ButtonClickedEvent, value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentDialog"/> class.
    /// </summary>
    public ContentDialog()
    {
        SetValue(TemplateButtonCommandProperty, new RelayCommand<ContentDialogButton>(OnButtonClick));
        RegisterRuntimeEventHandlers();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentDialog"/> class with the specified dialog host.
    /// </summary>
    /// <param name="dialogHost">The ContentDialogHost that manages the dialog's display and interaction.</param>
    /// <exception cref="ArgumentNullException">Thrown if dialogHost is null.</exception>
    public ContentDialog(ContentDialogHost? dialogHost)
    {
        DialogHost = dialogHost ?? throw new ArgumentNullException(nameof(dialogHost));

        SetValue(TemplateButtonCommandProperty, new RelayCommand<ContentDialogButton>(OnButtonClick));

        RegisterRuntimeEventHandlers();
    }

    private void RegisterRuntimeEventHandlers()
    {
        // Avoid registering runtime code that triggers designer behavior or throws exceptions
        // at design time (to reduce the possibility of designer crashes/rendering failures).
        if (!DesignerProperties.GetIsInDesignMode(this))
        {
            Loaded += static (sender, _) =>
            {
                var self = (ContentDialog)sender;
                self.OnLoadedInternal();
            };

            Unloaded += static (sender, _) =>
            {
                var self = (ContentDialog)sender;
                self.OnUnloadedInternal();
            };
        }
    }

    private ContentDialogHost? _dialogHost;

    /// <summary>
    /// Gets or sets <see cref="DialogHost"/> inside of which the dialogue will be placed.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if trying to set DialogHostEx when DialogHost is already set, or if trying to change DialogHostEx while the dialog is being shown.
    /// </exception>
    public ContentDialogHost? DialogHost
    {
        get => _dialogHost;
        set
        {
            if (_dialogHost is not null)
            {
                throw new InvalidOperationException(
                    "Cannot set DialogHostEx when DialogHost is already set."
                );
            }

            if (IsShowing)
            {
                throw new InvalidOperationException(
                    "Cannot change DialogHostEx while the dialog is being shown."
                );
            }

            if (!ReferenceEquals(_dialogHost, value))
            {
                _dialogHost = value;
            }
        }
    }

    protected TaskCompletionSource<ContentDialogResult>? Tcs { get; set; }

    // Helper indicating whether the dialog is currently shown (the async operation hasn't completed yet)
    private bool IsShowing => Tcs is not null && !Tcs.Task.IsCompleted;

    /// <summary>
    /// Shows the dialog
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "WpfAnalyzers.DependencyProperty",
        "WPF0041:Set mutable dependency properties using SetCurrentValue",
        Justification = "SetCurrentValue(ContentProperty, ...) will not work"
    )]
    public async Task<ContentDialogResult> ShowAsync(CancellationToken cancellationToken = default)
    {
        if (_dialogHost is null)
        {
            throw new InvalidOperationException("DialogHost was not set");
        }

        Tcs = new TaskCompletionSource<ContentDialogResult>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        CancellationTokenRegistration tokenRegistration = cancellationToken.Register(
            o => Tcs.TrySetCanceled((CancellationToken)o!),
            cancellationToken
        );

        ContentDialogResult result = ContentDialogResult.None;

        try
        {
            _dialogHost.Content = this;

            result = await Tcs.Task;

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return result;
        }
        finally
        {
            #if NET6_0_OR_GREATER
            await tokenRegistration.DisposeAsync();
            #else
            tokenRegistration.Dispose();
            #endif

            // DialogHost is a public container. To prevent the new dialog from being closed immediately when
            // it opens due to the unconditional clearing of the Content upon the closure of the previous dialog,
            // only clear the DialogHost content if this instance is still the current content.
            if (_dialogHost is not null && ReferenceEquals(_dialogHost.Content, this))
            {
                _dialogHost.Content = null;
            }

            OnClosed(result);
        }
    }

    /// <summary>
    /// Hides the dialog with result
    /// </summary>
    public virtual void Hide(ContentDialogResult result = ContentDialogResult.None)
    {
        var closingEventArgs = new ContentDialogClosingEventArgs(ClosingEvent, this) { Result = result };

        RaiseEvent(closingEventArgs);

        if (!closingEventArgs.Cancel)
        {
            _ = Tcs?.TrySetResult(result);
        }
    }

    /// <summary>
    /// Occurs after ContentPresenter.Content = null
    /// </summary>
    protected virtual void OnClosed(ContentDialogResult result)
    {
        var closedEventArgs = new ContentDialogClosedEventArgs(ClosedEvent, this) { Result = result };

        RaiseEvent(closedEventArgs);
    }

    /// <summary>
    /// Invoked when a <see cref="ContentDialogButton"/> is clicked.
    /// </summary>
    /// <param name="button">The button that was clicked.</param>
    protected virtual void OnButtonClick(ContentDialogButton button)
    {
        var buttonClickEventArgs = new ContentDialogButtonClickEventArgs(ButtonClickedEvent, this)
        {
            Button = button,
        };

        RaiseEvent(buttonClickEventArgs);

        ContentDialogResult result = button switch
        {
            ContentDialogButton.Primary   => ContentDialogResult.Primary,
            ContentDialogButton.Secondary => ContentDialogResult.Secondary,
            _                             => ContentDialogResult.None,
        };

        Hide(result);
    }

    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new ContentDialogAutomationPeer(this);
    }

    private void OnLoadedInternal()
    {
        if (!IsFocusInsideDialog())
        {
            SetInitialFocus();
        }

        RaiseEvent(new RoutedEventArgs(OpenedEvent));
    }

    private void OnUnloadedInternal()
    {
        if (!ReferenceEquals(_dialogHost?.Content, this))
        {
            if (Tcs is { Task.IsCompleted: false })
            {
                _ = Tcs.TrySetResult(ContentDialogResult.None);
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!DesignerProperties.GetIsInDesignMode(this))
        {
            if (GetVisualChild(0) is UIElement rootElement)
            {
                rootElement.Measure(availableSize);
                Size desiredSize = rootElement.DesiredSize;

                Size newSize = GetNewDialogSize(desiredSize);

                SetCurrentValue(DialogHeightProperty, newSize.Height);
                SetCurrentValue(DialogWidthProperty,  newSize.Width);

                ResizeWidth(rootElement);
                ResizeHeight(rootElement);

                return desiredSize;
            }
        }

        return base.MeasureOverride(availableSize);
    }

    private Size GetNewDialogSize(Size desiredSize)
    {
        // TODO: Handle negative values
        var paddingWidth  = Padding.Left + Padding.Right;
        var paddingHeight = Padding.Top + Padding.Bottom;

        var marginHeight = DialogMargin.Bottom + DialogMargin.Top;
        var marginWidth  = DialogMargin.Left + DialogMargin.Right;

        var width  = desiredSize.Width - marginWidth + paddingWidth;
        var height = desiredSize.Height - marginHeight + paddingHeight;

        return new Size(width, height);
    }

    private void ResizeWidth(UIElement element)
    {
        if (DialogWidth <= DialogMaxWidth)
        {
            return;
        }

        SetCurrentValue(DialogWidthProperty, DialogMaxWidth);
        element.UpdateLayout();

        SetCurrentValue(DialogHeightProperty, element.DesiredSize.Height);

        if (DialogHeight > DialogMaxHeight)
        {
            SetCurrentValue(DialogMaxHeightProperty, DialogHeight);
            /*Debug.WriteLine($"DEBUG | {GetType()} | WARNING | DialogHeight > DialogMaxHeight after resizing width!");*/
        }
    }

    private void ResizeHeight(UIElement element)
    {
        if (DialogHeight <= DialogMaxHeight)
        {
            return;
        }

        SetCurrentValue(DialogHeightProperty, DialogMaxHeight);
        element.UpdateLayout();

        SetCurrentValue(DialogWidthProperty, element.DesiredSize.Width);

        if (DialogWidth > DialogMaxWidth)
        {
            SetCurrentValue(DialogMaxWidthProperty, DialogWidth);
            /*Debug.WriteLine($"DEBUG | {GetType()} | WARNING | DialogWidth > DialogMaxWidth after resizing height!");*/
        }
    }
}