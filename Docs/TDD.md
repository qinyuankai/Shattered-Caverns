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

- `Player`：玩家角色及其实际实体碰撞体
- `PlayerInteraction`：玩家外围交互检测触发器，不参与区块边界判定
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
- 玩家实际实体碰撞体使用 `Player` Layer，外围交互检测触发器使用 `PlayerInteraction` Layer。
- `Player` 与 `Chunk` 允许发生物理交互，`PlayerInteraction` 与 `Chunk` 不发生物理交互。
- Layer 用于粗粒度的物理和查询筛选，不用于表达区块归属。
- Tag 用于少量通用身份标识。
- 对象的具体类型、属性和行为由脚本组件或数据定义。
- 区块内部的子对象根据其物理或查询用途设置 Layer，根据身份需要设置 Tag。
- Sorting Layer 只用于控制绘制顺序。
- Layer、Tag 和 Sorting Layer 的修改需要同步更新本文档。

## 三、玩家操作系统

### 1.组件结构

- 玩家移动由 `PlayerMovement` 组件负责，组件挂载于玩家根对象。
- 玩家根对象使用 `Rigidbody2D` 参与 2D 物理模拟。
- 玩家实体碰撞箱统一继承 `Player` 预制体的 `BoxCollider2D`，尺寸为 `0.8×0.8`，`Edge Radius` 为 `0.05`。
- 玩家 `Rigidbody2D` 启用插值（`Interpolate`），用于减小物理更新频率与画面渲染频率不同步造成的视觉抖动。

### 2.输入

- 水平输入使用旧版 Input Manager 的 `Horizontal` 输入轴，输入值乘以 `moveSpeed` 后作为刚体水平速度。
- 跳跃使用旧版 Input Manager 的 `Jump` 输入按钮，当前绑定为空格键；按下跳跃键后记录跳跃请求，在物理帧中确认玩家处于地面时，将刚体竖直速度设置为 `jumpVelocity`。
- 区块交互使用直接键盘输入。`F` 进入和确认操作，`X` 取消，`Q` 逆时针旋转，`E` 顺时针旋转，`R` 在放置模式切换可用区块。
- 普通移动在 `Update` 读取输入并记录跳跃请求，在 `FixedUpdate` 修改刚体速度、执行跳跃和更新移动状态。

### 3.移动与跳跃

- 正常移动时，地面和空中的水平输入均用于设置 `Rigidbody2D` 的水平速度。空中解冻后暂停移动和跳跃输入，直到检测到落地。
- 跳跃通过设置 `Rigidbody2D` 的竖直初速度实现，竖直运动过程由物理系统和重力处理。
- 玩家默认移动速度为 `4.2`，跳跃初速度为 `18`，玩家刚体的重力倍率为 `4.5`。

### 4.地面检测

- `PlayerMovement` 每个物理帧读取一次玩家刚体在 `Solid` 图层上的实际支撑接触。移动状态、解冻后的落地判定和 `PlayerFreezeController` 共用这次采样。冻结组件在移动组件之后执行，实体碰撞箱引用用于定位头顶光照检测点。
- 跳跃继续使用局部矩形，通过 `Physics2D.OverlapBox` 查询 `Solid` 图层。检测范围由 `groundCheckOffset` 和 `groundCheckSize` 控制。

### 5.状态机

- 玩家移动状态由 `PlayerMovementState` 状态机表示，当前包含 `Idle`、`Running`、`Rising`、`Falling` 和 `Climbing`。
- 当前状态通过 `CurrentState` 属性提供给后续动画系统读取。
- 玩家朝向由 `PlayerFacingDirection` 表示，当前包括 `Left`、`Right`、`Up` 和 `Down`。
- 当存在有效水平输入时，玩家朝向根据实际生效的水平输入更新；左右控制颠倒时，朝向也使用颠倒后的实际输入判断。
- 当玩家竖直速度绝对值超过 `verticalFacingSpeedThreshold` 时，朝向切换为 `Up` 或 `Down`。竖直速度处于容差内时，沿用最近一次有效的左右朝向。脚本和玩家预制体的默认阈值均为 `0.01`。
- 左右控制颠倒状态由 `isHorizontalInputInverted` 保存，并通过 `SetHorizontalInputInverted` 提供运行时修改接口。
- `ConfusionMushroom` 使用 `AutoTrigger` 图层上的 1×1 `BoxCollider2D` Trigger，忽略玩家外围 Trigger。通过玩家实体碰撞体所属的刚体取得 `PlayerMovement`，调用 `SetHorizontalInputInverted(true)` 后停用蘑菇。预制体作为所属区块的子对象摆放。
- 冻结由可序列化的 `isFrozen` 保存，可在 Inspector 中切换或由 `SetFrozen(bool)` 修改。`IsFrozen` 提供只读查询，与区块操作使用的输入开关独立。
- 进入冻结时清除缓存输入和刚体速度，将移动状态设为 `Idle`，并用 `FreezeAll` 锁定刚体的位置和旋转。冻结期间跳过主动移动和跳跃。解冻时恢复原有刚体约束，速度从零开始。
- 解冻后先经过一个物理步，再使用实际支撑接触恢复控制，避免读取区块旋转前的接触结果。掉空复位会清除等待落地的状态。

