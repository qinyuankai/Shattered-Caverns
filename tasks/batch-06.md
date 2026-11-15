# Batch 06：HUD 视觉重设计

## 任务目标

提高关卡标题、区块状态、碎片进度、操作提示和暂停入口的可读性，同时保持 HUD 只读取现有玩法数据。

## 涉及文件

- `Assets/Resources/GameApplication.prefab`
- `Assets/Scripts/Levels/Editor/GameInterfaceBuilder.cs`
- `Assets/Scripts/Levels/GameInterfaceController.cs`
- `Docs/TDD.md`
- `PROGRESS.md`

## 具体实现要求

1. 显示关卡标题、可用区块数量和活动区块状态。
2. 普通、Placing、Removing 三种操作提示明显区分。
3. 暂停按钮保持在安全区域，不压住其他文字。
4. HUD 不维护第二份 LevelManager 或区块状态。
5. 没有图标资源时使用清晰文字，不生成假图标。
6. 保留现有刷新频率，不引入每帧实例化或布局抖动。
7. 没有 LevelManager 的场景应安全显示空状态。

## 依赖关系

- 前置依赖：Batch 01。
- 后续依赖：Batch 07、Batch 13。

## 验收标准

- 普通状态操作提示正确。
- Placing 显示 F、Q、E、X。
- Removing 显示 F、X。
- 可用、已放置、碎片不足状态正确。
- 暂停按钮可点击且不遮挡。
- 4:3、16:9、16:10 下不遮住主要玩法区域。
- 无 LevelManager 时不报错。
- 36 项回归通过。

## 测试命令

Unity：

```text
进入 URP2DSceneTemplate
检查普通、放置、移除三种状态
收集碎片并观察区块状态
切换 4:3、16:9、16:10
Tools > Shattered Caverns > Validate Game Flow
```

终端：

```bash
git status --short --branch
git diff -- Assets/Resources/GameApplication.prefab Assets/Scripts/Levels/GameInterfaceController.cs
tail -60 Logs/GameFlowValidation.txt
```

## 完成条件

- HUD 状态、性能和分辨率专项验收通过。
- 没有复制玩法状态或修改玩法规则。
- `PROGRESS.md` 更新并记录独立提交。

