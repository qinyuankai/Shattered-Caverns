# Batch 14：main 同步与 Pull Request 整理

## 任务目标

在用户授权后，将 `feature/ui` 与最新 main 安全同步，完成全量验证、提交审计、推送和 Pull Request 准备。

## 涉及文件

- 冲突涉及的实际项目文件
- `README.md`
- `Docs/GDD.md`（仅在玩法约定确实变化时）
- `Docs/TDD.md`
- `AGENTS.md`（长期工程规则发生变化时）
- `PLAN.md`
- `PROGRESS.md`
- `tasks/`

## 具体实现要求

1. 执行前必须获得用户对同步、推送和 PR 操作的明确授权。
2. fetch 最新 main，比较双方提交和文件差异。
3. 冲突解决前先分析对象归属，禁止覆盖队友场景、角色、碎片或美术修改。
4. 只使用安全 merge 或经过确认的 rebase，不强制推送。
5. 审查全部提交粒度、`.meta`、大文件和生成目录。
6. 同步后重新运行完整 Unity 与 Windows 回归。
7. PR 描述必须列出已完成、待素材、已知限制和测试证据。
8. 不直接合并 main。

## 依赖关系

- 前置依赖：Batch 13、所有纳入 PR 的 Batch、用户授权。
- 后续依赖：团队代码审查与人工合并。

## 验收标准

- `feature/ui` 包含最新 main 且工作区干净。
- 无未解决冲突、缺失 `.meta`、Missing Script 或生成目录。
- 每个提交描述与实际范围一致，可单独解释和回滚。
- README、GDD、TDD、计划和进度与实现一致。
- Unity 自动验证与 Windows 构建回归通过。
- PR 描述包含测试环境、结果、资源与已知限制。
- 用户确认后才执行 push；不自动合并 main。

## 测试命令

```bash
git fetch origin --prune
git status --short --branch
git rev-list --left-right --count origin/main...HEAD
git log --oneline --decorate origin/main..HEAD
git diff --stat origin/main...HEAD
git ls-files | rg '(^|/)(Library|Temp|Logs|Build|Builds)/'
```

Unity：

```text
Tools > Shattered Caverns > Validate Game Flow
重新执行 Batch 13 的 Windows 构建与实机回归
```

## 完成条件

- 同步、提交审计、全量测试和文档审查全部通过。
- `PROGRESS.md` 中所有纳入 PR 的 Batch 均为 completed。
- 用户明确确认推送后完成 push，并准备可审查 PR。
- main 未被直接修改或自动合并。

