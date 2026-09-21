// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Displays a synchronous Fluent modal message box using the standard WPF buttons and results.
/// </summary>
/// <remarks>
/// Call Show on an STA UI thread. It returns only after the user dismisses the window.
/// The owner overloads require a non-null window on the calling dispatcher.
/// ServiceNotification and DefaultDesktopOnly use the native WPF message box because a WPF
/// window cannot be displayed on another desktop; custom labels are unavailable in those modes.
/// </remarks>
public static class MessageBox
{
    /// <summary>
    /// Displays a modal message box and returns the selected WPF result.
    /// </summary>
    /// <param name="text">The message to display; null is treated as empty text.</param>
    /// <returns>The standard WPF result selected before the modal window closes.</returns>
    public static MessageBoxResult Show(string text)
    {
        return ShowCore(
            null,
            text,
            string.Empty,
            MessageBoxButton.OK,
            MessageBoxImage.None,
            MessageBoxResult.None,
            MessageBoxOptions.None,
            null
        );
    }

    /// <summary>
    /// Displays a modal message box and returns the selected WPF result.
    /// </summary>
    /// <param name="text">The message to display; null is treated as empty text.</param>
    /// <param name="caption">The window caption; null is treated as empty text.</param>
    /// <returns>The standard WPF result selected before the modal window closes.</returns>
    public static MessageBoxResult Show(string text, string caption)
    {
        return ShowCore(
            null,
            text,
            caption,
            MessageBoxButton.OK,
            MessageBoxImage.None,
            MessageBoxResult.None,
            MessageBoxOptions.None,
            null
        );
    }

    /// <summary>
    /// Displays a modal message box and returns the selected WPF result.
    /// </summary>
    /// <param name="text">The message to display; null is treated as empty text.</param>
    /// <param name="caption">The window caption; null is treated as empty text.</param>
    /// <param name="button">The standard button combination.</param>
    /// <returns>The standard WPF result selected before the modal window closes.</returns>
    public static MessageBoxResult Show(string text, string caption, MessageBoxButton button)
    {
        return ShowCore(
            null,
            text,
            caption,
            button,
            MessageBoxImage.None,
            MessageBoxResult.None,
            MessageBoxOptions.None,
            null
        );
    }

    /// <summary>
    /// Displays a modal message box and returns the selected WPF result.
    /// </summary>
    /// <param name="text">The message to display; null is treated as empty text.</param>
    /// <param name="caption">The window caption; null is treated as empty text.</param>
    /// <param name="button">The standard button combination.</param>
    /// <param name="icon">The standard message icon, including its WPF aliases.</param>
    /// <returns>The standard WPF result selected before the modal window closes.</returns>
    public static MessageBoxResult Show(
        string text,
        string caption,
        MessageBoxButton button,
        MessageBoxImage icon)
    {
        return ShowCore(
            null,
            text,
            caption,
            button,
            icon,
            MessageBoxResult.None,
            MessageBoxOptions.None,
            null
        );
    }

    /// <summary>
    /// Displays a modal message box and returns the selected WPF result.
    /// </summary>
    /// <param name="text">The message to display; null is treated as empty text.</param>
    /// <param name="caption">The window caption; null is treated as empty text.</param>
    /// <param name="button">The standard button combination.</param>
    /// <param name="icon">The standard message icon, including its WPF aliases.</param>
    /// <param name="defaultResult">
    /// The initially focused result; an absent result selects the first button.
    /// </param>
    /// <returns>The standard WPF result selected before the modal window closes.</returns>
    public static MessageBoxResult Show(
        string text,
        string caption,
        MessageBoxButton button,
        MessageBoxImage icon,
        MessageBoxResult defaultResult)
    {
        return ShowCore(
            null,
            text,
            caption,
            button,
            icon,
            defaultResult,
            MessageBoxOptions.None,
            null
        );
    }

