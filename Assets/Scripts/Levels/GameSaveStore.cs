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
        bool changed = false;
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
            changed = true;
        }

        if (Data.unlockedLevelIds == null)
        {
            Data.unlockedLevelIds = new List<string>();
            changed = true;
        }
        if (Data.completedLevelIds == null)
        {
            Data.completedLevelIds = new List<string>();
            changed = true;
        }

        float masterVolume = SanitizeVolume(Data.masterVolume, 1f);
        float musicVolume = SanitizeVolume(Data.musicVolume, 0.7f);
        float soundVolume = SanitizeVolume(Data.soundVolume, 0.8f);
        changed |= masterVolume != Data.masterVolume || musicVolume != Data.musicVolume || soundVolume != Data.soundVolume;
        Data.masterVolume = masterVolume;
        Data.musicVolume = musicVolume;
        Data.soundVolume = soundVolume;
        changed |= NormalizeProgress(configuration);
        if (changed) Save();
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

    private bool NormalizeProgress(GameConfiguration configuration)
    {
        bool changed = false;
        var validIds = new HashSet<string>();
        foreach (GameLevelDefinition level in configuration.Levels) validIds.Add(level.id);

        changed |= NormalizeIds(Data.unlockedLevelIds, validIds);
        changed |= NormalizeIds(Data.completedLevelIds, validIds);
        foreach (string id in Data.completedLevelIds)
        {
            if (Data.unlockedLevelIds.Contains(id)) continue;
            Data.unlockedLevelIds.Add(id);
            changed = true;
        }

        if (!string.IsNullOrEmpty(Data.lastLevelId) && !validIds.Contains(Data.lastLevelId))
        {
            Data.lastLevelId = null;
            changed = true;
        }
        if (configuration.Levels.Count > 0 && !IsUnlocked(configuration.Levels[0].id))
        {
            Data.unlockedLevelIds.Add(configuration.Levels[0].id);
            changed = true;
        }
        return changed;
    }

    private static bool NormalizeIds(List<string> ids, HashSet<string> validIds)
    {
        bool changed = false;
        var seen = new HashSet<string>();
        for (int i = 0; i < ids.Count;)
        {
            if (validIds.Contains(ids[i]) && seen.Add(ids[i]))
            {
                i++;
                continue;
            }
            ids.RemoveAt(i);
            changed = true;
        }
        return changed;
    }
}
