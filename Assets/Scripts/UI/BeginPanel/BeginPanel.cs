using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BeginPanel : BasePanel<BeginPanel>
{
    public UIButton btnBegin;
    public UIButton btnRank;
    public UIButton btnSetting;
    public UIButton btnQuit;

    public override void Init()
    {
        btnBegin.onClick.Add(new EventDelegate(() =>
        {
            //显示选角面板
            ChoosePanel.Instance.Showme();
            //隐藏自己
            Hideme();
        }));

        btnRank.onClick.Add(new EventDelegate(() =>
        {
            //显示排行面板
            RankPanel.Instance.Showme();
        }));

        btnSetting.onClick.Add(new EventDelegate(() =>
        {
            //显示设置面板
            SettingPanel.Instance.Showme();
        }));

        btnQuit.onClick.Add(new EventDelegate(() =>
        {
            //退出游戏
            Application.Quit();
        }));

    }


}
