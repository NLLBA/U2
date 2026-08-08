using System.Collections.Generic;
using System.Xml.Serialization;

public class LevelData
{
    public List<LevelInfo> levelInfos = new List<LevelInfo>();
}

public class LevelInfo
{
    [XmlAttribute]
    public int id;

    [XmlAttribute]
    public string name;

    [XmlAttribute]
    public float beginTime;

    [XmlAttribute]
    public float bulletSpeedMul;

    [XmlAttribute]
    public float fireCdMul;

    [XmlAttribute]
    public float fireDelayMul;

    [XmlAttribute]
    public string fireIds;
}
