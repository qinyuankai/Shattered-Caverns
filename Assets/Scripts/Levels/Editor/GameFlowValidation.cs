using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
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
        ValidateLevelSelectVisualReferences();
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

    private static void ValidateLevelSelectVisualReferences()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/GameApplication.prefab");
        if (prefab == null) throw new Exception("Missing Assets/Resources/GameApplication.prefab");
        GameInterfaceController ui = prefab.GetComponentInChildren<GameInterfaceController>(true);
        if (ui == null) throw new Exception("GameApplication prefab is missing GameInterfaceController");

        Sprite[] entrances = Get<Sprite[]>(ui, "levelEntrances");
        if (entrances == null || entrances.Length < 4 || Array.Exists(entrances, sprite => sprite == null)
            || new HashSet<Sprite>(entrances).Count != entrances.Length)
            throw new Exception("Assign at least four distinct map entrance Sprites");
        string[] fields = { "levelMapBackground", "levelExplorer", "levelTierBoard" };
        foreach (string field in fields)
        {
            Sprite sprite = GetSpriteField(ui, field);
            string path = AssetDatabase.GetAssetPath(sprite);
            if (!path.StartsWith("Assets/Art/UI/", StringComparison.Ordinal))
                throw new Exception("GameApplication " + field + " must reference a Sprite in Assets/Art/UI: " + path);
        }
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
                    step = -1; break;
                case -1:
                    Transform levelPanel = Get<GameObject>(ui, "levelSelectPanel").transform;
                    Require(levelPanel.Find("Title") == null && levelPanel.Find("MapControls") == null && levelPanel.Find("DoorStatus") == null,
                        "Level selection has no heading or operation prompts");
                    Transform cavern = levelPanel.Find("TierMap");
                    Require(cavern != null && cavern.Find("Window/Route/Explorer") != null && cavern.Find("Window/Route/Door_00") != null,
                        "Level selection builds the cavern, explorer and level doors");
                    object map = Get<object>(ui, "levelSelectMap");
                    TestLevelSelectVisuals(ui, flow, cavern, map);
                    step = 1; break;
                case 1:
                    ClickPointer(ui, "LevelSelectPanel/BackButton");
                    Require(Get<GameObject>(ui, "mainMenuPanel").activeSelf
                        && !Get<GameObject>(ui, "levelSelectPanel").activeSelf,
                        "Level selection returns to the main menu");
                    step++; break;
                case 2:
                    ClickPointer(ui, "MainMenuPanel/SettingsButton");
                    Require(Get<GameObject>(ui, "settingsPanel").activeSelf, "Pointer raycast and click open settings");
                    Require(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == Get<Slider>(ui, "masterSlider").gameObject,
                        "Settings focus the master volume slider");
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
                case 3:
                    Require(flow.State == GameFlowState.Playing && flow.CurrentLevel != null, "Start button loads configured level");
                    Transform hud = ui.transform.Find("GameHUD");
                    Require(hud.childCount == 1 && hud.Find("PauseButton") != null,
                        "Gameplay HUD only shows the pause button");
                    Require(UnityEngine.Object.FindObjectsOfType<GameFlowController>().Length == 1, "Only one persistent application exists");
                    Require(UnityEngine.Object.FindObjectsOfType<UnityEngine.EventSystems.EventSystem>().Length == 1, "Only one active event system exists");
                    initialPosition = UnityEngine.Object.FindObjectOfType<PlayerMovement>().transform.position;
                    flow.PauseGame();
                    Require(flow.State == GameFlowState.Paused && Time.timeScale == 0, "Pause freezes gameplay");
                    Require(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject
                        == ui.transform.Find("PausePanel/ResumeButton").gameObject, "Pause focuses Resume");
                    ui.OpenSettings(); ui.Back();
                    Require(flow.State == GameFlowState.Paused && Time.timeScale == 0, "Settings return preserves pause");
                    flow.ResumeGame();
                    Require(flow.State == GameFlowState.Playing && Time.timeScale == 1, "Resume restores gameplay");
                    TestInteractionPause(flow);
                    TriggerFlow(LevelFlowTriggerAction.FailLevel);
                    Require(flow.State == GameFlowState.Failed && Time.timeScale == 0
                        && Get<GameObject>(ui, "resultPanel").activeSelf, "Failure shows result and freezes gameplay");
                    Require(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject
                        == ui.transform.Find("ResultPanel/RetryButton").gameObject, "Failure focuses Retry");
                    flow.CompleteLevel();
                    Require(!flow.SaveStore.IsCompleted(flow.CurrentLevel.id), "Completion cannot overwrite failure");
                    UnityEngine.Object.FindObjectOfType<PlayerMovement>().transform.position = new Vector3(99, 99, 0);
                    Click(ui, "ResultPanel/RetryButton");
                    step++; break;
                case 4:
                    Require(flow.State == GameFlowState.Playing, "Retry reloads level");
                    Require(Vector3.Distance(UnityEngine.Object.FindObjectOfType<PlayerMovement>().transform.position, initialPosition) < 3,
                        "Reload restores player spawn instead of retaining modified transform");
                    TriggerFlow(LevelFlowTriggerAction.CompleteLevel);
                    flow.CompleteLevel(); flow.FailLevel();
                    Require(flow.State == GameFlowState.Completed && flow.SaveStore.IsCompleted(flow.CurrentLevel.id), "Completion is idempotent and saves progress");
                    bool hasNextLevel = flow.HasNextLevel();
                    Require(Get<Button>(ui, "nextLevelButton").gameObject.activeSelf == hasNextLevel,
                        "Completion shows the next-level button when a next level exists");
                    GameObject focusedButton = Get<Button>(ui, hasNextLevel ? "nextLevelButton" : "retryButton").gameObject;
                    Require(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject
                        == focusedButton, "Completion focuses the next available action");
                    Click(ui, "ResultPanel/MenuButton"); step++; break;
                case 5:
                    Require(flow.State == GameFlowState.MainMenu, "Return to menu succeeds");
                    Require(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject
                        == ui.transform.Find("MainMenuPanel/StartButton").gameObject, "Main menu focuses Start");
                    Require(Get<Button>(ui, "continueButton").interactable, "Saved level enables Continue");
                    Require(Mathf.Approximately(flow.AudioPlayer.MasterVolume, 0.4f), "Audio settings survive scene changes");
                    Click(ui, "MainMenuPanel/ContinueButton"); step++; break;
                case 6:
                    Require(flow.State == GameFlowState.Playing, "Continue loads last level");
                    TestAudio(flow);
                    flow.ReturnToMainMenu();
                    step++; break;
                case 7:
                    ui.OpenLevelSelect();
                    object mapForEntry = Get<object>(ui, "levelSelectMap");
                    GetMethod(mapForEntry, "TryEnter").Invoke(mapForEntry, null);
                    Require(Get<int>(mapForEntry, "entering") == 0, "The explorer steps into the open mouth");
                    step++; break;
                case 8:
                    if (flow.State == GameFlowState.MainMenu) return;
                    Require(flow.State == GameFlowState.Playing && flow.CurrentLevel.id == flow.Configuration.Levels[0].id,
                        "Entering a map door loads the corresponding level scene");
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
        bool wasFrozen = player.IsFrozen;
        FieldInfo facing = typeof(PlayerMovement).GetField("facingDirection", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (PlayerFacingDirection direction in Enum.GetValues(typeof(PlayerFacingDirection)))
        {
            facing.SetValue(player, direction);
            enter.Invoke(interaction, null);
            if (interaction.CurrentState != PlayerChunkInteractionState.Normal) break;
        }
        if (interaction.CurrentState == PlayerChunkInteractionState.Normal)
        {
            player.SetFrozen(true);
            enter.Invoke(interaction, null);
        }
        Require(interaction.CurrentState != PlayerChunkInteractionState.Normal, "Existing scene can enter chunk operation");
        flow.PauseGame(); flow.ResumeGame();
        Require(Time.timeScale == 0 && interaction.CurrentState != PlayerChunkInteractionState.Normal,
            "Pause/resume retains chunk operation pause");
        interaction.CancelCurrentInteraction();
        player.SetFrozen(wasFrozen);
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

    private static void TestLevelSelectVisuals(GameInterfaceController ui, GameFlowController flow,
        Transform cavern, object map)
    {
        Transform route = cavern.Find("Window/Route");
        Require(cavern.parent.Find("Backdrop").GetComponent<Image>().sprite == GetSpriteField(ui, "levelMapBackground")
            && route.Find("RockTier_1").GetComponent<RawImage>().texture == GetSpriteField(ui, "levelTierBoard").texture
            && route.Find("Explorer").GetComponent<Image>().sprite == GetSpriteField(ui, "levelExplorer"),
            "Map uses the tier artwork and the existing explorer");
        Require(cavern.Find("Page") == null && cavern.Find("NextPage") == null && cavern.Find("PreviousPage") == null,
            "Continuous map has no paging controls");
        Image[] doors = Get<Image[]>(map, "doors");
        Require(doors.Length == flow.Configuration.Levels.Count, "Every configured level has one entrance");
        for (int i = 0; i < doors.Length; i++)
            for (int j = Mathf.Max(0, i - 3); j < i; j++)
                if (doors[i].sprite == doors[j].sprite) throw new Exception("Repeated entrance within four doors");
        MethodInfo move = GetMethod(map, "Move");
        MethodInfo enter = GetMethod(map, "TryEnter");
        var player = (RectTransform)route.Find("Explorer");
        Require(Get<int>(map, "floor") == 0 && Mathf.Approximately(Get<float>(map, "cameraOffset"), 0f),
            "Opening at the start frames the upper tiers");
        for (int i = 0; i < doors.Length; i++)
        {
            int row = i / 3;
            float direction = row % 2 == 0 ? 1f : -1f;
            if (Get<int>(map, "floor") != row)
            {
                float fromY = player.anchoredPosition.y;
                bool airborne = false;
                for (int frame = 0; frame < 360 && Get<int>(map, "floor") != row; frame++)
                {
                    move.Invoke(map, new object[] { -direction, 1f / 60f });
                    if (Get<int>(map, "floor") >= 0) continue;
                    airborne = true;
                    enter.Invoke(map, null);
                    if (Get<int>(map, "entering") >= 0) throw new Exception("Entered a door in midair");
                }
                Require(airborne && Get<int>(map, "floor") == row && player.anchoredPosition.y < fromY,
                    "Walking off the open end drops onto tier " + (row + 1));
            }
            for (int frame = 0; frame < 360 && Get<int>(map, "nearby") != i; frame++)
                move.Invoke(map, new object[] { direction, 1f / 60f });
            Require(Get<int>(map, "nearby") == i && Get<int>(map, "floor") == row
                && doors[i].transform.Find("Number").GetComponent<TMP_Text>().text == i.ToString("00"),
                "Walking reaches the matching numbered door " + i.ToString("00"));
            if (!flow.SaveStore.IsUnlocked(flow.Configuration.Levels[i].id))
            {
                enter.Invoke(map, null);
                if (Get<int>(map, "entering") >= 0) throw new Exception("Locked door accepted entry");
            }
        }
        if (doors.Length > 12)
        {
            Require(Get<float>(map, "cameraOffset") > 0f, "Walking past door 11 scrolls down to the next tier");
            Require(doors[12].gameObject.activeInHierarchy, "Door 12 remains part of the same route");
        }
        int lastTier = (doors.Length - 1) / 3;
        if (lastTier >= 3)
        {
            float y = player.anchoredPosition.y;
            for (int frame = 0; frame < 300; frame++) move.Invoke(map, new object[] { -1f, 1f / 60f });
            Require(Get<int>(map, "floor") == lastTier && Mathf.Approximately(y, player.anchoredPosition.y),
                "The final ledge catches and retains the explorer");
        }
        GetMethod(map, "ResetToStart").Invoke(map, null);
        Require(Get<int>(map, "floor") == 0 && Get<int>(map, "nearby") < 0
            && Mathf.Approximately(Get<float>(map, "cameraOffset"), 0f), "Reset returns the explorer and camera to the top");
        string original = flow.SaveStore.Data.lastLevelId;
        flow.SaveStore.Data.lastLevelId = flow.Configuration.Levels[doors.Length - 1].id;
        ui.OpenLevelSelect();
        Require(Get<int>(map, "nearby") == doors.Length - 1 && Get<int>(map, "floor") == lastTier,
            "Reopening restores the last door on its tier");
        flow.SaveStore.Data.lastLevelId = original;
        ui.OpenLevelSelect();
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
        flow.AudioPlayer.PauseMusic();
        Require(flow.AudioPlayer.IsMusicPaused && flow.AudioPlayer.CurrentMusic == clip,
            "Music pause preserves the current clip");
        flow.AudioPlayer.ResumeMusic();
        Require(!flow.AudioPlayer.IsMusicPaused && music.loop,
            "Music resumes with looping enabled");
        flow.AudioPlayer.StopMusic();
        Require(flow.AudioPlayer.CurrentMusic == null && !music.isPlaying,
            "Music stop clears the current clip");
        flow.AudioPlayer.PlayMusic(clip, 0.5f);
        flow.AudioPlayer.StopMusic();
        Require(flow.AudioPlayer.CurrentMusic == null && Mathf.Approximately(music.volume, 0.12f),
            "Immediate stop cancels a pending fade and restores channel gain");
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
        return (T)GetField(target, name).GetValue(target);
    }

    private static FieldInfo GetField(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) throw new Exception(target.GetType().Name + " is missing field " + name);
        return field;
    }

    private static MethodInfo GetMethod(object target, string name)
    {
        MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null) throw new Exception(target.GetType().Name + " is missing method " + name);
        return method;
    }

    private static Sprite GetSpriteField(object target, string name)
    {
        Sprite sprite = GetField(target, name).GetValue(target) as Sprite;
        if (sprite == null) throw new Exception(target.GetType().Name + "." + name + " is not assigned");
        return sprite;
    }

    private static void ClickPointer(GameInterfaceController ui, string path)
    {
        Canvas.ForceUpdateCanvases();
        var target = ui.transform.Find(path).GetComponent<RectTransform>();
        var events = UnityEngine.EventSystems.EventSystem.current;
        var data = new UnityEngine.EventSystems.PointerEventData(events);
        data.position = RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(target.rect.center));
        Rect canvasPixels = target.GetComponentInParent<Canvas>().rootCanvas.pixelRect;
        if (Mathf.RoundToInt(canvasPixels.width) == Screen.width
            && Mathf.RoundToInt(canvasPixels.height) == Screen.height)
        {
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            events.RaycastAll(data, hits);
            Require(hits.Count > 0 && hits[0].gameObject == target.gameObject,
                path + " receives the pointer raycast at " + data.position + " (hit " + (hits.Count > 0 ? hits[0].gameObject.name : "nothing") + ")");
        }
        else
        {
            results.Add("SKIP " + path + " screen raycast: Game View and Screen sizes differ");
        }
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
