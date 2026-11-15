# Batch 07：全局动效、焦点与分辨率收口

## 任务目标

在全部界面视觉稳定后，统一面板动效、按钮状态、输入锁定、焦点路径和分辨率适配，形成完整 UI 阶段基线。

## 涉及文件

- `Assets/Resources/GameApplication.prefab`
- `Assets/Scripts/Levels/Editor/GameInterfaceBuilder.cs`
- `Assets/Scripts/Levels/GameInterfaceController.cs`
- `Assets/Scripts/Levels/Editor/GameFlowValidation.cs`
- 必要时新增一个职责单一的 UI 展示组件及 `.meta`
- `Docs/TDD.md`
- `PROGRESS.md`

## 具体实现要求

1. 统一 Hover、Selected、Pressed、Disabled 状态。
2. 面板进入和退出只使用轻量淡入或位移，不影响操作效率。
3. 所有 UI 动效使用 unscaled time，暂停状态仍可工作。
4. Loading 和转场期间屏蔽重复输入与底层点击。
5. 每个面板首次打开、返回和动态按钮隐藏后焦点正确。
6. 完成 16:9、16:10、4:3、约 21:9 的全界面适配。
7. 只增加必要的自动检查，不重做输入系统或场景加载系统。

## 依赖关系

- 前置依赖：Batch 01～06。
- 后续依赖：Batch 08～14。

## 验收标准

- 暂停状态下动效正常。
- 快速连点开始、重试、返回不会重复加载。
- 转场期间按钮不可再次触发。
- 所有面板默认焦点和返回路径正确。
- 背景不变形、文字不截断、控件不越界。
- 鼠标、键盘和手柄选择状态均可辨认。
- 自动验证全部通过且无新增运行错误。

## 测试命令

Unity：

```text
逐一打开主菜单、选关、设置、暂停、通关、失败和 HUD
在 1280×720、1920×1080、1920×1200、1200×900、2560×1080 下复测
快速连续点击开始、重试、返回和下一关
Tools > Shattered Caverns > Validate Game Flow
```

终端：

```bash
git status --short --branch
git diff --stat
tail -80 Logs/GameFlowValidation.txt
```

## 完成条件

- 全分辨率矩阵和完整流程回归通过。
- 所有跨页面 UI 改动形成独立提交。
- `PROGRESS.md` 更新，UI 视觉阶段可以独立交付。

