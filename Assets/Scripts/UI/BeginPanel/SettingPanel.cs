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

        MusicData musicData = GameDataMgr.Instance.musicData;
        sliderMusic.value = musicData.musicVolume;
        sliderSound.value = musicData.soundVolume;
        toggleMusic.value = musicData.musicOn;
        toggleSound.value = musicData.soundOn;
    }

    public override void Hideme()
    {
        base.Hideme();
        GameDataMgr.Instance.SaveMusic();
    }
}
