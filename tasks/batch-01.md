# Batch 01：新版主菜单收口

## 任务目标

将当前工作区中的新版主菜单整理为稳定、可重复生成、可独立验收和回滚的正式基线，并创建单独的本地提交。

## 涉及文件

- `Assets/Art/Backgrounds/MainMenuBackground.png`
- `Assets/Art/Backgrounds/MainMenuBackground.png.meta`
- `Assets/Resources/GameApplication.prefab`
- `Assets/Scripts/Levels/Editor/GameInterfaceBuilder.cs`
- `Docs/TDD.md`
- `PROGRESS.md`

## 具体实现要求

1. 保留已经确认的暗色像素洞窟背景、暖金主按钮与青绿色次级强调色。
2. 标题、副标题、开始、继续、选关、设置和退出按钮使用左侧稳定布局。
3. 背景使用 `AspectRatioFitter.EnvelopeParent` 保持比例，仅允许裁切，不允许拉伸角色。
4. 背景与左侧遮罩必须关闭 Raycast Target。
5. Hover、Selected、Pressed 和 Disabled 状态必须清晰可辨认。
6. `Apply Main Menu Redesign` 可重复执行，不得重复创建 Background、MenuShade 或按钮 Accent。
7. 不修改选关、设置、暂停、结果、HUD 的视觉。
8. 不添加关卡、音频或新流程代码。

## 依赖关系

- 前置依赖：无。
- 后续依赖：Batch 02～07。

## 验收标准

- MainMenu Play 后背景、角色、标题和五个按钮完整显示。
- 开始游戏为默认焦点。
- 开始、继续、选关、设置、退出保持原有行为。
- 1280×720、1920×1080、1920×1200、1200×900、2560×1080 均无越界和拉伸。
- 重复应用工具后 Background 为 1 个、MenuShade 为 1 个、Accent 为 5 个。
- 现有 36 项流程验证通过。
- Prefab 无 Missing Script，新资源具有 `.meta`。
- Git 差异只包含本批次文件。

## 测试命令

Unity：

```text
打开 Assets/Scenes/MainMenu.unity
Tools > Shattered Caverns > Apply Main Menu Redesign
进入 Play Mode，依次切换五种目标分辨率
Tools > Shattered Caverns > Validate Game Flow
```

终端：

```bash
git status --short --branch
git diff --name-status
rg -c '^  m_Name: Background$' Assets/Resources/GameApplication.prefab
rg -c '^  m_Name: MenuShade$' Assets/Resources/GameApplication.prefab
rg -c '^  m_Name: Accent$' Assets/Resources/GameApplication.prefab
tail -60 Logs/GameFlowValidation.txt
```

## 完成条件

- 所有验收标准通过。
- `PROGRESS.md` 更新测试结果。
- 创建独立本地提交，建议信息：`feat: 重设计主菜单视觉`。
- 未经用户确认不得推送远程。

