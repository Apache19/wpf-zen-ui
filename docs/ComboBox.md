# ComboBox 下拉选择与文本搜索

`Wpf.Zen.Ui.Controls.ComboBox` 继承 WPF 原生 `ComboBox`，保留 `ItemsSource`、`SelectedItem`、`SelectedValue`、分组和项目模板等用法，并增加可编辑文本搜索、搜索交互模式、图标和状态画刷。

## 基本用法

```xml
<ui:ComboBox
    xmlns:ui="http://schemas.thezendev.com/wpf/2027"
    MinWidth="240"
    ItemsSource="{Binding Devices}"
    DisplayMemberPath="Name"
    SelectedValuePath="Id"
    SelectedValue="{Binding SelectedDeviceId, Mode=TwoWay}"
    ui:PlaceholderService.Content="请选择设备" />
```

示例中的 `Devices` 是提供 `Name`、`Id` 属性的对象集合。若业务直接使用对象，可改为绑定 `SelectedItem`。`DisplayMemberPath` 指定显示字段，`SelectedValuePath` 指定选中值字段；后者不参与文本搜索。

默认 `IsEditable="False"`，用户通过下拉列表选择项目。设为 `True` 后可在编辑框输入；`IsReadOnly="True"` 只禁止编辑文本，仍可选择列表项目。

## 启用自定义搜索

```xml
<ui:ComboBox
    xmlns:ui="http://schemas.thezendev.com/wpf/2027"
    IsEditable="True"
    IsTextSearchEnabled="True"
    TextSearchMode="Fuzzy"
    SearchInteraction="Filter"
    ItemsSource="{Binding Devices}"
    DisplayMemberPath="Name"
    SelectedItem="{Binding SelectedDevice, Mode=TwoWay}"
    ui:PlaceholderService.Content="输入名称或分段关键字" />
```

搜索由两个独立属性控制：

| 属性 | 类型 | 默认值 | 用途 |
| --- | --- | --- | --- |
| `TextSearchMode` | `ComboBoxTextSearchMode` | `Prefix` | 决定什么输入能匹配某个项目 |
| `SearchInteraction` | `ComboBoxSearchInteraction` | `ExplicitCommit` | 决定何时匹配、展示候选和更新选择 |
| `IsTextSearchFiltering` | 只读 `bool` | `false` | 表示当前正在展示临时筛选候选，供模板或绑定使用 |

`Contains`、`Tokens`、`Fuzzy` 要求控件启用，且 `IsEditable`、`IsTextSearchEnabled` 为 `true`，`IsReadOnly` 为 `false`。`Prefix` 始终使用 WPF 原生搜索与补全，不受 `SearchInteraction` 影响。

### 匹配规则

以下以候选文本 `ComboBoxItem` 为例：

| `TextSearchMode` | 规则 | 可匹配输入 |
| --- | --- | --- |
| `Prefix` | 原生前缀搜索，输入时进行行内补全 | `Combo` |
| `Contains` | 整段输入在候选文本任意位置连续出现 | `Box` |
| `Tokens` | 按空白拆分输入，每段都必须连续出现；各段顺序不限 | `box item`、`item combo` |
| `Fuzzy` | 按空白拆分输入，每段的字符按顺序出现即可，字符之间允许有间隔；各段顺序不限 | `cbi`、`cb item` |

`Fuzzy` 是字符子序列匹配，不提供拼写纠错、编辑距离或专门的驼峰边界评分，也不等同于 ReSharper 的完整匹配算法。

自定义搜索按以下强度选择最佳候选：全文相等、整段前缀、整段包含、各段连续包含、各段字符子序列。相同强度取原列表中靠前的项目。筛选列表保持源项目顺序，不按匹配强度重新排序，因此默认高亮的最佳候选不一定在第一行。

空输入或只有空白的输入不匹配任何项目；筛选模式此时显示空候选列表。`Contains` 不会拆词或自动去除输入两端的空白。

匹配遵循 `IsTextSearchCaseSensitive`（默认 `false`）和控件 `Language` 对应的区域比较规则；无法取得具体区域时使用当前区域。禁用的项目或已生成的禁用容器不参与匹配。

### 三种交互方式

| `SearchInteraction` | 输入时 | 下拉列表 | 确认时 |
| --- | --- | --- | --- |
| `ExplicitCommit` | 更新文本，保留原选中项 | 不自动展开，不筛选 | 回车或方法调用后匹配最佳项目、选中并填入全文 |
| `AutoSelect` | 实时匹配并更新 `SelectedItem`，保留输入文本 | 自动展开完整列表并高亮匹配项 | 回车、点击项目或方法调用后填入确认项目的全文 |
| `Filter` | 实时匹配，保留原选中项和输入文本 | 自动展开，只显示匹配候选的文字 | 回车、点击候选或方法调用后更新选择并填入全文 |

`ExplicitCommit` 适合输入完整查询后再处理的场景。它保留正常下拉列表，用户仍可按原生方式直接选择项目。