### 6.暂未实现功能

- 普通移动暂不实现土狼时间、跳跃缓冲、加速度和减速度。

### 7.冬季冻结判定

- `PlayerFreezeController` 挂在 `Player` 预制体根对象上，绑定 `PlayerMovement`、`PlayerChunkTracker` 和玩家实体碰撞箱。冻结判定由所在区块的 `ChunkWeather.CanFreezePlayer` 开启，离开允许冻僵的区域时清零计时并解除天气造成的冻结。
- `PlayerMovement.HasReceivedMovementInput` 记录进入关卡或掉空复位后的首次移动或跳跃输入。掉空时清除该标记并通过 `Respawned` 事件清零冻结计时。复位前持续按住的同方向输入等到松开、换方向或按下跳跃后恢复。
- 在 `FixedUpdate` 读取区块是否具备日照条件。有日照时使用天气机关共用的 `WeatherMechanismPhysics.HasClearSky`，从实体碰撞箱顶部中心沿世界方向向上检测 `Solid`。无日照或被遮挡时累计冻结时间，见光时清零并解除冻结。预制体默认阈值为 `0.95` 秒。
- 达到阈值后，只在玩家实体碰撞箱与下方 `Solid` 有支撑接触时调用 `SetFrozen(true)`。冻结使用实际碰撞接触，跳跃仍使用原地面检测框。
- 旋转预览暂停物理帧。退出预览后，在下一个物理帧根据保留的区块位置重新判断遮挡。

### 8.蒲公英滑翔与风口

- `PlayerMovement` 使用玩家实体碰撞箱检测 `AutoTrigger` 图层上的 `Updraft`，只有携带蒲公英时参与检测。重叠多个风口时取最高的上升速度，风口范围随区块旋转和镜像，上升方向始终为世界上方。
- 携带蒲公英时跳跃上升仍使用普通重力，空中水平速度默认每秒 2 格。进入风区自动以每秒 2 格上升。离开风区或开始下落后，`Vertical` 为正（默认 `↑` 或 `W`）才启用滑翔，默认每秒下降 1 格。风口抬升和滑翔期间暂时关闭重力，结束抬升或滑翔后恢复原重力倍率。
- `Updraft` 预制体原点在风区底边中心，默认 `BoxCollider2D` Trigger 尺寸为 7×13，可在 Inspector 调整范围和上升速度。

## 四、玩家拾取系统

### 1.职责划分

- `PlayerInventory` 挂载于玩家根对象，负责检测进入拾取触发器的物品。
- `ItemPickup` 是所有可拾取物的抽象基类，定义拾取请求和成功回调。
- 具体物品通过继承 `ItemPickup` 实现自己的拾取行为。

### 2.拾取回调

- 目前实现了 `FragmentPickup` 和 `DandelionPickup`。
- 蒲公英携带状态由 `PlayerInventory` 持有的道具实例决定，`PlayerMovement.IsCarryingDandelion` 读取背包状态。
- `ItemPickup.OnPickedUp` 返回 `bool`，用于表示本次拾取是否成功。
- 拾取成功时，`PlayerInventory` 调用物品的 `OnPickupSucceeded`。默认销毁物品，蒲公英覆盖该方法并暂时停用本体。
- 掉空复位时，`PlayerMovement` 恢复重力并通知背包归还蒲公英。背包清除携带记录，并将同一物品移到关卡出生点后重新启用。

### 3.碎片记录与关卡管理关联

- `FragmentPickup` 在 Inspector 直接引用目标区块的 `ChunkController`，并用 `fragmentId` 标记该区块内的碎片编号。
- `LevelManager` 按 `ChunkController` 记录碎片，每个区块对应一个已收集的 `fragmentId` 集合。
- 同一区块内的碎片编号从 `1` 开始，并且不允许重复记录。
- 关卡开始时，`PlayerInventory` 自动查找当前场景中的 `LevelManager`，并将碎片拾取请求转交给它。
- `PlayerInventory` 不直接保存碎片记录，只将拾取请求转交给 `LevelManager`。

### 4.调试信息

