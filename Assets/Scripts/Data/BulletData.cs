using System.Collections;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;

public class BulletData 
{
    public List<BulletInfo> bulletInfoList = new List<BulletInfo>();
}

public class BulletInfo
{
    //子弹数据的ID
    [XmlAttribute]
    public int id;
    //子弹的移动规则
    [XmlAttribute]
    public int type;
    //正朝向 移动速度
    [XmlAttribute]
    public float forwardSpeed;
    //横向移动速度
    [XmlAttribute]
    public float rightSpeed;
    //旋转速度
    [XmlAttribute]
    public float roundSpeed;
    //子弹特效资源路径
    [XmlAttribute]
    public string resName;
    //子弹销毁时 创建的死亡特效
    [XmlAttribute]
    public string deadEffRes;
    //子弹出生到销毁的生命周期时间
    [XmlAttribute]
    public float lifeTime;
}
