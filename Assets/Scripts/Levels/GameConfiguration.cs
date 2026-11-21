using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GameLevelDefinition
{
    public string id;
    public string displayName;
    public string scenePath;
    public AudioClip music;
}

[CreateAssetMenu(menuName = "Shattered Caverns/Game Configuration")]
public class GameConfiguration : ScriptableObject
{
    [SerializeField] private string mainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    [SerializeField] private List<GameLevelDefinition> levels = new List<GameLevelDefinition>();
    [SerializeField] private AudioClip menuMusic;
    [SerializeField] private AudioClip buttonSound;
    [SerializeField] private AudioClip completeSound;
    [SerializeField] private AudioClip failureSound;
    [SerializeField, Min(0f)] private float transitionDuration = 0.25f;
    [SerializeField, Min(0f)] private float musicFadeDuration = 0.5f;

    public string MainMenuScenePath => mainMenuScenePath;
    public IReadOnlyList<GameLevelDefinition> Levels => levels;
    public AudioClip MenuMusic => menuMusic;
    public AudioClip ButtonSound => buttonSound;
    public AudioClip CompleteSound => completeSound;
    public AudioClip FailureSound => failureSound;
    public float TransitionDuration => transitionDuration;
    public float MusicFadeDuration => musicFadeDuration;

    public GameLevelDefinition FindLevel(string id)
    {
        return levels.Find(level => level.id == id);
    }

    public GameLevelDefinition FindScene(string path)
    {
        return levels.Find(level => level.scenePath == path);
    }
}
