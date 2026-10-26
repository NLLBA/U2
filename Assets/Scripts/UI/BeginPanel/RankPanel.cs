using System.Collections.Generic;
using UnityEngine;

public class RankPanel : BasePanel<RankPanel>
{
    public UIButton btnClose;
    public UIScrollView scrollView;

    private readonly List<RankItem> items = new List<RankItem>();

    public override void Init()
    {
        btnClose.onClick.Add(new EventDelegate(Hideme));
        Hideme();
    }

    public override void Showme()
    {
        base.Showme();

        foreach (UILabel label in GetComponentsInChildren<UILabel>(true))
        {
            if (label.name == "lblTitle")
                label.text = "无尽模式排行榜";
        }

        if (scrollView == null)
            return;

        GameDataMgr.Instance.SortRanks();
        List<RankInfo> ranks = GameDataMgr.Instance.rankData.ranks;
        for (int i = 0; i < ranks.Count; i++)
        {
            if (items.Count > i)
            {
                items[i].gameObject.SetActive(true);
                items[i].Init(i + 1, ranks[i]);
                continue;
            }

            GameObject prefab = Resources.Load<GameObject>("UI/RankItem");
            if (prefab == null)
            {
                GameLog.Error(GameLogCategory.Data, "RANK_PREFAB_MISSING",
                    "Rank item prefab not found at Resources/UI/RankItem.");
                break;
            }

            GameObject row = Instantiate(prefab);
            row.transform.SetParent(scrollView.transform, false);
            row.transform.localPosition = new Vector3(35, 139 - i * 63, 0);

            RankItem item = row.GetComponent<RankItem>();
            if (item == null)
            {
                GameLog.Error(GameLogCategory.Data, "RANK_COMPONENT_MISSING",
                    "Rank item prefab has no RankItem component: " + row.name);
                Destroy(row);
                break;
            }

            item.Init(i + 1, ranks[i]);
            items.Add(item);
        }

        for (int i = ranks.Count; i < items.Count; i++)
        {
            if (items[i] != null)
                items[i].gameObject.SetActive(false);
        }

        scrollView.ResetPosition();
    }
}