    /// <summary>
    /// Displays a modal message box and returns the selected WPF result.
    /// </summary>
    /// <param name="text">The message to display; null is treated as empty text.</param>
    /// <param name="caption">The window caption; null is treated as empty text.</param>
    /// <param name="button">The standard button combination.</param>
    /// <param name="icon">The standard message icon, including its WPF aliases.</param>
    /// <param name="defaultResult">
    /// The initially focused result; an absent result selects the first button.
    /// </param>
    /// <param name="options">
    /// Text direction, alignment, or native desktop notification options.
    /// </param>
    /// <returns>The standard WPF result selected before the modal window closes.</returns>
    public static MessageBoxResult Show(
        string text,
        string caption,
        MessageBoxButton button,
        MessageBoxImage icon,
        MessageBoxResult defaultResult,
        MessageBoxOptions options)
    {
        return ShowCore(null, text, caption, button, icon, defaultResult, options, null);
    }

    /// <summary>
    /// Displays a modal message box and returns the selected WPF result.
    /// </summary>
    /// <param name="owner">The owning window on the current dispatcher.</param>
    /// <param name="text">The message to display; null is treated as empty text.</param>
    /// <returns>The standard WPF result selected before the modal window closes.</returns>
    public static MessageBoxResult Show(Window owner, string text)
    {
        if (owner is null)
        {
            throw new ArgumentNullException(nameof(owner));
        }

        return ShowCore(
            owner,
            text,
            string.Empty,
            MessageBoxButton.OK,
            MessageBoxImage.None,
            MessageBoxResult.None,
            MessageBoxOptions.None,
            null
        );
    }

    /// <summary>
    /// Displays a modal message box and returns the selected WPF result.
    /// </summary>
    /// <param name="owner">The owning window on the current dispatcher.</param>
    /// <param name="text">The message to display; null is treated as empty text.</param>
    /// <param name="caption">The window caption; null is treated as empty text.</param>
    /// <returns>The standard WPF result selected before the modal window closes.</returns>
    public static MessageBoxResult Show(Window owner, string text, string caption)
    {
        if (owner is null)
        {
            throw new ArgumentNullException(nameof(owner));
        }

        return ShowCore(
            owner,
            text,
            caption,
            MessageBoxButton.OK,
            MessageBoxImage.None,
            MessageBoxResult.None,
            MessageBoxOptions.None,
            null
        );
    }

    /// <summary>
    /// Displays a modal message box and returns the selected WPF result.
    /// </summary>
    /// <param name="owner">The owning window on the current dispatcher.</param>
    /// <param name="text">The message to display; null is treated as empty text.</param>
    /// <param name="caption">The window caption; null is treated as empty text.</param>
    /// <param name="button">The standard button combination.</param>
    /// <returns>The standard WPF result selected before the modal window closes.</returns>
    public static MessageBoxResult Show(
        Window owner,
        string text,
        string caption,
        MessageBoxButton button)
    {
        if (owner is null)
        {
            throw new ArgumentNullException(nameof(owner));
        }

        return ShowCore(
            owner,
            text,
            caption,
            button,
            MessageBoxImage.None,
            MessageBoxResult.None,
            MessageBoxOptions.None,
            null
        );
    }

    /// <summary>
    /// Displays a modal message box and returns the selected WPF result.
    /// </summary>
    /// <param name="owner">The owning window on the current dispatcher.</param>
    /// <param name="text">The message to display; null is treated as empty text.</param>
    /// <param name="caption">The window caption; null is treated as empty text.</param>
    /// <param name="button">The standard button combination.</param>
    /// <param name="icon">The standard message icon, including its WPF aliases.</param>
    /// <returns>The standard WPF result selected before the modal window closes.</returns>
    public static MessageBoxResult Show(
        Window owner,
        string text,
        string caption,
        MessageBoxButton button,
        MessageBoxImage icon)
    {
        if (owner is null)
        {
            throw new ArgumentNullException(nameof(owner));
        }

        return ShowCore(
            owner,
            text,
            caption,
            button,
            icon,
            MessageBoxResult.None,
            MessageBoxOptions.None,
            null
        );
    }

