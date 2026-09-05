// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Zen.Ui.AutomationPeers;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Represents reusable dialog content that a <see cref="ContentDialogHost"/> presents within its
/// modal scope.
/// </summary>
/// <remarks>
/// <para>
/// The inherited <see cref="HeaderedContentControl.Header"/> and
/// <see cref="ContentControl.Content"/> properties provide the heading and body slots;
/// <see cref="Footer"/> provides a logically owned slot for application-defined actions or
/// supplementary content. The dialog coordinates close requests and carries their result, while
/// the controls placed in these slots retain their normal WPF styling, commanding and automation
/// behavior.
/// </para>
/// <para>
/// The default template deliberately supplies a standard three-region dialog composition.
/// <see cref="HeaderPadding"/>, inherited <see cref="Control.Padding"/> and
/// <see cref="FooterPadding"/> control the surrounding space for those regions, while
/// <see cref="CornerRadius"/> describes the shell shape and the body is scrollable so its header
/// and footer remain fixed under a height constraint. Use
/// <see cref="ContentDialogActionBar"/> for a conventional collection of footer actions. Arbitrary
/// slot content remains supported; layouts that cross region boundaries should replace the
/// control template.
/// </para>
/// <para>
/// This control does not create a native window or establish modality by itself. Call
/// <see cref="ContentDialogHost.ShowAsync"/> to start a presentation session; the host owns input
/// isolation, focus restoration and transitions for that session.
/// Implement <see cref="IContentDialogAware"/> on the dialog or its view model to receive
/// presentation notifications and a cancellation token for asynchronous opening work.
/// </para>
/// <para>
/// <c>PART_ContentPresenter</c> is optional and identifies realized body content for initial-focus
/// discovery. If it is absent, the control searches direct content and finally focuses itself.
/// Custom templates should present the Header, Content and Footer slots through their corresponding
/// content-source properties.
/// </para>
/// </remarks>
[TemplatePart(Name = "PART_ContentPresenter", Type = typeof(ContentPresenter))]
public partial class ContentDialog : HeaderedContentControl
{
    internal ContentDialogHost? Host
    {
        get;
        private set;
    }

    /// <summary>
    /// Requests closure with CommandParameter as the result, regardless of IsDismissible.
    /// </summary>
    public static readonly RoutedCommand CloseCommand = new(nameof(CloseCommand), typeof(ContentDialog));

    /// <summary>Requests user dismissal with a null result when IsDismissible permits it.</summary>
    public static readonly RoutedCommand DismissCommand = new(nameof(DismissCommand), typeof(ContentDialog));

    static ContentDialog()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ContentDialog),
            new FrameworkPropertyMetadata(typeof(ContentDialog))
        );

        // These defaults are behavioral contracts, so custom templates without the default style
        // still provide a focus fallback and contained tab navigation.
        FocusableProperty.OverrideMetadata(
            typeof(ContentDialog),
            new FrameworkPropertyMetadata(true)
        );

        KeyboardNavigation.TabNavigationProperty.OverrideMetadata(
            typeof(ContentDialog),
            new FrameworkPropertyMetadata(KeyboardNavigationMode.Cycle)
        );

        KeyboardNavigation.ControlTabNavigationProperty.OverrideMetadata(
            typeof(ContentDialog),
            new FrameworkPropertyMetadata(KeyboardNavigationMode.Cycle)
        );
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentDialog"/> class.
    /// The dialog is reusable and initially detached. No host is reserved until ShowAsync.
    /// </summary>
    public ContentDialog()
    {
        CommandBindings.Add(
            new CommandBinding(
                CloseCommand,
                (_, e) =>
                {
                    Close(e.Parameter);
                    e.Handled = true;
                },
                (_, e) =>
                {
                    e.CanExecute = IsOpen && !IsClosing;
                    e.Handled = true;
                }
            )
        );

        CommandBindings.Add(
            new CommandBinding(
                DismissCommand,
                (_, e) =>
                {
                    Dismiss();
                    e.Handled = true;
                },
                (_, e) =>
                {
                    e.CanExecute = IsOpen && !IsClosing && IsDismissible;
                    e.Handled = true;
                }
            )
        );
    }

    /// <summary>
    /// Requests closure with an application-defined result (null by default).
    /// Closing can veto the request.
    /// The first accepted request wins; calls outside a session do nothing.
    /// Await business work before calling Close on the UI dispatcher.
    /// </summary>
    public void Close(object? result = null)
    {
        VerifyAccess();
        Host?.RequestClose(this, result);
    }

    internal void Dismiss()
    {
        if (IsDismissible)
        {
            Close();
        }
    }

    /// <summary>
    /// Raises the synchronous veto notification for an ordinary close request.
    /// </summary>
    protected virtual void OnClosing(ContentDialogClosingEventArgs e)
    {
        RaiseEvent(e);
    }

    internal bool AcceptClose(object? result)
    {
        var args = new ContentDialogClosingEventArgs(ClosingEvent, this) { Result = result };

        OnClosing(args);

        return !args.Cancel;
    }

    internal void Attach(ContentDialogHost host)
    {
        Host = host;

        SetValue(IsOpenPropertyKey, true);
        SetValue(IsClosingPropertyKey, false);

        CommandManager.InvalidateRequerySuggested();
    }

    internal void MarkClosing()
    {
        SetValue(IsClosingPropertyKey, true);
        CommandManager.InvalidateRequerySuggested();
    }

    internal void NotifyOpened()
    {
        RaiseEvent(new RoutedEventArgs(OpenedEvent, this));
    }

    internal void NotifyClosed(object? result)
    {
        // Keep the instance reserved during notification, including handlers that pump Dispatcher.
        try
        {
            RaiseEvent(new ContentDialogClosedEventArgs(ClosedEvent, this) { Result = result });
        }
        finally
        {
            Host = null;

            try
            {
                SetValue(IsOpenPropertyKey, false);
            }
            finally
            {
                // Property observers may throw; both read-only flags must still leave the session.
                try
                {
                    SetValue(IsClosingPropertyKey, false);
                }
                finally
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }
    }

    /// <summary>
    /// Gets an enumerator over all logical slots so inheritance, resource lookup and traversal agree with
    /// AddLogicalChild. The base enumerator retains WPF's Header/Content ownership semantics.
    /// </summary>
    protected override IEnumerator LogicalChildren
    {
        get
        {
            var children  = new List<object>();
            var inherited = base.LogicalChildren;

            if (inherited is not null)
            {
                while (inherited.MoveNext())
                {
                    children.Add(inherited.Current);
                }
            }

            if (Footer is { } footer && !children.Any(child => ReferenceEquals(child, footer)))
            {
                children.Add(footer);
            }

            return new LogicalChildrenEnumerator(children);
        }
    }

    /// <summary>
    /// Maintains the same logical ownership contract as HeaderedContentControl.Header.
    /// </summary>
    protected virtual void OnFooterChanged(object? oldFooter, object? newFooter)
    {
        RemoveLogicalChild(oldFooter);
        AddLogicalChild(newFooter);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new ContentDialogAutomationPeer(this);
    }
}