- `LevelManager` 使用序列化调试列表显示当前已收集的区块和碎片。
- 调试列表只是运行时字典的显示镜像，不参与实际拾取判断。

### 5.拾取流程

```text
玩家进入物品触发器
→ PlayerInventory 查找 ItemPickup
→ 调用 ItemPickup.OnPickedUp
→ 返回 true 时调用 ItemPickup.OnPickupSucceeded
→ 返回 false 时保留物品
```

## 五、区块系统

### 1.区块对象结构

- 区块根对象使用 `Chunk` Layer 和 `Chunk` Tag，作为区块整体平移与旋转的对象。
- 区块根对象挂载 `ChunkController`，负责提供区块边界和区块中心点信息。
- 区块根对象挂载 `ChunkWeather`，通过 Inspector 配置并对外只读提供 `HasSunlight`、`HasRain`、`CanFreezePlayer`。默认有日照、无降雨且不冻僵。外部系统通过 `ChunkController.Weather` 获取配置并执行各自的机制。
- `ChunkSunlightVisual` 与 `ChunkWeather` 同挂在区块根对象，绑定 `ChunkController` 和 `SolidTilemap`，负责冻僵区块的瓦片亮暗反馈。
- `Grid` 是区块根对象的子对象，负责承载 `SolidTilemap`。机关、碎片和出口等地图对象直接挂在所属区块根对象下。
- `Boundary` 作为区块根对象的子对象，挂载与区块范围一致的 `BoxCollider2D`。
- `Boundary` 使用 `Chunk` Layer，并将 `Is Trigger` 开启，用于向玩家报告区块接触情况。
- `ChunkController` 通过 Inspector 显式引用 `Boundary` 的 `BoxCollider2D`，不自动搜索子对象中的碰撞体。
- `ChunkController` 通过 Inspector 显式引用 `Background` 的 `SpriteRenderer`，不自动从子对象中搜索背景渲染器。
- 区块中心、相邻区块距离和边界距离均根据 `Boundary` 的实际尺寸计算，不直接依赖固定的 `16` 作为运行时判定尺寸。

当前区块的基本结构如下：

```text
Chunk
├── ChunkController
├── ChunkWeather
├── ChunkSunlightVisual
├── Grid
│   └── SolidTilemap
├── Boundary
│   └── BoxCollider2D（Trigger，16×16）
├── Background
└── 机关、碎片、出口等场景对象
```

### 2.Tilemap与区块碰撞

- `Grid` 为区块提供局部网格坐标。
- `SolidTilemap` 用于绘制区块中的实体地面、墙壁及其他阻挡对象。
- `SolidTilemap` 挂载 `Tilemap Renderer`，负责瓦片显示。
- `SolidTilemap` 挂载 `Tilemap Collider 2D`，负责根据实体瓦片生成碰撞形状。
- `SolidTilemap` 使用 `Solid` Layer 和 `Solid` Tag。
- 背景和装饰 Tilemap 根据绘制用途设置 Sorting Layer，非实体装饰不添加碰撞体。
- 当前基础墙体 Tile 使用 `Assets/Settings/Tiles` 下的 Tile 资源和 Tile Palette 进行关卡绘制。
- 基础墙体使用 `Assets/Settings/PhysicsMaterials/DefaultWall2D.physicsMaterial2D` 作为统一的 2D 物理材质；具体摩擦和弹性参数由该资源集中维护。
- 区块边界碰撞体与实体碰撞体分开设置，`Boundary` 不用于阻挡角色移动。

### 3.关卡区块配置与状态

- 每个关卡配置一个 `LevelManager`，统一管理本关卡的固定区块、可活动区块和碎片需求。
- `LevelManager` 的区块配置表按列表顺序保存区块引用、是否可移动、所需碎片数量和当前是否已放置。
- `LevelManager` 的 `availableChunks` 列表在 Inspector 中显示为 `Available Chunks`。它保存当前未放置且已解锁的可移动区块，关卡可在列表中配置开局可用区块，这些区块视为碎片已集齐。
- `LevelManager` 根据配置表顺序从 `1` 开始为区块分配 `ChunkId`，并将区块根对象命名为 `Chunk_001`、`Chunk_002` 等。
- 区块可移动性只保存在 `LevelManager` 的区块配置表中。
- 固定区块始终视为已放置；可移动区块的放置状态由 `LevelManager` 维护，并同步控制区块根对象是否激活。
- 已放置的可移动区块初始化时自动视为已收集满碎片，但不加入可使用区块表。
- 初始化可使用区块表时，剔除空引用、重复引用、未配置、固定和已放置的区块，并保留有效条目的配置顺序。
- 已确认的放置状态通过 `IsChunkPlaced` 和 `TrySetChunkPlaced` 访问。放置预览可临时激活区块对象，确认时才更新放置记录。
- `IsChunkUnlocked` 判断区块是否已放置或已进入可用列表，`HasFragment` 查询指定碎片编号的实际拾取记录。
- `LevelManager` 只为尚未放置、未解锁且 `isMovable` 为真的区块初始化碎片收集记录。
- 区块碎片收集完成后，`LevelManager` 将该未放置区块加入可使用区块表。
- 初始已放置的可移动区块不进入碎片记录表，但自动视为已收集满；被拿下后进入可使用区块表。
- 固定区块不会被玩家背包读取，也不会进入可放置区块选择范围。
- 调整配置表顺序会改变区块显示编号，已绑定目标引用的碎片仍指向原区块。

