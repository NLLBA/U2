using System.Collections;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;

public class RoleData 
{
    [XmlArray("roleDatas")]
    public List<RoleInfo> roles = new List<RoleInfo>();
}

public class RoleInfo
{
    [XmlAttribute]
    public int hp;
    [XmlAttribute]
    public int speed;
    [XmlAttribute]
    public int volume;
    [XmlAttribute("resName")]
    public string asset;
    [XmlAttribute]
    public float scale;
    [XmlAttribute("shieldStyle")]
    public int shieldId;
}
