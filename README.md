# 破碎洞窟（Shattered Caverns）

## 项目概览

| 项目 | 内容 |
| --- | --- |
| 项目名称 | 破碎洞窟（Shattered Caverns） |
| 游戏类型 | 2D 平台解谜游戏 |
| 目标平台 | Windows |

## 指导教师与团队成员

| 角色 | 姓名 |
| --- | --- |
| 指导老师 | 刘琳 |
| 助教 | 刘怿轩 |
| 组长 | 秦渊凯 |
| 组员 | 唐语晗、王尉丞、王罡 |

## 项目简介

《破碎洞窟》是一款 2D 平台解谜游戏。玩家需要探索洞穴，理解环境规则，并通过改变地图方块的连接方式来突破障碍。

## 开发环境

- Unity `2022.3.62f3c1`
- C#
- 目标平台：Windows


## 运行与 UI

使用 Unity `2022.3.62f3c1` 打开项目，打开 `Assets/Scenes/MainMenu.unity` 后点击 Play。
主菜单与暂停／结束界面位于 `Assets/Resources/GameApplication.prefab`；双击该 Prefab 可以编辑 Canvas 布局与按钮。
关卡与音频在 `Assets/Settings/GameConfiguration.asset` 中配置。当前仅接入一个测试关卡，音乐与音效槽位尚未配置正式素材。

游戏中按 Esc 打开暂停菜单。继续游戏从最近关卡起点进入；重新开始重置当前场景，保留解锁进度与音量。
队友可通过 `GameFlowController.Instance` 或挂载 `LevelFlowEvents` 接入通关、失败与场景重开，详见 `Docs/TDD.md` 第七节。

已提供 Editor 流程验证工具：`Tools > Shattered Caverns > Validate Game Flow`。
