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
- 玩家 `Rigidbody2D` 启用插值（`Interpolate`），用于减小物理更新频率与画面渲染频率不同步造成的视觉抖动。

### 2.输入

- 水平输入使用旧版 Input Manager 的 `Horizontal` 输入轴，输入值乘以 `moveSpeed` 后作为刚体水平速度。
- 跳跃使用旧版 Input Manager 的 `Jump` 输入按钮，当前绑定为空格键；按下跳跃键后记录跳跃请求，在物理帧中确认玩家处于地面时，将刚体竖直速度设置为 `jumpVelocity`。
- 区块交互使用直接键盘输入：`F` 用于进入和确认操作，`X` 用于取消，`Q` 用于旋转待放置区块，`E` 用于切换可用区块。
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
- 玩家朝向由 `PlayerFacingDirection` 表示，当前包括 `Left`、`Right`、`Up` 和 `Down`。
- 当存在有效水平输入时，玩家朝向根据实际生效的水平输入更新；左右控制颠倒时，朝向也使用颠倒后的实际输入判断。
- 当玩家竖直速度绝对值超过 `verticalFacingSpeedThreshold` 时，朝向切换为 `Up` 或 `Down`；否则恢复为最近一次水平朝向。
- 左右控制颠倒状态由 `isHorizontalInputInverted` 保存，并通过 `SetHorizontalInputInverted` 提供运行时修改接口。

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

### 3.碎片记录与关卡管理关联

- `FragmentPickup` 使用 `chunkId` 和 `fragmentId` 标识所属区块与碎片编号。
- `LevelManager` 使用 `Dictionary<int, HashSet<int>>` 保存碎片记录。
- 外层键为 `chunkId`，内层集合保存该区块已收集的 `fragmentId`。
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
→ 返回 true 时销毁物品
→ 返回 false 时保留物品
```

## 五、区块系统

### 1.区块对象结构

- 区块根对象使用 `Chunk` Layer 和 `Chunk` Tag，作为区块整体平移与旋转的对象。
- 区块根对象挂载 `ChunkController`，负责提供区块边界和区块中心点信息。
- 区块根对象下设置 `Grid`，区块内部的 Tilemap 和地图对象均作为其子对象。
- `Boundary` 作为区块根对象的子对象，挂载与区块范围一致的 `BoxCollider2D`。
- `Boundary` 使用 `Chunk` Layer，并将 `Is Trigger` 开启，用于向玩家报告区块接触情况。
- `ChunkController` 通过 Inspector 显式引用 `Boundary` 的 `BoxCollider2D`，不自动搜索子对象中的碰撞体。
- `ChunkController` 通过 Inspector 显式引用 `Background` 的 `SpriteRenderer`，不自动从子对象中搜索背景渲染器。
- 区块中心、相邻区块距离和边界距离均根据 `Boundary` 的实际尺寸计算，不直接依赖固定的 `16` 作为运行时判定尺寸。

当前区块的基本结构如下：

```text
Chunk
├── ChunkController
├── Grid
│   └── SolidTilemap
├── Boundary
│   └── BoxCollider2D（Trigger，16×16）
└── Background
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
- `LevelManager` 额外维护可使用区块表，只保存已收集满碎片但当前未放置的可移动区块。
- `LevelManager` 根据配置表顺序从 `1` 开始为区块分配 `ChunkId`，并将区块根对象命名为 `Chunk_001`、`Chunk_002` 等。
- `LevelManager` 将配置表中的可移动性写入对应 `ChunkController`，不在区块对象上手动配置该属性。
- 固定区块始终视为已放置；可移动区块的放置状态由 `LevelManager` 维护，并同步控制区块根对象是否激活。
- 已放置的可移动区块初始化时自动视为已收集满碎片，但不加入可使用区块表。
- 可使用区块表中的区块必须是可移动且未放置的区块。
- 区块放置状态通过 `IsChunkPlaced` 和 `TrySetChunkPlaced` 访问，不由其他系统直接修改区块对象的激活状态。
- `LevelManager` 只为尚未放置且 `isMovable` 为真的区块初始化碎片收集记录。
- 区块碎片收集完成后，`LevelManager` 将该未放置区块加入可使用区块表。
- 初始已放置的可移动区块不进入碎片记录表，但自动视为已收集满；被拿下后进入可使用区块表。
- 固定区块不会被玩家背包读取，也不会进入可放置区块选择范围。
- 区块编号按照配置表顺序生成，配置表中的区块顺序不应随意调整。

### 4.玩家所在区块

