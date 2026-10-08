using System.Collections.Generic;
using System.Xml.Serialization;

public class FireData
{
    [XmlArray("fireInfoList")]
    public List<FireInfo> fires = new List<FireInfo>();
}

public class FireInfo
{
    // 发火点 ID。
    [XmlAttribute]
    public int id;

    // 发火点类型。
    [XmlAttribute]
    public int type;

    // 该组的子弹数量。
    [XmlAttribute]
    public int num;

    // 每颗子弹的间隔时间。
    [XmlAttribute]
    public float cd;

    // 关联的子弹 ID。
    [XmlAttribute]
    public string ids;

    // 组间隔时间。
    [XmlAttribute]
    public float delay;
}
