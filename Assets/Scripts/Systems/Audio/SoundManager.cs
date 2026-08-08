using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum SoundCue
{
    UiClick,
    PlayerHit,
    Skill,
    BossWarning,
    BossAppear,
    BossCleared,
    GameOver,
}

public class SoundManager : MonoBehaviour
{
    private const int SampleRate = 44100;

    private static SoundManager instance;

    public static SoundManager Instance => instance;

    private readonly Dictionary<SoundCue, AudioClip> clips = new Dictionary<SoundCue, AudioClip>();
    private readonly HashSet<int> boundButtonIds = new HashSet<int>();

    private AudioSource audioSource;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static SoundManager EnsureInstance()
    {
        if (instance != null)
            return instance;

        GameObject host = new GameObject("SoundManager");
        return host.AddComponent<SoundManager>();
    }

    public static void Play(SoundCue cue)
    {
        if (!Application.isPlaying)
            return;

        EnsureInstance().PlayInternal(cue);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.ignoreListenerPause = true;

        CreateDefaultClips();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        StartCoroutine(BindButtonsAfterSceneLoad());
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        instance = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        boundButtonIds.Clear();
        StartCoroutine(BindButtonsAfterSceneLoad());
    }

    private IEnumerator BindButtonsAfterSceneLoad()
    {
        yield return null;

        UIButton[] buttons = Resources.FindObjectsOfTypeAll<UIButton>();
        for (int i = 0; i < buttons.Length; i++)
        {
            UIButton button = buttons[i];
            if (button == null || !button.gameObject.scene.IsValid())
                continue;

            int id = button.GetInstanceID();
            if (!boundButtonIds.Add(id))
                continue;

            button.onClick.Add(new EventDelegate(PlayUiClick));
        }
    }

    private void PlayUiClick()
    {
        PlayInternal(SoundCue.UiClick);
    }

    private void PlayInternal(SoundCue cue)
    {
        MusicData settings = GameDataMgr.Instance.musicData;
        if (settings == null || !settings.SoundIsopen)
            return;

        AudioClip clip;
        if (!clips.TryGetValue(cue, out clip) || clip == null)
            return;

        audioSource.PlayOneShot(clip, Mathf.Clamp01(settings.soundValue));
    }

    private void CreateDefaultClips()
    {
        clips[SoundCue.UiClick] = CreateTone("UI Click", 0.08f, 900f, 620f, 0.28f, 0.08f);
        clips[SoundCue.PlayerHit] = CreateTone("Player Hit", 0.2f, 180f, 70f, 0.48f, 0.35f);
        clips[SoundCue.Skill] = CreateTone("Skill", 0.3f, 420f, 1180f, 0.34f, 0.12f);
        clips[SoundCue.BossWarning] = CreateTone("Boss Warning", 0.42f, 720f, 420f, 0.42f, 0.18f);
        clips[SoundCue.BossAppear] = CreateTone("Boss Appear", 0.55f, 110f, 260f, 0.5f, 0.32f);
        clips[SoundCue.BossCleared] = CreateTone("Boss Cleared", 0.45f, 420f, 980f, 0.4f, 0.08f);
        clips[SoundCue.GameOver] = CreateTone("Game Over", 0.7f, 360f, 90f, 0.42f, 0.16f);
    }

    private AudioClip CreateTone(string name, float duration, float startFrequency, float endFrequency,
        float amplitude, float noiseAmount)
    {
        int sampleCount = Mathf.Max(1, Mathf.CeilToInt(duration * SampleRate));
        float[] samples = new float[sampleCount];
        float phase = 0f;
        uint noiseState = 2463534242u;

        for (int i = 0; i < sampleCount; i++)
        {
            float progress = (float)i / sampleCount;
            float frequency = Mathf.Lerp(startFrequency, endFrequency, progress);
            phase += Mathf.PI * 2f * frequency / SampleRate;

            float attack = Mathf.Clamp01(progress / 0.04f);
            float release = Mathf.Clamp01((1f - progress) / 0.25f);
            float envelope = attack * release;

            noiseState ^= noiseState << 13;
            noiseState ^= noiseState >> 17;
            noiseState ^= noiseState << 5;
            float noise = ((noiseState & 0xffffu) / 32767.5f) - 1f;

            float harmonic = Mathf.Sin(phase) + Mathf.Sin(phase * 2f) * 0.22f;
            samples[i] = (harmonic * (1f - noiseAmount) + noise * noiseAmount) * amplitude * envelope;
        }

        AudioClip clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
