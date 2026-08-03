// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System;
using System.Diagnostics;
using System.Windows;
using Wpf.Zen.Ui.Appearance;
using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Input;

namespace Wpf.Zen.Ui.SyntaxHighlight.Controls;

/// <summary>
/// Formats and display a fragment of the source code.
/// </summary>
public class CodeBlock : System.Windows.Controls.ContentControl
{
    private string _sourceCode = string.Empty;

    #region DP: SyntaxContent

    /// <summary>
    /// Property for <see cref="SyntaxContent"/>.
    /// </summary>
    public static readonly DependencyProperty SyntaxContentProperty = DependencyProperty.Register(
        nameof(SyntaxContent),
        typeof(object),
        typeof(CodeBlock),
        new PropertyMetadata(null)
    );

    /// <summary>
    /// Gets the formatted <see cref="System.Windows.Controls.ContentControl.Content"/>.
    /// </summary>
    public object? SyntaxContent
    {
        get => GetValue(SyntaxContentProperty);
        internal set => SetValue(SyntaxContentProperty, value);
    }

    #endregion

    #region DP: ButtonCommand

    /// <summary>
    /// Property for <see cref="ButtonCommand"/>.
    /// </summary>
    public static readonly DependencyProperty ButtonCommandProperty = DependencyProperty.Register(
        nameof(ButtonCommand),
        typeof(IRelayCommand),
        typeof(CodeBlock)
    );

    /// <summary>
    /// Gets the command triggered after clicking the control button.
    /// </summary>
    public IRelayCommand ButtonCommand => (IRelayCommand)GetValue(ButtonCommandProperty);

    #endregion

    static CodeBlock()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(CodeBlock),
            new FrameworkPropertyMetadata(typeof(CodeBlock))
        );
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeBlock"/> class, and assigns <see cref="ButtonCommand"/> default action.
    /// </summary>
    public CodeBlock()
    {
        SetValue(ButtonCommandProperty, new RelayCommand<string>(OnTemplateButtonClick));
        Unloaded += CodeBlock_Unloaded;
        Loaded   += CodeBlock_Loaded;
    }

    private void CodeBlock_Loaded(object sender, RoutedEventArgs e)
    {
        ThemeManager.Changed += ThemeOnChanged;
    }

    private void CodeBlock_Unloaded(object sender, RoutedEventArgs e)
    {
        ThemeManager.Changed -= ThemeOnChanged;
    }

    private void ThemeOnChanged(object? sender, ThemeChangedEventArgs e)
    {
        UpdateSyntax();
    }

    /// <summary>
    /// This method is invoked when the Content property changes.
    /// </summary>
    /// <param name="oldContent">The old value of the Content property.</param>
    /// <param name="newContent">The new value of the Content property.</param>
    protected override void OnContentChanged(object oldContent, object newContent)
    {
        UpdateSyntax();
    }

    protected virtual void UpdateSyntax()
    {
        _sourceCode = Highlighter.Clean(Content as string ?? string.Empty);

        var richTextBox = new RichTextBox();

        richTextBox.Document.Blocks.Clear();
        richTextBox.Document.Blocks.Add(Highlighter.FormatAsParagraph(_sourceCode));

        SetCurrentValue(SyntaxContentProperty, richTextBox);
    }

    private void OnTemplateButtonClick(string? _)
    {
        try
        {
            Clipboard.Clear();
            Clipboard.SetText(_sourceCode);
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
    }
}
