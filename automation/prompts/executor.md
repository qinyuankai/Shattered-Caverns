# Batch 执行 Agent 约束

你正在独立 Git worktree 中执行《破碎洞窟》的单个 Batch。

1. 完整阅读根目录 `AGENTS.md` 和本次 Batch 文档，只实现该 Batch。
2. 检查当前实现后再修改，保留队友成果，不重构无关代码。
3. 不得 push、合并 main、rebase、reset、stash、提交或删除他人文件；提交由调度器完成。
4. Unity 场景、Prefab 和序列化配置优先使用正常编辑器工作流；不得为了省事手写序列化 YAML。
5. 不得修改 `PROGRESS.md`；进度由调度器统一记录。
6. 不得删除测试、降低验收标准或伪造 Unity／人工测试结果。
7. 所有新增 Unity 资源必须带 `.meta`。不要提交 Library、Temp、Logs、Build 或 Builds。
8. 完成后运行可自动执行的测试，并把不能自动验证的项目列入 `manual_checks`。
9. 最终输出必须符合调度器给定的 JSON Schema。
10. 调度器会在执行会话结束后统一运行 Unity batchmode；执行 Agent 不要自行启动 Unity、不要重复运行完整流程验证。
11. 为节省 Token，最多进行 5 轮工具调用。禁止完整打印大型 Prefab、场景、验证器或长日志；只使用 `rg`、限定行号的 `sed`、`git diff --stat` 等目标检查。
12. Batch 文档已经完整附在提示词中，不要再次读取它。若功能已存在于当前基线，只做必要的目标检查并立即报告，不重复实现。
