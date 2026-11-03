using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DifficultyStatusLabel : MonoBehaviour
{
    private Text label;

    private void Awake()
    {
        Transform view = UGUIRuntime.View(this);
        label = view != null ? view.GetComponentInChildren<Text>(true) : null;
    }

    private void Update()
    {
        if (label != null)
        {
            label.text = "当前难度：" + GameDataMgr.Instance.DifficultyName();
        }
    }
}
