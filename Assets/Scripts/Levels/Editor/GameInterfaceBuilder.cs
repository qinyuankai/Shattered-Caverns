using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class GameInterfaceBuilder
{
    private const string MainMenuBackgroundPath = "Assets/Art/Backgrounds/MainMenuBackground.png";
    private static TMP_FontAsset font;
    private static readonly Color Ink = new Color(0.035f, 0.065f, 0.09f);
    private static readonly Color Surface = new Color(0.075f, 0.13f, 0.16f);
    private static readonly Color Accent = new Color(0.5f, 0.87f, 0.72f);
    private static readonly Color Gold = new Color(0.91f, 0.66f, 0.29f);
    private static readonly Color White = new Color(0.9f, 0.94f, 0.91f);
    private static readonly Color Muted = new Color(0.55f, 0.68f, 0.69f);

    [MenuItem("Tools/Shattered Caverns/Build Game Interface")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/GameApplication.prefab") != null)
        {
            Debug.LogWarning("GameApplication already exists. Edit the existing prefab instead of regenerating it.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (Resources.Load<TMP_Settings>("TMP Settings") == null)
        {
            AssetDatabase.importPackageCompleted += AfterPackageImport;
            AssetDatabase.ImportPackage("Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage", false);
            return;
        }
        AssetDatabase.Refresh();
        if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
            AssetDatabase.MoveAsset("Assets/TextMesh Pro", "Assets/Settings/TextMeshPro");
        ConfigureRendering();
        font = CreateFont();
        GameConfiguration configuration = ScriptableObject.CreateInstance<GameConfiguration>();
        AssetDatabase.CreateAsset(configuration, "Assets/Settings/GameConfiguration.asset");
        SerializedObject config = new SerializedObject(configuration);
        SerializedProperty levels = config.FindProperty("levels");
        levels.arraySize = 1;
        SerializedProperty first = levels.GetArrayElementAtIndex(0);
        first.FindPropertyRelative("id").stringValue = "cavern-01";
        first.FindPropertyRelative("displayName").stringValue = "初探洞窟";
        first.FindPropertyRelative("scenePath").stringValue = "Assets/Scenes/URP2DSceneTemplate.unity";
        config.ApplyModifiedPropertiesWithoutUndo();
        LevelSelectButton levelButton = CreateLevelButton();
        BuildApplication(configuration, levelButton);
        Scene menu = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        Camera camera = cameraObject.GetComponent<Camera>();
        cameraObject.tag = "MainCamera";
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Ink;
        camera.orthographic = true;
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        EditorSceneManager.SaveScene(menu, configuration.MainMenuScenePath);
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(configuration.MainMenuScenePath, true),
            new EditorBuildSettingsScene(configuration.Levels[0].scenePath, true)
        };
        AssetDatabase.SaveAssets();
        Debug.Log("Game interface created. Open MainMenu and press Play.");
    }

    [MenuItem("Tools/Shattered Caverns/Apply Responsive Layout")]
    public static void ApplyResponsiveLayout()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        string path = "Assets/Resources/GameApplication.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.GetComponentInChildren<CanvasScaler>(true).matchWidthOrHeight = 1f;
            RectTransform pause = root.transform.Find("GameCanvas/GameHUD/PauseButton").GetComponent<RectTransform>();
            pause.anchorMin = pause.anchorMax = pause.pivot = Vector2.one;
            pause.anchoredPosition = new Vector2(-40, -30);
            Transform art = root.transform.Find("GameCanvas/MainMenuPanel/CavernIllustration");
            for (int i = 0; i < 9; i++)
            {
                Transform block = art.Find("CavernBlock" + i);
                if (block == null) continue;
                block.GetComponent<RectTransform>().anchoredPosition = new Vector2(780 + i % 3 * 142, -285 - i / 3 * 142);
            }
            RectTransform caption = art.Find("Caption").GetComponent<RectTransform>();
            caption.anchoredPosition = new Vector2(780, -760);
            caption.sizeDelta = new Vector2(350, 45);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [MenuItem("Tools/Shattered Caverns/Apply Main Menu Redesign %#m")]
    public static void ApplyMainMenuRedesign()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        ConfigureMainMenuBackground();
        string path = "Assets/Resources/GameApplication.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform mainMenu = root.transform.Find("GameCanvas/MainMenuPanel");
            ApplyMainMenuStyle(mainMenu);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        Debug.Log("Main menu redesign applied.");
    }

    private static void ConfigureMainMenuBackground()
    {
        TextureImporter importer = AssetImporter.GetAtPath(MainMenuBackgroundPath) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static void ApplyMainMenuStyle(Transform mainMenu)
    {
        Sprite backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(MainMenuBackgroundPath);
        Transform oldIllustration = mainMenu.Find("CavernIllustration");
        if (oldIllustration != null) Object.DestroyImmediate(oldIllustration.gameObject);

        Transform existingBackground = mainMenu.Find("Background");
        GameObject background = existingBackground == null
            ? new GameObject("Background", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter))
            : existingBackground.gameObject;
        background.transform.SetParent(mainMenu, false);
        background.transform.SetAsFirstSibling();
        Stretch(background.GetComponent<RectTransform>());
        Image backgroundImage = background.GetComponent<Image>();
        backgroundImage.sprite = backgroundSprite;
        backgroundImage.color = Color.white;
        backgroundImage.raycastTarget = false;
        AspectRatioFitter fitter = background.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = backgroundSprite == null ? 16f / 9f : backgroundSprite.rect.width / backgroundSprite.rect.height;

        Transform existingShade = mainMenu.Find("MenuShade");
        GameObject shade = existingShade == null
            ? new GameObject("MenuShade", typeof(RectTransform), typeof(Image))
            : existingShade.gameObject;
        shade.transform.SetParent(mainMenu, false);
        shade.transform.SetSiblingIndex(1);
        RectTransform shadeRect = shade.GetComponent<RectTransform>();
        shadeRect.anchorMin = Vector2.zero;
        shadeRect.anchorMax = new Vector2(0.48f, 1f);
        shadeRect.offsetMin = Vector2.zero;
        shadeRect.offsetMax = Vector2.zero;
        Image shadeImage = shade.GetComponent<Image>();
        shadeImage.color = new Color(0.01f, 0.02f, 0.035f, 0.38f);
        shadeImage.raycastTarget = false;

        StyleMenuText(mainMenu.Find("Eyebrow"), "SHATTERED CAVERNS", new Vector2(130, -105), new Vector2(540, 35), 17, Gold);
        StyleMenuText(mainMenu.Find("Title"), "破碎洞窟", new Vector2(125, -155), new Vector2(560, 95), 68, White, FontStyles.Bold, 7f);
        StyleMenuText(mainMenu.Find("Subtitle"), "重组碎片  ·  连接前路", new Vector2(130, -265), new Vector2(520, 42), 21, Muted);

        StyleMenuButton(mainMenu.Find("StartButton"), "开始游戏", new Vector2(130, -400), new Vector2(350, 60), true);
        StyleMenuButton(mainMenu.Find("ContinueButton"), "继续游戏", new Vector2(130, -476), new Vector2(350, 54), false);
        StyleMenuButton(mainMenu.Find("LevelsButton"), "选择关卡", new Vector2(130, -546), new Vector2(350, 54), false);
        StyleMenuButton(mainMenu.Find("SettingsButton"), "设置", new Vector2(130, -616), new Vector2(350, 54), false);
        StyleMenuButton(mainMenu.Find("QuitButton"), "退出游戏", new Vector2(130, -686), new Vector2(350, 54), false);
        StyleMenuText(mainMenu.Find("Footer"), "每一次连接，都是一种可能。", new Vector2(130, -805), new Vector2(560, 30), 15, Muted);
    }

    private static void StyleMenuText(Transform target, string value, Vector2 position, Vector2 size,
        float fontSize, Color color, FontStyles fontStyle = FontStyles.Normal, float characterSpacing = 0f)
    {
        if (target == null) return;
        RectTransform rect = target.GetComponent<RectTransform>();
        Place(rect, position, size, true);
        TextMeshProUGUI text = target.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.fontStyle = fontStyle;
        text.characterSpacing = characterSpacing;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = false;
    }

    private static void StyleMenuButton(Transform target, string value, Vector2 position, Vector2 size, bool primary)
    {
        if (target == null) return;
        Place(target.GetComponent<RectTransform>(), position, size, true);
        Image image = target.GetComponent<Image>();
        image.color = primary ? Gold : new Color(0.035f, 0.065f, 0.09f, 0.88f);
        Button button = target.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = primary ? new Color(1f, 0.9f, 0.7f) : new Color(0.72f, 0.95f, 0.88f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = primary ? new Color(0.78f, 0.62f, 0.4f) : new Color(0.5f, 0.72f, 0.66f);
        colors.disabledColor = new Color(0.38f, 0.42f, 0.43f, 0.5f);
        colors.fadeDuration = 0.12f;
        button.colors = colors;

        Transform labelTransform = target.Find("Label");
        if (labelTransform != null)
        {
            RectTransform labelRect = labelTransform.GetComponent<RectTransform>();
            Place(labelRect, Vector2.zero, size - new Vector2(48, 4));
            TextMeshProUGUI label = labelTransform.GetComponent<TextMeshProUGUI>();
            label.text = primary ? value + "    →" : value;
            label.fontSize = primary ? 24 : 22;
            label.color = primary ? Ink : White;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.fontStyle = primary ? FontStyles.Bold : FontStyles.Normal;
        }

        Transform existingAccent = target.Find("Accent");
        GameObject accent = existingAccent == null
            ? new GameObject("Accent", typeof(RectTransform), typeof(Image))
            : existingAccent.gameObject;
        accent.transform.SetParent(target, false);
        accent.transform.SetAsFirstSibling();
        RectTransform accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = Vector2.zero;
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(4f, 0f);
        Image accentImage = accent.GetComponent<Image>();
        accentImage.color = primary ? White : Accent;
        accentImage.raycastTarget = false;
    }

    private static void AfterPackageImport(string packageName)
    {
        AssetDatabase.importPackageCompleted -= AfterPackageImport;
        EditorApplication.delayCall += Build;
    }

    private static void ConfigureRendering()
    {
        if (GraphicsSettings.defaultRenderPipeline != null) return;
        Renderer2DData renderer = ScriptableObject.CreateInstance<Renderer2DData>();
        AssetDatabase.CreateAsset(renderer, "Assets/Settings/CavernsRenderer2D.asset");
        UnityEngine.Rendering.ResourceReloader.ReloadAllNullIn(renderer, UniversalRenderPipelineAsset.packagePath);
        UniversalRenderPipelineAsset pipeline = UniversalRenderPipelineAsset.Create(renderer);
        AssetDatabase.CreateAsset(pipeline, "Assets/Settings/CavernsPipeline.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline;
        EditorUtility.SetDirty(renderer);
    }

    private static TMP_FontAsset CreateFont()
    {
        Font source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Settings/Fonts/NotoSansSC.ttf");
        TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(source, 48, 6,
            UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048);
        asset.name = "CavernsChinese";
        AssetDatabase.CreateAsset(asset, "Assets/Settings/Fonts/CavernsChinese.asset");
        asset.material.name = "CavernsChinese Material";
        asset.atlasTexture.name = "CavernsChinese Atlas";
        AssetDatabase.AddObjectToAsset(asset.material, asset);
        AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
        SerializedObject settings = new SerializedObject(TMP_Settings.instance);
        settings.FindProperty("m_defaultFontAsset").objectReferenceValue = asset;
        settings.ApplyModifiedPropertiesWithoutUndo();
        return asset;
    }

    private static LevelSelectButton CreateLevelButton()
    {
        GameObject root = new GameObject("LevelSelectItem", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        root.GetComponent<Image>().color = Surface;
        root.GetComponent<LayoutElement>().preferredHeight = 76f;
        TextMeshProUGUI label = Text(root.transform, "Label", "", new Vector2(24, 0), new Vector2(660, 70), 23, White);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        LevelSelectButton view = root.AddComponent<LevelSelectButton>();
        Assign(view, "button", root.GetComponent<Button>());
        Assign(view, "label", label);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefab/LevelSelectItem.prefab");
        Object.DestroyImmediate(root);
        return prefab.GetComponent<LevelSelectButton>();
    }

    private static void BuildApplication(GameConfiguration configuration, LevelSelectButton levelButton)
    {
        GameObject root = new GameObject("GameApplication");
        GameFlowController flow = root.AddComponent<GameFlowController>();
        GameAudioPlayer audio = root.AddComponent<GameAudioPlayer>();
        AudioSource music = new GameObject("MusicPlayer", typeof(AudioSource)).GetComponent<AudioSource>();
        music.transform.SetParent(root.transform);
        music.loop = true;
        music.playOnAwake = false;
        AudioSource sound = new GameObject("SoundPlayer", typeof(AudioSource)).GetComponent<AudioSource>();
        sound.transform.SetParent(root.transform);
        sound.playOnAwake = false;
        Assign(audio, "musicSource", music);
        Assign(audio, "soundSource", sound);
        GameObject events = new GameObject("UIEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        events.transform.SetParent(root.transform);
        GameObject canvasObject = new GameObject("GameCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(root.transform);
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.GetComponent<Canvas>().sortingOrder = 100;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = 1f;
        GameInterfaceController ui = canvasObject.AddComponent<GameInterfaceController>();
        Assign(flow, "configuration", configuration);
        Assign(flow, "audioPlayer", audio);
        Assign(flow, "gameInterface", ui);
        Assign(ui, "eventSystem", events.GetComponent<EventSystem>());
        Assign(ui, "levelButtonPrefab", levelButton);

        GameObject hud = Panel(canvasObject.transform, "GameHUD", Color.clear);
        Assign(ui, "hudPanel", hud);
        TextMeshProUGUI levelTitle = Text(hud.transform, "LevelTitle", "初探洞窟", new Vector2(40, -35), new Vector2(500, 45), 25, White, true);
        Assign(ui, "levelTitle", levelTitle);
        Button pause = Button(hud.transform, "PauseButton", "暂停  Esc", new Vector2(1320, -30), new Vector2(220, 48), ui.OpenPause, true);
        RectTransform pauseRect = pause.GetComponent<RectTransform>();
        pauseRect.anchorMin = pauseRect.anchorMax = pauseRect.pivot = Vector2.one;
        pauseRect.anchoredPosition = new Vector2(-40, -30);
        pause.GetComponent<Image>().color = Surface;
        Assign(ui, "fragmentProgress", Text(hud.transform, "FragmentProgress", "", new Vector2(40, -95), new Vector2(340, 240), 20, White, true));
        TextMeshProUGUI hint = Text(hud.transform, "InteractionHint", "", new Vector2(0, -395), new Vector2(1200, 55), 20, Accent);
        Assign(ui, "interactionHint", hint);

        GameObject main = Panel(canvasObject.transform, "MainMenuPanel", Ink);
        Assign(ui, "mainMenuPanel", main);
        Text(main.transform, "Eyebrow", "SHATTERED CAVERNS  /  破碎洞窟", new Vector2(130, -110), new Vector2(650, 45), 18, Accent, true);
        Text(main.transform, "Title", "在破碎之处\n连接新的道路", new Vector2(125, -195), new Vector2(850, 180), 64, White, true);
        Text(main.transform, "Subtitle", "观察洞窟 · 收集碎片 · 重构前路", new Vector2(130, -395), new Vector2(650, 50), 22, Muted, true);
        Button(main.transform, "StartButton", "开始游戏     →", new Vector2(130, -500), new Vector2(390, 62), ui.StartGame, true, true);
        Button continueButton = Button(main.transform, "ContinueButton", "继续游戏", new Vector2(130, -578), new Vector2(390, 54), ui.ContinueGame, true);
        Assign(ui, "continueButton", continueButton);
        Button(main.transform, "LevelsButton", "选择关卡", new Vector2(130, -647), new Vector2(185, 54), ui.OpenLevelSelect, true);
        Button(main.transform, "SettingsButton", "设置", new Vector2(335, -647), new Vector2(185, 54), ui.OpenSettings, true);
        Button(main.transform, "QuitButton", "退出游戏", new Vector2(130, -716), new Vector2(390, 54), ui.QuitGame, true);
        Text(main.transform, "Footer", "单人平台解谜  /  每一次连接，都是一种可能。", new Vector2(130, -825), new Vector2(850, 35), 16, Muted, true);
        ApplyMainMenuStyle(main.transform);

        GameObject levels = Page(canvasObject.transform, "LevelSelectPanel", "选择关卡", "从已经解锁的洞窟继续探索。", ui.Back);
        Assign(ui, "levelSelectPanel", levels);
        GameObject scrollObject = new GameObject("LevelScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollObject.transform.SetParent(levels.transform, false);
        Place(scrollObject.GetComponent<RectTransform>(), new Vector2(0, -35), new Vector2(780, 450));
        scrollObject.GetComponent<Image>().color = Color.clear;
        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(scrollObject.transform, false);
        Stretch(viewport.GetComponent<RectTransform>());
        GameObject list = new GameObject("LevelList", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        list.transform.SetParent(viewport.transform, false);
        RectTransform listRect = list.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0, 1); listRect.anchorMax = Vector2.one;
        listRect.pivot = new Vector2(0.5f, 1); listRect.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout = list.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 14; layout.padding = new RectOffset(8, 8, 8, 8);
        layout.childControlWidth = true; layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        list.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
        scroll.viewport = viewport.GetComponent<RectTransform>(); scroll.content = listRect;
        scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
        Assign(ui, "levelList", list.transform);

        GameObject settings = Page(canvasObject.transform, "SettingsPanel", "声音设置", "调整你的洞窟声景。设置会自动保留。", ui.Back);
        Assign(ui, "settingsPanel", settings);
        MakeSlider(settings.transform, ui, "Master", "总音量", 85);
        MakeSlider(settings.transform, ui, "Music", "背景音乐", -25);
        MakeSlider(settings.transform, ui, "Sound", "音效", -135);

        GameObject pausePanel = Panel(canvasObject.transform, "PausePanel", new Color(Ink.r, Ink.g, Ink.b, 0.96f));
        Assign(ui, "pausePanel", pausePanel);
        Text(pausePanel.transform, "Title", "暂停探索", new Vector2(0, 245), new Vector2(600, 85), 48, White);
        Text(pausePanel.transform, "Subtitle", "前路仍在，稍作停留。", new Vector2(0, 170), new Vector2(600, 45), 22, Muted);
        Button(pausePanel.transform, "ResumeButton", "继续探索", new Vector2(0, 70), new Vector2(390, 62), ui.Resume, false, true);
        Button(pausePanel.transform, "SettingsButton", "声音设置", new Vector2(0, -10), new Vector2(390, 54), ui.OpenSettings);
        Button(pausePanel.transform, "RestartButton", "重新开始本关", new Vector2(0, -80), new Vector2(390, 54), ui.RestartLevel);
        Button(pausePanel.transform, "MenuButton", "返回主菜单", new Vector2(0, -150), new Vector2(390, 54), ui.ReturnToMainMenu);
        Text(pausePanel.transform, "RestartHint", "重新开始将重置本关，已解锁关卡与设置会保留。", new Vector2(0, -255), new Vector2(900, 45), 18, Muted);

        GameObject result = Panel(canvasObject.transform, "ResultPanel", new Color(Ink.r, Ink.g, Ink.b, 0.97f));
        Assign(ui, "resultPanel", result);
        Assign(ui, "resultTitle", Text(result.transform, "Title", "洞窟已贯通", new Vector2(0, 210), new Vector2(900, 90), 54, Accent));
        Assign(ui, "resultDescription", Text(result.transform, "Description", "", new Vector2(0, 110), new Vector2(1000, 60), 22, Muted));
        Assign(ui, "nextLevelButton", Button(result.transform, "NextButton", "下一关", new Vector2(0, 10), new Vector2(390, 62), ui.NextLevel, false, true));
        Button(result.transform, "RetryButton", "重新开始本关", new Vector2(0, -75), new Vector2(390, 54), ui.RestartLevel);
        Button(result.transform, "MenuButton", "返回主菜单", new Vector2(0, -150), new Vector2(390, 54), ui.ReturnToMainMenu);

        Assign(ui, "message", Text(canvasObject.transform, "Message", "", new Vector2(0, -330), new Vector2(1300, 65), 22, Accent));
        GameObject curtain = Panel(canvasObject.transform, "Transition", Ink);
        CanvasGroup group = curtain.AddComponent<CanvasGroup>(); group.alpha = 0; group.blocksRaycasts = false;
        Assign(ui, "transition", group);
        main.SetActive(true); levels.SetActive(false); settings.SetActive(false);
        pausePanel.SetActive(false); result.SetActive(false); hud.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/GameApplication.prefab");
        Object.DestroyImmediate(root);
    }

    private static void DrawCavern(Transform parent)
    {
        Transform art = Panel(parent, "CavernIllustration", Color.clear).transform;
        for (int i = 0; i < 9; i++)
        {
            float x = 780 + i % 3 * 142;
            float y = -285 - i / 3 * 142;
            if (i == 2 || i == 6) continue;
            GameObject block = Panel(art, "CavernBlock" + i, i == 4 ? new Color(0.14f, 0.3f, 0.29f) : Surface);
            Place(block.GetComponent<RectTransform>(), new Vector2(x, y), new Vector2(130, 130), true);
            GameObject line = Panel(block.transform, "Path", i == 4 ? Accent : Muted);
            Place(line.GetComponent<RectTransform>(), Vector2.zero, new Vector2(90, 3));
            if (i == 4)
            {
                GameObject dot = Panel(block.transform, "Fragment", Accent);
                Place(dot.GetComponent<RectTransform>(), new Vector2(0, 26), new Vector2(17, 17));
                dot.transform.localRotation = Quaternion.Euler(0, 0, 45);
            }
        }
        Text(art, "Caption", "碎片重组，前路重现。", new Vector2(780, -760), new Vector2(350, 45), 20, Muted, true);
    }

    private static GameObject Page(Transform parent, string name, string title, string subtitle, UnityAction back)
    {
        GameObject page = Panel(parent, name, Ink);
        Text(page.transform, "Title", title, new Vector2(0, 295), new Vector2(900, 80), 48, White);
        Text(page.transform, "Subtitle", subtitle, new Vector2(0, 225), new Vector2(1000, 50), 22, Muted);
        Button(page.transform, "BackButton", "返回", new Vector2(0, -330), new Vector2(280, 55), back);
        return page;
    }

    private static void MakeSlider(Transform parent, GameInterfaceController ui, string name, string label, float y)
    {
        Text(parent, name + "Label", label, new Vector2(-285, y + 15), new Vector2(180, 45), 24, White);
        TMP_Text value = Text(parent, name + "Value", "", new Vector2(310, y + 15), new Vector2(120, 45), 22, Accent);
        GameObject root = new GameObject(name + "Slider", typeof(RectTransform), typeof(Slider));
        root.transform.SetParent(parent, false);
        Place(root.GetComponent<RectTransform>(), new Vector2(35, y + 15), new Vector2(370, 40));
        GameObject track = Panel(root.transform, "Track", Surface);
        Place(track.GetComponent<RectTransform>(), Vector2.zero, new Vector2(370, 8));
        GameObject fillArea = new GameObject("FillArea", typeof(RectTransform));
        fillArea.transform.SetParent(root.transform, false);
        Stretch(fillArea.GetComponent<RectTransform>());
        GameObject fill = Panel(fillArea.transform, "Fill", Accent);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        Stretch(fillRect); fillRect.sizeDelta = new Vector2(0, -32);
        GameObject handleArea = new GameObject("HandleArea", typeof(RectTransform));
        handleArea.transform.SetParent(root.transform, false); Stretch(handleArea.GetComponent<RectTransform>());
        GameObject handle = Panel(handleArea.transform, "Handle", White);
        Place(handle.GetComponent<RectTransform>(), Vector2.zero, new Vector2(18, 28));
        Slider slider = root.GetComponent<Slider>(); slider.minValue = 0; slider.maxValue = 1;
        slider.fillRect = fillRect; slider.handleRect = handle.GetComponent<RectTransform>(); slider.targetGraphic = handle.GetComponent<Image>();
        string prefix = char.ToLowerInvariant(name[0]) + name.Substring(1);
        Assign(ui, prefix + "Slider", slider); Assign(ui, prefix + "Value", value);
    }

    private static Button Button(Transform parent, string name, string label, Vector2 position, Vector2 size, UnityAction action, bool topLeft = false, bool primary = false)
    {
        GameObject root = Panel(parent, name, primary ? Accent : Surface);
        Place(root.GetComponent<RectTransform>(), position, size, topLeft);
        Button button = root.AddComponent<Button>();
        ColorBlock colors = button.colors; colors.highlightedColor = new Color(0.75f, 1, 0.9f);
        colors.selectedColor = new Color(0.75f, 1, 0.9f); colors.pressedColor = new Color(0.45f, 0.7f, 0.6f);
        colors.disabledColor = new Color(0.4f, 0.45f, 0.45f, 0.5f); button.colors = colors;
        Text(root.transform, "Label", label, Vector2.zero, size - new Vector2(16, 4), 23, primary ? Ink : White);
        UnityEventTools.AddPersistentListener(button.onClick, action);
        return button;
    }

    private static TextMeshProUGUI Text(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize, Color color, bool topLeft = false)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        root.transform.SetParent(parent, false); Place(root.GetComponent<RectTransform>(), position, size, topLeft);
        TextMeshProUGUI text = root.GetComponent<TextMeshProUGUI>();
        text.font = font; text.text = value; text.fontSize = fontSize; text.color = color;
        text.alignment = topLeft ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Center;
        text.raycastTarget = false; text.enableWordWrapping = true;
        return text;
    }

    private static GameObject Panel(Transform parent, string name, Color color)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image));
        root.transform.SetParent(parent, false); Stretch(root.GetComponent<RectTransform>());
        Image image = root.GetComponent<Image>(); image.color = color; image.raycastTarget = color.a > 0;
        return root;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size, bool topLeft = false)
    {
        rect.anchorMin = rect.anchorMax = topLeft ? new Vector2(0, 1) : new Vector2(0.5f, 0.5f);
        rect.pivot = topLeft ? new Vector2(0, 1) : new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size; rect.anchoredPosition = position;
    }

    private static void Assign(Object target, string property, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(property).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
