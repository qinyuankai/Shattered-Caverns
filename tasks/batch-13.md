# Batch 13：Windows 构建与全量回归

## 任务目标

在不增加新功能的前提下，对纳入 Demo 的全部内容进行 Windows 构建、实机流程测试和发布前检查。

## 涉及文件

- `ProjectSettings/` 中确有必要的构建配置
- `README.md`
- `Docs/TDD.md`
- `PROGRESS.md`
- 构建产物仅作为测试输出，不提交仓库

## 具体实现要求

1. 安装与 Unity 2022.3.62f3c1 匹配的 Windows Build Support。
2. MainMenu 必须是 Build Settings 第一个场景。
3. 本 Batch 不新增功能；发现问题时建立独立修复提交。
4. 构建输出放在明确的仓库外或被忽略目录。
5. 检查 Console、Player.log、Missing Script、资源引用与平台差异。
6. 不提交 Library、Temp、Logs 或构建产物。

## 依赖关系

- 前置依赖：所有计划纳入 Demo 的功能 Batch、Windows Build Support、Windows 运行环境。
- 后续依赖：Batch 14。

## 验收标准

- Windows 构建成功并能够启动。
- 主菜单、开始、继续、选关、设置完整可用。
- 暂停、恢复、重开、通关、失败、下一关和返回主菜单完整可用。
- 存档跨程序重启保持。
- 16:9 与 16:10 至少各完成一次实机验证。
- 音频在 Windows 上正常。
- Console 与 Player.log 无错误。
- 仓库不包含生成目录或构建产物。

## 测试命令

Unity：

```text
Tools > Shattered Caverns > Validate Game Flow
File > Build Settings > Windows > Build
在 Windows 环境运行构建并执行完整用户流程
```

终端：

```bash
git status --short --branch
git diff --stat
git ls-files | rg '(^|/)(Library|Temp|Logs|Build|Builds)/'
```

## 完成条件

- Editor 自动回归和 Windows 实机回归全部通过。
- 所有平台问题拥有独立修复记录。
- `PROGRESS.md` 记录构建环境、版本、结果和相关提交。
- 发布前门禁满足，可以进入 Batch 14。

