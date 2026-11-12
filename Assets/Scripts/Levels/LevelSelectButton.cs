using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelSelectButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;
    private GameFlowController flow;
    private string levelId;

    public void Bind(GameFlowController controller, GameLevelDefinition level, int number)
    {
        flow = controller;
        levelId = level.id;
        bool unlocked = flow.SaveStore.IsUnlocked(levelId);
        string status = flow.SaveStore.IsCompleted(levelId) ? "已通关" : unlocked ? "可探索" : "未解锁";
        label.text = $"{number:00}   {level.displayName}                         {status}";
        button.interactable = unlocked;
        button.onClick.RemoveListener(Load);
        button.onClick.AddListener(Load);
    }

    private void Load()
    {
        flow.AudioPlayer.PlaySound(flow.Configuration.ButtonSound);
        flow.LoadLevel(levelId);
    }
}
