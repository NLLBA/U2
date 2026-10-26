using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BKMusic : MonoBehaviour
{
    private static BKMusic instance;

    public static BKMusic Instance => instance;

    private AudioSource source;
    private Coroutine playRoutine;
    private bool musicEnabled;
    private bool combatIntensity;
    public bool CombatIntensity => combatIntensity;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            // Each authored scene can provide a different clip. Keep one
            // persistent player, but let the newly loaded scene select its
            // track before the duplicate is removed.
            AudioSource sceneSource = GetComponent<AudioSource>();
            if (sceneSource != null && sceneSource.clip != null)
                instance.UseSceneClip(sceneSource.clip);
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (Application.isPlaying)
            DontDestroyOnLoad(gameObject);
        // 获取音效组件。
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        ApplySavedSettings();
    }

    private void Start()
    {
        // Awake 时音频资源可能还未完成加载，首帧再确认一次播放状态。
        ApplySavedSettings();
    }

    private void OnEnable()
    {
        if (source != null)
        {
            ApplySavedSettings();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            StartPlaybackWhenReady();
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused)
            StartPlaybackWhenReady();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public void SetOpen(bool isOpen)
    {
        if (!EnsureSource())
            return;

        musicEnabled = isOpen;
        source.loop = true;
        source.mute = !isOpen;
        if (!isOpen)
        {
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
                playRoutine = null;
            }

            source.Stop();
            return;
        }

        StartPlaybackWhenReady();
    }

    public void SetMusicVolume(float value)
    {
        if (EnsureSource())
        {
            source.volume = Mathf.Clamp01(value);
        }
    }

    private void UseSceneClip(AudioClip clip)
    {
        if (clip == null || source == null || source.clip == clip)
        {
            StartPlaybackWhenReady();
            return;
        }

        source.Stop();
        source.clip = clip;
        StartPlaybackWhenReady();
    }

    public void SetCombatIntensity(bool intense)
    {
        combatIntensity = intense;
        if (EnsureSource())
            source.pitch = combatIntensity ? 1.12f : 1f;
    }

    private void ApplySavedSettings()
    {
        if (!EnsureSource())
            return;

        MusicData settings = GameDataMgr.Instance.musicData;
        source.pitch = combatIntensity ? 1.12f : 1f;
        SetMusicVolume(settings.musicVolume);
        SetOpen(settings.musicOn);
    }

    private void StartPlaybackWhenReady()
    {
        if (!EnsureSource() || !musicEnabled || source.clip == null || source.isPlaying || playRoutine != null)
            return;

        if (source.clip.loadState == AudioDataLoadState.Loaded)
        {
            source.Play();
            return;
        }

        if (source.clip.loadState == AudioDataLoadState.Unloaded)
            source.clip.LoadAudioData();

        playRoutine = StartCoroutine(PlayAfterLoading());
    }

    private bool EnsureSource()
    {
        if (source == null)
            source = GetComponent<AudioSource>();
        return source != null;
    }

    private IEnumerator PlayAfterLoading()
    {
        float timeout = 0f;
        while (musicEnabled && source != null && source.clip != null &&
            source.clip.loadState == AudioDataLoadState.Loading && timeout < 10f)
        {
            timeout += Time.unscaledDeltaTime;
            yield return null;
        }

        if (musicEnabled && source != null && source.clip != null &&
            source.clip.loadState == AudioDataLoadState.Loaded && !source.isPlaying)
        {
            source.Play();
        }

        playRoutine = null;
    }
}
