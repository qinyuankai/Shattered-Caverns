# Batch 11：关卡音乐与玩法音效接入

## 任务目标

为正式关卡和核心玩法动作接入区分明确的音乐与音效反馈，并保持队友玩法逻辑不变。

## 涉及文件

- `Assets/Audio/`（目录需在 Batch 10 中确认）
- `Assets/Settings/GameConfiguration.asset`
- `Assets/Scripts/Character/PlayerMovement.cs`（最小触发点）
- `Assets/Scripts/Items/FragmentPickup.cs`（最小触发点）
- `Assets/Scripts/Character/PlayerChunkInteraction.cs`（最小触发点）
- 必要的机关脚本
- `Docs/TDD.md`
- `PROGRESS.md`

## 具体实现要求

1. 为每关配置背景音乐，不把场景路径或 AudioClip 写死在代码中。
2. 接入跳跃、碎片拾取、区块旋转、区块放置、区块拿下和机关触发音效。
3. 每次有效动作只播放一次；无效动作不播放成功音效。
4. 统一调用 `GameAudioPlayer.PlaySound`，不为每个玩法对象创建独立 AudioSource。
5. 暂停和 Loading 状态不得产生错误触发。
6. 空 AudioClip 引用必须安全。
7. 对队友脚本只做必要调用点修改，不重构动作逻辑。

## 依赖关系

- 前置依赖：Batch 08、Batch 10、正式音乐与玩法音效、稳定玩法接口。
- 后续依赖：Batch 13。

## 验收标准

- 每关进入时播放对应音乐。
- 相同音乐不会重复重启。
- 每个有效动作音效恰好一次，无效操作无成功音效。
- 音效受主音量和音效音量共同控制。
- 暂停、重试、切换场景后音频状态正确。
- 空引用无异常。
- 角色、碎片和区块原有玩法测试无回归。

## 测试命令

Unity：

```text
逐关测试背景音乐切换
逐项测试跳跃、拾取、旋转、放置、拿下和机关
测试无效操作、暂停、重试和场景切换
Tools > Shattered Caverns > Validate Game Flow
```

终端：

```bash
git status --short --branch
git diff -- Assets/Scripts/Character Assets/Scripts/Items Assets/Settings/GameConfiguration.asset
tail -80 Logs/GameFlowValidation.txt
```

## 完成条件

- 所有音乐与玩法音效专项验收通过。
- 队友玩法逻辑没有被重构。
- 音频授权与测试记录写入 `PROGRESS.md`。
- 玩法音频形成独立提交。

