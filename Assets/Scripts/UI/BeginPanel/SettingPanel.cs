using UnityEngine;

public class SettingPanel : BasePanel<SettingPanel>
{
    public UIButton btnClose;
    public UISlider sliderMusic;
    public UISlider sliderSound;
    public UIToggle toggleMusic;
    public UIToggle toggleSound;

    public override void Init()
    {
        RefreshControls();

        btnClose.onClick.Add(new EventDelegate(Hideme));
        sliderMusic.onChange.Add(new EventDelegate(() =>
            GameDataMgr.Instance.SetMusicVolume(sliderMusic.value)));
        sliderSound.onChange.Add(new EventDelegate(() =>
            GameDataMgr.Instance.SetSoundVolume(sliderSound.value)));
        toggleMusic.onChange.Add(new EventDelegate(() =>
            GameDataMgr.Instance.SetMusic(toggleMusic.value)));
        toggleSound.onChange.Add(new EventDelegate(() =>
            GameDataMgr.Instance.SetSound(toggleSound.value)));
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
        MusicData musicData = GameDataMgr.Instance.musicData;
        sliderMusic.Set(musicData.musicVolume, false);
        sliderSound.Set(musicData.soundVolume, false);
        SetEnabledToggle(toggleMusic, musicData.musicOn);
        SetEnabledToggle(toggleSound, musicData.soundOn);
    }

    private static void SetEnabledToggle(UIToggle toggle, bool enabled)
    {
        if (toggle == null)
            return;

        // The cyan indicator always means that this audio channel is enabled.
        toggle.invertSpriteState = false;
        toggle.Set(enabled, false);

        // Set() does not refresh the sprite when the logical value is unchanged.
        if (toggle.activeSprite != null)
        {
            toggle.activeSprite.alpha = enabled ? 1f : 0f;
        }
    }
}
