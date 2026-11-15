# Batch 04：暂停界面视觉重设计

## 任务目标

把暂停界面制作成与主菜单一致的游戏内覆盖层，并保持全局暂停与区块操作暂停的既有协调机制。

## 涉及文件

- `Assets/Resources/GameApplication.prefab`
- `Assets/Scripts/Levels/Editor/GameInterfaceBuilder.cs`
- `Assets/Scripts/Levels/GameInterfaceController.cs`（仅在表现逻辑确有需要时）
- `Docs/TDD.md`
- `PROGRESS.md`

## 具体实现要求

1. 使用半透明遮罩和中央内容卡片，不完全遮蔽游戏环境。
2. 包含继续、设置、重新开始和返回主菜单。
3. 继续为默认焦点；危险操作使用次级或警示层级。
4. UI 不直接修改 `Time.timeScale`，继续由 `GameFlowController` 管理。
5. 设置页面返回后仍保持暂停。
6. 区块操作期间暂停和恢复后，区块操作状态保持不变。
7. 不增加确认弹窗，不修改区块交互代码。

## 依赖关系

- 前置依赖：Batch 03。
- 后续依赖：Batch 07、Batch 13。

## 验收标准

- Esc 可打开和关闭暂停界面。
- 暂停时 `Time.timeScale == 0`。
- 默认焦点为继续。
- 设置返回后仍暂停。
- 区块操作暂停后恢复仍保持原操作状态。
- 重开重新加载当前场景，返回主菜单正常。
- 4:3、16:9、16:10 下界面不越界。
- 36 项回归通过。

## 测试命令

Unity：

```text
进入 URP2DSceneTemplate
普通状态按 Esc 暂停／继续
进入区块操作后暂停／继续
暂停 > 设置 > 返回
暂停 > 重新开始／返回主菜单
Tools > Shattered Caverns > Validate Game Flow
```

终端：

```bash
git status --short --branch
git diff -- Assets/Resources/GameApplication.prefab Assets/Scripts/Levels/GameInterfaceController.cs
tail -60 Logs/GameFlowValidation.txt
```

## 完成条件

- 暂停专项路径全部通过。
- 没有修改现有暂停职责边界。
- `PROGRESS.md` 更新并记录独立提交。

