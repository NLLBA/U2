using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BKMusic : MonoBehaviour
{
    private static BKMusic instance;

    public static BKMusic Instance => instance;

    private AudioSource bkaudio;

   private AudioClip clip;

    private void Awake()
    {
        instance = this;
        //获取音效组件
        bkaudio = this.GetComponent<AudioSource>();
        //背景音乐初始化
        SetBKMusicIsOpen(GameDataMgr.Instance.musicData.musicIsopen);
        SetMusicValue(GameDataMgr.Instance.musicData.musicvalue);
    }

    public void SetBKMusicIsOpen(bool isOpen)
    {
        bkaudio.mute = isOpen;
    }

    public void SetMusicValue(float value)
    {
        bkaudio.volume = value;
    }
    
}