- 玩家根对象挂载 `PlayerChunkTracker`，由玩家侧维护当前所在区块。
- `PlayerChunkTracker` 固定查询名为 `Chunk` 的 Layer，不通过 Inspector 配置区块 LayerMask。
- `Boundary` 的触发器回调只用于注册玩家已经接触到的区块。
- 玩家开始运行时，`PlayerChunkTracker` 会通过玩家中心点查询当前位置附近的 `Chunk` Layer 碰撞体，避免玩家初始位置已经位于区块内部时遗漏注册。
- 已注册区块保存在内部追踪集合中；区块进入集合后不会因为玩家离开而立即移除，当前归属由玩家中心点对已注册区块逐帧重新判定。
- 当多个已注册区块同时包含玩家中心点时，优先选择区块中心点距离玩家更近的区块。
- 玩家所在区块的最终判定以玩家根对象位置作为角色中心点，不以角色碰撞体边缘作为判定依据。
- `ChunkController.ContainsWorldPoint` 将玩家世界坐标转换为 `Boundary` 的局部坐标，并判断该点是否位于区块范围内。
- 区块发生旋转时，局部坐标判定仍按照区块自身方向执行。
- `PlayerChunkTracker.CurrentChunk` 保存当前区块，玩家中心点不属于任何区块时为空。
- `PlayerChunkTracker.CurrentChunkCenter` 保存当前区块中心点的世界坐标；当前区块为空时为零向量。
- 已发现的区块保存在追踪集合中，离开区块不会删除该区块记录；当前归属仍由每帧中心点判定结果决定。
- 当前区块发生变化时触发 `CurrentChunkChanged` 事件，供摄像机和区块放置系统使用。

### 5.摄像机移动

- 主摄像机挂载 `CameraController`。
- 摄像机在 `LateUpdate` 中更新，确保玩家移动和区块判定完成后再调整位置。
- 玩家属于区块时，摄像机目标位置为 `PlayerChunkTracker.CurrentChunkCenter`。
- 玩家中心点不属于任何区块时，摄像机目标位置切换为玩家位置。
- 摄像机使用 `Vector3.SmoothDamp` 平滑移动到目标位置，不采用定时跳转。
- 摄像机的 Z 坐标保持为 `-10`，以适配 2D 正交摄像机。
- 摄像机使用正交投影，具体可视范围由 `Orthographic Size` 决定。

### 6.区块拿下

- 玩家根对象挂载 `PlayerChunkInteraction`，负责区块拿下和放置操作。
- `PlayerChunkInteraction` 通过 Inspector 显式引用玩家身上的 `PlayerMovement` 和 `PlayerChunkTracker`，场景级 `LevelManager` 在运行时自动查找。
- 进入拿下或放置模式时，`PlayerChunkInteraction` 调用 `PlayerMovement.SetInputEnabled(false)`，该方法会同时清除水平输入和未执行的跳跃请求；退出操作模式后重新启用输入。
- 玩家按下 `F` 时，先检查玩家是否处于区块内，并根据当前朝向计算目标区块位置。
- `Solid` 检测只从玩家中心点进行到当前区块对应边界，超出当前区块边界的阻挡不参与判断。
- 相邻目标中心的距离根据当前区块 `Boundary` 的实际尺寸和世界方向计算，固定使用的区块尺寸不是判定依据。
- 目标位置必须存在已放置的可移动区块，固定区块不能被拿下。
- 进入拿下模式时，将目标区块 `Background` 的 `SpriteRenderer` 颜色设置为红色。
- 拿下和放置的颜色反馈使用可调节透明度，默认值为 `0.2`，避免覆盖区块原有视觉效果。
- 满足条件后进入拿下状态并暂停游戏时间。
- 进入拿下或放置状态时暂时禁用 `PlayerMovement` 输入，防止跳跃请求在暂停期间被缓存。
- 进入拿下或放置状态时，主摄像机的正交尺寸设置为 `12`，位置移动到当前区块与目标区块中心的中点。
- 操作模式期间暂时禁用 `CameraController`，避免普通摄像机跟随逻辑覆盖操作视角。
- 通过确认、取消或其他退出路径离开操作模式时，恢复主摄像机原来的位置、正交尺寸和控制器启用状态。
- 再次按下 `F` 确认拿下，按下 `X` 取消操作。
- 确认后通过 `LevelManager.TrySetChunkPlaced` 将目标区块设置为未放置，并加入可使用区块表。

### 7.区块放置

- 玩家按下 `F` 时，如果目标位置没有已放置区块、当前区块边界路径没有 `Solid` 阻挡且存在可使用区块，则进入放置模式。
- 放置模式直接激活并移动所选区块对象，不创建额外的预览对象。
- 进入放置模式时，将所选区块 `Background` 的 `SpriteRenderer` 颜色设置为绿色。
- 进入放置模式时保存所选区块原有的激活状态、位置、旋转和缩放；取消放置时恢复这些状态。
- 放置模式中按 `Q` 将所选区块旋转 `90` 度，按 `E` 切换下一个可使用区块。
- 玩家处于左右控制颠倒状态时，放置区块自动进行左右镜像；镜像状态在进入放置模式时确定。
- 再次按下 `F` 确认放置，通过 `LevelManager.TrySetChunkPlaced` 将区块设置为已放置并从可使用区块表移除。
- 按下 `X` 取消放置，恢复所选区块进入放置模式前的状态。
- 通过确认或取消退出操作模式时，恢复相关区块 `Background` 的原始颜色。

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

