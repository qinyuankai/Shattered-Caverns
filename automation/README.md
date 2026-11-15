# 多 Batch 自动调度器

本目录为《破碎洞窟》已有开发计划提供串行、可恢复的自动调度。它不会重新规划项目，不会自动 push、合并 main、清理工作区或降低验收标准。

## 工作方式

1. 从 `PROGRESS.md` 读取状态，从 `config.yaml` 读取依赖。
2. 在仓库外创建本次运行的集成 worktree；每个 Batch 再使用独立分支和 worktree。
3. 每次实现和验收都通过全新的 `codex exec --ephemeral` 会话完成。
4. Python 调度器负责依赖、次数、超时、Token 预算、停止和恢复。
5. 执行后运行 Unity batchmode 编译检查，再由只读验收会话审查差异与证据。
6. 失败最多修复两次。仍失败则停止并标记运行 blocked。
7. UI、场景、音频、Windows 实机等无法可靠自动观察的内容进入人工验收门，不会冒充通过。

## 运行前条件

- 当前分支必须是 `feature/ui`。
- 源工作区必须干净。
- `PLAN.md`、`PROGRESS.md`、`tasks/` 和 `automation/` 必须已经提交，独立 worktree 才能读取。
- Unity 2022.3.62f3c1 与 Codex CLI 必须可用。
- 不要同时在 Unity 中打开调度器创建的同一个 worktree。

若 Python 提示缺少 `yaml`，只需安装这一项小型依赖：

```bash
python3 -m pip install -r automation/requirements.txt
```

当前工作区有未提交的主菜单成果时，真实启动会主动拒绝；调度器不会替你 stash、reset 或提交它们。

## macOS 命令

在项目根目录运行：

```bash
python3 automation/run.py validate
python3 automation/run.py simulate
python3 automation/run.py probe
python3 automation/run.py preflight
python3 automation/run.py --max-batches 1
```

状态、停止与恢复：

```bash
python3 automation/run.py status
python3 automation/run.py stop
python3 automation/run.py resume --max-batches 1
```

人工验收后放行当前 Batch：

```bash
python3 automation/run.py approve 01 --note "在五种分辨率下完成 Play Mode 验收，按钮和焦点正常"
python3 automation/run.py resume --max-batches 1
```

测试与报告：

```bash
python3 automation/run.py self-test
python3 automation/run.py report
```

实时状态保存在 `automation/runtime/active-run.json`，日志保存在 `automation/logs/<run-id>/batch-XX/`。这两处运行数据默认不纳入 Git。

可在另一个终端持续查看：

```bash
watch -n 5 python3 automation/run.py status
tail -f automation/logs/<run-id>/batch-XX/executor-attempt-1.jsonl
```

macOS 没有 `watch` 时，直接重复运行 status，或使用：

```bash
while true; do clear; python3 automation/run.py status; sleep 5; done
```

## 模型与成本控制

- 默认实现：`gpt-5.6-sol`，low。
- 复杂 Batch：`gpt-5.6-sol`，medium。
- 第一次和第二次修复：分别升级到 medium、high。
- 默认验收：`gpt-5.6-sol`，low；复杂任务或重试后使用 medium。
- Codex CLI 提供 Token 用量时会写入运行状态和最终报告。
- CLI 当前不返回可靠价格，因此 `max_cost_usd` 保留为 `null`，不能伪造成本上限；使用总 Token、单 Batch Token、超时、Batch 数和重试数进行硬限制。

可在 `config.yaml` 修改模型、推理强度、最大 Batch 数、两类超时和 Token 上限。模型名称必须先通过 `python3 automation/run.py validate` 验证。

当前保守默认值为：单次最多 3 个 Batch、总计 300,000 Token、单 Batch 120,000 Token，并至少预留 25,000 Token 才允许启动下一会话。由于人工门通常会更早暂停，实际不会为了“凑满数量”继续消耗。

## 睡眠、断网与应用状态

- Mac 合盖或进入系统睡眠后，进程通常暂停，不能保证无人值守继续。可在接电情况下使用 `caffeinate -dimsu python3 automation/run.py --max-batches 1` 阻止自动睡眠；不要依赖合盖继续运行。
- 网络断开时 Codex 会话会失败，并按上限重试；超过上限后状态变为 blocked 并停止，不能自动继续消耗额度。若只是主动 stop 或进程中断，恢复网络后使用 `resume`，已完成 Batch 不会重复执行；blocked 状态需要先人工检查日志和失败 worktree，再决定如何重新执行。
- 调度器直接调用本机 Codex CLI，不要求这个聊天窗口一直打开；但需要有效的 Codex 登录状态、网络、电脑保持唤醒，以及 Unity/Codex CLI 可执行文件仍可用。
- 安全停止会先写 STOP 标记。正在运行的会话收到终止信号，宽限期后才强制结束。

## 自动与人工验收边界

项目已安装 Unity Test Framework，但当前没有测试程序集；现有 36 项验证来自编辑器内的 `Tools > Shattered Caverns > Validate Game Flow`。调度器会自动执行 Unity batchmode 导入与编译门禁，但不会把“能编译”写成“36 项流程验证通过”。以下内容必须保留人工验收证据：

- 主菜单、选关、设置、暂停、结果界面和 HUD 的视觉、焦点、射线与多分辨率表现；
- 正式场景中的出口、死亡、Prefab 和 Inspector 引用；
- 音乐、按钮音效、玩法音效及授权来源；
- Windows 构建的启动、显示模式、存档和实机流程；
- Batch 14 的 main 同步、push 和 PR 授权。

## Git 安全边界

- 默认串行，不并行修改 Unity 序列化文件。
- 所有分支均使用 `codex/` 前缀，worktree 放在项目旁的 `Shattered-Caverns-automation-worktrees/`。
- 只在私有 Batch 分支提交，并在通过门禁后快进本次集成分支。
- 不自动 push、不自动同步或合并 main、不强推、不 reset、不 stash、不清理 worktree。
- Batch 14 仍需要用户明确授权，调度器配置本身不会赋予推送或合并权限。
