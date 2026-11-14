using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class GameFlowValidation
{
    private static int step;
    private static double deadline;
    private static double readyAt;
    private static readonly List<string> results = new List<string>();
    private static readonly List<string> errors = new List<string>();
    private static string oldSave;
    private static bool hadSave;
    private static bool active;
    private static Vector3 initialPosition;
    private static string originalScene;

    static GameFlowValidation()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("Tools/Shattered Caverns/Validate Game Flow")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        ValidateConfiguration();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (Component component in prefab.GetComponentsInChildren<Component>(true))
                if (component == null) throw new Exception("Missing script in " + AssetDatabase.GetAssetPath(prefab));
        }
        originalScene = SceneManager.GetActiveScene().path;
        hadSave = PlayerPrefs.HasKey(GameSaveStore.SaveKey);
        oldSave = PlayerPrefs.GetString(GameSaveStore.SaveKey, "");
        SessionState.SetBool("Caverns.Validation", true);
        SessionState.SetBool("Caverns.HadSave", hadSave);
        SessionState.SetString("Caverns.OldSave", oldSave);
        SessionState.SetString("Caverns.OriginalScene", originalScene);
        PlayerPrefs.DeleteKey(GameSaveStore.SaveKey);
        PlayerPrefs.Save();
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        EditorApplication.isPlaying = true;
    }

    private static void ValidateConfiguration()
    {
        GameConfiguration configuration = AssetDatabase.LoadAssetAtPath<GameConfiguration>("Assets/Settings/GameConfiguration.asset");
        if (configuration == null) throw new Exception("Missing Assets/Settings/GameConfiguration.asset");

        var enabledScenes = new HashSet<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            if (scene.enabled) enabledScenes.Add(scene.path);

        RequireScene(configuration.MainMenuScenePath, "Main menu", enabledScenes);
        if (configuration.Levels.Count == 0) throw new Exception("Game configuration must contain at least one level");
        var ids = new HashSet<string>();
        var scenePaths = new HashSet<string>();
        foreach (GameLevelDefinition level in configuration.Levels)
        {
            if (level == null) throw new Exception("Game configuration contains an empty level entry");
            if (string.IsNullOrWhiteSpace(level.id)) throw new Exception("Level ID cannot be empty");
            if (!ids.Add(level.id)) throw new Exception("Duplicate level ID: " + level.id);
            if (string.IsNullOrWhiteSpace(level.displayName)) throw new Exception("Level display name cannot be empty: " + level.id);
            if (!scenePaths.Add(level.scenePath)) throw new Exception("Duplicate level scene: " + level.scenePath);
            RequireScene(level.scenePath, "Level " + level.id, enabledScenes);
        }
    }

    private static void RequireScene(string path, string label, HashSet<string> enabledScenes)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new Exception(label + " scene path cannot be empty");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            throw new Exception(label + " scene does not exist: " + path);
        if (!enabledScenes.Contains(path))
            throw new Exception(label + " scene is not enabled in Build Settings: " + path);
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool("Caverns.Validation", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Application.runInBackground = true;
            active = true; step = 0; results.Clear(); errors.Clear();
            deadline = EditorApplication.timeSinceStartup + 90;
            readyAt = EditorApplication.timeSinceStartup + 2;
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            RestoreSave();
            SessionState.SetBool("Caverns.Validation", false);
            string path = SessionState.GetString("Caverns.OriginalScene", "");
            if (!string.IsNullOrEmpty(path)) EditorSceneManager.OpenScene(path);
        }
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            errors.Add(message + "\n" + stack);
    }

    private static void Tick()
    {
        if (!active || EditorApplication.timeSinceStartup < readyAt) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out at step " + step);
            GameFlowController flow = GameFlowController.Instance;
            if (flow == null || flow.State == GameFlowState.Loading) return;
            GameInterfaceController ui = UnityEngine.Object.FindObjectOfType<GameInterfaceController>();
            switch (step)
            {
                case 0:
                    Require(flow.State == GameFlowState.MainMenu, "Main menu bootstraps persistent application");
                    Require(!Get<Button>(ui, "continueButton").interactable, "Continue is disabled without progress");
                    TestSaveNormalization(flow);
                    ui.OpenLevelSelect();
                    Require(Get<GameObject>(ui, "levelSelectPanel").activeSelf, "Level selection opens");
                    ui.Back();
                    ClickPointer(ui, "MainMenuPanel/SettingsButton");
                    Require(Get<GameObject>(ui, "settingsPanel").activeSelf, "Pointer raycast and click open settings");
                    Get<Slider>(ui, "masterSlider").value = 0.4f;
                    Get<Slider>(ui, "musicSlider").value = 0.3f;
                    Get<Slider>(ui, "soundSlider").value = 0.2f;
                    ui.Back();
                    GameSaveStore loaded = new GameSaveStore(flow.Configuration);
                    Require(Mathf.Approximately(loaded.Data.masterVolume, 0.4f)
                        && Mathf.Approximately(loaded.Data.musicVolume, 0.3f)
                        && Mathf.Approximately(loaded.Data.soundVolume, 0.2f), "Volume sliders persist and reload");
                    Click(ui, "MainMenuPanel/StartButton");
                    step++; break;
                case 1:
                    Require(flow.State == GameFlowState.Playing && flow.CurrentLevel != null, "Start button loads configured level");
                    Require(UnityEngine.Object.FindObjectsOfType<GameFlowController>().Length == 1, "Only one persistent application exists");
                    Require(UnityEngine.Object.FindObjectsOfType<UnityEngine.EventSystems.EventSystem>().Length == 1, "Only one active event system exists");
                    initialPosition = UnityEngine.Object.FindObjectOfType<PlayerMovement>().transform.position;
                    flow.PauseGame();
                    Require(flow.State == GameFlowState.Paused && Time.timeScale == 0, "Pause freezes gameplay");
                    ui.OpenSettings(); ui.Back();
                    Require(flow.State == GameFlowState.Paused && Time.timeScale == 0, "Settings return preserves pause");
                    flow.ResumeGame();
                    Require(flow.State == GameFlowState.Playing && Time.timeScale == 1, "Resume restores gameplay");
                    TestInteractionPause(flow);
                    TriggerFlow(LevelFlowTriggerAction.FailLevel);
                    Require(flow.State == GameFlowState.Failed && Time.timeScale == 0
                        && Get<GameObject>(ui, "resultPanel").activeSelf, "Failure shows result and freezes gameplay");
                    flow.CompleteLevel();
                    Require(!flow.SaveStore.IsCompleted(flow.CurrentLevel.id), "Completion cannot overwrite failure");
                    UnityEngine.Object.FindObjectOfType<PlayerMovement>().transform.position = new Vector3(99, 99, 0);
                    Click(ui, "ResultPanel/RetryButton");
                    step++; break;
                case 2:
                    Require(flow.State == GameFlowState.Playing, "Retry reloads level");
                    Require(Vector3.Distance(UnityEngine.Object.FindObjectOfType<PlayerMovement>().transform.position, initialPosition) < 3,
                        "Reload restores player spawn instead of retaining modified transform");
                    TriggerFlow(LevelFlowTriggerAction.CompleteLevel);
                    flow.CompleteLevel(); flow.FailLevel();
                    Require(flow.State == GameFlowState.Completed && flow.SaveStore.IsCompleted(flow.CurrentLevel.id), "Completion is idempotent and saves progress");
                    Require(!Get<Button>(ui, "nextLevelButton").gameObject.activeSelf, "Final level hides next-level button");
                    Click(ui, "ResultPanel/MenuButton"); step++; break;
                case 3:
                    Require(flow.State == GameFlowState.MainMenu, "Return to menu succeeds");
                    Require(Get<Button>(ui, "continueButton").interactable, "Saved level enables Continue");
                    Require(Mathf.Approximately(flow.AudioPlayer.MasterVolume, 0.4f), "Audio settings survive scene changes");
                    Click(ui, "MainMenuPanel/ContinueButton"); step++; break;
                case 4:
                    Require(flow.State == GameFlowState.Playing, "Continue loads last level");
                    TestAudio(flow);
                    Require(errors.Count == 0, "No runtime errors during flow validation");
                    Finish(null); break;
            }
            readyAt = EditorApplication.timeSinceStartup + 0.3;
        }
        catch (Exception exception)
        {
            Finish(exception);
        }
    }

    private static void TestInteractionPause(GameFlowController flow)
    {
        PlayerChunkInteraction interaction = UnityEngine.Object.FindObjectOfType<PlayerChunkInteraction>();
        MethodInfo enter = typeof(PlayerChunkInteraction).GetMethod("TryEnterInteractionState", BindingFlags.Instance | BindingFlags.NonPublic);
        PlayerMovement player = UnityEngine.Object.FindObjectOfType<PlayerMovement>();
        FieldInfo facing = typeof(PlayerMovement).GetField("facingDirection", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (PlayerFacingDirection direction in Enum.GetValues(typeof(PlayerFacingDirection)))
        {
            facing.SetValue(player, direction);
            enter.Invoke(interaction, null);
            if (interaction.CurrentState != PlayerChunkInteractionState.Normal) break;
        }
        Require(interaction.CurrentState != PlayerChunkInteractionState.Normal, "Existing scene can enter chunk operation");
        flow.PauseGame(); flow.ResumeGame();
        Require(Time.timeScale == 0 && interaction.CurrentState != PlayerChunkInteractionState.Normal,
            "Pause/resume retains chunk operation pause");
        interaction.CancelCurrentInteraction();
        Require(Time.timeScale == 1, "Cancelling chunk operation restores time");
    }

    private static void TestSaveNormalization(GameFlowController flow)
    {
        string validId = flow.Configuration.Levels[0].id;
        var data = new GameSaveData
        {
            lastLevelId = "removed-level",
            unlockedLevelIds = new List<string> { "removed-level", validId, validId },
            completedLevelIds = new List<string> { "removed-level", validId, validId },
            masterVolume = 2f,
            musicVolume = -1f,
            soundVolume = 0.5f
        };
        PlayerPrefs.SetString(GameSaveStore.SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();

        GameSaveStore normalized = new GameSaveStore(flow.Configuration);
        GameSaveStore reloaded = new GameSaveStore(flow.Configuration);
        Require(normalized.Data.lastLevelId == null
            && normalized.Data.unlockedLevelIds.Count == 1 && normalized.IsUnlocked(validId)
            && normalized.Data.completedLevelIds.Count == 1 && normalized.IsCompleted(validId)
            && reloaded.Data.unlockedLevelIds.Count == 1 && reloaded.Data.completedLevelIds.Count == 1
            && Mathf.Approximately(reloaded.Data.masterVolume, 1f)
            && Mathf.Approximately(reloaded.Data.musicVolume, 0f)
            && Mathf.Approximately(reloaded.Data.soundVolume, 0.5f),
            "Outdated save data is normalized and persisted");
        flow.SaveStore.Save();
    }

    private static void TestAudio(GameFlowController flow)
    {
        AudioClip clip = AudioClip.Create("ValidationTone", 4410, 1, 44100, false);
        flow.AudioPlayer.PlayMusic(clip);
        flow.AudioPlayer.PlayMusic(clip);
        Require(flow.AudioPlayer.CurrentMusic == clip, "Music player accepts clips without duplicate playback setup");
        AudioSource music = Get<AudioSource>(flow.AudioPlayer, "musicSource");
        AudioSource sound = Get<AudioSource>(flow.AudioPlayer, "soundSource");
        Require(Mathf.Approximately(music.volume, 0.12f) && Mathf.Approximately(sound.volume, 0.08f),
            "Master volume multiplies independent music and sound levels");
        flow.AudioPlayer.PlaySound(clip);
        flow.AudioPlayer.PlayMusic(null);
        UnityEngine.Object.Destroy(clip);
    }

    private static void TriggerFlow(LevelFlowTriggerAction action)
    {
        GameObject triggerObject = new GameObject("ValidationLevelFlowTrigger", typeof(BoxCollider2D), typeof(LevelFlowTrigger));
        LevelFlowTrigger trigger = triggerObject.GetComponent<LevelFlowTrigger>();
        typeof(LevelFlowTrigger).GetField("action", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(trigger, action);
        Collider2D player = null;
        foreach (Collider2D candidate in UnityEngine.Object.FindObjectOfType<PlayerMovement>().GetComponentsInChildren<Collider2D>())
        {
            if (candidate.isTrigger) continue;
            player = candidate;
            break;
        }
        if (player == null) throw new Exception("Player physical collider was not found");
        typeof(LevelFlowTrigger).GetMethod("OnTriggerEnter2D", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(trigger, new object[] { player });
        UnityEngine.Object.Destroy(triggerObject);
    }

    private static T Get<T>(object target, string name)
    {
        return (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }

    private static void ClickPointer(GameInterfaceController ui, string path)
    {
        var target = ui.transform.Find(path).GetComponent<RectTransform>();
        var events = UnityEngine.EventSystems.EventSystem.current;
        var data = new UnityEngine.EventSystems.PointerEventData(events);
        data.position = RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(target.rect.center));
        var hits = new List<UnityEngine.EventSystems.RaycastResult>();
        events.RaycastAll(data, hits);
        Require(hits.Count > 0 && hits[0].gameObject == target.gameObject, "Menu button is not blocked by another graphic");
        UnityEngine.EventSystems.ExecuteEvents.Execute(target.gameObject, data, UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);
        UnityEngine.EventSystems.ExecuteEvents.Execute(target.gameObject, data, UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler);
        UnityEngine.EventSystems.ExecuteEvents.Execute(target.gameObject, data, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
    }

    private static void Click(GameInterfaceController ui, string path)
    {
        ui.transform.Find(path).GetComponent<Button>().onClick.Invoke();
    }

    private static void Require(bool condition, string text)
    {
        if (!condition) throw new Exception(text);
        results.Add("PASS " + text);
    }

    private static void Finish(Exception exception)
    {
        active = false;
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        string report = string.Join("\n", results) + "\n" + string.Join("\n", errors)
            + (exception == null ? "\nSUCCESS" : "\nFAIL " + exception);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/GameFlowValidation.txt", report);
        if (exception == null) Debug.Log("Game flow validation passed: " + results.Count + " checks.");
        else Debug.LogError(report);
        EditorApplication.isPlaying = false;
    }

    private static void RestoreSave()
    {
        if (SessionState.GetBool("Caverns.HadSave", false))
            PlayerPrefs.SetString(GameSaveStore.SaveKey, SessionState.GetString("Caverns.OldSave", ""));
        else PlayerPrefs.DeleteKey(GameSaveStore.SaveKey);
        PlayerPrefs.Save();
    }
}
