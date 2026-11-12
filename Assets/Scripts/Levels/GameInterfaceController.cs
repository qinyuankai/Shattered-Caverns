using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GameInterfaceController : MonoBehaviour
{
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject levelSelectPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private GameObject hudPanel;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button nextLevelButton;
    [SerializeField] private TMP_Text resultTitle;
    [SerializeField] private TMP_Text resultDescription;
    [SerializeField] private TMP_Text levelTitle;
    [SerializeField] private TMP_Text interactionHint;
    [SerializeField] private TMP_Text fragmentProgress;
    [SerializeField] private TMP_Text message;
    [SerializeField] private TMP_Text masterValue;
    [SerializeField] private TMP_Text musicValue;
    [SerializeField] private TMP_Text soundValue;
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider soundSlider;
    [SerializeField] private Transform levelList;
    [SerializeField] private LevelSelectButton levelButtonPrefab;
    [SerializeField] private CanvasGroup transition;
    [SerializeField] private EventSystem eventSystem;

    private GameFlowController flow;
    private LevelManager levelManager;
    private PlayerChunkInteraction chunkInteraction;
    private GameObject activePanel;
    private float messageUntil;
    private float nextHudRefresh;

    public void Initialize(GameFlowController controller)
    {
        flow = controller;
        flow.StateChanged += RefreshState;
        masterSlider.onValueChanged.AddListener(SetMasterVolume);
        musicSlider.onValueChanged.AddListener(SetMusicVolume);
        soundSlider.onValueChanged.AddListener(SetSoundVolume);
        transition.alpha = 0f;
        transition.blocksRaycasts = false;
        message.text = "";
        SetPanel(null);
    }

    private void OnDestroy()
    {
        if (flow != null) flow.StateChanged -= RefreshState;
    }

    private void Update()
    {
        if (flow == null) return;
        if (EventSystem.current != null && EventSystem.current != eventSystem)
            eventSystem.enabled = false;
        else if (!eventSystem.enabled && FindObjectOfType<EventSystem>() == null)
            eventSystem.enabled = true;
        if (Time.unscaledTime > messageUntil) message.text = "";
        if (Input.GetKeyDown(KeyCode.Escape) && flow.State != GameFlowState.Loading)
        {
            if (activePanel == settingsPanel || activePanel == levelSelectPanel) Back();
            else if (flow.State == GameFlowState.Playing) OpenPause();
            else if (flow.State == GameFlowState.Paused) Resume();
        }
        if (flow.CurrentLevel != null && Time.unscaledTime >= nextHudRefresh)
        {
            nextHudRefresh = Time.unscaledTime + 0.15f;
            RefreshHud();
        }
    }

    public void ShowScene(bool isLevel)
    {
        levelManager = FindObjectOfType<LevelManager>();
        chunkInteraction = FindObjectOfType<PlayerChunkInteraction>();
        hudPanel.SetActive(isLevel);
        SetPanel(isLevel ? null : mainMenuPanel);
        if (isLevel) levelTitle.text = flow.CurrentLevel.displayName;
        RefreshState();
    }

    private void RefreshState()
    {
        continueButton.interactable = flow.Configuration.FindLevel(flow.SaveStore.Data.lastLevelId) != null
            && flow.SaveStore.IsUnlocked(flow.SaveStore.Data.lastLevelId);
        switch (flow.State)
        {
            case GameFlowState.MainMenu:
                SetPanel(mainMenuPanel);
                break;
            case GameFlowState.Playing:
                SetPanel(null);
                break;
            case GameFlowState.Paused:
                SetPanel(pausePanel);
                break;
            case GameFlowState.Completed:
            case GameFlowState.Failed:
                bool completed = flow.State == GameFlowState.Completed;
                resultTitle.text = completed ? "洞窟已贯通" : "探索暂时中断";
                resultDescription.text = completed
                    ? "关卡进度已保存。继续前行，或再次探索。"
                    : "重新开始本关，再试一种连接方式。";
                nextLevelButton.gameObject.SetActive(completed && flow.HasNextLevel());
                SetPanel(resultPanel);
                break;
            case GameFlowState.Loading:
                SetPanel(null);
                break;
        }
    }

    private void RefreshHud()
    {
        if (chunkInteraction != null)
        {
            switch (chunkInteraction.CurrentState)
            {
                case PlayerChunkInteractionState.Placing:
                    interactionHint.text = "放置区块    F 确认  /  Q 旋转  /  E 切换  /  X 取消";
                    break;
                case PlayerChunkInteractionState.Removing:
                    interactionHint.text = "拿下区块    F 确认  /  X 取消";
                    break;
                default:
                    interactionHint.text = "A / D 移动    空格 跳跃    F 区块操作    Esc 暂停";
                    break;
            }
        }
        if (levelManager == null)
        {
            fragmentProgress.text = "";
            return;
        }
        var text = new System.Text.StringBuilder($"可用区块  {levelManager.AvailableChunks.Count}");
        foreach (LevelChunkConfiguration item in levelManager.ChunkConfigurations)
        {
            if (item == null || item.chunk == null || !item.isMovable) continue;
            int id = item.chunk.ChunkId;
            string status = levelManager.IsChunkPlaced(id) ? "已放置"
                : levelManager.IsChunkAvailable(id) ? "可放置"
                : $"碎片 {levelManager.GetCollectedFragmentCount(id)}/{item.requiredFragmentCount}";
            text.Append($"\n区块 {id:00}  ·  {status}");
        }
        fragmentProgress.text = text.ToString();
    }

    public void StartGame() { ClickSound(); flow.StartGame(); }
    public void ContinueGame() { ClickSound(); flow.ContinueGame(); }
    public void RestartLevel() { ClickSound(); flow.RestartLevel(); }
    public void ReturnToMainMenu() { ClickSound(); flow.ReturnToMainMenu(); }
    public void NextLevel() { ClickSound(); flow.LoadNextLevel(); }
    public void QuitGame() { ClickSound(); flow.QuitGame(); }

    public void OpenLevelSelect()
    {
        ClickSound();
        foreach (Transform child in levelList) Destroy(child.gameObject);
        int number = 1;
        foreach (GameLevelDefinition level in flow.Configuration.Levels)
            Instantiate(levelButtonPrefab, levelList).Bind(flow, level, number++);
        SetPanel(levelSelectPanel);
    }

    public void OpenSettings()
    {
        ClickSound();
        if (flow.State == GameFlowState.Playing) flow.PauseGame();
        masterSlider.SetValueWithoutNotify(flow.AudioPlayer.MasterVolume);
        musicSlider.SetValueWithoutNotify(flow.AudioPlayer.MusicVolume);
        soundSlider.SetValueWithoutNotify(flow.AudioPlayer.SoundVolume);
        RefreshVolumeLabels();
        SetPanel(settingsPanel);
    }

    public void OpenPause()
    {
        ClickSound();
        flow.PauseGame();
    }

    public void Resume()
    {
        ClickSound();
        flow.ResumeGame();
        SetPanel(null);
    }

    public void Back()
    {
        ClickSound();
        flow.SaveStore.Save();
        SetPanel(flow.State == GameFlowState.Paused ? pausePanel : mainMenuPanel);
    }

    public void SetMasterVolume(float value)
    {
        flow.AudioPlayer.SetMasterVolume(value);
        RefreshVolumeLabels();
    }

    public void SetMusicVolume(float value)
    {
        flow.AudioPlayer.SetMusicVolume(value);
        RefreshVolumeLabels();
    }

    public void SetSoundVolume(float value)
    {
        flow.AudioPlayer.SetSoundVolume(value);
        RefreshVolumeLabels();
    }

    private void RefreshVolumeLabels()
    {
        masterValue.text = $"{flow.AudioPlayer.MasterVolume:P0}";
        musicValue.text = $"{flow.AudioPlayer.MusicVolume:P0}";
        soundValue.text = $"{flow.AudioPlayer.SoundVolume:P0}";
    }

    private void ClickSound()
    {
        flow.AudioPlayer.PlaySound(flow.Configuration.ButtonSound);
    }

    private void SetPanel(GameObject panel)
    {
        mainMenuPanel.SetActive(panel == mainMenuPanel);
        levelSelectPanel.SetActive(panel == levelSelectPanel);
        settingsPanel.SetActive(panel == settingsPanel);
        pausePanel.SetActive(panel == pausePanel);
        resultPanel.SetActive(panel == resultPanel);
        activePanel = panel;
        if (eventSystem != null)
        {
            Selectable first = panel == null ? null : panel.GetComponentInChildren<Selectable>();
            eventSystem.SetSelectedGameObject(first == null ? null : first.gameObject);
        }
    }

    public void ShowMessage(string text)
    {
        message.text = text;
        messageUntil = Time.unscaledTime + 4f;
    }

    public IEnumerator FadeTo(float alpha, float duration)
    {
        transition.blocksRaycasts = true;
        float start = transition.alpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            transition.alpha = Mathf.Lerp(start, alpha, duration <= 0f ? 1f : elapsed / duration);
            yield return null;
        }
        transition.alpha = alpha;
        transition.blocksRaycasts = alpha > 0f;
    }
}
