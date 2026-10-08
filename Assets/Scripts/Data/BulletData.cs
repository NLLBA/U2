using System.Collections.Generic;
using System.Xml.Serialization;

public class BulletData
{
    [XmlArray("bulletInfoList")]
    public List<BulletInfo> bullets = new List<BulletInfo>();
}

public class BulletInfo
{
    // 子弹数据 ID。
    [XmlAttribute]
    public int id;

    // 子弹移动规则。
    [XmlAttribute]
    public int type;

    [XmlAttribute("forwardSpeed")]
    public float moveRate;

    [XmlAttribute("rightSpeed")]
    public float sideSpeed;

    [XmlAttribute("roundSpeed")]
    public float turnSpeed;

    [XmlAttribute("resName")]
    public string asset;

    [XmlAttribute("deadEffRes")]
    public string deathFx;

    [XmlAttribute("lifeTime")]
    public float life;
}
