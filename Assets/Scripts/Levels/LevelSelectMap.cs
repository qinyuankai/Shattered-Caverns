using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

internal sealed class LevelSelectMap
{
    [System.Serializable]
    internal struct Platform
    {
        public float left;
        public float right;
        public float surface;

        public Platform(float left, float right, float surface)
        {
            this.left = left;
            this.right = right;
            this.surface = surface;
        }
    }

    [System.Serializable]
    internal sealed class Layout
    {
        [Min(1)] public float entranceWidth = 134f;
        [Min(1)] public float walkSpeed = 205f;
        [Min(1)] public float gravity = 950f;
        public Platform[] platforms =
        {
            new Platform(6, 635, 574), new Platform(168, 806, 422),
            new Platform(6, 635, 263), new Platform(6, 806, 94)
        };
    }

    private const int DoorsPerRow = 3;
    private const float ExplorerHeight = 58f;
    private const float EntryDuration = 0.28f;
    private static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);
    private readonly GameFlowController flow;
    private readonly Layout layout;
    private readonly RectTransform viewport;
    private readonly RectTransform board;
    private readonly RectTransform content;
    private readonly RectTransform player;
    private readonly Platform[] platforms;
    private readonly float initialTop;
    private readonly float bottom;
    private readonly Image[] doors;
    private readonly TMP_Text[] numbers;
    private readonly Image[] completionMarks;
    private float cameraOffset;
    private float cameraVelocity;
    private int floor;
    private int nearby = -1;
    private int entering = -1;
    private float entryTime;
    private float x;
    private float feet;
    private float fallSpeed;
    private float facing = 1f;

    public LevelSelectMap(RectTransform panel, TMP_FontAsset font, GameFlowController flow, Layout layout,
        Sprite backdrop, Sprite mapArt, Sprite[] entrances, Sprite explorer)
    {
        this.flow = flow;
        this.layout = layout;
        viewport = panel;

        Image background = Picture(panel, "Backdrop", backdrop, Vector2.zero);
        background.transform.SetAsFirstSibling();
        AspectRatioFitter cover = background.gameObject.AddComponent<AspectRatioFitter>();
        cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        cover.aspectRatio = backdrop.rect.width / backdrop.rect.height;

        Image frame = Picture(panel, "TierMap", null, mapArt.rect.size);
        frame.color = new Color32(161, 112, 78, 255);
        board = frame.rectTransform;
        RectTransform window = Rect(board, "Window", Vector2.one * -6f);
        window.anchorMin = Vector2.zero;
        window.anchorMax = Vector2.one;
        window.gameObject.AddComponent<RectMask2D>();
        content = Rect(window, "Route", Vector2.zero);
        content.anchorMin = content.anchorMax = content.pivot = Vector2.zero;

        int count = flow.Configuration.Levels.Count;
        platforms = new Platform[Mathf.Max(layout.platforms.Length, Mathf.CeilToInt(count / (float)DoorsPerRow))];
        Rect artRect = mapArt.rect;
        initialTop = artRect.height - 4f;
        bottom = BuildTerrain(mapArt);
        doors = new Image[count];
        numbers = new TMP_Text[count];
        completionMarks = new Image[count];
        var variation = new System.Random(7549);
        var recent = new Queue<int>();
        var uses = new int[entrances.Length];
        var candidates = new List<int>(entrances.Length);
        for (int i = 0; i < count; i++)
        {
            int leastUsed = int.MaxValue;
            candidates.Clear();
            for (int j = 0; j < entrances.Length; j++)
            {
                if (recent.Contains(j) || uses[j] > leastUsed) continue;
                if (uses[j] < leastUsed) candidates.Clear();
                leastUsed = uses[j];
                candidates.Add(j);
            }
            int variant = candidates[variation.Next(candidates.Count)];
            uses[variant]++;
            recent.Enqueue(variant);
            if (recent.Count > Mathf.Min(3, entrances.Length - 1)) recent.Dequeue();

            Sprite mouth = entrances[variant];
            float width = layout.entranceWidth * Mathf.Lerp(0.91f, 1.07f, (float)variation.NextDouble());
            Vector2 size = new Vector2(width, width * mouth.rect.height / mouth.rect.width);
            int row = i / DoorsPerRow;
            int column = i % DoorsPerRow;
            Platform ledge = layout.platforms[RoutePlatform(row)];
            float along = (column + 0.68f) / (DoorsPerRow + 0.18f);
            if (row % 2 == 1) along = 1f - along;
            Image door = Picture(content, "Door_" + i.ToString("00"), mouth, size);
            doors[i] = door;
            RectTransform rect = door.rectTransform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = BottomCenter;
            rect.anchoredPosition = new Vector2(Mathf.Lerp(ledge.left, ledge.right, along), platforms[row].surface);

            TMP_Text label = Label(rect, "Number", font, new Vector2(44f, 32f), 24f);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = BottomCenter;
            label.rectTransform.anchoredPosition = new Vector2(width * 0.45f + 9f, 17f);
            label.text = i.ToString("00");
            numbers[i] = label;
            Image mark = Picture(label.transform, "Completed", null, new Vector2(6f, 10f));
            mark.rectTransform.anchoredPosition = new Vector2(0f, 19f);
            mark.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -32f);
            mark.color = new Color32(255, 202, 114, 255);
            completionMarks[i] = mark;
        }

        player = Picture(content, "Explorer", explorer,
            new Vector2(ExplorerHeight * explorer.rect.width / explorer.rect.height, ExplorerHeight)).rectTransform;
        player.anchorMin = player.anchorMax = Vector2.zero;
        player.pivot = BottomCenter;
    }

    private int RoutePlatform(int row)
    {
        if (row == 0) return 0;
        return row % 2 == 0 ? 2 : 1;
    }

    private float BuildTerrain(Sprite art)
    {
        const float bandHeight = 160f;
        const float belowSurface = 54f;
        const float inset = 4f;
        Platform[] samples = layout.platforms;
        Rect rect = art.rect;
        Texture texture = art.texture;
        float interiorWidth = rect.width - 2f * inset;
        float top = initialTop;
        for (int row = 0; row < platforms.Length; row++)
        {
            bool last = row == platforms.Length - 1;
            Platform sample = samples[last ? samples.Length - 1 : RoutePlatform(row)];
            float sourceTop = row == 0 && !last ? initialTop : sample.surface + bandHeight - belowSurface;
            float sourceBottom = last ? inset : sample.surface - belowSurface;
            float height = sourceTop - sourceBottom;
            float surface = top - (sourceTop - sample.surface);
            platforms[row] = new Platform(sample.left, sample.right, surface);

            RectTransform band = Rect(content, "RockTier_" + (row + 1), new Vector2(interiorWidth, height));
            band.anchorMin = band.anchorMax = band.pivot = Vector2.zero;
            band.anchoredPosition = new Vector2(inset, top - height);
            RawImage rock = band.gameObject.AddComponent<RawImage>();
            rock.texture = texture;
            rock.uvRect = new Rect((rect.x + inset) / texture.width, (rect.y + sourceBottom) / texture.height,
                interiorWidth / texture.width, height / texture.height);
            rock.raycastTarget = false;
            top -= height;
        }
        return top;
    }

    public void Open()
    {
        int last = -1;
        nearby = entering = -1;
        for (int i = 0; i < doors.Length; i++)
        {
            StyleDoor(i, false);
            if (flow.Configuration.Levels[i].id == flow.SaveStore.Data.lastLevelId) last = i;
        }
        FitBoard();
        if (last < 0) ResetToStart();
        else Place(doors[last].rectTransform.anchoredPosition.x, last / DoorsPerRow);
    }

    private void FitBoard()
    {
        float scale = Mathf.Min(viewport.rect.width / (board.sizeDelta.x + 44f), viewport.rect.height / 941f);
        board.localScale = new Vector3(scale, scale, 1f);
        board.anchoredPosition = new Vector2(0f, viewport.rect.height * 0.012f);
    }

    private void ResetToStart() => Place(platforms[0].left + 47f, 0);

    private void Follow(float dt)
    {
        float visibleHeight = board.sizeDelta.y - 6f;
        float start = initialTop - visibleHeight;
        float target = Mathf.Clamp(start + visibleHeight * 0.42f - feet, 0f, Mathf.Max(0f, start - bottom));
        if (dt > 0f)
            cameraOffset = Mathf.SmoothDamp(cameraOffset, target, ref cameraVelocity, 0.12f, Mathf.Infinity, dt);
        else
        {
            cameraOffset = target;
            cameraVelocity = 0f;
        }
        content.anchoredPosition = new Vector2(-3f, cameraOffset - start);
    }

    private void Place(float startX, int row)
    {
        x = startX;
        feet = platforms[row].surface;
        floor = row;
        fallSpeed = 0f;
        facing = 1f;
        Move(0f, 0f);
    }

    public void Update()
    {
        FitBoard();
        float dt = Time.unscaledDeltaTime;
        if (entering >= 0)
        {
            AdvanceEntry(dt);
            return;
        }

        if (Input.GetKeyDown(KeyCode.R)) ResetToStart();
        bool left = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        bool right = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
        Move((right ? 1 : 0) - (left ? 1 : 0), Mathf.Min(dt, 0.1f));
        if (Input.GetKeyDown(KeyCode.F)) TryEnter();
    }

    private void Move(float direction, float dt)
    {
        if (direction != 0f) facing = Mathf.Sign(direction);
        float margin = player.sizeDelta.x * 0.5f + 6f;
        x = Mathf.Clamp(x + direction * layout.walkSpeed * dt, margin, board.sizeDelta.x - margin);
        float nextFeet = feet - fallSpeed * dt - 0.5f * layout.gravity * dt * dt;
        fallSpeed += layout.gravity * dt;
        floor = -1;
        for (int row = 0; row < platforms.Length; row++)
        {
            Platform ledge = platforms[row];
            if (feet < ledge.surface || nextFeet > ledge.surface || x < ledge.left || x > ledge.right) continue;
            nextFeet = ledge.surface;
            fallSpeed = 0f;
            floor = row;
            break;
        }
        feet = nextFeet;
        player.anchoredPosition = new Vector2(x, feet);
        player.localScale = new Vector3(facing, 1f, 1f);

        Follow(dt);
        int closest = -1;
        if (floor >= 0)
        {
            int first = floor * DoorsPerRow;
            for (int i = first; i < Mathf.Min(first + DoorsPerRow, doors.Length); i++)
            {
                if (Mathf.Abs(x - doors[i].rectTransform.anchoredPosition.x) > doors[i].rectTransform.sizeDelta.x * 0.22f) continue;
                closest = i;
                break;
            }
        }
        if (closest == nearby) return;
        if (nearby >= 0) StyleDoor(nearby, false);
        nearby = closest;
        if (nearby >= 0) StyleDoor(nearby, true);
    }

    private void StyleDoor(int index, bool focus)
    {
        string id = flow.Configuration.Levels[index].id;
        bool unlocked = flow.SaveStore.IsUnlocked(id);
        doors[index].color = !unlocked ? new Color32(169, 155, 154, 255)
            : focus ? Color.white : new Color32(223, 210, 195, 255);
        numbers[index].color = !unlocked ? new Color32(186, 174, 164, 255)
            : focus ? new Color32(255, 223, 157, 255) : new Color32(244, 217, 180, 255);
        completionMarks[index].gameObject.SetActive(flow.SaveStore.IsCompleted(id));
    }

    private void TryEnter()
    {
        if (nearby < 0 || !flow.SaveStore.IsUnlocked(flow.Configuration.Levels[nearby].id)) return;
        entering = nearby;
        entryTime = 0f;
        flow.AudioPlayer.PlaySound(flow.Configuration.ButtonSound);
    }

    private void AdvanceEntry(float dt)
    {
        entryTime += dt;
        float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(entryTime / EntryDuration));
        float scale = Mathf.Lerp(1f, 0.64f, t);
        player.localScale = new Vector3(facing * scale, scale, 1f);
        Vector2 mouth = doors[entering].rectTransform.anchoredPosition;
        player.anchoredPosition = Vector2.Lerp(new Vector2(x, feet), mouth + Vector2.up * 12f, t);
        if (entryTime < EntryDuration) return;
        int index = entering;
        entering = -1;
        flow.LoadLevel(flow.Configuration.Levels[index].id);
    }

    private static TMP_Text Label(Transform parent, string name, TMP_FontAsset font, Vector2 size, float fontSize)
    {
        TMP_Text label = Rect(parent, name, size).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Normal;
        label.enableWordWrapping = false;
        label.color = new Color32(242, 215, 180, 255);
        label.raycastTarget = false;
        return label;
    }

    private static RectTransform Rect(Transform parent, string name, Vector2 size)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
        return rect;
    }

    private static Image Picture(Transform parent, string name, Sprite sprite, Vector2 size)
    {
        Image image = Rect(parent, name, size).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }
}
