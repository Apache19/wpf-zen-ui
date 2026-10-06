// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Resources;
using System.Windows.Data;
using System.Windows.Markup;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// 将控件 Language 与资源键转换为本地化文本；绑定可随语言变更重新求值。
/// </summary>
internal sealed class DateTimePickerLocalization : IValueConverter
{
    private static readonly ResourceManager Resources = new(
        "Wpf.Zen.Ui.Controls.DateTimePicker.DateTimePickerStrings",
        typeof(DateTimePickerLocalization).Assembly);

    /// <summary>
    /// 按指定区域查找资源，缺失翻译时使用英文中性资源。
    /// </summary>
    internal static string GetString(string key, CultureInfo culture)
    {
        return Resources.GetString(key, culture) ?? key;
    }

    /// <summary>
    /// 使用绑定的 Language 选择翻译；无有效语言时使用当前界面区域。
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var resourceCulture = CultureInfo.CurrentUICulture;
        if (value is XmlLanguage language)
        {
            try
            {
                resourceCulture = language.GetSpecificCulture();
            }
            catch (InvalidOperationException)
            {
                // 未注册的语言标签采用当前界面区域。
            }
        }
        string key = (string)parameter;
        return GetString(key, resourceCulture);
    }

    /// <summary>
    /// 本地化标签只读，不向 Language 回写。
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
