using System.Collections;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;

public class FireData 
{
    public List<FireInfo> fireInfoList = new List<FireInfo>();
}

public class FireInfo
{
    //发火点ID
    [XmlAttribute]
    public int id;
    //发火点类型
    [XmlAttribute]
    public int type;
    //数量 该组子弹 有多少颗
    [XmlAttribute]
    public int num;
    //每颗的间隔时间
    [XmlAttribute]
    public float cd;
    //关联的 子弹ID
    [XmlAttribute]
    public string ids;
    //组间 间隔时间
    [XmlAttribute]
    public float delay;
}