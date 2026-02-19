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

    /// <summary>Gets a value indicating whether the dialog is shown in the legacy <see cref="ContentPresenter"/> host.</summary>
    public bool IsLegacyHost => (bool)GetValue(IsLegacyHostProperty);

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentDialog"/> class.
    /// </summary>
    public ContentDialog()
    {
        SetValue(TemplateButtonCommandProperty, new RelayCommand<ContentDialogButton>(OnButtonClick));

        RegisterRuntimeEventHandlers();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentDialog"/> class.
    /// </summary>
    /// <param name="dialogHost"><see cref="DialogHost"/> inside of which the dialogue will be placed. The new <see cref="ContentDialog"/> will replace the current <see cref="ContentPresenter.Content"/>.</param>
    /// <remarks>
    /// DEPRECATED: This constructor overload is deprecated. Use the constructor that accepts a <see cref="ContentDialogHost"/>
    /// instead for enhanced modal dialog capabilities.
    /// </remarks>
    [Obsolete(
        "ContentDialog(ContentPresenter? is deprecated. Please use ContentDialog(ContentDialogHost? instead.",
        false
    )]
    public ContentDialog(ContentPresenter? dialogHost)
    {
        // Prefer the legacy DialogHost (ContentPresenter) when both ContentDialogHost
        // and the legacy host exist in the same window, and ContentDialogService is
        // configured to use the legacy host.
        // This ensures consistency between the host instance used locally and
        // the actual instance utilized internally by ContentDialogService.
        if (dialogHost is not null)
        {
            DialogHost = dialogHost;
        }
        else
        {
            // Fallback to using ContentDialogHost, which must be obtained from the currently active window.
            Window? activeWindow = null;

            // try Application.Current windows
            try
            {
                Application? app = Application.Current;
                if (app != null)
                {
                    activeWindow =
                        app.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? app.MainWindow;
                }
            }
            catch
            {
                // ignore and fallback
            }

            // fallback: Win32 foreground window -> HwndSource -> Window
            activeWindow ??= Win32.Utilities.TryGetWindowFromForegroundHwnd();

            var hostEx = ContentDialogHost.GetForWindow(activeWindow);
            if (hostEx is not null)
            {
                DialogHostEx = hostEx;
            }
            else
            {
                // The legacy constructor immediately throws when the dialogHost parameter is null.
                // For backward compatibility, we now fall back to using the new ContentDialogHost
                // when no dialogHost is specified. Only when both are unavailable do we actually
                // throw the null argument exception.
                throw new ArgumentNullException(nameof(dialogHost));
            }
        }

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
        DialogHostEx = dialogHost ?? throw new ArgumentNullException(nameof(dialogHost));

        SetValue(TemplateButtonCommandProperty, new RelayCommand<ContentDialogButton>(OnButtonClick));

        RegisterRuntimeEventHandlers();
    }

    private void RegisterRuntimeEventHandlers()
    {
        // Avoid registering runtime code that triggers designer behavior or throws exceptions
        // at design time (to reduce the possibility of designer crashes/rendering failures).
        if (!Designer.DesignerHelper.IsInDesignMode)
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

    // Legacy and new host coexist for compatibility during migration.
    private ContentPresenter?  _dialogHost;
    private ContentDialogHost? _dialogHostEx;

    /// <summary>
    /// Gets or sets <see cref="DialogHost"/> inside of which the dialogue will be placed.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if trying to set DialogHost when DialogHostEx is already set, or if trying to change DialogHost while the dialog is being shown.
    /// </exception>
    [Obsolete("DialogHost is deprecated. Please use DialogHostEx instead.")]
    public ContentPresenter? DialogHost
    {
        get => _dialogHost;
        set
        {
            if (_dialogHostEx is not null)
            {
                throw new InvalidOperationException(
                    "Cannot set DialogHost when DialogHostEx is already set."
                );
            }

            if (IsShowing)
            {
                throw new InvalidOperationException(
                    "Cannot change DialogHost while the dialog is being shown."
                );
            }

            if (ReferenceEquals(_dialogHost, value))
            {
                return;
            }

            if (_dialogHost is not null)
            {
                ContentDialogHostBehavior.SetIsEnabled(_dialogHost, false);
            }

            _dialogHost = value;

            if (_dialogHost is not null)
            {
                ContentDialogHostBehavior.SetIsEnabled(_dialogHost, true);
            }

            UpdateIsLegacyHost();
        }
    }

    /// <summary>
    /// Gets or sets <see cref="DialogHostEx"/> inside of which the dialogue will be placed.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if trying to set DialogHostEx when DialogHost is already set, or if trying to change DialogHostEx while the dialog is being shown.
    /// </exception>
    public ContentDialogHost? DialogHostEx
    {
        get => _dialogHostEx;
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

            if (!ReferenceEquals(_dialogHostEx, value))
            {
                _dialogHostEx = value;
            }

            UpdateIsLegacyHost();
        }
    }

    [Obsolete("ContentPresenter is deprecated. Please use DialogHost instead.")]
    public ContentPresenter? ContentPresenter { get; set; } = default;

    protected TaskCompletionSource<ContentDialogResult>? Tcs { get; set; }

    // Helper indicating whether the dialog is currently shown (the async operation hasn't completed yet)
    private bool IsShowing => Tcs is not null && !Tcs.Task.IsCompleted;

    private void UpdateIsLegacyHost()
    {
        SetValue(IsLegacyHostPropertyKey, _dialogHostEx is null);
    }

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
        if (_dialogHost is null && _dialogHostEx is null)
        {
            throw new InvalidOperationException("DialogHost was not set");
        }

        // Uses `RunContinuationsAsynchronously` to execute continuations asynchronously
        // rather than synchronously on the caller's stack when TCS completes.
        //
        // Benefits:
        // - Prevents UI-thread reentrancy
        // - Eliminates deadlock risks
        // - Ensures predictable continuation scheduling
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
            if (_dialogHostEx is not null)
            {
                _dialogHostEx.Content = this;
            }
            else
            {
                _dialogHost!.Content = this;
            }

            result = await Tcs.Task;

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
            if (_dialogHostEx is not null && ReferenceEquals(_dialogHostEx.Content, this))
            {
                _dialogHostEx.Content = null;
            }
            else if (_dialogHost is not null && ReferenceEquals(_dialogHost.Content, this))
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

    protected override Size MeasureOverride(Size availableSize)
    {
        // Avoid throwing exceptions when visual child elements cannot be obtained (designer or template not applied).
        if (VisualChildrenCount == 0)
        {
            return base.MeasureOverride(availableSize);
        }

        var rootElement = (UIElement)GetVisualChild(0)!;

        rootElement.Measure(availableSize);
        Size desiredSize = rootElement.DesiredSize;

        Size newSize = GetNewDialogSize(desiredSize);

        SetCurrentValue(DialogHeightProperty, newSize.Height);
        SetCurrentValue(DialogWidthProperty,  newSize.Width);

        ResizeWidth(rootElement);
        ResizeHeight(rootElement);

        return desiredSize;
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

        OnLoaded();
        RaiseEvent(new RoutedEventArgs(OpenedEvent));
    }

    /// <summary>
    /// Occurs after Loaded event
    /// </summary>
    protected virtual void OnLoaded() { }

    private void OnUnloadedInternal()
    {
        if (!ReferenceEquals(_dialogHostEx?.Content, this) && !ReferenceEquals(_dialogHost?.Content, this))
        {
            // If a new dialog instance is created and shown (e.g., via ShowAsync) while this dialog is still displayed,
            // this instance will be removed from the visual tree. If the Hide method has not been called to complete the async operation,
            // the ShowAsync task will be left dangling — waiting indefinitely without returning.
            // Therefore, when this instance is removed from the visual tree, we must check the async task status:
            // if not completed, return ContentDialogResult.None to resolve it.
            if (Tcs is { Task.IsCompleted: false })
            {
                _ = Tcs.TrySetResult(ContentDialogResult.None);
            }
        }

        OnUnloaded();
    }

    /// <summary>
    /// Occurs after Unloaded event
    /// </summary>
    protected virtual void OnUnloaded() { }

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