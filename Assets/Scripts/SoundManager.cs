using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [System.Serializable]
    public struct SoundData
    {
        public string soundID;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume;
    }

    [System.Serializable]
    public struct BgmData
    {
        public string bgmID;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume;
    }

    [Header("SFX List")]
    public List<SoundData> soundList = new List<SoundData>();

    [Header("BGM List")]
    public List<BgmData> bgmList = new List<BgmData>();

    [Header("Audio Sources")]
    public AudioSource sfxAudioSource;
    public AudioSource bgmAudioSource;

    [Header("BGM Fade Settings")]
    [Tooltip("Duration for BGM fade in effect")]
    public float bgmFadeDuration = 1.5f;

    private Dictionary<string, SoundData> soundDictionary = new Dictionary<string, SoundData>();
    private Dictionary<string, BgmData> bgmDictionary = new Dictionary<string, BgmData>();
    private Coroutine bgmFadeCoroutine;
    private Coroutine sfxFadeCoroutine; // 用來記錄 SFX 淡出協程

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeDictionaries();
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (sfxAudioSource == null)
        {
            sfxAudioSource = gameObject.AddComponent<AudioSource>();
        }

        if (bgmAudioSource == null)
        {
            bgmAudioSource = gameObject.AddComponent<AudioSource>();
        }
        bgmAudioSource.loop = true;
    }

    private void InitializeDictionaries()
    {
        soundDictionary.Clear();
        foreach (var sound in soundList)
        {
            if (!string.IsNullOrEmpty(sound.soundID) && sound.clip != null)
            {
                if (!soundDictionary.ContainsKey(sound.soundID))
                {
                    soundDictionary.Add(sound.soundID, sound);
                }
            }
        }

        bgmDictionary.Clear();
        foreach (var bgm in bgmList)
        {
            if (!string.IsNullOrEmpty(bgm.bgmID) && bgm.clip != null)
            {
                if (!bgmDictionary.ContainsKey(bgm.bgmID))
                {
                    bgmDictionary.Add(bgm.bgmID, bgm);
                }
            }
        }
    }

    public void PlaySound(string soundID)
    {
        PlaySound(soundID, false);
    }

    public void PlaySound(string soundID, bool loop)
    {
        if (soundDictionary.Count == 0 && soundList.Count > 0)
        {
            InitializeDictionaries();
        }

        if (sfxAudioSource == null)
        {
            sfxAudioSource = gameObject.AddComponent<AudioSource>();
        }

        if (soundDictionary.TryGetValue(soundID, out SoundData data))
        {
            // 如果剛好有正在執行的淡出協程，先關閉它以防音量錯亂
            if (sfxFadeCoroutine != null)
            {
                StopCoroutine(sfxFadeCoroutine);
                sfxFadeCoroutine = null;
            }

            sfxAudioSource.loop = loop;
            sfxAudioSource.clip = data.clip;
            sfxAudioSource.volume = data.volume;
            sfxAudioSource.Play();
        }
        else
        {
            Debug.LogWarning($"[SoundManager] SFX not found: {soundID}");
        }
    }

    public void StopSFX()
    {
        if (sfxFadeCoroutine != null)
        {
            StopCoroutine(sfxFadeCoroutine);
            sfxFadeCoroutine = null;
        }

        if (sfxAudioSource != null)
        {
            sfxAudioSource.Stop();
        }
    }

    /// <summary>
    /// 【新增】支援淡出停止 SFX
    /// </summary>
    public void FadeOutSFX(float duration)
    {
        if (sfxAudioSource == null || !sfxAudioSource.isPlaying) return;

        if (sfxFadeCoroutine != null)
        {
            StopCoroutine(sfxFadeCoroutine);
        }
        sfxFadeCoroutine = StartCoroutine(FadeOutSFXRoutine(duration));
    }

    private IEnumerator FadeOutSFXRoutine(float duration)
    {
        float startVolume = sfxAudioSource.volume;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            sfxAudioSource.volume = Mathf.Lerp(startVolume, 0f, timer / duration);
            yield return null;
        }

        sfxAudioSource.Stop();
        sfxAudioSource.volume = startVolume; // 恢復預設音量供下次使用
        sfxFadeCoroutine = null;
    }

    public void PlayBGM(string bgmID)
    {
        if (bgmAudioSource == null)
        {
            bgmAudioSource = gameObject.AddComponent<AudioSource>();
            bgmAudioSource.loop = true;
        }

        if (bgmDictionary.Count == 0 && bgmList.Count > 0)
        {
            InitializeDictionaries();
        }

        if (bgmDictionary.TryGetValue(bgmID, out BgmData data))
        {
            if (bgmAudioSource.isPlaying && bgmAudioSource.clip == data.clip)
            {
                return;
            }

            if (bgmFadeCoroutine != null)
            {
                StopCoroutine(bgmFadeCoroutine);
            }

            bgmAudioSource.Stop();
            bgmAudioSource.clip = data.clip;
            bgmAudioSource.volume = 0f;
            bgmAudioSource.Play();

            bgmFadeCoroutine = StartCoroutine(FadeInBGM(data.volume, bgmFadeDuration));
        }
        else
        {
            Debug.LogWarning($"[SoundManager] BGM not found: {bgmID}");
        }
    }

    private IEnumerator FadeInBGM(float targetVolume, float duration)
    {
        float timer = 0f;
        bgmAudioSource.volume = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            bgmAudioSource.volume = Mathf.Lerp(0f, targetVolume, timer / duration);
            yield return null;
        }

        bgmAudioSource.volume = targetVolume;
        bgmFadeCoroutine = null;
    }

    public void StopBGM()
    {
        if (bgmFadeCoroutine != null)
        {
            StopCoroutine(bgmFadeCoroutine);
            bgmFadeCoroutine = null;
        }

        if (bgmAudioSource != null)
        {
            bgmAudioSource.Stop();
        }
    }

    public void HandleInkTag(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return;

        string cleanTag = tag.Trim();
        if (cleanTag.StartsWith("#")) cleanTag = cleanTag.Substring(1).Trim();

        if (cleanTag.StartsWith("bgm:"))
        {
            string bgmName = cleanTag.Substring(4).Trim();
            if (!string.IsNullOrEmpty(bgmName))
            {
                PlayBGM(bgmName);
            }
        }
        else if (cleanTag.StartsWith("sfx:"))
        {
            string sfxParams = cleanTag.Substring(4).Trim();
            if (!string.IsNullOrEmpty(sfxParams))
            {
                // 檢查是否為直接停止語法 #sfx:stop
                if (sfxParams.Equals("stop", System.StringComparison.OrdinalIgnoreCase))
                {
                    StopSFX();
                    return;
                }

                bool isLoop = false;
                if (sfxParams.EndsWith(".loop", System.StringComparison.OrdinalIgnoreCase))
                {
                    isLoop = true;
                    sfxParams = sfxParams.Substring(0, sfxParams.Length - 5).Trim();
                }

                PlaySound(sfxParams, isLoop);
            }
        }
        // 【新增】支援透過點語法或冒號語法停止音效
        else if (cleanTag.StartsWith("sfx.stop", System.StringComparison.OrdinalIgnoreCase))
        {
            StopSFX();
        }
        else if (cleanTag.StartsWith("sfx.fadeout", System.StringComparison.OrdinalIgnoreCase))
        {
            // 例如 #sfx.fadeout.2.0
            string[] parts = cleanTag.Split('.');
            float fadeTime = 1.5f; // 預設 1.5 秒
            if (parts.Length >= 3 && float.TryParse(parts[2], out float parsedTime))
            {
                fadeTime = parsedTime;
            }
            FadeOutSFX(fadeTime);
        }
    }

    public void PlaySoundAtPosition(string soundID, Vector3 position)
    {
        if (soundDictionary.TryGetValue(soundID, out SoundData data))
        {
            AudioSource.PlayClipAtPoint(data.clip, position, data.volume);
        }
        else
        {
            Debug.LogWarning($"[SoundManager] SFX not found: {soundID}");
        }
    }
}