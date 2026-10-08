using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using System.Xml.Serialization;

public class DifficultyData
{
    [XmlArray("difficultyInfos")]
    public List<DifficultyInfo> difficulties = new List<DifficultyInfo>();
}

public class DifficultyInfo
{
    [XmlAttribute]
    public int id;

    [XmlAttribute]
    public string name;

    [XmlAttribute("hpAdd")]
    public int hpBonus;

    [XmlAttribute("bulletSpeedMul")]
    public float bulletRate;

    [XmlAttribute("fireCdMul")]
    public float fireRate;

    [XmlAttribute("fireDelayMul")]
    public float delayRate;

    [XmlAttribute]
    public float shieldTime;

    [XmlAttribute("skillCD")]
    public float skillCd;
}
