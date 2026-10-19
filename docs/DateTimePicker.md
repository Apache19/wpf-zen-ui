# DateTimePicker 日期时间选择器

`Wpf.Zen.Ui.Controls.DateTimePicker` 编辑可空的完整 `DateTime`。弹出层中的日历、时间列和快捷命令共享一个待提交值；确认后才会回写正式值。

```xml
<ui:DateTimePicker
    xmlns:ui="http://schemas.thezendev.com/wpf/2027"
    SelectedDateTime="{Binding AppointmentTime, Mode=TwoWay}"
    DateTimeFormat="yyyy-MM-dd HH:mm:ss"
    Language="zh-CN"
    FirstDayOfWeek="Monday" />
```

## 值与事务

| 属性 | 行为 |
| --- | --- |
| `SelectedDateTime` | 已提交的完整日期时间，默认 `null`，默认双向绑定 |
| `SelectedDate` | 正式值的日期投影，默认双向绑定 |
| `HasInputError` | 只读输入错误状态，默认 `false`；随当前编辑区域更新 |
| `PendingDateTime` | 只读待提交值，弹出层关闭时为 `null` |
| `IsDropDownOpen` | 默认双向绑定；打开建立事务，直接关闭取消事务 |
| `Text` | 完整日期时间文本，默认双向绑定 |
| `DateTimeFormat` | 默认 `yyyy-MM-dd HH:mm:ss`，遵循 .NET 日期时间格式字符串 |

打开时复制正式值；正式值为空时以 `DateTime.Now` 初始化编辑状态，业务值仍为空。取消、Esc、外部点击、禁用、卸载或替换模板都会放弃临时编辑。弹出层打开时外部修改正式值会重新初始化当前事务，防止随后提交覆盖新的业务值。

修改 `SelectedDate` 保留正式值的时间、`Kind` 和亚秒精度；原值为空时采用零点；设置 `null` 清空两个正式属性。修改日期或单个时间字段不会转换时区。小时和分钟循环不进位、不跨日。未经编辑的亚秒精度保留。

监听 `SelectedDateTimeChanged` 冒泡事件可获得正式值的旧值和新值；日历浏览和弹出层临时编辑不触发该事件。内部同步通过 `SetCurrentValue` 保留绑定。

## 输入与键盘

- 主编辑框在 Enter 或失去键盘焦点时提交；外部设置 `Text` 立即解析。
- 使用 `DateTimeFormat` 和 `Language` 对应区域优先精确解析；数字日期前缀可在提交时补齐末尾字段。例如默认格式下，`2026-1` 补为 `2026-01-01 00:00:00`，`2026-1-2 3:4` 补为 `2026-01-02 03:04:00`。
- 补全先解析显式格式（含标准格式如 `G`），再参考 ISO 和当前区域日期时间模式，识别字段顺序、分隔符、引号字面量及转义。`05-12` 在默认格式下补为当年 5 月 12 日零点；`12:36` 补为当天 12:36:00。单独数字只有超过其它字段上限 59 时才认作年份，例如 `596` 补为 `0596-01-01 00:00:00`，`32` 因含义不明确而保留。明确年份时缺省月、日为 1，时、分、秒为 0，不沿用原正式值。相邻无分隔字段、重复字段、月份名称等复杂格式仍可按完整格式精确解析。非法日期、越界字段或无法确定含义的文本不会修改正式值，触发 `DateValidationError`。Enter 或显式提交失败时保留原文供修正；编辑框失焦提交失败时丢弃错误文本，恢复正式值的格式化显示，正式值为空时清空文本。
- 主编辑框的 Esc 恢复已提交值的显示文本。F4、Alt+Down 或打开按钮始终允许打开弹出层，错误文本不会阻止打开。合法草稿先应用；无效草稿在取消时保留，在确认选择时被格式化结果覆盖。
- 点击控件本体或调用控件的 `Focus()` 会将焦点转入内部文本框；打开按钮不获取焦点，也不参与 Tab 导航。
- 时间字段只接受 ASCII 数字，支持一位数字中间状态；合法值立即更新待提交值。空值、越界值不覆盖最后有效值，失焦恢复两位显示。
- 上下方向键、滚轮、相邻值按钮和重复按钮均按当前字段循环选择；Home / End 选择字段端点。
- 弹出层 Esc 或 Alt+Up 取消。有效输入上的 Enter 提交；日历年／十年浏览中的 Enter 保留原生下钻行为，按钮上的 Enter 执行按钮命令。
- Tab 在弹出层中循环移动焦点。原生按钮支持鼠标和触摸输入。