`AutoSelect` 会在输入过程中更新 `SelectedItem`、`SelectedValue`，并在选择变化时触发 `SelectionChanged`。此时 `Text` 仍是查询文字，不一定等于选中项的完整文本；没有匹配时立即清空选择。关闭下拉框不会撤销已经发生的实时选择。原生列表导航仍可能选择项目并填入全文。

`Filter` 的候选高亮是临时状态。比如原列表为 `ComboBoxItem`、`TextBlock`、`TextBox`，输入 `box` 时只展示 `ComboBoxItem`、`TextBox`。在这两个候选之间移动高亮不会改变 ComboBox 的 `SelectedItem`，也不会触发它的 `SelectionChanged`；确认后才提交对应的原始项目。

筛选使用独立候选列表，不修改 `ItemsSource`、原集合视图的 `Filter` 或其他控件共用的视图。候选按搜索文本平面展示，不沿用原项目的 `ItemTemplate`、`ItemContainerStyle` 或 `GroupStyle`。普通下拉列表仍使用这些设置。

### 键盘、取消与输入法

自定义搜索启用时，不带修饰键的 Enter 会确认搜索结果。筛选候选显示期间，还支持：

| 操作 | 行为 |
| --- | --- |
| Up / Down | 移动候选高亮，保持查询文本和正式选择 |
| PageUp / PageDown | 向前或向后移动最多 10 个候选，到边界停止 |
| Enter | 确认当前候选 |
| 点击候选 | 确认点击的候选 |
| Escape | 关闭候选列表，保留查询文本和原选中项 |
| Tab | 关闭候选列表，不提交候选，继续焦点导航 |

筛选列表被关闭或因失焦而关闭时，不会自动提交候选。自定义搜索不把失焦作为自动匹配触发条件。输入法组合输入期间暂缓自定义匹配，不把正在进行的输入法确认操作当作选择确认；组合结束后再更新文本及实时候选。

## 显式确认与绑定

`TryCommitTextSearch()` 可用于按钮或其他应用事件。下面的事件处理器使用名为 `DeviceComboBox` 的控件：

```xml
<StackPanel xmlns:ui="http://schemas.thezendev.com/wpf/2027">
    <ui:ComboBox
        x:Name="DeviceComboBox"
        IsEditable="True"
        IsTextSearchEnabled="True"
        TextSearchMode="Tokens"
        SearchInteraction="ExplicitCommit"
        ItemsSource="{Binding Devices}"
        DisplayMemberPath="Name"
        SelectedItem="{Binding SelectedDevice, Mode=TwoWay}"
        Text="{Binding DeviceQuery, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
    <Button Content="匹配" Click="OnMatchDeviceClick" />
</StackPanel>
```

```csharp
private void OnMatchDeviceClick(object sender, RoutedEventArgs e)
{
    bool matched = DeviceComboBox.TryCommitTextSearch();
    if (!matched)
    {
        // 应用可在这里更新自己的匹配结果提示。
    }
}
```

方法在控件所属 UI 线程调用；它会在需要时应用模板。行为如下：

- 成功返回 `true`，选中结果、填入其完整搜索文本并关闭下拉框。即使确认的是已选中的同一个项目，也会补全编辑框文字。
- 候选展示仍有效时，优先确认当前候选；否则按当前编辑文字寻找最佳匹配。候选列表已经关闭后，之前的临时高亮不再保留。
- 空输入或无匹配返回 `false`，清空选择并保留输入文字。不能将 `false` 理解为“所有状态都没有变化”。
- 原生 `Prefix` 模式、搜索未启用、控件禁用或只读、编辑部件不可用、正在输入法组合输入或内部同步时，返回 `false`，不执行确认。

选择使用原始项目，因此 `SelectedItem` 和 `SelectedValuePath` 继续按原生规则工作。文本与选择同步保留既有绑定；查询源需要随输入立即更新时，显式设置 `Text` 绑定的 `UpdateSourceTrigger=PropertyChanged`。

从代码或绑定源设置 `Text` 仍走 WPF 原生的文本同步与精确匹配路径，不会模拟用户输入的 `Contains`、`Tokens`、`Fuzzy` 实时搜索。需要处理此类文字时，可先设置 `Text`，再显式调用 `TryCommitTextSearch()`。从外部设置 `SelectedItem` 会显示该项目的完整文本。

外部文本或选择变化、项目集合变化、搜索设置变化会清理临时候选，避免之后确认过期结果。候选不会持续订阅每个数据项内部字段的变化；修改项目名称后，应重新输入或显式确认以重新匹配。

## 搜索文本的来源

控件复用 WPF 的搜索文字提取规则，包括项目上的 `TextSearch.Text`、控件上的 `TextSearch.TextPath`、`DisplayMemberPath`，以及内容和字符串回退规则。

对普通对象，通常设置 `DisplayMemberPath="Name"` 即可。使用复杂 `ItemTemplate` 时，应显式提供 `TextSearch.TextPath`，让搜索和确认后的编辑框文字有确定来源：

