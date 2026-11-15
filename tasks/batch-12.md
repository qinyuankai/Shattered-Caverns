# Batch 12：可选 Windows 显示设置

## 任务目标

仅在产品确认后，为 Windows 版本增加必要的窗口、全屏、分辨率和可选垂直同步设置。

## 涉及文件

- `Assets/Resources/GameApplication.prefab`
- `Assets/Scripts/Levels/GameInterfaceController.cs`
- `Assets/Scripts/Levels/GameSaveStore.cs`
- `Assets/Scripts/Levels/GameConfiguration.cs` 或职责明确的新显示设置组件
- `Assets/Scripts/Levels/Editor/GameFlowValidation.cs`
- `Docs/TDD.md`
- `PROGRESS.md`

## 具体实现要求

1. 执行前必须确认本 Batch 是否纳入 Demo。
2. 只实现确认过的窗口／全屏、分辨率和垂直同步项目。
3. 不增加语言、键位、画质档位或其他高级图形设置。
4. 有效设置持久化；无效或已不存在的分辨率自动回退。
5. 不破坏当前音量存档；存档版本变化必须兼容旧数据。
6. UI 继续复用现有设置页面和视觉系统。

## 依赖关系

- 前置依赖：Batch 03、产品确认、Windows 目标分辨率清单。
- 后续依赖：Batch 13。

## 验收标准

- 每项设置能够应用并正确显示当前状态。
- 重新启动后恢复有效设置。
- 无效分辨率安全回退。
- 4:3、16:9、16:10 和约 21:9 均可进入可用画面。
- 音量存档和关卡进度不受影响。
- Windows 构建中复测通过。

## 测试命令

Unity：

```text
切换窗口／全屏和目标分辨率
重启 Play Mode 与 Windows 构建验证持久化
Tools > Shattered Caverns > Validate Game Flow
```

终端：

```bash
git status --short --branch
git diff -- Assets/Resources/GameApplication.prefab Assets/Scripts/Levels
tail -80 Logs/GameFlowValidation.txt
```

## 完成条件

- 用户已确认需求范围。
- Editor 与 Windows 构建专项测试通过。
- 旧存档兼容性得到证明。
- `PROGRESS.md` 更新并记录独立提交。