### 4.玩家所在区块

- 玩家根对象挂载 `PlayerChunkTracker`，由玩家侧维护当前所在区块。
- `PlayerChunkTracker` 固定查询名为 `Chunk` 的 Layer，不通过 Inspector 配置区块 LayerMask。
- `Boundary` 的触发器回调只用于注册玩家已经接触到的区块。
- 玩家开始运行时，`PlayerChunkTracker` 会通过玩家中心点查询当前位置附近的 `Chunk` Layer 碰撞体，避免玩家初始位置已经位于区块内部时遗漏注册。
- 已注册区块保存在内部追踪集合中，当前归属按玩家中心点逐帧判定。候选区块必须处于激活状态且由 `LevelManager` 确认已放置。
- 当多个已注册区块同时包含玩家中心点时，优先选择区块中心点距离玩家更近的区块。
- 玩家所在区块的最终判定以玩家根对象位置作为角色中心点，不以角色碰撞体边缘作为判定依据。
- `ChunkController.ContainsWorldPoint` 将玩家世界坐标转换为 `Boundary` 的局部坐标，并判断该点是否位于区块范围内。
- 区块发生旋转时，局部坐标判定仍按照区块自身方向执行。
- `PlayerChunkTracker.CurrentChunk` 保存当前区块，玩家中心点不属于任何区块时为空。
- `PlayerChunkTracker.CurrentChunkCenter` 保存当前区块中心点的世界坐标；当前区块为空时为零向量。
- 当前区块发生变化时触发 `CurrentChunkChanged` 事件。摄像机和区块交互读取追踪器属性。

### 5.摄像机移动

- `Camera` 预制体使用 `MainCamera` Tag，挂载 `CameraController`，统一管理跟随视角和区块操作视角。
- 摄像机在 `LateUpdate` 中于区块追踪器之后更新。
- 玩家属于区块时，摄像机目标位置为 `PlayerChunkTracker.CurrentChunkCenter`。
- 玩家中心点不属于任何区块时，摄像机目标位置切换为玩家位置。
- 摄像机使用 `Vector3.SmoothDamp` 平滑移动到目标位置，不采用定时跳转。
- 摄像机的 Z 坐标保持为 `-10`，以适配 2D 正交摄像机。
- 摄像机使用正交投影，具体可视范围由 `Orthographic Size` 决定。

### 6.区块拿下

- 玩家根对象挂载 `PlayerChunkInteraction`，负责区块拿下、放置和冻结时的原地旋转。
- `PlayerChunkInteraction` 通过 Inspector 显式引用玩家身上的 `PlayerMovement` 和 `PlayerChunkTracker`，场景级 `LevelManager` 在运行时自动查找。
- `PlayerChunkInteraction` 在操作状态变化时通知 `GameFlowController`，由全局流程统一设置物理时间和玩家移动输入。
- 玩家未冻结时按下 `F`，先检查玩家是否处于区块内，再根据 `PlayerMovement.FacingDirection` 计算相邻区块目标。
- 遮挡检测从玩家中心划线到当前区块对应边界，只将属于当前区块的非 Trigger `Solid` 碰撞体计为阻挡。
- 相邻目标中心的距离根据当前区块 `Boundary` 的实际尺寸和世界方向计算，固定使用的区块尺寸不是判定依据。
- `LevelManager.FindPlacedChunkAt` 按目标中心查询已放置区块。可移动区块进入拿下模式，固定区块阻止操作，空位可以尝试放置。
- 进入拿下模式时，将目标区块 `Background` 的 `SpriteRenderer` 颜色设置为红色。
- 拿下和放置的颜色反馈使用可调节透明度，默认值为 `0.2`，避免覆盖区块原有视觉效果。
- 满足条件后进入拿下状态并暂停游戏时间。
- 进入拿下或放置模式时，`PlayerChunkInteraction` 将两个区块中心的中点交给 `CameraController`。控制器暂停跟随并使用操作视角，预制体的操作正交尺寸为 `12`。
- 退出模式时，`CameraController` 恢复原位置和正交尺寸，并继续跟随。
- 再次按下 `F` 确认拿下，按下 `X` 取消操作。
- 确认后通过 `LevelManager.TrySetChunkPlaced` 将目标区块设置为未放置，并加入可使用区块表。

