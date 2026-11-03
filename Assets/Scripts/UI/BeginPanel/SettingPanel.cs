using UnityEngine;
using UnityEngine.UI;

public class SettingPanel : BasePanel<SettingPanel>
{
    public Button btnClose;
    public Slider sliderMusic;
    public Slider sliderSound;
    public Toggle toggleMusic;
    public Toggle toggleSound;

    private Text musicLabel;
    private Text soundLabel;
    private Text musicValue;
    private Text soundValue;

    public override void Init()
    {
        BuildAudioLabels();
        RefreshControls();

        btnClose.onClick.AddListener(Hideme);
        sliderMusic.onValueChanged.AddListener(OnMusicVolumeChanged);
        sliderSound.onValueChanged.AddListener(OnSoundVolumeChanged);
        toggleMusic.onValueChanged.AddListener(OnMusicToggleChanged);
        toggleSound.onValueChanged.AddListener(OnSoundToggleChanged);
        Hideme();
    }

    public override void Showme()
    {
        base.Showme();
        RefreshControls();
    }

    public override void Hideme()
    {
        base.Hideme();
        GameDataMgr.Instance.SaveMusic();
    }

    private void RefreshControls()
    {
        BuildAudioLabels();
        MusicData musicData = GameDataMgr.Instance.musicData;
        sliderMusic.SetValueWithoutNotify(musicData.musicVolume);
        sliderSound.SetValueWithoutNotify(musicData.soundVolume);
        SetEnabledToggle(toggleMusic, musicData.musicOn);
        SetEnabledToggle(toggleSound, musicData.soundOn);
        RefreshValueLabels();
    }

    private void BuildAudioLabels()
    {
        Transform view = UGUIRuntime.View(this);
        if (view == null)
            return;

        Text template = UGUIRuntime.Find<Text>(view, "lblTitle");
        musicLabel = EnsureLabel(view, "UGUI_MusicLabel", new Vector2(-330f, 75f),
            new Vector2(190f, 48f), "背景音乐", 25,
            new Color(0.88f, 0.94f, 0.97f, 1f), TextAnchor.MiddleCenter, template);
        soundLabel = EnsureLabel(view, "UGUI_SoundLabel", new Vector2(-330f, -100f),
            new Vector2(190f, 48f), "战斗音效", 25,
            new Color(0.88f, 0.94f, 0.97f, 1f), TextAnchor.MiddleCenter, template);
        musicValue = EnsureLabel(view, "UGUI_MusicValue", new Vector2(240f, 75f),
            new Vector2(92f, 38f), "100%", 20,
            new Color(0.28f, 0.88f, 0.94f, 1f), TextAnchor.MiddleCenter, template);
        soundValue = EnsureLabel(view, "UGUI_SoundValue", new Vector2(240f, -100f),
            new Vector2(92f, 38f), "100%", 20,
            new Color(0.28f, 0.88f, 0.94f, 1f), TextAnchor.MiddleCenter, template);

    }

    private static Text EnsureLabel(Transform parent, string name, Vector2 position, Vector2 size,
        string text, int fontSize, Color color, TextAnchor alignment, Text template)
    {
        Text label = UGUIRuntime.Find<Text>(parent, name);
        if (label == null)
        {
            label = UGUIRuntime.CreateText(parent, name, position, Mathf.RoundToInt(size.x),
                Mathf.RoundToInt(size.y), text, fontSize, color, alignment, template);
        }
        label.gameObject.SetActive(true);
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        label.rectTransform.anchoredPosition = position;
        label.rectTransform.sizeDelta = size;
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = Mathf.Max(14, fontSize - 3);
        label.resizeTextMaxSize = fontSize;
        return label;
    }

    private void OnMusicVolumeChanged(float value)
    {
        GameDataMgr.Instance.SetMusicVolume(value);
        RefreshValueLabels();
    }

    private void OnSoundVolumeChanged(float value)
    {
        GameDataMgr.Instance.SetSoundVolume(value);
        RefreshValueLabels();
    }

    private void OnMusicToggleChanged(bool value)
    {
        GameDataMgr.Instance.SetMusic(value);
    }

    private void OnSoundToggleChanged(bool value)
    {
        GameDataMgr.Instance.SetSound(value);
    }

    private void RefreshValueLabels()
    {
        if (musicValue != null)
            musicValue.text = Mathf.RoundToInt(sliderMusic.value * 100f) + "%";
        if (soundValue != null)
            soundValue.text = Mathf.RoundToInt(sliderSound.value * 100f) + "%";
    }

    private static void SetEnabledToggle(Toggle toggle, bool enabled)
    {
        if (toggle == null)
            return;

        toggle.SetIsOnWithoutNotify(enabled);
        UGUIToggleSwitchVisual visual = toggle.GetComponent<UGUIToggleSwitchVisual>();
        if (visual != null)
            visual.Refresh();
    }
}
