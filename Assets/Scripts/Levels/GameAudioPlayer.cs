using UnityEngine;

public class GameAudioPlayer : MonoBehaviour
{
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource soundSource;
    private GameSaveStore saveStore;

    public float MasterVolume => saveStore.Data.masterVolume;
    public float MusicVolume => saveStore.Data.musicVolume;
    public float SoundVolume => saveStore.Data.soundVolume;
    public AudioClip CurrentMusic => musicSource.clip;

    public void Initialize(GameSaveStore store)
    {
        saveStore = store;
        ApplyVolumes();
    }

    public void PlayMusic(AudioClip clip)
    {
        if (musicSource.clip == clip) return;
        musicSource.Stop();
        musicSource.clip = clip;
        if (clip != null) musicSource.Play();
    }

    public void PlaySound(AudioClip clip)
    {
        if (clip != null) soundSource.PlayOneShot(clip);
    }

    public void SetMasterVolume(float value)
    {
        saveStore.Data.masterVolume = Mathf.Clamp01(value);
        ApplyVolumes();
    }

    public void SetMusicVolume(float value)
    {
        saveStore.Data.musicVolume = Mathf.Clamp01(value);
        ApplyVolumes();
    }

    public void SetSoundVolume(float value)
    {
        saveStore.Data.soundVolume = Mathf.Clamp01(value);
        ApplyVolumes();
    }

    private void ApplyVolumes()
    {
        musicSource.volume = MasterVolume * MusicVolume;
        soundSource.volume = MasterVolume * SoundVolume;
    }
}
