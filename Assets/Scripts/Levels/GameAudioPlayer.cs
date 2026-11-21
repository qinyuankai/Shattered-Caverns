using System.Collections;
using UnityEngine;

public class GameAudioPlayer : MonoBehaviour
{
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource soundSource;
    private GameSaveStore saveStore;
    private Coroutine musicTransition;
    private AudioClip requestedMusic;
    private float musicGain = 1f;
    private bool isMusicPaused;

    public float MasterVolume => saveStore.Data.masterVolume;
    public float MusicVolume => saveStore.Data.musicVolume;
    public float SoundVolume => saveStore.Data.soundVolume;
    public AudioClip CurrentMusic => musicSource.clip;
    public bool IsMusicPaused => isMusicPaused;

    public void Initialize(GameSaveStore store)
    {
        saveStore = store;
        ApplyVolumes();
    }

    public void PlayMusic(AudioClip clip)
    {
        PlayMusic(clip, 0f);
    }

    public void PlayMusic(AudioClip clip, float fadeDuration)
    {
        if (requestedMusic == clip && (musicTransition != null || musicSource.clip == clip)) return;
        CancelMusicTransition();
        requestedMusic = clip;
        if (fadeDuration > 0f && !isMusicPaused && isActiveAndEnabled)
        {
            musicTransition = StartCoroutine(TransitionMusic(clip, fadeDuration));
            return;
        }
        SetMusic(clip);
        musicGain = 1f;
        ApplyVolumes();
    }

    public void StopMusic(float fadeDuration = 0f)
    {
        PlayMusic(null, fadeDuration);
    }

    public void PauseMusic()
    {
        isMusicPaused = true;
        musicSource.Pause();
    }

    public void ResumeMusic()
    {
        if (!isMusicPaused) return;
        isMusicPaused = false;
        musicSource.UnPause();
    }

    private void SetMusic(AudioClip clip)
    {
        musicSource.Stop();
        musicSource.clip = clip;
        musicSource.loop = true;
        if (clip == null) return;
        musicSource.Play();
        if (isMusicPaused) musicSource.Pause();
    }

    private IEnumerator TransitionMusic(AudioClip clip, float duration)
    {
        if (musicSource.clip != null) yield return FadeMusic(0f, duration);
        SetMusic(clip);
        musicGain = 0f;
        ApplyVolumes();
        if (clip != null) yield return FadeMusic(1f, duration);
        musicGain = 1f;
        ApplyVolumes();
        musicTransition = null;
    }

    private IEnumerator FadeMusic(float target, float duration)
    {
        float initial = musicGain;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (!isMusicPaused)
            {
                elapsed += Time.unscaledDeltaTime;
                musicGain = Mathf.Lerp(initial, target, elapsed / duration);
                ApplyVolumes();
            }
            yield return null;
        }
    }

    private void CancelMusicTransition()
    {
        if (musicTransition == null) return;
        StopCoroutine(musicTransition);
        musicTransition = null;
    }

    private void OnDisable()
    {
        CancelMusicTransition();
        requestedMusic = musicSource.clip;
        musicGain = 1f;
        if (saveStore != null) ApplyVolumes();
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
        musicSource.volume = MasterVolume * MusicVolume * musicGain;
        soundSource.volume = MasterVolume * SoundVolume;
    }
}
