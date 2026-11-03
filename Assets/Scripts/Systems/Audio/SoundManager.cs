using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    private readonly HashSet<int> buttonIds = new HashSet<int>();

    private AudioSource source;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        Ensure();
    }

    public static SoundManager Ensure()
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

        Ensure().PlayClip(cue);
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

        source = gameObject.GetComponent<AudioSource>();
        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
        }

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;

        CreateClips();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        StartCoroutine(BindButtons());
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        buttonIds.Clear();
        StartCoroutine(BindButtons());
    }

    private IEnumerator BindButtons()
    {
        yield return null;

        Button[] buttons = Resources.FindObjectsOfTypeAll<Button>();
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null || !button.gameObject.scene.IsValid())
                continue;

            int id = button.GetInstanceID();
            if (!buttonIds.Add(id))
                continue;

            button.onClick.AddListener(PlayUiClick);
        }
    }

    private void PlayUiClick()
    {
        PlayClip(SoundCue.UiClick);
    }

    private void PlayClip(SoundCue cue)
    {
        MusicData settings = GameDataMgr.Instance.musicData;
        if (settings == null || !settings.soundOn)
            return;

        AudioClip clip;
        if (!clips.TryGetValue(cue, out clip) || clip == null)
            return;

        source.PlayOneShot(clip, Mathf.Clamp01(settings.soundVolume));
    }

    private void CreateClips()
    {
        clips[SoundCue.UiClick] = NewTone("UI Click", 0.08f, 900f, 620f, 0.28f, 0.08f);
        clips[SoundCue.PlayerHit] = NewTone("Player Hit", 0.2f, 180f, 70f, 0.48f, 0.35f);
        clips[SoundCue.Skill] = NewTone("Skill", 0.3f, 420f, 1180f, 0.34f, 0.12f);
        clips[SoundCue.BossWarning] = NewTone("Boss Warning", 0.42f, 720f, 420f, 0.42f, 0.18f);
        clips[SoundCue.BossAppear] = NewTone("Boss Appear", 0.55f, 110f, 260f, 0.5f, 0.32f);
        clips[SoundCue.BossCleared] = NewTone("Boss Cleared", 0.45f, 420f, 980f, 0.4f, 0.08f);
        clips[SoundCue.GameOver] = NewTone("Game Over", 0.7f, 360f, 90f, 0.42f, 0.16f);
    }

    private AudioClip NewTone(string name, float duration, float startFreq, float endFreq,
        float amplitude, float noiseMix)
    {
        int count = Mathf.Max(1, Mathf.CeilToInt(duration * SampleRate));
        float[] samples = new float[count];
        float phase = 0f;
        uint noiseSeed = 2463534242u;

        for (int i = 0; i < count; i++)
        {
            float progress = (float)i / count;
            float frequency = Mathf.Lerp(startFreq, endFreq, progress);
            phase += Mathf.PI * 2f * frequency / SampleRate;

            float attack = Mathf.Clamp01(progress / 0.04f);
            float release = Mathf.Clamp01((1f - progress) / 0.25f);
            float envelope = attack * release;

            noiseSeed ^= noiseSeed << 13;
            noiseSeed ^= noiseSeed >> 17;
            noiseSeed ^= noiseSeed << 5;
            float noise = ((noiseSeed & 0xffffu) / 32767.5f) - 1f;

            float harmonic = Mathf.Sin(phase) + Mathf.Sin(phase * 2f) * 0.22f;
            samples[i] = (harmonic * (1f - noiseMix) + noise * noiseMix) * amplitude * envelope;
        }

        AudioClip clip = AudioClip.Create(name, count, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
