using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BeginPanel : BasePanel<BeginPanel>
{
    public Button btnBegin;
    public Button btnRank;
    public Button btnSetting;
    public Button btnQuit;

    public override void Init()
    {
        btnBegin.onClick.AddListener(() =>
        {
            //显示选角面板
            ChoosePanel.Instance.Showme();
            //隐藏自己
            Hideme();
        });

        btnRank.onClick.AddListener(() =>
        {
            //显示排行面板
            RankPanel.Instance.Showme();
        });

        btnSetting.onClick.AddListener(() =>
        {
            //显示设置面板
            SettingPanel.Instance.Showme();
        });

        btnQuit.onClick.AddListener(() =>
        {
            //退出游戏
            Application.Quit();
        });

    }


}