再次提交与当前显示完全相同的文本不会丢失格式未展示的秒、亚秒或 `Kind`。若输入了不同文本，则以解析结果为准；没有时区信息的文本保留已有值的 `Kind`。控件不承担业务时区转换；需要全球唯一时间点时请在业务层处理。

可调用 `CommitText()` 显式提交主编辑框，调用 `Commit()` 确认弹出层，调用 `Cancel()` 放弃弹出层事务。前两个方法返回是否成功。将 `DateValidationError` 参数的 `ThrowException` 设为 `true` 可抛出错误。

## 日期约束与快捷命令

```xml
<ui:DateTimePicker
    DisplayDateStart="2026-01-01"
    DisplayDateEnd="2026-12-31">
    <CalendarDateRange Start="2026-10-10" End="2026-10-12" />
</ui:DateTimePicker>
```

`DisplayDateStart`、`DisplayDateEnd` 忽略时间部分，包含端点；`BlackoutDates` 使用原生 `CalendarBlackoutDatesCollection`。文本、OK、快捷命令和外部属性赋值都执行日期约束检查。

外部设置禁止日期或范围外的正式值会抛出 `ArgumentOutOfRangeException`，保留原值。设置日期边界时，范围必须有序且包含已有正式值；要整体更换范围，可先清空正式值和旧边界，再设置新范围。收紧范围若使待提交日期无效，会清空日历高亮并禁用 OK，等待用户选择有效日期。

禁止日期集合在 UI 线程修改。原生日历不允许将当前高亮的日期加入禁止集合。空值控件打开时即使“现在”不可选，也不会扩大日期范围或自动提交其它日期；用户需要先选有效日期。

| 快捷命令参数 | 预选结果 |
| --- | --- |
| `Year` | 本地当前年度第一天零点 |
| `Month` | 本地当前月份第一天零点 |
| `Week` | 本地当前周第一天零点，遵循 `FirstDayOfWeek` |
| `Today` | 本地今天零点 |
| `Now` | 点击时的完整本地时间 |

快捷命令基于执行时的本地时钟，结果具有 `DateTimeKind.Local`。日期不可选时按钮禁用。执行后更新待提交值、日历显示和时间字段，覆盖时间字段中的未完成输入，弹出层保持打开。`SelectedDateTime`、`SelectedDate` 和主编辑框文本保持原值；通过 OK 确认后才提交，取消则放弃预选结果。`FirstDayOfWeek` 默认跟随 `Language`；显式设置的值优先。

清除按钮遵循库内文本控件惯例：文本非空且控件内有键盘焦点时显示叉号；`IsClearButtonEnabled` 默认为 `true`，设为 `false` 可隐藏并禁用。清除会取消弹出层事务，同步清空 `SelectedDateTime`、`SelectedDate` 和错误草稿，并保留属性绑定。打开按钮与清除按钮在鼠标按下时即将焦点交给内部 TextBox。

## 模板与辅助技术

默认样式随 `Themes/Generic.xaml` 发布。通过 `CalendarStyle` 可定制内部原生日历；默认样式增大日期按钮以改善点击和触摸操作。日历继续使用 WPF 原生主题，时间区域和弹出层使用库的动态主题画刷。

| 模板部件 | 类型 |
| --- | --- |
| `PART_TextBox` | `System.Windows.Controls.TextBox` |
| `PART_Button` | `ButtonBase` |
| `PART_ClearButton` | `ButtonBase` |
| `PART_Popup` | `Popup` |
| `PART_CalendarHost` | `ContentControl`，内容由控件管理 |
| `PART_Hour`、`PART_Minute`、`PART_Second` | `DateTimePickerTimeColumn` |

时间列自身使用 `PART_Input`（原生 `TextBox`），公开 `Value`、`Maximum`、只读 `VisibleValues`、只读 `HasValidInput` 和 `StepCommand`，供模板定制。五行视口始终以当前值居中，无需无限列表。