    /// <summary>
    /// Displays a modal message box and returns the selected WPF result.
    /// </summary>
    /// <param name="owner">The owning window on the current dispatcher.</param>
    /// <param name="text">The message to display; null is treated as empty text.</param>
    /// <param name="caption">The window caption; null is treated as empty text.</param>
    /// <param name="button">The standard button combination.</param>
    /// <param name="icon">The standard message icon, including its WPF aliases.</param>
    /// <param name="defaultResult">
    /// The initially focused result; an absent result selects the first button.
    /// </param>
    /// <returns>The standard WPF result selected before the modal window closes.</returns>
    public static MessageBoxResult Show(
        Window owner,
        string text,
        string caption,
        MessageBoxButton button,
        MessageBoxImage icon,
        MessageBoxResult defaultResult)
    {
        if (owner is null)
        {
            throw new ArgumentNullException(nameof(owner));
        }

        return ShowCore(owner, text, caption, button, icon, defaultResult, MessageBoxOptions.None, null);
    }

    /// <summary>
    /// Displays a modal message box and returns the selected WPF result.
    /// </summary>
    /// <param name="owner">The owning window on the current dispatcher.</param>
    /// <param name="text">The message to display; null is treated as empty text.</param>
    /// <param name="caption">The window caption; null is treated as empty text.</param>
    /// <param name="button">The standard button combination.</param>
    /// <param name="icon">The standard message icon, including its WPF aliases.</param>
    /// <param name="defaultResult">
    /// The initially focused result; an absent result selects the first button.
    /// </param>
    /// <param name="options">
    /// Text direction, alignment, or native desktop notification options.
    /// </param>
    /// <returns>The standard WPF result selected before the modal window closes.</returns>
    public static MessageBoxResult Show(
        Window owner,
        string text,
        string caption,
        MessageBoxButton button,
        MessageBoxImage icon,
        MessageBoxResult defaultResult,
        MessageBoxOptions options)
    {
        if (owner is null)
        {
            throw new ArgumentNullException(nameof(owner));
        }

        return ShowCore(owner, text, caption, button, icon, defaultResult, options, null);
    }

    /// <summary>
    /// Displays a message box with optional per-result button labels.
    /// Null or empty labels use the built-in CurrentUICulture resources.
    /// </summary>
    /// <param name="text">The message to display; null is treated as empty text.</param>
    /// <param name="caption">The window caption; null is treated as empty text.</param>
    /// <param name="button">The standard button combination.</param>
    /// <param name="icon">The standard message icon, including its WPF aliases.</param>
    /// <param name="defaultResult">
    /// The initially focused result; an absent result selects the first button.
    /// </param>
    /// <param name="options">
    /// Text direction, alignment, or native desktop notification options.
    /// </param>
    /// <param name="buttonText">
    /// Optional per-button labels; missing labels use localized resources.
    /// </param>
    /// <returns>The standard WPF result selected before the modal window closes.</returns>
    public static MessageBoxResult Show(
        string text,
        string caption = "",
        MessageBoxButton button = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.None,
        MessageBoxResult defaultResult = MessageBoxResult.None,
        MessageBoxOptions options = MessageBoxOptions.None,
        MessageBoxButtonText? buttonText = null)
    {
        return ShowCore(null, text, caption, button, icon, defaultResult, options, buttonText);
    }

