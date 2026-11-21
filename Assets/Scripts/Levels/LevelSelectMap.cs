using TMPro;
using UnityEngine;
using UnityEngine.UI;

internal sealed class LevelSelectMap
{
    private const int GridSize = 16;
    private const int DoorsPerFloor = 3;
    private const int Floors = 4;
    private const int DoorsPerPage = DoorsPerFloor * Floors;
    private const float TileSize = 28f;
    private const float DoorReach = 0.72f;

    private static readonly Color Gold = new Color(0.91f, 0.66f, 0.29f);
    private static readonly Color Teal = new Color(0.42f, 0.82f, 0.74f);
    private static readonly Color Muted = new Color(0.62f, 0.70f, 0.73f);
    private static readonly Color Rock = new Color(0.26f, 0.31f, 0.34f);

    private readonly GameFlowController flow;
    private readonly RectTransform player;
    private readonly TMP_Text status;
    private readonly TMP_Text hint;
    private readonly Image[] doors = new Image[DoorsPerPage];
    private readonly TMP_Text[] numbers = new TMP_Text[DoorsPerPage];
    private float x;
    private float feet;
    private float fallSpeed;
    private int page;
    private int floor;
    private int nearbyDoor = -1;

    public LevelSelectMap(Transform parent, TMP_FontAsset font, GameFlowController flow)
    {
        this.flow = flow;
        RectTransform frame = CreateImage(parent, "LevelMap", new Vector2(0, -30),
            Vector2.one * (GridSize * TileSize + 6f), Rock).rectTransform;
        RectTransform map = CreateImage(frame, "Cavern", Vector2.zero, Vector2.one * (GridSize * TileSize),
            new Color(0.026f, 0.055f, 0.072f)).rectTransform;

        for (int row = 0; row < Floors; row++)
        {
            int left = LeftEdge(row);
            int right = RightEdge(row);
            for (int col = left; col < right; col++)
                CreateImage(map, "PlatformTile", GridPosition(col + 0.5f, FloorY(row) + 0.5f),
                    Vector2.one * (TileSize - 1f), Rock);

            for (int slot = 0; slot < DoorsPerFloor; slot++)
            {
                int index = row * DoorsPerFloor + slot;
                Vector2 position = GridPosition(DoorX(index), FloorY(row)) + new Vector2(0, 19);
                doors[index] = CreateImage(map, "Door", position, new Vector2(24, 38), Muted);
                CreateImage(doors[index].transform, "DoorPanel", new Vector2(0, -1), new Vector2(18, 32),
                    new Color(0.05f, 0.09f, 0.11f));
                numbers[index] = CreateText(doors[index].transform, "Number", font, Vector2.zero,
                    new Vector2(22, 26), 13);
            }
        }

        player = CreateImage(map, "Explorer", Vector2.zero, new Vector2(16, 13), Teal).rectTransform;
        CreateImage(player, "Head", new Vector2(0, 10), new Vector2(12, 10), new Color(0.92f, 0.86f, 0.71f));
        CreateImage(player, "Face", new Vector2(1, 10), new Vector2(7, 2), Rock);
        status = CreateText(parent, "DoorStatus", font, new Vector2(0, -272), new Vector2(720, 32), 20);
        hint = CreateText(parent, "MapControls", font, new Vector2(0, 225), new Vector2(900, 40), 20);
    }

    public void Open()
    {
        page = 0;
        ShowPage();
    }

    public void Update()
    {
        int pages = Mathf.Max(1, Mathf.CeilToInt(flow.Configuration.Levels.Count / (float)DoorsPerPage));
        if (Input.GetKeyDown(KeyCode.PageDown) && page + 1 < pages)
        {
            page++;
            ShowPage();
        }
        else if (Input.GetKeyDown(KeyCode.PageUp) && page > 0)
        {
            page--;
            ShowPage();
        }
        if (Input.GetKeyDown(KeyCode.R)) ResetExplorer();

        bool left = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        bool right = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
        Move((right ? 1 : 0) - (left ? 1 : 0), Mathf.Min(Time.unscaledDeltaTime, 0.05f));

        int door = FindNearbyDoor();
        if (door != nearbyDoor) SetNearbyDoor(door);
        if (door < 0 || !Input.GetKeyDown(KeyCode.F)) return;

        GameLevelDefinition level = flow.Configuration.Levels[page * DoorsPerPage + door];
        if (!flow.SaveStore.IsUnlocked(level.id)) return;
        flow.AudioPlayer.PlaySound(flow.Configuration.ButtonSound);
        flow.LoadLevel(level.id);
    }

