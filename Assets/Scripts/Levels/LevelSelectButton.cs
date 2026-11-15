using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LevelSelectButton : MonoBehaviour, ISelectHandler
{
    private static readonly Color CompletedBackground = new Color(0.22f, 0.17f, 0.1f);
    private static readonly Color AvailableBackground = new Color(0.075f, 0.13f, 0.16f);
    private static readonly Color LockedBackground = new Color(0.055f, 0.08f, 0.095f);
    private static readonly Color CompletedText = new Color(0.91f, 0.66f, 0.29f);
    private static readonly Color AvailableText = new Color(0.9f, 0.94f, 0.91f);
    private static readonly Color LockedText = new Color(0.62f, 0.7f, 0.7f);

    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;
    private GameFlowController flow;
    private string levelId;

    public void Bind(GameFlowController controller, GameLevelDefinition level, int number)
    {
        flow = controller;
        levelId = level.id;
        bool unlocked = flow.SaveStore.IsUnlocked(levelId);
        bool completed = flow.SaveStore.IsCompleted(levelId);
        string status = completed ? "已通关" : unlocked ? "可探索" : "未解锁";
        label.text = $"{number:00}  {level.displayName}  ·  {status}";
        label.enableWordWrapping = false;
        label.enableAutoSizing = true;
        label.fontSizeMin = 18f;
        label.color = completed ? CompletedText : unlocked ? AvailableText : LockedText;
        button.targetGraphic.color = completed ? CompletedBackground : unlocked ? AvailableBackground : LockedBackground;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = unlocked ? new Color(0.72f, 0.95f, 0.88f) : Color.white;
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.5f, 0.72f, 0.66f);
        colors.disabledColor = Color.white;
        button.colors = colors;
        button.interactable = unlocked;
        button.onClick.RemoveListener(Load);
        if (unlocked) button.onClick.AddListener(Load);
    }

    private void Load()
    {
        flow.AudioPlayer.PlaySound(flow.Configuration.ButtonSound);
        flow.LoadLevel(levelId);
    }

    public void OnSelect(BaseEventData eventData)
    {
        ScrollRect scroll = GetComponentInParent<ScrollRect>();
        RectTransform item = transform as RectTransform;
        if (scroll == null || scroll.viewport == null || scroll.content == null || item == null) return;

        Canvas.ForceUpdateCanvases();
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, item);
        Rect viewport = scroll.viewport.rect;
        float correction = bounds.max.y > viewport.yMax
            ? viewport.yMax - bounds.max.y
            : bounds.min.y < viewport.yMin ? viewport.yMin - bounds.min.y : 0f;
        if (Mathf.Approximately(correction, 0f)) return;

        Vector2 position = scroll.content.anchoredPosition;
        position.y += correction;
        scroll.content.anchoredPosition = position;
        scroll.StopMovement();
    }
}
