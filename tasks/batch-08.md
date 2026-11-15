# Batch 08：正式关卡配置与存档校准

## 任务目标

将正式关卡安全接入开始、继续、选关、下一关和解锁流程，并使用稳定关卡 ID 校准现有存档系统。

## 涉及文件

- `Assets/Settings/GameConfiguration.asset`
- `ProjectSettings/EditorBuildSettings.asset`
- `Assets/Scripts/Levels/GameSaveStore.cs`（仅在正式数据暴露现有缺口时）
- `Assets/Scripts/Levels/Editor/GameFlowValidation.cs`
- `README.md`
- `Docs/TDD.md`
- `PROGRESS.md`

## 具体实现要求

1. 执行前 fetch 并比较最新 main，确认队友是否已增加或修改场景。
2. 每关必须有稳定且唯一的 ID、显示名称、场景路径与顺序。
3. 所有关卡场景必须真实存在并加入 Build Settings。
4. 第一关默认解锁，通关后按配置顺序解锁下一关。
5. Continue 只加载仍然有效且已解锁的最近关卡。
6. 旧存档中的未知、重复或失效 ID 继续由现有规范化逻辑清理。
7. 不把同一个测试场景伪装成多个正式关卡。
8. 不加入玩家位置、碎片、区块布局或检查点存档。

## 依赖关系

- 前置依赖：Batch 02、正式场景、稳定 ID、显示名称和关卡顺序。
- 后续依赖：Batch 09、Batch 11、Batch 13。

## 验收标准

- 每个配置场景存在且在 Build Settings 启用。
- ID 非空且唯一，Scene Path 非空且唯一，显示名称非空。
- Start 进入第一关，Continue 进入最近有效关卡。
- 锁定关卡不可点击，完成第 N 关只解锁第 N+1 关。
- 中间关显示下一关，末关隐藏下一关。
- 删除或改名后的旧 ID 不导致异常。
- 至少覆盖首关、中间关和末关三条路径。

## 测试命令

Unity：

```text
检查 File > Build Settings 中的场景顺序
逐关执行 Start、Continue、Level Select、Complete、Next Level
Tools > Shattered Caverns > Validate Game Flow
```

终端：

```bash
git fetch origin --prune
git rev-list --left-right --count origin/main...HEAD
git status --short --branch
rg -n 'id:|displayName:|scenePath:' Assets/Settings/GameConfiguration.asset
tail -80 Logs/GameFlowValidation.txt
```

## 完成条件

- 正式关卡资料齐全且全部验收通过。
- 存档范围未被擅自扩大。
- `PROGRESS.md` 记录关卡清单、测试和提交哈希。
- 配置、Build Settings 与验证改动形成独立提交。