### 7.区块放置

- 玩家按下 `F` 时，如果目标位置没有已放置区块、当前区块边界路径没有 `Solid` 阻挡且存在可使用区块，则进入放置模式。
- 放置模式直接激活并移动所选区块对象，不创建额外的预览对象。
- 进入放置模式时固定来源区块和目标中心。旋转预览、切换预览和确认均使用同一个目标中心。来源区块失效时取消预览。
- 进入放置模式时，将所选区块 `Background` 的 `SpriteRenderer` 颜色设置为绿色。
- 进入放置模式时保存所选区块原有的激活状态、位置、旋转和缩放；取消放置时恢复这些状态。
- 放置模式中按 `Q` 或 `E` 将预览逆时针或顺时针旋转 `90` 度，按 `R` 切换下一个可使用区块。
- 玩家处于左右控制颠倒状态时，放置区块自动进行左右镜像；镜像状态在进入放置模式时确定。
- 再次按下 `F` 确认放置，通过 `LevelManager.TrySetChunkPlaced` 将区块设置为已放置并从可使用区块表移除。
- 按下 `X` 取消放置，恢复所选区块进入放置模式前的状态。
- 通过确认或取消退出操作模式时，恢复相关区块 `Background` 的原始颜色。

### 8.机关作用方向

- 将 `MechanismDirection` 直接挂在机关对象上。机关对象的局部上方（`+Y`）定义作用方向，贴图朝向随该对象的 Transform 一起变化。
- `WorldDirection` 返回考虑父级旋转和镜像后的二维单位向量。`IsFacing(Vector2 worldDirection)` 判断作用方向是否与指定世界方向同向。

### 9.冻结时旋转当前区块

- 玩家冻结且处于区块内时，`F` 进入 `Rotating` 状态，选中 `PlayerChunkTracker.CurrentChunk` 并暂停游戏。可移动性只影响拿下资格，原地旋转不修改区块放置记录。
- 选中时若玩家左右颠倒，将区块当前的局部 X 缩放取反一次。`Q` 逆时针旋转 `90` 度，`E` 顺时针旋转 `90` 度。两种预览均保持区块边界的世界中心和玩家在区块内的局部位置，角色自身旋转和朝向保持原值。
- 旋转和镜像预览直接应用变换并同步 2D 物理状态。
- `F` 确认预览。`X` 或预览中解除冻结时，恢复区块的原位置、旋转、缩放及玩家的原位置。

### 10.天气机关

- 关卡初始化、应用放置或旋转预览、还原预览及确认区块操作后，`LevelManager` 同步 2D 物理变换并发出布局变更事件。当前激活的冻僵区块通过 `ChunkSunlightVisual` 重算瓦片颜色，包含临时激活的放置预览。每块实体瓦片从顶部中心向世界上方使用 `WeatherMechanismPhysics.HasClearSky` 检测遮挡。照到日光的瓦片保留原色，阴影下的瓦片乘以预制体配置的亮度倍率。
- `SunlightBud` 和 `AbsorbentMoss` 预制体作为所属区块的子对象摆放，读取父级 `ChunkWeather`。在 `FixedUpdate` 从预制体的采样点沿世界上方检测 `Solid`，排除机关自身的碰撞体。区块确认放置时同步物理变换。
- `SunlightBud` 以花心为原点，切换闭合外观和预设的 3×1 `BoxCollider2D` 平台。
- `AbsorbentMoss` 以底边中心为原点，累计淋雨达 `absorptionSeconds` 后切换到湿态。预设的 3×1 和 3×6 碰撞体底边对齐。
- 两种机关在启用实体前都使用 `Player` 图层检查目标范围，玩家实体占用时等待。地形可以遮住日照或降雨，生长后的碰撞体可以与地形重叠。
- `VineWall` 可悬空或贴墙。`Wall Width` 为零时只有一条居中的藤蔓，贴墙时用 `Wall Surfaces` 选择双面、仅正面（局部 +X）或仅背面（局部 −X）。攀爬检测面是宽 0.1 的 `AutoTrigger`。藤蔓沿世界方向竖直时，玩家实体进入范围即附着，`PlayerMovement` 用 `Vertical` 输入控制上下速度并暂停重力，跳跃脱离后离开该检测范围才可重新附着。
- 可生长藤蔓引用所属区块的实体 Tilemap。启用面的贴图和 Trigger 各自从墙面外侧向两端生长，每轮每端最多两格。降雨只从启用面检测。每格须有墙体支撑，且墙面外侧新增的范围未被非 Trigger 的 `Solid` 占用。受阻的一侧停在障碍前，空着的一侧可继续生长。长度随区块拿下和放回保留。
- 支撑墙所在 Tilemap 的障碍按新增范围覆盖的碰撞瓦片判断。物理重叠查询检查其他实体，跳过已经按格检测的支撑墙碰撞体。

