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
    [SerializeField] private Button retryButton;
    [SerializeField] private Image resultAccent;
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
    private Coroutine panelRevealRoutine;
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
                Color resultColor = completed
                    ? new Color(0.91f, 0.66f, 0.29f)
                    : new Color(0.78f, 0.38f, 0.34f);
                resultTitle.color = resultColor;
                if (resultAccent != null) resultAccent.color = resultColor;
                nextLevelButton.gameObject.SetActive(completed && flow.HasNextLevel());
                SetPanel(resultPanel);
                if (eventSystem != null)
                {
                    Button focus = nextLevelButton.gameObject.activeSelf ? nextLevelButton : retryButton;
                    if (focus != null) eventSystem.SetSelectedGameObject(focus.gameObject);
                }
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
                    interactionHint.color = new Color(0.5f, 0.87f, 0.72f);
                    break;
                case PlayerChunkInteractionState.Removing:
                    interactionHint.text = "拿下区块    F 确认  /  X 取消";
                    interactionHint.color = new Color(0.91f, 0.66f, 0.29f);
                    break;
                default:
                    interactionHint.text = "A / D 移动    空格 跳跃    F 区块操作    Esc 暂停";
                    interactionHint.color = new Color(0.78f, 0.86f, 0.84f);
                    break;
            }
        }
        else interactionHint.text = "";
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
        Button firstLevelButton = null;
        foreach (GameLevelDefinition level in flow.Configuration.Levels)
        {
            LevelSelectButton item = Instantiate(levelButtonPrefab, levelList);
            item.Bind(flow, level, number++);
            Button itemButton = item.GetComponent<Button>();
            if (firstLevelButton == null && itemButton.interactable) firstLevelButton = itemButton;
        }
        ScrollRect scroll = levelList.GetComponentInParent<ScrollRect>();
        if (scroll != null) scroll.scrollSensitivity = 28f;
        SetPanel(levelSelectPanel);
        if (eventSystem != null && firstLevelButton != null)
            eventSystem.SetSelectedGameObject(firstLevelButton.gameObject);
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
        if (eventSystem != null) eventSystem.SetSelectedGameObject(masterSlider.gameObject);
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
        if (panelRevealRoutine != null)
        {
            StopCoroutine(panelRevealRoutine);
            panelRevealRoutine = null;
        }
        ResetPanelPresentation(activePanel);
        mainMenuPanel.SetActive(panel == mainMenuPanel);
        levelSelectPanel.SetActive(panel == levelSelectPanel);
        settingsPanel.SetActive(panel == settingsPanel);
        pausePanel.SetActive(panel == pausePanel);
        resultPanel.SetActive(panel == resultPanel);
        activePanel = panel;
        CanvasGroup panelGroup = panel == null ? null : panel.GetComponent<CanvasGroup>();
        if (panelGroup != null)
        {
            panelGroup.alpha = 0f;
            panelGroup.interactable = true;
            panelGroup.blocksRaycasts = true;
            panelRevealRoutine = StartCoroutine(RevealPanel(panelGroup));
        }
        if (eventSystem != null)
        {
            Selectable first = panel == null ? null : panel.GetComponentInChildren<Selectable>();
            eventSystem.SetSelectedGameObject(first == null ? null : first.gameObject);
        }
    }

    private static void ResetPanelPresentation(GameObject panel)
    {
        if (panel == null) return;
        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        if (group == null) return;
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
    }

    private IEnumerator RevealPanel(CanvasGroup group)
    {
        const float duration = 0.12f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            group.alpha = 1f - (1f - t) * (1f - t);
            yield return null;
        }
        group.alpha = 1f;
        group.interactable = true;
        panelRevealRoutine = null;
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
