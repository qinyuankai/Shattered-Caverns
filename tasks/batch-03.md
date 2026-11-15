# Batch 03：设置界面视觉重设计

## 任务目标

完成总音量、音乐音量和音效音量的正式界面视觉，同时保持现有数据与音频架构不变。

## 涉及文件

- `Assets/Resources/GameApplication.prefab`
- `Assets/Scripts/Levels/Editor/GameInterfaceBuilder.cs`
- `Assets/Scripts/Levels/GameInterfaceController.cs`（仅在表现逻辑确有需要时）
- `Docs/TDD.md`
- `PROGRESS.md`

## 具体实现要求

1. 三个 Slider 的标签、轨道、填充、手柄和百分比层级清楚。
2. 打开设置后默认聚焦总音量。
3. 保留 `SetValueWithoutNotify` 初始化方式，避免打开页面时触发额外写入或声音。
4. 从主菜单进入时返回主菜单；从暂停进入时返回暂停界面。
5. 暂停状态下调整音量不得恢复游戏时间。
6. 不增加分辨率、全屏、语言、键位或画质设置。
7. 不重构 AudioMixer，不接入正式音频素材。

## 依赖关系

- 前置依赖：Batch 01。
- 后续依赖：Batch 04、Batch 07、Batch 10、Batch 12。

## 验收标准

- 三个 Slider 在目标分辨率下完整可见且互不覆盖。
- 0%、50%、100% 显示正确。
- 鼠标、键盘和手柄均能调整。
- 主菜单与暂停两种来源的返回路径正确。
- 音量跨场景和重新进入设置后保持。
- 暂停状态始终保持 `Time.timeScale == 0`。
- 36 项回归通过。

## 测试命令

Unity：

```text
MainMenu Play Mode > 设置
分别将三个 Slider 调到 0%、50%、100%
进入关卡 > 暂停 > 设置 > 返回
Tools > Shattered Caverns > Validate Game Flow
```

终端：

```bash
git status --short --branch
git diff -- Assets/Resources/GameApplication.prefab Assets/Scripts/Levels/GameInterfaceController.cs
tail -60 Logs/GameFlowValidation.txt
```

## 完成条件

- 设置专项验收与回归通过。
- 没有扩大设置或存档范围。
- `PROGRESS.md` 更新并记录独立提交。

