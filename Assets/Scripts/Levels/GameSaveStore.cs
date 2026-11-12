using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GameSaveData
{
    public int version = 1;
    public string lastLevelId;
    public List<string> unlockedLevelIds = new List<string>();
    public List<string> completedLevelIds = new List<string>();
    public float masterVolume = 1f;
    public float musicVolume = 0.7f;
    public float soundVolume = 0.8f;
}

public class GameSaveStore
{
    public const string SaveKey = "ShatteredCaverns.Progress.v1";
    public GameSaveData Data { get; private set; }

    public GameSaveStore(GameConfiguration configuration)
    {
        try
        {
            Data = JsonUtility.FromJson<GameSaveData>(PlayerPrefs.GetString(SaveKey, ""));
        }
        catch (ArgumentException)
        {
            Data = null;
        }

        if (Data == null || Data.version != 1)
        {
            Data = new GameSaveData();
        }

        if (Data.unlockedLevelIds == null) Data.unlockedLevelIds = new List<string>();
        if (Data.completedLevelIds == null) Data.completedLevelIds = new List<string>();
        Data.masterVolume = SanitizeVolume(Data.masterVolume, 1f);
        Data.musicVolume = SanitizeVolume(Data.musicVolume, 0.7f);
        Data.soundVolume = SanitizeVolume(Data.soundVolume, 0.8f);
        if (configuration.Levels.Count > 0) Unlock(configuration.Levels[0].id);
    }

    public bool IsUnlocked(string id) => Data.unlockedLevelIds.Contains(id);
    public bool IsCompleted(string id) => Data.completedLevelIds.Contains(id);

    public void Unlock(string id)
    {
        if (!IsUnlocked(id)) Data.unlockedLevelIds.Add(id);
    }

    public void Complete(string id)
    {
        if (!IsCompleted(id)) Data.completedLevelIds.Add(id);
    }

    public void Save()
    {
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Data));
        PlayerPrefs.Save();
    }

    private static float SanitizeVolume(float value, float fallback)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp01(value);
    }
}