## 六、待补充技术章节

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

## 七、全局流程与 UI

### 1.资源与启动

- `Assets/Scenes/MainMenu.unity` 为构建列表首场景，`Assets/Scenes/Level0.unity` 为第 0 关。
- `Assets/Resources/GameApplication.prefab` 包含 `GameFlowController`、两个音频播放器、EventSystem 与 uGUI Canvas。
- `GameFlowController` 在场景加载前实例化该 Prefab，并使用 `DontDestroyOnLoad` 保留；不需要在每个关卡重复放置管理器。
- 同一 Prefab 内的 UI、音频与流程引用均序列化绑定。关卡中的玩家与交互组件在场景加载后查找。
- UI 采用 uGUI 和 TextMeshPro。Canvas 使用 1600×900 参考分辨率与 Scale With Screen Size。
- 主菜单背景使用 `Assets/Art/Backgrounds/MainMenuBackground.png`，按 Sprite (2D and UI) 导入，关闭 Mipmap 与纹理压缩，使用 Bilinear 过滤和 Clamp 包裹。
- `MainMenuPanel/Background` 使用 `AspectRatioFitter.EnvelopeParent` 保持背景比例并裁切超出部分；背景和全屏遮罩均关闭 Raycast Target，不阻挡菜单按钮。
- 主菜单标题使用保留原图比例的透明 Sprite `Assets/Art/UI/MainMenuTitle.png`。
- 主菜单标题与开始、继续、选关居中排列，声音设置和退出分别锚定左下与右下。中央按钮共用切角木牌 Sprite，悬停及键盘选中使用带暖灯高光和碎片标记的 Sprite，按钮文字以实际字形的中线垂直居中，禁用继续时压暗，角落按钮使用文字颜色反馈，原有点击事件保持序列化绑定。
- 选关由 `LevelSelectMap` 在 `LevelSelectPanel` 中生成 16×16 格的 uGUI 地图。地图运动使用不受时间缩放影响的 UI 坐标，与关卡物理独立。
- 关卡门按配置顺序从 `00` 开始编号，每页最多 12 扇门，超过时使用 Page Up / Page Down 切换地图。门的颜色和进关权限读取现有存档，进门复用 `GameFlowController.LoadLevel`。
- 设置界面使用三个独立音量卡片展示标签、用途说明、滑轨与百分比；打开页面时以 `SetValueWithoutNotify` 同步存档值并默认聚焦总音量，从暂停状态进入和返回时保持全局暂停。
- 暂停界面使用半透明全屏遮罩和居中内容卡片，继续操作为默认焦点，重新开始使用警示层级；界面只调用 `GameFlowController` 的暂停、恢复、重开和返回接口，不直接修改时间缩放。
- 通关与失败继续共用 `ResultPanel`：控制器根据流程状态切换标题、说明和暖金/暖红强调色；有下一关时默认聚焦下一关，否则默认聚焦重试。
- HUD 使用左上状态卡、右上暂停入口和底部操作提示条；普通、放置与拿下状态使用不同文字和强调色，仍以 0.15 秒间隔只读 `LevelManager` 与 `PlayerChunkInteraction`。
- 主菜单、选关、设置、暂停和结果面板带 0.12 秒 unscaled-time 淡入；淡入不禁用交互，场景加载期间的输入屏蔽仍由 Loading 状态和 Transition CanvasGroup 负责。
- 全局按钮统一 Hover、Selected、Pressed 和 Disabled 色彩反馈；Canvas 按 1600×900 高度适配，已覆盖 4:3、16:9、16:10 和约 21:9 比例。
- 中文字体位于 `Assets/Settings/Fonts`，使用 Noto Sans SC，附带 OFL 许可；TMP 基础资源位于 `Assets/Settings/TextMeshPro`。
- `CavernsPipeline.asset` 和 `CavernsRenderer2D.asset` 为缺失管线引用的替代配置，使用 URP 2D Renderer；没有调整 Layer、Tag 或物理矩阵。

### 2.关卡配置