    private void ShowPage()
    {
        int count = flow.Configuration.Levels.Count;
        int pages = Mathf.Max(1, Mathf.CeilToInt(count / (float)DoorsPerPage));
        hint.text = "A / D 或方向键移动    F 进门    R 回到起点";
        if (pages > 1) hint.text += $"    PgUp / PgDn 翻页  {page + 1}/{pages}";
        for (int slot = 0; slot < doors.Length; slot++)
        {
            int index = page * DoorsPerPage + slot;
            doors[slot].gameObject.SetActive(index < count);
            if (index >= count) continue;
            numbers[slot].text = $"{index + 1:00}";
            StyleDoor(slot, false);
        }
        ResetExplorer();
    }

    private void ResetExplorer()
    {
        x = 1f;
        feet = 1.25f;
        fallSpeed = 0f;
        floor = -1;
        player.anchoredPosition = GridPosition(x, feet) + Vector2.up * 6.5f;
        SetNearbyDoor(-1);
    }

    private void Move(float direction, float deltaTime)
    {
        x = Mathf.Clamp(x + direction * 5f * deltaTime, 0.35f, GridSize - 0.35f);
        fallSpeed += 26f * deltaTime;
        float nextFeet = feet + fallSpeed * deltaTime;
        floor = -1;
        for (int row = 0; row < Floors; row++)
        {
            float top = FloorY(row);
            if (feet > top || nextFeet < top || x < LeftEdge(row) || x >= RightEdge(row)) continue;
            nextFeet = top;
            fallSpeed = 0;
            floor = row;
            break;
        }
        feet = nextFeet;
        player.anchoredPosition = GridPosition(x, feet) + Vector2.up * 6.5f;
    }

    private int FindNearbyDoor()
    {
        if (floor < 0) return -1;
        for (int slot = floor * DoorsPerFloor; slot < (floor + 1) * DoorsPerFloor; slot++)
            if (doors[slot].gameObject.activeSelf && Mathf.Abs(x - DoorX(slot)) <= DoorReach)
                return slot;
        return -1;
    }

    private void SetNearbyDoor(int slot)
    {
        if (nearbyDoor >= 0 && doors[nearbyDoor].gameObject.activeSelf)
            StyleDoor(nearbyDoor, false);
        nearbyDoor = slot;
        if (slot < 0)
        {
            status.text = flow.Configuration.Levels.Count == 0 ? "暂无关卡" : "走到门前查看关卡";
            return;
        }
        StyleDoor(slot, true);
        int index = page * DoorsPerPage + slot;
        GameLevelDefinition level = flow.Configuration.Levels[index];
        string action = !flow.SaveStore.IsUnlocked(level.id) ? "未解锁"
            : flow.SaveStore.IsCompleted(level.id) ? "已通关 · 按 F 重玩" : "按 F 进入";
        status.text = $"{index + 1:00}  {level.displayName}    {action}";
    }

    private void StyleDoor(int slot, bool nearby)
    {
        string id = flow.Configuration.Levels[page * DoorsPerPage + slot].id;
        bool unlocked = flow.SaveStore.IsUnlocked(id);
        Color color = !unlocked ? Muted : nearby || flow.SaveStore.IsCompleted(id) ? Gold : Teal;
        doors[slot].color = Color.Lerp(new Color(0.10f, 0.15f, 0.17f), color, nearby ? 1f : 0.72f);
        numbers[slot].color = color;
    }

    private static int FloorY(int row) => 3 + row * 4;
    private static int LeftEdge(int row) => row == Floors - 1 || row % 2 == 0 ? 0 : 5;
    private static int RightEdge(int row) => row == Floors - 1 || row % 2 == 1 ? GridSize : 11;

    private static float DoorX(int index)
    {
        int row = index / DoorsPerFloor;
        float fromSide = 2.5f + index % DoorsPerFloor * 3f;
        return row % 2 == 0 ? fromSide : GridSize - fromSide;
    }

    private static Vector2 GridPosition(float column, float row)
    {
        return new Vector2(column - GridSize / 2f, GridSize / 2f - row) * TileSize;
    }

    private static RectTransform CreateRect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static Image CreateImage(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        Image image = CreateRect(parent, name, position, size).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text CreateText(Transform parent, string name, TMP_FontAsset font, Vector2 position,
        Vector2 size, float fontSize)
    {
        TMP_Text text = CreateRect(parent, name, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Midline;
        text.color = Muted;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }
}
