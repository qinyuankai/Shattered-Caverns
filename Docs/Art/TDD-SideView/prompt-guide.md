# 《破碎洞窟》横版美术提示词与技术对照

## 使用范围

依据 Docs/TDD.md 生成四张画风与玩法概念图，沿用用户确认的横版 2D、三个正方形区块横向排列、中间区块缺失构图。使用内置 image_gen，不使用 CLI。精确生成提示词见下文。

## 根据技术文档补充的内容

- Chunk 暂定 16×16 基础单位，角色以 1×1 为比例。不是每张区块图只有 16×16 像素。
- 区块整体平移、旋转，内部普通块、机关、存档点和出口等随所属区块变换。角落旋转提示和水平箭头只是本次示意的视觉设计，并非已确定的操作 UI。
- 玩家具有 Idle / Running / Rising / Falling 状态。构图加入简单台阶与头顶空间，避免继续表现成俯视道路。跳跃高度、距离、台阶可达性仍需实际物理参数验证。
- 碎片按 chunkId 分组，以 fragmentId 区分；每个区块所需数量由 LevelFragmentManager 配置。图中不再假定固定四片。以同色同形印记辅助表示所属区块，这是美术建议，不是文档已有实现。
- 按 Background → Environment → Characters → Effects → Foreground → UI 组织绘制层次。背景低对比、实体块上沿清晰、角色突出，前景不遮挡关键道路。
- Checkpoint / Exit 等是文档定义的对象身份。图中存档点外观仅作概念，激活方式、动画、存档行为未确定。
- Layer / Tag / 类名仅用于工程组织，不应绘制到图片中。
- 文档地图拼接、知识锁、存档等章节仍待补充；本次不会将相关行为当作已实现系统。空槽、放置和平移旋转画面用于表达设计意图。
- 未引入敌人、钥匙锁或伤害机关作为新玩法要求。

## 资源制作建议（新增建议，并非 TDD 强制规格）

建议先统一 1 基础单位 = 16×16 像素：角色单帧 16×16 像素，区块逻辑画布 256×256 像素，三块场景 768×256 像素；预览使用整数倍放大。若角色辨识度不足，可整套改为 32 像素/单位，保持物理比例一致。生成图的像素尺寸和网格可能有误差，正式资源需人工整理到这一网格，不应直接把概念图当作 Tilemap。

建议 Unity 导入时核对 Pixels Per Unit、Point 过滤、禁用有损压缩；具体参数应与项目最终单位规范一起确定。贴图碰撞形状与物理体不应仅凭 AI 图像自动推定。可拾取物采用透明背景、特效留在独立层。

## 四种画风

- 暖色矿洞：01-warm-mineshaft.png
- 冷色晶洞：02-crystal-cavern.png
- 苔藓遗迹：03-mossy-ruins.png
- 极简复古：04-minimal-retro.png

## 实际生成使用的公共提示词（英文，原文）

四张图均使用以下完整公共提示词，加上对应方案的 STYLE BLOCK。

```text
Use case: stylized-concept.
Asset type: gameplay art-direction concept for Shattered Caverns, a Unity 2D platform puzzle game.
Canvas: panoramic 3:1 landscape. Exactly THREE equal SQUARE chunk slots in ONE horizontal row, aligned top and bottom. LEFT square contains a cave room, CENTER square is completely missing, RIGHT square contains a cave room and daylight exit. No fourth chunk, no inset, no inventory strip, no duplicate scene.
Camera: strict orthographic SIDE VIEW platformer cross section. Gravity is downward. Never overhead, never isometric. Horizontal platforms have clear upper collision silhouettes.
Scale: each square chunk represents 16 by 16 BASE UNITS. Player bounding box is exactly 1 by 1 base unit, a compact squat placeholder sprite, not a tall two-unit humanoid. Player is 1/16 of one chunk's width AND height. Basic blocks and platform steps are clearly based on this unit scale. Aim for a logical 16 pixels per base unit with nearest-neighbor pixel rendering; this is an art production convention rather than a requirement of Unity. Tiny character near left center faces RIGHT.
Layout: each solid chunk has rocky ceiling and walls, open traversable air, solid ground around y=11/16, layered rock below. The two exposed floor endpoints bordering the missing CENTER slot are at the EXACT SAME HEIGHT, glowing subtly amber to indicate compatible chunk connections. Center is a dark EMPTY square bordered by fine amber dashed lines and a faint precisely 16 by 16 grid. No bridge, no ghost floor and no terrain in missing chunk. Two small curved corner arrows outside the dashed center border imply rotation of an entire square chunk; tiny horizontal double-arrow along its top border implies translation. Keep these restrained gameplay cues, not interface clutter.
LEFT chunk includes a small simple one-unit-high step on the approach, with generous headroom, expressing ordinary running and jumping. Floor at the missing-slot endpoint returns to the aligned height. One small stationary collectible torn parchment map shard hovers one unit above a safe ledge, with a distinctive amber diamond-shaped seal matching the tiny amber diamond at the missing slot corner: group identity by shape and color together, NO IDs or words. Fragment counts vary per chunk; do NOT draw a fixed four-fragment assembly. Also one very small floor-mounted unlit carved stone checkpoint marker at the far-left safe ground, environmental concept only. No enemies, no spikes, no keys, no locks, no invented mechanics.
RIGHT chunk contains a few static stones and an unmistakable bright cave-mouth exit on its far-right floor, with ground extending all the way into the opening. The checkpoint, ground blocks, decor and exit visually belong inside their square chunk boundaries. All ground/decor is attached to its chunk. Background darker, lower contrast; environment platforms mid contrast; small character high contrast; tiny effects sparse; foreground edge rock sparse and must not obscure playable silhouettes. No full-screen fog, no heavy foreground cover.
Visual style: handcrafted crisp PIXEL ART, visible square pixel clusters, hard edges, discrete shading, limited palette, no anti-aliasing, no painterly brushwork, no photorealism, no 3D. The frame demonstrates the pre-placement puzzle state; it is not a blueprint and must remain attractive game art.
Text: NONE. No words, labels, letters, numbers, coordinate rulers, title, watermark, control hints or debug UI.
```