- `Assets/Settings/GameConfiguration.asset` 保存菜单场景路径和有序关卡列表。
- 每项包含稳定且唯一的 `id`、显示名称、完整场景路径与音乐资源。
- ID 用于存档，后续不要随意更名；关卡顺序决定下一关与解锁顺序。
- 新增关卡时同时加入此配置与 Build Settings，并保留原场景 GUID。
- 当前关卡顺序为 `Level0`、`Winter1` 至 `Winter6`、`Spring1` 至 `Spring7`。第 0 关的出口随所在区块旋转和移动，通关后解锁 `Winter1`。
- 冬季每关的设计表包含上下两个 16×16 图块，对应第 1–16 行和第 19–34 行。跳过第 17、18 行分隔，保留图块内部空格。`1` 生成地块，`-1` 和 `0` 分别定位玩家和过关点。
- 冬 4 开局放置两个区块，上方区块固定，`Available Chunks` 为空。其余冬季关卡只预放置出生区块，另一块由 `Available Chunks` 直接提供给玩家。各区块的 `ChunkWeather` 启用冻僵。
- 春季工作簿第 0 关至第 6 关对应场景 `Spring1` 至 `Spring7`，按 16×16 格相邻摆放。全空槽留空，带地形的待放置区块按配置由碎片解锁。新增首关的存档 ID 为 `SpringIntro`，顺延的六关保留原存档 ID。
- `Spring3` 的淋雨藤蔓区块按拿下、旋转和放回流程操作，`Spring6` 全部区块有降雨，`Spring7` 仅上层苔藓区块有降雨。

### 3.流程入口

- `GameFlowController.Instance.CompleteLevel()`：仅在 Playing 状态记录通关，解锁下一关并显示结束界面；重复调用不重复记录。
- `FailLevel()`：仅在 Playing 状态进入失败界面，不写入通关记录。
- `RestartLevel()` 异步重新加载当前关卡，使用场景初始状态。即死伤害可调用此接口。
- `RespawnPlayer()` 仅在游玩状态有效。它把玩家移回场景加载时记录的世界坐标，取消区块预览，恢复输入方向，清除速度和缓存跳跃，再刷新玩家所属区块。已收集碎片、已确认的区块修改和机关状态随当前场景保留。
- `LoadLevel(string levelId)`：只加载已解锁且已加入构建列表的关卡。
- `LoadNextLevel()`、`ReturnToMainMenu()`、`StartGame()`、`ContinueGame()` 提供流程入口。
- `LevelFlowEvents` 可挂到出口或机关对象，通过 UnityEvent 绑定其公开通关、失败、整关重开和玩家复位方法。该组件本身不判断碰撞或通关条件。
- `LevelFlowTrigger` 可挂到带 2D Trigger Collider 的出口、伤害区域或掉空区域，使用 Inspector 选择通关、失败、玩家复位或重载当前关卡。它只响应自身或所属刚体带 `Player` Tag 的碰撞体，默认只触发一次。
- 在关卡场景中直接摆放玩家，玩家根对象的位置就是出生位置。`GameFlowController` 在加载关卡时将该位置记录在内存中，供 `RespawnPlayer()` 使用。
- 过关点使用 `Assets/Prefab/LevelExitPoint.prefab`，配置为 `Exit` Tag、`AutoTrigger` Layer 和 `BoxCollider2D` Trigger，通过 `LevelFlowTrigger` 接入通关流程。只有玩家实体碰撞体进入才触发通关，外围交互 Trigger 不触发。
- 第 0 关和冬、春关卡使用 `Assets/Prefab/FallBoundary.prefab`。预制体配置可重复触发的 `RespawnPlayer` 动作，只响应玩家实体碰撞体。各场景根据地图布局配置位置和覆盖宽度。
- 致死钟乳石使用 `Assets/Prefab/LethalStalactite.prefab`，作为所属区块的子对象摆放。预制体使用 `Damage` Layer 和贴合三角形的 `PolygonCollider2D` Trigger，玩家实体碰撞体进入时由 `LevelFlowTrigger` 重载当前关卡。
- 场景切换时取消当前区块预览、禁用移动输入，使用不受 Time.timeScale 影响的淡入淡出，加载期间屏蔽重复加载请求。

### 4.暂停与输入

- 全局状态包含 MainMenu、Playing、Paused、Completed、Failed 和 Loading。
- `GameFlowController` 统一写入 `Time.timeScale` 和 `PlayerMovement.SetInputEnabled`。游玩期间只有退出区块操作后才恢复物理和移动输入。
- 菜单暂停保留区块交互状态，继续时恢复到先前的操作。加载和结算期间保持暂停，区块操作取消后仍由当前全局状态决定是否恢复。
- `PlayerChunkInteraction.Update` 在全局流程阻止输入时不读取操作键。
- 新增 `CancelCurrentInteraction()` 接口，在通关、失败和切换场景前复用原有取消逻辑恢复预览与摄像机。
- HUD 只读现有 `LevelManager` 与 `PlayerChunkInteraction`，不持有第二份背包／区块数据。