```xml
<ui:ComboBox
    xmlns:ui="http://schemas.thezendev.com/wpf/2027"
    IsEditable="True"
    TextSearchMode="Tokens"
    SearchInteraction="Filter"
    ItemsSource="{Binding Devices}"
    TextSearch.TextPath="Name">
    <ui:ComboBox.ItemTemplate>
        <DataTemplate>
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="{Binding Code}" Margin="0,0,8,0" />
                <TextBlock Text="{Binding Name}" />
            </StackPanel>
        </DataTemplate>
    </ui:ComboBox.ItemTemplate>
</ui:ComboBox>
```

这里普通列表显示编号与名称，搜索、筛选候选和确认后的编辑框使用 `Name`。搜索文字也是确认后回填的文字；如果将 `TextSearch.TextPath` 指向包含别名或拼音的字段，确认后也会显示该字段，不会自动换回 `DisplayMemberPath`。

## 外观与状态画刷

使用 `Icon` 设置图标槽内容，`IconMargin` 设置外边距；默认样式的图标边距为 `10,0,0,0`。`PlaceholderService.Content` 提供占位内容。圆角使用 `Border.CornerRadius` 附加属性。

普通状态使用 `Foreground`、`Background`、`BorderBrush`。以下状态属性均可单独设置：

| 状态 | 前景 | 背景 | 边框 |
| --- | --- | --- | --- |
| 鼠标悬停 | `MouseOverForeground` | `MouseOverBackground` | `MouseOverBorderBrush` |
| 可编辑控件内部获得键盘焦点 | `FocusedForeground` | `FocusedBackground` | `FocusedBorderBrush` |
| 非可编辑控件展开下拉框 | `DropDownOpenForeground` | `DropDownOpenBackground` | `DropDownOpenBorderBrush` |
| 禁用 | `DisabledForeground` | `DisabledBackground` | `DisabledBorderBrush` |

状态画刷的依赖属性默认值为 `null`，默认样式可提供主题值；当前状态的某个画刷为 `null` 时，该项回退到普通画刷。优先级为禁用、可编辑焦点或非可编辑展开、悬停、普通。只读的可编辑控件仍使用可编辑焦点状态。

只读属性 `VisualForeground`、`VisualBackground`、`VisualBorderBrush` 提供最终计算结果，适合在自定义模板中通过 `TemplateBinding` 使用。

## 自定义模板

默认模板已接入全部搜索模式。自行替换 `ControlTemplate` 时，需要保留原生 ComboBox 的可编辑框、下拉框及列表结构，并接入以下约定：

| 部件或状态 | 要求 |
| --- | --- |
| `PART_EditableTextBox` | 原生 `System.Windows.Controls.TextBox` 或派生类型；用于编辑、文本同步和输入法状态 |
| `PART_SearchResults` | 原生 `System.Windows.Controls.ListBox` 或派生类型；`Filter` 模式必需，放在下拉框内 |
| `IsTextSearchFiltering` | 为 `true` 时隐藏普通项目列表并显示候选列表；其他时间显示普通列表 |

候选列表的 `ItemsSource`、选择和生命周期由控件管理，不要绑定到业务集合或正式 `SelectedItem`。候选对象公开 `Text` 供显示，可使用 `DisplayMemberPath="Text"` 或绑定 `Text` 的模板；它不是原始业务对象。列表及其容器应设置 `Focusable="False"`，让键盘输入保留在编辑框。

可参考默认模板中的以下两个片段。第一个放入下拉内容，第二个放入 `ControlTemplate.Triggers`；`NormalItemsHost` 指模板中承载普通列表的元素名称：

```xml
<ListBox
    x:Name="PART_SearchResults"
    DisplayMemberPath="Text"
    Focusable="False"
    Visibility="Collapsed">
    <ListBox.ItemContainerStyle>
        <Style TargetType="ListBoxItem">
            <Setter Property="Focusable" Value="False" />
        </Style>
    </ListBox.ItemContainerStyle>
</ListBox>
```

```xml
<Trigger Property="IsTextSearchFiltering" Value="True">
    <Setter TargetName="NormalItemsHost" Property="Visibility" Value="Collapsed" />
    <Setter TargetName="PART_SearchResults" Property="Visibility" Value="Visible" />
</Trigger>
```

缺少 `PART_SearchResults` 时，使用 `Filter` 处理输入会抛出 `InvalidOperationException`。仅使用原生搜索或另外两种交互模式时不需要该部件。

## 示例与源码

- [图库 ComboBox 页面](../src/Wpf.Zen.Ui.Gallery/Views/Pages/BasicInput/ComboBoxPage.xaml)：包含绑定、分组，以及三种搜索交互的切换示例。
- [ComboBox 公开属性与方法](../src/Wpf.Zen.Ui/Controls/ComboBox/ComboBox.cs)
- [默认控件模板](../src/Wpf.Zen.Ui/Controls/ComboBox/ComboBox.xaml)
- [搜索行为测试](../tests/Wpf.Zen.Ui.UnitTests/Controls/ComboBox/ComboBoxSearchTests.cs)
