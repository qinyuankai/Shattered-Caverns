# Batch 10：菜单与流程音频接入

## 任务目标

为主菜单、按钮、通关和失败流程接入具有合法来源的正式音频，并验证现有三类音量控制。

## 涉及文件

- `Assets/Audio/`（建议目录，执行前需确认并同步 `AGENTS.md`）
- 音频文件及对应 `.meta`
- `Assets/Settings/GameConfiguration.asset`
- `Assets/Resources/GameApplication.prefab`（仅在 AudioSource 配置需要调整时）
- `README.md`
- `Docs/TDD.md`
- `PROGRESS.md`

## 具体实现要求

1. 执行前确认音频文件、用途、循环方式和授权来源。
2. 复用现有 MusicPlayer 与 SoundPlayer，不新建第二个音频管理器。
3. 接入 Menu Music、Button Sound、Complete Sound 和 Failure Sound。
4. 相同音乐重复请求时不得从头播放。
5. 主音量分别乘音乐音量和音效音量。
6. 空引用和静音状态必须安全。
7. 不实现录音功能；如“记录音乐”确指录音，应另开需求。
8. 不在本 Batch 接入跳跃、拾取或区块操作音效。

## 依赖关系

- 前置依赖：Batch 01、Batch 03、Batch 05、正式音频与授权信息。
- 后续依赖：Batch 11、Batch 13。

## 验收标准

- 主菜单只播放菜单音乐。
- 切换场景时菜单音乐按现有流程停止或替换。
- 按钮、通关和失败音效分别只播放一次。
- 0%、50%、100% 三档音量人工试听正确。
- 音量跨场景和重新启动后保持。
- 空引用不会产生错误。
- 所有音频授权来源在文档中可追溯。

## 测试命令

Unity：

```text
在 Inspector 检查 GameConfiguration 音频引用
测试主菜单、按钮、通关、失败
分别测试三类音量的 0%、50%、100%
Tools > Shattered Caverns > Validate Game Flow
```

终端：

```bash
git status --short --branch
find Assets/Audio -type f | sort
git diff -- Assets/Settings/GameConfiguration.asset Docs/TDD.md README.md
tail -80 Logs/GameFlowValidation.txt
```

## 完成条件

- 正式音频、授权记录和专项试听全部通过。
- 没有引入重复音频管理器或假占位素材。
- `PROGRESS.md` 记录素材版本、测试结果和提交哈希。
- 流程音频形成独立提交。