## 暖色矿洞 STYLE BLOCK（实际原文）

```text
Art direction: Warm classic mining cave: chocolate-brown and charcoal rock, ochre dirt platforms with crisp golden top edges, sparse weathered wooden supports, one small amber lantern, restrained teal mineral accents. Background dark muted burgundy. Compact navy-and-cream placeholder player. Exit warm ivory daylight. Palette anchors #241D26 #4B3335 #80513C #C18A4A #F5CB73 #4E9B91. Moderate 16-bit texture, readable warm adventurous mood.
```

## 冷色晶洞 STYLE BLOCK（实际原文）

```text
Art direction: Cool luminous crystal cave: deep navy and indigo layered rock, steel-blue stone platforms with pale cyan top edges, sparse cyan and violet crystals, no bloom, local light in stepped pixel shades. Warm gold reserved for fragment and chunk manipulation cues. Compact pale grey placeholder player. Exit ice-white daylight. Palette anchors #10152F #26345D #465C89 #79AFC1 #B7F1E8 #A589CF #F2BD62. Mysterious quiet exploration with restrained crisp 16-bit detail.
```

## 苔藓遗迹 STYLE BLOCK（实际原文）

```text
Art direction: Ancient ruins inside natural cave: charcoal rock, cracked olive-grey squared stone platform modules, small broken arch fragments and sparse moss, one small amber brazier. No extra arches that look like functional exits. Compact ivory-and-charcoal placeholder player. Far-right actual exit glows soft yellow-green daylight. Palette anchors #202827 #3D4941 #68715A #99997A #B6BE83 #E7C681 #6B8D82. Sparse archaeological ornament, somber readable 16-bit pixel art.
```

## 极简复古 STYLE BLOCK（实际原文）

```text
Art direction: Minimal clean retro pixel game: large deliberate pixel clusters, flat plum cave silhouette layers, terracotta platforms with cream top edge, a few small mint mushrooms as decorative plants. Only about 12 to 16 total colors; two or three shades per material. No busy texture or glow. Compact cream-and-navy placeholder player. Bright cream far-right exit. Palette anchors #21182E #43243E #714052 #AA655C #DEAA7A #F3E7B7 #8ABF9E. Prioritize strong collision silhouettes, easily reusable modular art and clear mechanics.
```


## 复用方法

生成配套资源时不要复制上述“三个区块、中心缺失”的场景构图。选择一组 STYLE BLOCK，加上下列某个资源模板；在同一次生成中明确尺寸、朝向、数量、背景。将选定方案图片作为“风格参考图”附上可以提高一致性。模型生成的图集仍需切图和网格校正。

以下模板中的 [STYLE BLOCK] 替换为同一方案的完整英文画风描述。建议在全部资源中固定同一套像素比例、描边、调色板和光照方向。

### 通用配套资源提示词前缀

```text
Create a matching asset for the side-view 2D platform puzzle game Shattered Caverns.
Style reference: [STYLE BLOCK].
Strict orthographic side elevation, gravity down. Crisp square pixel clusters, hard edges, limited palette, discrete shading, no anti-aliasing, no smooth gradients, no top-down or isometric perspective, no text, logo or watermark.
Production scale target: 16 pixels per world unit. Preserve the chosen palette and pixel density across every asset. Use a genuinely transparent background for isolated sprites and tiles, with no painted checkerboard. Export concept art only; final tile alignment will be validated separately.
```

### 地形 Tile 图集

