using System.Collections.Generic;
using System.Xml.Serialization;

public class DifficultyData
{
    public List<DifficultyInfo> difficultyInfos = new List<DifficultyInfo>();
}

public class DifficultyInfo
{
    [XmlAttribute]
    public int id;

    [XmlAttribute]
    public string name;

    [XmlAttribute]
    public int hpAdd;

    [XmlAttribute]
    public float bulletSpeedMul;

    [XmlAttribute]
    public float fireCdMul;

    [XmlAttribute]
    public float fireDelayMul;

    [XmlAttribute]
    public float shieldTime;

    [XmlAttribute]
    public float skillCD;
}
