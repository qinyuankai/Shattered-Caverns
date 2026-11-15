# Batch 02：选关界面视觉重设计

## 任务目标

在不改变关卡配置、解锁规则和存档结构的前提下，使选关界面与新版主菜单保持一致，并支持长列表和完整焦点操作。

## 涉及文件

- `Assets/Resources/GameApplication.prefab`
- `Assets/Prefab/LevelSelectItem.prefab`
- `Assets/Scripts/Levels/Editor/GameInterfaceBuilder.cs`
- `Assets/Scripts/Levels/LevelSelectButton.cs`（仅在现有表现字段不足时）
- `Docs/TDD.md`
- `PROGRESS.md`

## 具体实现要求

1. 使用主菜单已经确认的色板、按钮层级和文字风格。
2. 关卡项必须明确显示编号、名称与状态。
3. 已通关、可探索、未解锁三种状态可快速区分。
4. 未解锁关卡不可点击，但文字仍保持可读。
5. 列表继续使用 ScrollRect，支持未来增加多个关卡。
6. 打开页面后聚焦第一个可用关卡。
7. 不向正式配置写入虚构关卡，不修改稳定关卡 ID。
8. 没有缩略图素材时不生成占位大图。

## 依赖关系

- 前置依赖：Batch 01。
- 后续依赖：Batch 07、Batch 08。

## 验收标准

- 三种关卡状态视觉清晰。
- 锁定关卡不可触发加载。
- 第一可用关卡默认获得焦点。
- 长列表可滚动，返回主菜单正确。
- 4:3、16:9、16:10 下文字不截断、不重叠。
- 临时测试多关卡状态后，正式 `GameConfiguration.asset` 不残留虚构数据。
- 36 项回归通过。

## 测试命令

Unity：

```text
MainMenu Play Mode > 选择关卡
使用鼠标、方向键和确认键遍历列表
在临时测试数据下检查已通关／可探索／未解锁
Tools > Shattered Caverns > Validate Game Flow
```

终端：

```bash
git status --short --branch
git diff -- Assets/Prefab/LevelSelectItem.prefab Assets/Resources/GameApplication.prefab
tail -60 Logs/GameFlowValidation.txt
```

## 完成条件

- 专项验收和回归测试全部通过。
- 临时测试数据已恢复。
- `PROGRESS.md` 记录结果和提交哈希。
- 独立提交中不包含正式关卡、音频或其他页面视觉改动。

