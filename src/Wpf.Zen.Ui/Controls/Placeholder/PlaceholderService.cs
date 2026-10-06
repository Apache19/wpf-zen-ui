// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using NativeComboBox = System.Windows.Controls.ComboBox;
using NativeTextBox = System.Windows.Controls.TextBox;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// 为编辑控件提供统一的占位内容入口；内容可以是文本或可视元素，默认 null。
/// </summary>
/// <remarks>
/// 原生 TextBox 与 ComboBox 由服务监听输入并更新 PART_Placeholder。
/// 其它控件可在模板中绑定 Content，并自行根据空值状态控制占位符显示。
/// 占位内容不参与输入、选择值或数据提交。
/// </remarks>
public static class PlaceholderService
{
    private const string PlaceholderPartName = "PART_Placeholder";

    /// <summary>
    /// 标识占位内容附加属性，默认 null；由支持此契约的模板呈现。
    /// </summary>
    public static readonly DependencyProperty ContentProperty =
        DependencyProperty.RegisterAttached(
            "Content",
            typeof(object),
            typeof(PlaceholderService),
            new PropertyMetadata(null, OnContentChanged)
        );

    /// <summary>
    /// 设置占位内容。设为 null 可移除提示，不影响控件的实际输入值。
    /// </summary>
    /// <param name="element">使用占位模板的控件。</param>
    /// <param name="value">文本或其它占位内容。</param>
    public static void SetContent(DependencyObject element, object? value)
    {
        element.SetValue(ContentProperty, value);
    }

    /// <summary>
    /// 获取控件的占位内容。
    /// </summary>
    /// <param name="element">要读取的控件。</param>
    /// <returns>占位内容，未设置时为 null。</returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static object? GetContent(DependencyObject element)
    {
        return element.GetValue(ContentProperty);
    }

    /// <summary>
    /// 为需要命令式适配的原生编辑器安排初始化，其它控件由模板管理显示。
    /// </summary>
    private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // 只为原生编辑控件安装行为；其它控件通过模板绑定消费此附加属性。
        if (d is not NativeTextBox && d is not NativeComboBox)
        {
            return;
        }

        if (d is Control control)
        {
            if (control.IsLoaded)
            {
                // Try to unsubscribe as much as possible.
                control.Loaded -= Control_Loaded;
                InitializeControl(control);
            }
            else
            {
                control.Loaded -= Control_Loaded;
                control.Loaded += Control_Loaded;
            }
        }
    }

    /// <summary>
    /// 在模板可用后安装原生编辑器事件。
    /// </summary>
    private static void Control_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        control.Loaded -= Control_Loaded;
        InitializeControl(control);
    }

    /// <summary>
    /// 刷新占位内容并确保每种输入事件仅订阅一次。
    /// </summary>
    private static void InitializeControl(Control control)
    {
        UpdateVisibility(control);

        switch (control)
        {
            case NativeTextBox textBox:
            {
                textBox.TextChanged -= OnTextChanged;
                textBox.TextChanged += OnTextChanged;
                break;
            }

            case NativeComboBox comboBox:
                comboBox.SelectionChanged -= OnComboSelectionChanged;
                comboBox.SelectionChanged += OnComboSelectionChanged;
                comboBox.RemoveHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnTextChanged));
                comboBox.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnTextChanged));
                break;
        }
    }

    /// <summary>
    /// 输入变化时重新判断原生编辑器是否为空。
    /// </summary>
    private static void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is Control control)
        {
            UpdateVisibility(control);
        }
    }

    /// <summary>
    /// 选择项变化时刷新组合框占位状态。
    /// </summary>
    private static void OnComboSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is Control control)
        {
            UpdateVisibility(control);
        }
    }

    /// <summary>
    /// 根据原生编辑器状态更新占位部件，保留部件自身的内容绑定。
    /// </summary>
    private static void UpdateVisibility(Control control)
    {
        var part = control.Template?.FindName(PlaceholderPartName, control);
        if (part is not PlaceholderControl placeholder)
        {
            return;
        }

        // Check binding first to avoid overwriting it
        if (BindingOperations.GetBinding(placeholder, ContentControl.ContentProperty) == null)
        {
            placeholder.SetCurrentValue(ContentControl.ContentProperty, GetContent(control));
        }

        bool isEmpty;
        switch (control)
        {
            case NativeTextBox tb:
            {
                isEmpty = string.IsNullOrEmpty(tb.Text);
                break;
            }

            case NativeComboBox cb:
            {
                var editableTextBox = cb.Template?.FindName("PART_EditableTextBox", cb) as NativeTextBox;
                isEmpty = cb.SelectedItem == null && string.IsNullOrEmpty(editableTextBox?.Text ?? cb.Text);
                break;
            }

            default:
            {
                isEmpty = true;
                break;
            }
        }

        placeholder.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
    }
}