底部按钮使用 `CommitCommand`、`CancelCommand` 和 `SelectShortcutCommand`。Popup 中应显式将 `CommandTarget` 指向所属 `DateTimePicker`。快捷命令参数为上表区分大小写的字符串。更换或移除模板会取消事务、解除旧部件事件，并复用同一个日历及禁止日期集合。

控件提供 UI Automation 的 Value 和 ExpandCollapse 模式，时间列提供 RangeValue 模式；值和展开状态变化会通知辅助技术。可通过 `AutomationProperties.Name` 为主编辑框设置业务名称。底部文字可通过自定义模板本地化。

## 实现选择

本实现采用 `Control + Calendar` 组合，是设计方案第 12 节允许的继承风险处理。查阅本地 WPF 官方 `DatePicker.cs` 后确认：

- `OnTextChanged` 和 `OnSelectedDateChanged` 通过私有方法执行日期解析与格式化，无法通过派生重写替换。
- `OnApplyTemplate` 无条件将私有日历赋给 `Popup.Child`。
- 默认日历选择流程直接写入 `SelectedDate`，与待提交事务模型不同。

组合实现保留方案列出的常用日期 API，但它不是 `System.Windows.Controls.DatePicker` 的派生类，不提供 `SelectedDateFormat`。完整值通知请使用 `SelectedDateTimeChanged`，弹出窗口生命周期可监听 `CalendarOpened` / `CalendarClosed`。

第一版提供 24 小时制和秒级字段，不提供独立 TimePicker、时区选择或完整日期时间上下限。当前日期验证集中在同一入口，后续可以在此增加完整时间范围规则。

## 验证与示例

图库的 **Date and time → DatePicker** 页面包含实时正式值显示、格式及禁止日期示例。回归测试位于 `tests/Wpf.Zen.Ui.UnitTests/Controls/DateTimePickerTests.cs`，覆盖事务、绑定、文化、日期约束、实际模板、循环输入、键盘、生命周期和辅助技术。

使用仓库的 xUnit v3 可执行测试入口：

```powershell
dotnet run --project tests/Wpf.Zen.Ui.UnitTests/Wpf.Zen.Ui.UnitTests.csproj -- `
    -class Wpf.Zen.Ui.UnitTests.Controls.DateTimePickerTests
```


## 占位符及统一机制评估

DateTimePicker 使用现有 `PlaceholderService.Content` 附加属性和 `PlaceholderControl`。
提示只在 `Text` 为空时显示，获得焦点后仍可见；错误草稿非空时隐藏。
占位内容不会写入 Text、SelectedDateTime 或 UIA Value，不参与命中测试或焦点导航。
清除后恢复提示；未设置内容时不显示提示。业务占位文案由应用负责本地化。

```xml
<ui:DateTimePicker Language="zh-CN"
                   ui:PlaceholderService.Content="请选择日期和时间" />
