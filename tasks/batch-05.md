# Batch 05：通关与失败界面重设计

## 任务目标

在共用现有 `ResultPanel` 和流程状态机的前提下，为通关与失败提供清晰区分的正式视觉。

## 涉及文件

- `Assets/Resources/GameApplication.prefab`
- `Assets/Scripts/Levels/Editor/GameInterfaceBuilder.cs`
- `Assets/Scripts/Levels/GameInterfaceController.cs`
- `Docs/TDD.md`
- `PROGRESS.md`

## 具体实现要求

1. 通关采用暖金与青绿色的积极反馈。
2. 失败采用克制的暖红、灰色或低饱和反馈。
3. 共用现有 ResultPanel，不复制第二套流程控制器。
4. 动态设置标题、说明、下一关、重试和返回主菜单。
5. 没有下一关时隐藏下一关按钮并把焦点放到重试。
6. 不增加评分、星级、计时、死亡动画或正式触发器。

## 依赖关系

- 前置依赖：Batch 01。
- 后续依赖：Batch 07、Batch 09、Batch 10。

## 验收标准

- 通关和失败均冻结游戏并显示正确视觉。
- 两种状态不会互相覆盖。
- 重试恢复场景初始状态。
- 有下一关时按钮显示；最后一关时隐藏。
- 失败和最后一关通关的默认焦点均正确。
- 结果音效为空时不报错。
- 目标分辨率下文字、按钮不越界。
- 36 项回归通过。

## 测试命令

Unity：

```text
Play Mode 调用 GameFlowController.Instance.CompleteLevel()
Play Mode 调用 GameFlowController.Instance.FailLevel()
分别测试重试、下一关、返回主菜单
Tools > Shattered Caverns > Validate Game Flow
```

终端：

```bash
git status --short --branch
git diff -- Assets/Resources/GameApplication.prefab Assets/Scripts/Levels/GameInterfaceController.cs
tail -60 Logs/GameFlowValidation.txt
```

## 完成条件

- 通关与失败专项路径全部通过。
- 未复制流程系统或扩大结果数据范围。
- `PROGRESS.md` 更新并记录独立提交。

