using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Xml.Serialization;

public class LevelData
{
    [XmlArray("levelInfos")]
    public List<LevelInfo> levels = new List<LevelInfo>();
}

public class LevelInfo
{
    [XmlAttribute]
    public int id;

    [XmlAttribute]
    public string name;

    [XmlAttribute]
    public float beginTime;

    [XmlAttribute("bulletSpeedMul")]
    public float bulletRate;

    [XmlAttribute("fireCdMul")]
    public float fireRate;

    [XmlAttribute("fireDelayMul")]
    public float delayRate;

    [XmlAttribute]
    public string fireIds;
}