- `Assets/Scenes/MainMenu.unity` 为构建列表首场景，原玩法场景仍为 `Assets/Scenes/URP2DSceneTemplate.unity`。
- `Assets/Resources/GameApplication.prefab` 包含 `GameFlowController`、两个音频播放器、EventSystem 与 uGUI Canvas。
- `GameFlowController` 在场景加载前实例化该 Prefab，并使用 `DontDestroyOnLoad` 保留；不需要在每个关卡重复放置管理器。
- 同一 Prefab 内的 UI、音频与流程引用均序列化绑定。关卡中的玩家与交互组件在场景加载后查找。
- UI 采用 uGUI 和 TextMeshPro。Canvas 使用 1600×900 参考分辨率与 Scale With Screen Size。
- 主菜单背景使用 `Assets/Art/Backgrounds/MainMenuBackground.png`，按 Sprite (2D and UI) 导入，关闭 Mipmap 与纹理压缩，使用 Bilinear 过滤和 Clamp 包裹。
- `MainMenuPanel/Background` 使用 `AspectRatioFitter.EnvelopeParent` 保持背景比例并裁切超出部分；背景和左侧遮罩均关闭 Raycast Target，不阻挡菜单按钮。
- 主菜单标题和按钮使用左上锚点排列；开始游戏为暖金色主按钮，其余功能使用深色次级按钮，现有 Button 事件保持序列化绑定。
- 选关界面沿用主菜单的深色、青绿色与暖金色色板；关卡项显示两位编号、配置名称和状态，已通关使用暖金强调、可探索使用明亮文字、未解锁使用可读的低对比度文字且禁用交互。
- 关卡列表由 `ScrollRect`、顶部锚定的内容节点和纵向布局组成，可随配置增加项目；打开页面时只在可交互项目中选择第一个作为键盘或手柄焦点。
- 中文字体位于 `Assets/Settings/Fonts`，使用 Noto Sans SC，附带 OFL 许可；TMP 基础资源位于 `Assets/Settings/TextMeshPro`。
- `CavernsPipeline.asset` 和 `CavernsRenderer2D.asset` 为缺失管线引用的替代配置，使用 URP 2D Renderer；没有调整 Layer、Tag 或物理矩阵。

### 2.关卡配置

- `Assets/Settings/GameConfiguration.asset` 保存菜单场景路径和有序关卡列表。
- 每项包含稳定且唯一的 `id`、显示名称、完整场景路径与音乐资源。
- ID 用于存档，后续不要随意更名；关卡顺序决定下一关与解锁顺序。
- 新增关卡时同时加入此配置与 Build Settings，并保留原场景 GUID。
- 当前仅配置真实存在的测试关卡，没有占位的虚构关卡。

### 3.流程入口

- `GameFlowController.Instance.CompleteLevel()`：仅在 Playing 状态记录通关，解锁下一关并显示结束界面；重复调用不重复记录。
- `FailLevel()`：仅在 Playing 状态进入失败界面，不写入通关记录。
- `RestartLevel()`：异步重新加载当前关卡，重置场景运行状态。
- `LoadLevel(string levelId)`：只加载已解锁且已加入构建列表的关卡。
- `LoadNextLevel()`、`ReturnToMainMenu()`、`StartGame()`、`ContinueGame()` 提供流程入口。
- `LevelFlowEvents` 可挂到出口或机关对象，通过 UnityEvent 绑定其公开通关、失败、重开方法。该组件本身不判断碰撞或通关条件。
- `LevelFlowTrigger` 可挂到带 2D Trigger Collider 的出口或伤害区域，使用 Inspector 选择通关或失败；它只响应自身或所属刚体带 `Player` Tag 的碰撞体，默认只触发一次。
- 场景切换时取消当前区块预览、禁用移动输入，使用不受 Time.timeScale 影响的淡入淡出，加载期间屏蔽重复加载请求。

### 4.暂停与输入

- 全局状态包含 MainMenu、Playing、Paused、Completed、Failed 和 Loading。
- 菜单暂停保留区块交互状态；继续时根据交互状态决定恢复时间还是保持零时间。
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

### 6.Editor 工具与验证

- `Tools > Shattered Caverns > Build Game Interface` 使用 Unity API 生成初始资源，已有 GameApplication 时拒绝覆盖；正常开发直接编辑现有 Prefab。
- `Tools > Shattered Caverns > Apply Main Menu Redesign`（macOS `Cmd+Shift+M`）通过 Unity API 配置背景导入并将主菜单视觉结构应用到现有 Prefab。
- `Tools > Shattered Caverns > Validate Game Flow` 先检查关卡配置与 Build Settings，再在 Play Mode 中验证主菜单、菜单焦点、设置保存、存档规范化、场景重载、暂停、区块操作暂停、流程触发器、结束界面、音频与继续游戏。
- 验证报告输出到被忽略的 `Logs/GameFlowValidation.txt`；测试前备份该存档键，退出测试后恢复，不影响玩家进度。
- 验证器临时允许后台运行，避免编辑器失去焦点时停止场景帧而导致超时。
- 当前 macOS Editor 流程验证通过 36 项检查，并检查所有项目 Prefab 无 Missing Script；尚未验证 Windows 构建或正式音频听感。
