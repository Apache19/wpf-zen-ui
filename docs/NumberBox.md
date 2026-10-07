# NumberBox 即时输入状态

`NumberBox.HasInputError` 是只读依赖属性，默认 `false`，支持作为绑定源和样式触发条件。

- 未完成的数值（例如单独的负号）、无法求值的表达式、非有限数值会使状态为 `true`。
- 空文本对应 `null`，状态为 `false`；必填及业务范围由应用校验。
- 开启 `AcceptsExpression` 后按相同表达式解析规则检查当前文本；切换设置本身不额外提交草稿。
- 正常输入、清空、Esc 恢复、失焦提交、步进、外部值变化和范围纠正会随显示内容更新状态。
- 控件自行生成的格式化文本代表有效值，即使自定义格式含单位或省略小数，也不作为非法草稿重新解析。

应用可直接读取 `WeightEditor.HasInputError`，或使用 `{Binding HasInputError, ElementName=WeightEditor}` 设置提示和按钮状态，无需订阅 `TextChanged` 再解析文本。

该属性不等同于 WPF 的 `Validation.HasError`，不会覆盖业务校验或绑定错误。`false` 也不保证绑定源已更新，例如 `Value` 绑定仍采用默认的失焦更新方式时。无效文本恢复为旧值后，错误状态会清除；它不记录历史失败。
