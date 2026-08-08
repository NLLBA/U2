using System.Collections;
using System.Collections.Generic;
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
        btnClose.onClick.Add(new EventDelegate(() =>
        {
            //隐藏自己
            Hideme();
        }));

        sliderMusic.onChange.Add(new EventDelegate(() =>
        {
            //改变音量大小
            GameDataMgr.Instance.SetMusicValue(sliderMusic.value);
        }));

        sliderSound.onChange.Add(new EventDelegate(() =>
        {
            //改变音效大小
            GameDataMgr.Instance.SetSoundValue(sliderSound.value);
        }));

        toggleMusic.onChange.Add(new EventDelegate(() =>
        {
            //背景音乐开关
            GameDataMgr.Instance.SetMusicIsOpen(toggleMusic.value);
        }));

        toggleSound.onChange.Add(new EventDelegate(() =>
        {
            //音效开关
            GameDataMgr.Instance.SetSoundIsOpen(toggleSound.value);
        }));

        Hideme();
    }

    public override void Showme()
    {
        base.Showme();
        //显示自己时 更新内容
        MusicData musicData = GameDataMgr.Instance.musicData;
        sliderMusic.value = musicData.musicvalue;
        sliderSound.value = musicData.soundValue;
        toggleMusic.value = musicData.musicIsopen;
        toggleSound.value = musicData.SoundIsopen;

    }

    public override void Hideme()
    {
        base.Hideme();
        //隐藏自己时 需要保存的数据
        GameDataMgr.Instance.SaveMusic();
    }

}
