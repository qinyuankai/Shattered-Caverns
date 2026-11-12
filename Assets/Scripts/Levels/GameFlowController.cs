using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameFlowState
{
    MainMenu,
    Playing,
    Paused,
    Completed,
    Failed,
    Loading
}

[DefaultExecutionOrder(-200)]
public class GameFlowController : MonoBehaviour
{
    [SerializeField] private GameConfiguration configuration;
    [SerializeField] private GameAudioPlayer audioPlayer;
    [SerializeField] private GameInterfaceController gameInterface;

    public static GameFlowController Instance { get; private set; }
    public GameFlowState State { get; private set; }
    public GameConfiguration Configuration => configuration;
    public GameSaveStore SaveStore { get; private set; }
    public GameAudioPlayer AudioPlayer => audioPlayer;
    public GameLevelDefinition CurrentLevel { get; private set; }
    public bool BlocksGameplayInput => State != GameFlowState.Playing;
    public event Action StateChanged;

    private bool isLoading;
    private PlayerMovement playerMovement;
    private PlayerChunkInteraction chunkInteraction;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        GameObject prefab = Resources.Load<GameObject>("GameApplication");
        if (prefab != null) Instantiate(prefab);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SaveStore = new GameSaveStore(configuration);
        audioPlayer.Initialize(SaveStore);
        gameInterface.Initialize(this);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this)
        {
            Time.timeScale = 1f;
            Instance = null;
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) SaveStore?.Save();
    }

    private void OnApplicationQuit()
    {
        SaveStore?.Save();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindScene(scene);
    }

    private void BindScene(Scene scene)
    {
        playerMovement = FindObjectOfType<PlayerMovement>();
        chunkInteraction = FindObjectOfType<PlayerChunkInteraction>();
        CurrentLevel = configuration.FindScene(scene.path);
        Time.timeScale = 1f;
        if (CurrentLevel != null)
        {
            SaveStore.Unlock(CurrentLevel.id);
            SaveStore.Data.lastLevelId = CurrentLevel.id;
            SaveStore.Save();
        }

        audioPlayer.PlayMusic(CurrentLevel == null ? configuration.MenuMusic : CurrentLevel.music);
        SetState(isLoading ? GameFlowState.Loading : CurrentLevel == null ? GameFlowState.MainMenu : GameFlowState.Playing);
        gameInterface.ShowScene(CurrentLevel != null);
    }

    public void StartGame()
    {
        if (configuration.Levels.Count > 0) LoadLevel(configuration.Levels[0].id);
    }

    public void ContinueGame()
    {
        string id = SaveStore.Data.lastLevelId;
        if (configuration.FindLevel(id) != null && SaveStore.IsUnlocked(id)) LoadLevel(id);
    }

    public void LoadLevel(string levelId)
    {
        GameLevelDefinition level = configuration.FindLevel(levelId);
        if (level != null && SaveStore.IsUnlocked(levelId)) BeginLoad(level.scenePath);
    }

    public void ReturnToMainMenu()
    {
        BeginLoad(configuration.MainMenuScenePath);
    }

    public void RestartLevel()
    {
        if (CurrentLevel != null) BeginLoad(CurrentLevel.scenePath);
    }

    public void LoadNextLevel()
    {
        if (CurrentLevel == null) return;
        for (int i = 0; i < configuration.Levels.Count - 1; i++)
        {
            if (configuration.Levels[i].id == CurrentLevel.id)
            {
                LoadLevel(configuration.Levels[i + 1].id);
                return;
            }
        }
        ReturnToMainMenu();
    }

    public bool HasNextLevel()
    {
        return CurrentLevel != null && configuration.Levels.Count > 0
            && configuration.Levels[configuration.Levels.Count - 1].id != CurrentLevel.id;
    }

    public void PauseGame()
    {
        if (State != GameFlowState.Playing) return;
        SetState(GameFlowState.Paused);
        Time.timeScale = 0f;
        if (playerMovement != null) playerMovement.SetInputEnabled(false);
    }

    public void ResumeGame()
    {
        if (State != GameFlowState.Paused) return;
        bool isInteracting = chunkInteraction != null
            && chunkInteraction.CurrentState != PlayerChunkInteractionState.Normal;
        Time.timeScale = isInteracting ? 0f : 1f;
        if (playerMovement != null) playerMovement.SetInputEnabled(!isInteracting);
        SetState(GameFlowState.Playing);
    }

    public void CompleteLevel()
    {
        if (State != GameFlowState.Playing || CurrentLevel == null) return;
        SaveStore.Complete(CurrentLevel.id);
        for (int i = 0; i < configuration.Levels.Count - 1; i++)
        {
            if (configuration.Levels[i].id == CurrentLevel.id)
                SaveStore.Unlock(configuration.Levels[i + 1].id);
        }
        SaveStore.Save();
        FinishLevel(GameFlowState.Completed, configuration.CompleteSound);
    }

    public void FailLevel()
    {
        if (State != GameFlowState.Playing || CurrentLevel == null) return;
        FinishLevel(GameFlowState.Failed, configuration.FailureSound);
    }

    private void FinishLevel(GameFlowState state, AudioClip sound)
    {
        if (chunkInteraction != null) chunkInteraction.CancelCurrentInteraction();
        if (playerMovement != null) playerMovement.SetInputEnabled(false);
        Time.timeScale = 0f;
        audioPlayer.PlaySound(sound);
        SetState(state);
    }

    public void QuitGame()
    {
        SaveStore.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void BeginLoad(string scenePath)
    {
        if (State == GameFlowState.Loading) return;
        if (!Application.CanStreamedLevelBeLoaded(scenePath))
        {
            gameInterface.ShowMessage("场景无法加载，请检查关卡配置与构建列表。");
            return;
        }
        if (chunkInteraction != null) chunkInteraction.CancelCurrentInteraction();
        if (playerMovement != null) playerMovement.SetInputEnabled(false);
        SaveStore.Save();
        isLoading = true;
        SetState(GameFlowState.Loading);
        StartCoroutine(LoadScene(scenePath));
    }

    private IEnumerator LoadScene(string scenePath)
    {
        yield return gameInterface.FadeTo(1f, configuration.TransitionDuration);
        Time.timeScale = 1f;
        AsyncOperation operation = SceneManager.LoadSceneAsync(scenePath);
        while (!operation.isDone) yield return null;
        SetState(GameFlowState.Loading);
        if (playerMovement != null) playerMovement.SetInputEnabled(false);
        yield return gameInterface.FadeTo(0f, configuration.TransitionDuration);
        isLoading = false;
        SetState(CurrentLevel == null ? GameFlowState.MainMenu : GameFlowState.Playing);
        if (playerMovement != null) playerMovement.SetInputEnabled(true);
    }

    private void SetState(GameFlowState state)
    {
        State = state;
        StateChanged?.Invoke();
    }
}
