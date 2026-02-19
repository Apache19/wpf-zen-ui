// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Ui.Controls;

/// <summary>
/// Extends <see cref="System.Windows.Controls.GridView"/> to use Wpf.Ui custom styles
/// </summary>
/// <example>
/// To use this enhanced GridView in a ListView:
/// <code lang="xml">
/// &lt;ListView&gt;
///     &lt;ListView.View&gt;
///         &lt;local:GridView&gt;
///             &lt;GridViewColumn Header="First Name" DisplayMemberBinding="{Binding FirstName}"/&gt;
///             &lt;GridViewColumn Header="Last Name" DisplayMemberBinding="{Binding LastName}"/&gt;
///         &lt;/local:GridView&gt;
///     &lt;/ListView.View&gt;
/// &lt;/ListView&gt;
/// </code>
/// </example>
public class GridView : System.Windows.Controls.GridView
{
    // todo 这是tmd什么神仙做法
    //   修正：给GridView创建默认样式，在默认样式里更改 ColumnHeaderContainerStyle
    //   否则会影响全局 ColumnHeaderContainer
    static GridView()
    {
        ResourceDictionary resourceDict = new()
        {
            Source = new Uri(
                "pack://application:,,,/Wpf.Ui;component/Controls/GridView/GridViewColumnHeader.xaml"
            ),
        };

        Style defaultStyle = (Style)resourceDict["UiGridViewColumnHeaderStyle"];

        ColumnHeaderContainerStyleProperty.OverrideMetadata(
            typeof(GridView),
            new FrameworkPropertyMetadata(defaultStyle)
        );
    }
}
