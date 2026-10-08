using UnityEngine;

public class BKMusic : MonoBehaviour
{
    private static BKMusic instance;

    public static BKMusic Instance => instance;

    private AudioSource source;
    private AudioClip clip;

    private void Awake()
    {
        instance = this;
        // 获取音效组件。
        source = GetComponent<AudioSource>();
        // 初始化背景音乐。
        SetOpen(GameDataMgr.Instance.musicData.musicOn);
        SetMusicVolume(GameDataMgr.Instance.musicData.musicVolume);
    }

    public void SetOpen(bool isOpen)
    {
        source.mute = isOpen;
    }

    public void SetMusicVolume(float value)
    {
        source.volume = value;
    }
}
