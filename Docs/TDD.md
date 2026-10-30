# 《破碎洞窟》技术设计文档

## 一、技术基线

- Unity：`2022.3.62f3c1`
- 渲染管线：Universal Render Pipeline 2D
- 目标平台：Windows
- 游戏类型：2D 平台解谜游戏

## 二、项目对象分类与渲染约定

### 1.区块结构

- 区块（Chunk）是地图组织与变换的基本单位，暂定尺寸为 `16×16` 个基础单位（以角色为1×1）。
- 每个区块作为独立的大结构存在，负责其内部对象的整体平移与旋转。
- 普通块、陷阱、机关、存档点和出口等作为逻辑子对象附属于区块，并跟随所属区块变换。
- 区块根对象主要用于组织层级和执行整体变换，并可挂载区块范围触发器，用于检测玩家进入或离开区块。

### 2.Layer

- `Player`：玩家角色及其碰撞体
- `Chunk`：区块范围检测碰撞体，用于检测玩家进入或离开区块
- `Solid`：普通实体块、墙壁及其他阻挡玩家移动的对象
- `Enemy`：敌人角色及其实体碰撞体
- `AutoTrigger`：玩家接触后自动触发的机关检测碰撞体
- `ManualTrigger`：玩家进入范围后，需要通过输入操作触发的机关检测碰撞体
- `Damage`：对玩家或其他对象造成伤害的碰撞体或检测区域

Layer 只用于物理碰撞矩阵、射线检测和其他查询筛选，不用于表示对象的具体类型或所属区块。存档点、出口、机关等对象的具体类型和行为由脚本组件或数据定义。

### 3.Tag

- `Player`：玩家
- `Solid`：普通实体块、地面及其他普通实体对象
- `Enemy`：敌人
- `Checkpoint`：存档点
- `Exit`：关卡出口
- `Chunk`：区块

其他对象的具体类型和行为由脚本组件或数据定义。

### 4.Sorting Layer

绘制顺序从后到前：

- `Background`
- `Environment`
- `Characters`
- `Effects`
- `Foreground`
- `UI`

### 5.使用约定

- 区块根对象用于组织子对象、执行整体变换，并通过 `Chunk` Layer 提供区块范围检测。
- Layer 用于粗粒度的物理和查询筛选，不用于表达区块归属。
- Tag 用于少量通用身份标识。
- 对象的具体类型、属性和行为由脚本组件或数据定义。
- 区块内部的子对象根据其物理或查询用途设置 Layer，根据身份需要设置 Tag。
- Sorting Layer 只用于控制绘制顺序。
- Layer、Tag 和 Sorting Layer 的修改需要同步更新本文档。

## 三、玩家移动系统

### 1.组件结构

- 玩家移动由 `PlayerMovement` 组件负责，组件挂载于玩家根对象。
- 玩家根对象使用 `Rigidbody2D` 参与 2D 物理模拟。

### 2.输入

- 水平输入使用旧版 Input Manager 的 `Horizontal` 输入轴，输入值乘以 `moveSpeed` 后作为刚体水平速度。
- 跳跃使用旧版 Input Manager 的 `Jump` 输入按钮，当前绑定为空格键；按下跳跃键后记录跳跃请求，在物理帧中确认玩家处于地面时，将刚体竖直速度设置为 `jumpVelocity`。
- `Update` 只负责读取输入并记录跳跃请求，`FixedUpdate` 负责修改刚体速度、执行跳跃和更新移动状态。

### 3.移动与跳跃

- 水平移动通过修改 `Rigidbody2D` 的水平速度实现。
- 跳跃通过设置 `Rigidbody2D` 的竖直初速度实现，竖直运动过程由物理系统和重力处理。

### 4.地面检测

- 地面检测使用玩家根对象局部坐标中的矩形检测区域，通过 `Physics2D.OverlapBox` 查询 `Solid` Layer。
- 地面检测区域由 `groundCheckOffset` 和 `groundCheckSize` 控制，不额外创建检测碰撞体。

### 5.状态机

- 玩家移动状态由 `PlayerMovementState` 状态机表示，当前包含 `Idle`、`Running`、`Rising` 和 `Falling` 四种状态。
- 当前状态通过 `CurrentState` 属性提供给后续动画系统读取。

### 6.暂未实现功能

- 当前暂不实现土狼时间、跳跃缓冲、加速度、减速度和特殊空中控制。

## 四、玩家拾取系统

### 1.职责划分

- `PlayerInventory` 挂载于玩家根对象，负责检测进入拾取触发器的物品。
- `ItemPickup` 是所有可拾取物的抽象基类，负责定义物品类型和拾取回调接口。
- 具体物品通过继承 `ItemPickup` 实现自己的拾取行为。

### 2.物品类型与拾取回调

- 当前物品类型包括 `Fragment`、`Key` 和 `Collectible`。
- `ItemPickup.OnPickedUp` 返回 `bool`，用于表示本次拾取是否成功。
- 拾取成功时，`PlayerInventory` 销毁场景中的物品对象；拾取失败时保留物品对象。

### 3.碎片记录与关卡配置

- `FragmentPickup` 使用 `chunkId` 和 `fragmentId` 标识所属区块与碎片编号。
- `PlayerInventory` 使用 `Dictionary<int, HashSet<int>>` 保存碎片记录。
- 外层键为 `chunkId`，内层集合保存该区块已收集的 `fragmentId`。
- 同一区块内的碎片编号从 `1` 开始，并且不允许重复记录。
- 每个关卡配置一个 `LevelFragmentManager`。
- `LevelFragmentManager` 只保存需要通过碎片拼出的区块及其所需碎片数量。
- 关卡开始时，`PlayerInventory` 自动查找当前场景中的 `LevelFragmentManager`，并只为配置表中的区块初始化收集记录。
- 未配置在表中的区块不属于当前关卡的碎片收集范围。

### 4.调试信息

- `PlayerInventory` 使用序列化调试列表显示当前已收集的区块和碎片。
- 调试列表只是运行时字典的显示镜像，不参与实际拾取判断。

### 5.拾取流程

```text
玩家进入物品触发器
→ PlayerInventory 查找 ItemPickup
→ 调用 ItemPickup.OnPickedUp
→ 返回 true 时销毁物品
→ 返回 false 时保留物品
```

## 五、待补充技术章节

### 1.输入系统

### 2.物理与单位规范

### 3.地图方块与地图拼接系统

### 4.知识锁与解谜系统

### 5.关卡和场景结构

### 6.数据结构与配置方式

### 7.存档系统

### 8.UI 与场景切换

### 9.资源加载和管理

### 10.测试与调试方式