### 5.本地记录与音频

- `GameSaveStore` 使用独立的 PlayerPrefs 键 `ShatteredCaverns.Progress.v1` 保存版本化 JSON。
- 保存稳定关卡 ID 列表、最近关卡和三个音量值；损坏 JSON 或不支持的版本回退为初始数据。
- 读取存档时删除配置中已不存在或重复的关卡 ID，已完成关卡保持解锁，无效最近关卡被清空，修正结果会写回 PlayerPrefs。
- 返回设置、切换场景、通关、应用暂停与退出时写盘。
- 不记录玩家位置、碎片实例、区块变换或音乐播放时间；重开使用场景初始状态。
- `GameAudioPlayer` 管理独立的 MusicPlayer 与 SoundPlayer，实际音量为主音量乘对应通道音量。
- 配置中的菜单音乐、各关卡音乐、按钮／通关／失败音效可在 Inspector 指定；当前没有正式音频素材，允许为空。
- `PlayMusic(AudioClip)` 与 `PlaySound(AudioClip)` 可供玩法对象调用；相同背景音乐不会因为重复请求而从头播放。
- `PlayMusic(AudioClip, float fadeDuration)` 支持先淡出旧音乐、再淡入新音乐；时长为每一段淡变的秒数，0 表示立即切换。重复请求正在切换到的音乐不会重置过渡；新请求取消旧过渡。
- `StopMusic(float fadeDuration = 0f)` 停止并清空音乐，传入空音乐具有相同效果。`PauseMusic()`／`ResumeMusic()` 保留并恢复播放位置，`IsMusicPaused` 表示接口暂停状态。
- 淡变使用不受 `Time.timeScale` 影响的时间，音量设置会即时乘入淡变增益；手动暂停音乐时淡变也暂停。游戏暂停与区块操作默认保持背景音乐播放，不自动调用音乐暂停接口。
- 场景切换通过 `GameConfiguration.MusicFadeDuration` 设置淡变时长，默认每段 0.5 秒；相同音乐跨场景保持播放。接口入口为 `GameFlowController.Instance.AudioPlayer`，应在全局流程初始化后调用。
- 接入素材时，在 `Assets/Settings/GameConfiguration.asset` 的 `Menu Music`、各关卡 `Music` 和对应音效字段拖入 AudioClip；`Music Fade Duration` 调整切换时长。音乐循环由播放器保证；空引用不会阻止流程运行。音量沿用现有设置与存档。

### 6.Editor 工具与验证

- `Tools > Shattered Caverns > Build Game Interface` 使用 Unity API 生成初始资源，已有 GameApplication 时拒绝覆盖；正常开发直接编辑现有 Prefab。
- `Tools > Shattered Caverns > Apply Main Menu Redesign`（macOS `Cmd+Shift+M`）通过 Unity API 配置背景导入并将主菜单视觉结构应用到现有 Prefab。
- `Tools > Shattered Caverns > Apply Settings Redesign` 通过 Unity API 将声音设置的卡片、滑轨、手柄、百分比和导航样式应用到现有 Prefab，不修改音频与存档架构。
- `Tools > Shattered Caverns > Apply Pause Redesign` 通过 Unity API 将遮罩、中央卡片、按钮层级与提示信息应用到现有 Prefab，不修改暂停职责边界或区块交互代码。
- `Tools > Shattered Caverns > Apply Result Redesign` 通过 Unity API 将结果遮罩、中央卡片、状态强调条和按钮层级应用到共用结果面板。
- `Tools > Shattered Caverns > Apply HUD Redesign` 通过 Unity API 将状态卡、暂停入口和底部提示条应用到现有 HUD，不创建第二份玩法状态。
- `Tools > Shattered Caverns > Apply Interface Polish` 为五个主要面板配置 CanvasGroup，并统一按钮状态色和 CanvasScaler 适配设置。
- `Tools > Shattered Caverns > Validate Game Flow` 先检查关卡配置与 Build Settings，再在 Play Mode 中验证主菜单、菜单焦点、设置保存、存档规范化、场景重载、暂停、区块操作暂停、流程触发器、结束界面、音频与继续游戏。
- 验证报告输出到被忽略的 `Logs/GameFlowValidation.txt`；测试前备份该存档键，退出测试后恢复，不影响玩家进度。
- 验证器临时允许后台运行，避免编辑器失去焦点时停止场景帧而导致超时。
- 当前 macOS Editor 流程验证通过 36 项检查，并检查所有项目 Prefab 无 Missing Script；尚未验证 Windows 构建或正式音频听感。
