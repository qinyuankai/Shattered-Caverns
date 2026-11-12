# 人物参考与场景替换提示词

使用内置 image_gen 编辑原四套示意图。原图保留，新图文件名附加 -character。Image 1 为场景编辑目标，Image 2 为 character-reference.jpg 人物身份参考。

## 实际编辑提示词

Use case: identity-preserve, precise-object-edit.
Image 1 is the EDIT TARGET: existing panoramic side-view pixel-art cave gameplay illustration.
Image 2 is CHARACTER IDENTITY REFERENCE ONLY: brown-haired explorer on white.
Edit ONLY the tiny placeholder player standing on the left chunk. Replace it with the character from Image 2, faithfully adapted to a small in-game pixel sprite facing RIGHT in a relaxed standing pose. Preserve recognizable chestnut bobbed shaggy hair, peach skin, bright red scarf with short trailing tails behind to the left, teal short-sleeved tunic, brown belt with golden square buckle, dark shorts, brown boots with cream cuffs. Keep natural chibi proportions from reference, simplifying facial and clothing details for tiny sprite readability, do not stretch the reference into a square. Fit the ENTIRE sprite including scarf inside a 1x1 world-unit bounding box, roughly 1/16 of a square chunk's height, the same gameplay footprint as the old placeholder. Boots must touch the original floor baseline, character stays at the original horizontal position. Red scarf and teal tunic remain identifiable across palettes; adapt only lighting and pixel density to target scene.
Invariants: preserve the original canvas aspect and size, exact three-chunk horizontal layout, completely missing center chunk, dashed square border, grid, rotation arrows, horizontal double arrow, fragment collectible, checkpoint, terrain, steps, cave walls, decor, exit, colors and lighting outside the player region. Do not add any extra character, portrait, inset, HUD, text or reference white background. NO enlargement of the character to showcase it. No other scene changes. Crisp pixel edges, no blur, no antialiasing.

## 配套角色素材身份段落

用此段替换原 prompt-guide.md 中 neutral hooded placeholder 等临时角色描述；搭配已选画风与角色动作模板，并附上 character-reference.jpg。不要将场景三地图块构图复制到单个角色素材中。

```text
Character identity: match the attached approved pixel-art explorer reference. Chestnut shaggy bobbed hair, peach skin, red scarf with short trailing tails, teal short-sleeved tunic, brown belt with a golden square buckle, dark shorts, brown boots with cream cuffs. Preserve these identity colors across environments. Chibi proportions, side view facing right, same outfit and consistent anatomy in every frame. Fit the full silhouette and scarf inside the chosen 1x1 world-unit sprite cell without stretching anatomy. Simplify detail to fit the chosen pixel resolution. No hood, helmet, weapon or added accessories. For isolated sprites use genuine transparency, no white backdrop and no baked environment. Generate the requested single pose while maintaining the approved feet baseline.
```

人物完整参考的细节多于 16×16 像素能稳定表达的细节；若制作精灵时难以辨识，可统一提高每单位像素数，同时保持游戏占地 1×1 不变。示意图不用于直接确定碰撞尺寸。
