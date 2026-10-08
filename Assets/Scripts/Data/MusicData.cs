using System.Xml.Serialization;

public class MusicData
{
    // 背景音乐音量，范围 0-1。
    [XmlElement("musicvalue")]
    public float musicVolume = 0.5f;

    // 背景音乐开关。
    [XmlElement("musicIsopen")]
    public bool musicOn = true;

    // 音效音量，范围 0-1。
    [XmlElement("soundValue")]
    public float soundVolume = 0.5f;

    // 音效开关。
    [XmlElement("SoundIsopen")]
    public bool soundOn = true;
}
