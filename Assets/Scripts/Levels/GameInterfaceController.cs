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
    [SerializeField] private TMP_Text continueLabel;
    [SerializeField] private Button nextLevelButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private Image resultAccent;
    [SerializeField] private TMP_Text resultTitle;
    [SerializeField] private TMP_Text resultDescription;
    [SerializeField] private TMP_FontAsset levelNumberFont;
    [SerializeField] private TMP_Text message;
    [SerializeField] private TMP_Text masterValue;
    [SerializeField] private TMP_Text musicValue;
    [SerializeField] private TMP_Text soundValue;
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider soundSlider;
    [SerializeField] private CanvasGroup transition;
    [SerializeField] private EventSystem eventSystem;
    [SerializeField] private Sprite levelMapBackground;
    [SerializeField] private Sprite levelTierBoard;
    [SerializeField] private Sprite[] levelEntrances;
    [SerializeField] private Sprite levelExplorer;
    [SerializeField] private LevelSelectMap.Layout tierLayout = new LevelSelectMap.Layout();

    private GameFlowController flow;
    private LevelSelectMap levelSelectMap;
    private GameObject activePanel;
    private Coroutine panelRevealRoutine;
    private float messageUntil;

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
        if (activePanel == levelSelectPanel) levelSelectMap?.Update();
    }

    public void ShowScene(bool isLevel)
    {
        hudPanel.SetActive(isLevel);
        SetPanel(isLevel ? null : mainMenuPanel);
        RefreshState();
    }

    private void RefreshState()
    {
        string lastLevelId = flow.SaveStore.Data.lastLevelId;
        GameLevelDefinition lastLevel = flow.Configuration.FindLevel(lastLevelId);
        continueButton.interactable = lastLevel != null && flow.SaveStore.IsUnlocked(lastLevelId);
        if (continueLabel != null)
        {
            continueLabel.color = continueButton.interactable
                ? new Color(0.9f, 0.94f, 0.91f)
                : new Color(0.56f, 0.53f, 0.49f);
        }
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

    public void StartGame() { ClickSound(); flow.StartGame(); }
    public void ContinueGame() { ClickSound(); flow.ContinueGame(); }
    public void RestartLevel() { ClickSound(); flow.RestartLevel(); }
    public void ReturnToMainMenu() { ClickSound(); flow.ReturnToMainMenu(); }
    public void NextLevel() { ClickSound(); flow.LoadNextLevel(); }
    public void QuitGame() { ClickSound(); flow.QuitGame(); }

    public void OpenLevelSelect()
    {
        ClickSound();
        if (levelSelectMap == null)
        {
            levelSelectMap = new LevelSelectMap((RectTransform)levelSelectPanel.transform, levelNumberFont,
                flow, tierLayout, levelMapBackground, levelTierBoard, levelEntrances, levelExplorer);
        }
        SetPanel(levelSelectPanel);
        Canvas.ForceUpdateCanvases();
        levelSelectMap.Open();
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