    /// <summary>
    /// Displays an owned message box with optional per-result button labels.
    /// Null or empty labels use the built-in CurrentUICulture resources.
    /// </summary>
    /// <param name="owner">The owning window on the current dispatcher.</param>
    /// <param name="text">The message to display; null is treated as empty text.</param>
    /// <param name="caption">The window caption; null is treated as empty text.</param>
    /// <param name="button">The standard button combination.</param>
    /// <param name="icon">The standard message icon, including its WPF aliases.</param>
    /// <param name="defaultResult">
    /// The initially focused result; an absent result selects the first button.
    /// </param>
    /// <param name="options">
    /// Text direction, alignment, or native desktop notification options.
    /// </param>
    /// <param name="buttonText">
    /// Optional per-button labels; missing labels use localized resources.
    /// </param>
    /// <returns>The standard WPF result selected before the modal window closes.</returns>
    public static MessageBoxResult Show(
        Window owner,
        string text,
        string caption = "",
        MessageBoxButton button = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.None,
        MessageBoxResult defaultResult = MessageBoxResult.None,
        MessageBoxOptions options = MessageBoxOptions.None,
        MessageBoxButtonText? buttonText = null)
    {
        if (owner is null)
        {
            throw new ArgumentNullException(nameof(owner));
        }

        return ShowCore(owner, text, caption, button, icon, defaultResult, options, buttonText);
    }

    /// <summary>
    /// Validates before creating a window and routes desktop-specific requests to Windows.
    /// </summary>
    private static MessageBoxResult ShowCore(
        Window? owner,
        string text,
        string caption,
        MessageBoxButton button,
        MessageBoxImage icon,
        MessageBoxResult defaultResult,
        MessageBoxOptions options,
        MessageBoxButtonText? buttonText)
    {
        Validate(button, icon, defaultResult, options);

        if ((options & (MessageBoxOptions.ServiceNotification | MessageBoxOptions.DefaultDesktopOnly)) != 0)
        {
            if (owner is not null)
            {
                throw new ArgumentException(
                    "Desktop notification options cannot specify an owner.",
                    nameof(owner)
                );
            }

            if (buttonText is not null)
            {
                throw new NotSupportedException(
                    "Desktop notifications do not support custom button text."
                );
            }

            // todo: 为什么这个路径要弹原生消息框？
            return System.Windows.MessageBox.Show(text, caption, button, icon, defaultResult, options);
        }

        owner?.VerifyAccess();

        var window = new MessageBoxWindow(text, caption, button, icon, defaultResult, options, buttonText);
        if (owner is not null)
        {
            window.Owner = owner;

            // 保证主题与所有者窗口一致
            if (Theme.GetRequested(owner) is { } requestedTheme)
            {
                Theme.SetRequested(window, requestedTheme);
            }
        }

        // Window.ShowDialog resolves the active native owner when no explicit owner was supplied,
        // disables the thread's windows and restores activation in its finally path.
        window.ShowDialog();
        return window.Result;
    }

    /// <summary>
    /// Matches the accepted enum values in WPF MessageBox.ShowCore, including icon aliases.
    /// </summary>
    internal static void Validate(
        MessageBoxButton button,
        MessageBoxImage icon,
        MessageBoxResult defaultResult,
        MessageBoxOptions options)
    {
        if (!Enum.IsDefined(typeof(MessageBoxButton), button))
        {
            throw new InvalidEnumArgumentException(
                nameof(button),
                (int)button,
                typeof(MessageBoxButton)
            );
        }

        if (!Enum.IsDefined(typeof(MessageBoxImage), icon))
        {
            throw new InvalidEnumArgumentException(
                nameof(icon),
                (int)icon,
                typeof(MessageBoxImage)
            );
        }

        if (!Enum.IsDefined(typeof(MessageBoxResult), defaultResult))
        {
            throw new InvalidEnumArgumentException(
                nameof(defaultResult),
                (int)defaultResult,
                typeof(MessageBoxResult)
            );
        }

        const MessageBoxOptions mask = MessageBoxOptions.ServiceNotification
            | MessageBoxOptions.DefaultDesktopOnly
            | MessageBoxOptions.RightAlign
            | MessageBoxOptions.RtlReading;

        if ((options & ~mask) != 0)
        {
            throw new InvalidEnumArgumentException(
                nameof(options),
                (int)options,
                typeof(MessageBoxOptions)
            );
        }
    }
}