```text
Asset: modular cave terrain tile atlas. Use the common asset prefix and [STYLE BLOCK].
A 4 by 4 grid of exactly 16 equal 16x16-pixel tile cells on a logical 64x64-pixel canvas, no gutters. Row 1: isolated solid block, top surface, underside, interior fill. Row 2: left wall, right wall, top-left exterior corner, top-right exterior corner. Row 3: bottom-left exterior corner, bottom-right exterior corner, inner-left corner, inner-right corner. Row 4: ceiling variant, floor variant A, floor variant B, wall variant. Flat collision-facing surfaces snap to the cell edges. No slopes, player, objects, cast shadows extending outside cells, or baked lighting effects. Genuine transparent background for empty portions. Maintain consistent edge heights so repeated tiles join seamlessly. Validate corner coverage manually after generation.
```

### 单个完整区块

```text
Asset: exactly ONE complete square side-view cave chunk, 16x16 world units on a logical 256x256-pixel canvas.
Use the common asset prefix and [STYLE BLOCK]. Horizontal traversal corridor with rocky ceiling and floor. Left and right connections use the same 2-unit-tall clear opening above a floor surface at y=176 pixels from the top. Upper platform faces horizontal, all geometry snapped to a 16-pixel grid. Keep all terrain and decoration inside this square. No character, checkpoint, exit, collectible, UI, borders, arrows, or manipulation handles; these are separate objects. Render only environmental terrain and decor over transparent background. The connection height is a proposed art template, not a TDD rule.
```

### 临时角色与动画关键帧

角色形象尚未确定。先选择一张角色定稿，再将其作为身份参考生成各状态；仅靠提示词不能保证逐帧身份和动画连续性。

```text
Asset: provisional compact explorer sprite, orthographic side view facing right.
Use the common asset prefix and [STYLE BLOCK]. Neutral hooded silhouette with minimal facial details, exactly one world unit wide and one world unit tall, fit entire visible sprite within a 16x16-pixel cell, feet aligned to the bottom baseline. Generate ONE pose only: [idle standing / running contact pose / running passing pose / rising jump / falling]. Same outfit, palette, proportions, silhouette and pixel density as the attached approved character reference. Genuine transparent background, no floor, no environment, no shadow outside the cell. Do not make a tall 1x2-unit humanoid.
```

### 可拾取地图碎片

```text
Asset: one collectible torn parchment map fragment icon.
Use the common asset prefix and [STYLE BLOCK]. Fit inside a 16x16-pixel logical cell. Readable small torn parchment silhouette with a simple dark route mark and [amber diamond / cyan circle / violet triangle] group seal. Generate ONE fragment only, local piece [A / B / C] of its chosen group. Each piece differs by torn outline and route segment; keep group palette and seal identical. No numeric IDs or text. No baked sparkles, glow, floor or pickup animation; effects generated separately. Genuine transparent background. A/B/C are art variants, not a fixed required fragment count.
```

### 存档点与出口（外观概念）

```text
Asset: ONE [small carved-stone checkpoint marker / cave-mouth exit surround] for a side-view cave platformer.
Use the common asset prefix and [STYLE BLOCK]. Checkpoint target footprint 1x1 unit; exit surround target 2x3 units, with an unobstructed passage and clear ground baseline. These footprints are optional art proposals, not TDD requirements. Render state [inactive / active] as a visual concept only, consistent silhouette across states. No character, terrain, UI, interaction key prompt or invented gameplay. Transparent background. For exit art, render the stone surround only; daylight and background are separate layers.
```

### 平移与旋转交互提示（可选 UI 概念）

```text
Asset: pixel-art chunk manipulation indicator, ONE [horizontal double-arrow / clockwise curved corner arrow / dashed square slot border / compatible-connection marker].
Use the common asset prefix and [STYLE BLOCK]. Small simple amber silhouette, clearly readable over dark terrain, genuine transparent background, no terrain and no text. Keep line thickness consistent with the approved UI set. This is an interface concept, not a confirmed control scheme.
```

### 洞穴远景

```text
Asset: horizontally tileable distant cave background layer.
Use the common asset prefix and [STYLE BLOCK]. Side-view distant rock silhouettes and very sparse decorative mineral shapes, no traversable-looking platforms, no character or gameplay objects. Low contrast, darker and less saturated than environment terrain. Logical canvas 512x256 pixels. Left and right edges must tile seamlessly. No baked UI, light beams, fog over the player area, or scene-specific exit. Opaque background. Validate seam manually.
```

## 下一步资产一致性检查

固定同一方案后，先验证角色与一个实体块的 1×1 比例，再扩展 Tile 图集。以独立物件保存碎片、存档点和出口，以独立层保存特效和背景。用 Unity 实际跳跃和碰撞测试验证关卡，再确认区块整体旋转/平移时子物件的归属和可读性。

