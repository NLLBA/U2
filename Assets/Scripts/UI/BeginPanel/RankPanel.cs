using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RankPanel : BasePanel<RankPanel>
{
    public UIButton btnClose;
    public UIScrollView scrollView;
    //存储单条数据
    private List<RankItem> items = new List<RankItem>();

    public override void Init()
    {
        btnClose.onClick.Add(new EventDelegate(() =>
        {
            Hideme();
        }));

        Hideme();

        

    }

    public override void Showme()
    {
        base.Showme();
        //更新面板上显示的信息
        //获取本地排行榜信息
        List<RankInfo> list = GameDataMgr.Instance.rankData.rankInfos;
        for (int i = 0; i < list.Count; i++)
        {
            if (items.Count > i)
            {
                items[i].InitInfo(i + 1, list[i].name, list[i].time);

            }
            else
            {
                GameObject obj = Instantiate(Resources.Load<GameObject>("UI/RankItem"));
                //父对象
                obj.transform.SetParent(scrollView.transform, false);
                //位置
                obj.transform.localPosition = new Vector3(35, 139 - i * 63, 0);

                RankItem item = obj.GetComponent<RankItem>();
                item.InitInfo(i + 1, list[i].name, list[i].time);
                items.Add(item);

            }
        }
    }
}