```

| 维度 | ui:TextBox 内置机制 | 附加属性 + PlaceholderControl |
| --- | --- | --- |
| 使用 | PlaceholderText 直观，另有启用开关 | 同一附加属性适用于不同控件，null 即移除提示 |
| 内容 | 字符串；用 TextBlock 对齐编辑区布局 | object 内容，可用文本、图标或组合内容 |
| 维护 | 每种控件维护专用属性、可见状态和回调 | 复用内容入口及呈现控件；空值语义由具体编辑器决定 |
| 布局 | 对多行文本、滚动、对齐的模拟更接近编辑器 | 模板必须对齐内边距、文本起点并处理裁剪 |
| 生命周期 | 控件自身管理状态，但增加同步状态 | 原有服务依赖事件和部件查找；声明式模板可消除这部分依赖 |

推荐统一到附加属性和专用呈现控件，但不应照搬原服务的命令式部件更新。
DateTimePicker 通过 TemplateBinding 呈现内容，以 Text 的空值触发器控制显示，
模板重建和绑定更新自然生效，不增加 IsPlaceholderVisible 状态或输入事件订阅。
服务只为原生 TextBox/ComboBox 保留现有事件适配，其它控件消费附加属性时自行定义空值语义。
后续统一 TextBox/ComboBox 时，宜将模板绑定、空值规则和视觉呈现统一，逐步移除事件适配。
多行、滚动、可编辑组合框、模板替换、动态提示及 UIA 需分别验证，不能把“无选择项”等同于“无文本”。
本次没有改写这些控件已有的输入和占位行为。

自定义 DateTimePicker 模板可使用 `PART_Placeholder`，类型为 `PlaceholderControl`；
它是默认模板的展示约定，控件代码不要求该部件。绑定 `PlaceholderService.Content` 并按 Text 控制显示即可。

## 本地化

新增界面文本统一放在 `DateTimePickerStrings*.resx`，使用 ResourceManager 的卫星程序集与父区域回退。
覆盖确认、取消、快捷操作、清除、打开提示、时间字段、步进按钮的辅助技术名称、数字错误提示及日期验证消息。
以控件的 `Language` 选择资源，与日期解析及原生日历区域一致；可从父元素继承，运行时修改立即更新模板绑定。
时间列采用固定 24 小时语义和拉丁数字。自定义模板若覆盖标签，应自行提供相应翻译。

语言集合对齐本地 WPF `PresentationFramework/Resources/xlf/Strings.*.xlf`：
英文中性资源，以及捷克语、德语、西班牙语、法语、意大利语、日语、韩语、波兰语、
巴西葡萄牙语、俄语、土耳其语、简体中文和繁体中文，共 14 种。
例如 `de-DE` 回退到 `de`，`zh-CN` 使用 `zh-Hans`，`zh-TW` 使用 `zh-Hant`；
未提供翻译的语言回退英文。新增翻译为本控件文案，并非 WPF 内置资源的直接复制。

WPF 的 Language 默认是 en-US，设置 CurrentUICulture 本身不等于修改 Language。
应用可以在根元素上设置 Language，同时统一日历、日期格式和新增界面文案的区域。
按钮按内容测量，时间标题列采用自动宽度，以容纳不同语言的长度。
新增语言时复制中性资源、保留全部键，并补充模板切换和资源完整性测试。

## 日历鼠标捕获与模板组合

CalendarItem 在日期按下时以 SubTree 模式捕获鼠标。日期按钮的抬起处理会设置
Handled，可能跳过 CalendarItem.OnMouseUp 中的释放逻辑，使后续外围点击继续路由到日历。
这属于鼠标捕获生命周期问题，键盘焦点仍可保留在日期按钮上。

DateTimePicker 在日历上监听包含已处理事件的冒泡 MouseUp，在左键选择完成后，
仅释放属于本日历的 CalendarItem 捕获。Popup 随后通过自身 LostMouseCapture 逻辑
恢复捕获，时间列、操作按钮和外部点击关闭均继续使用 WPF 原生交互机制。
该处理不提前中断按下或拖动，也不依赖 CalendarItem 的模板部件名称。

将新功能放进 Calendar 模板，并不会改变 CalendarItem 的捕获释放代码；
即使进一步放入 CalendarItem 模板以扩大捕获子树，也无法解决外部点击被吞掉的问题，
且会把时间编辑和事务命令耦合到原生日历的内部模板契约。
因此继续采用 Popup 中组合 Calendar 与时间编辑器的结构，将捕获协调放在宿主控件中。

## 即时输入状态

`HasInputError` 是只读依赖属性，可作为绑定源或样式触发器条件。文本草稿无法解析、日期超出配置范围或属于禁止日期时为 `true`；弹出层打开期间改为检查待提交日期与各时间字段。输入检查不提交正式值，也不触发 `DateValidationError`，该事件仍在提交失败时触发。

空文本代表可空值，状态为 `false`。可按现有规则补全的片段也为 `false`。必填、业务规则和绑定转换错误仍由应用校验。取消、Esc、失焦恢复、外部值更新及日期约束变化后会重新计算状态。

```xml
<ui:DateTimePicker x:Name="DateEditor" SelectedDateTime="{Binding AppointmentTime}" />
<TextBlock Text="日期时间输入尚未有效">
    <TextBlock.Style>
        <Style TargetType="TextBlock">
            <Setter Property="Visibility" Value="Collapsed" />
            <Style.Triggers>
                <DataTrigger Binding="{Binding HasInputError, ElementName=DateEditor}" Value="True">
                    <Setter Property="Visibility" Value="Visible" />
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </TextBlock.Style>
</TextBlock>
```

`false` 只表示当前编辑内容可接受，不保证草稿已提交到 `SelectedDateTime` 或绑定源。应用保存前仍应按当前编辑区域调用 `CommitText()` 或 `Commit()`。失焦恢复旧值后状态也会恢复，属性不记录历史输入失败。
