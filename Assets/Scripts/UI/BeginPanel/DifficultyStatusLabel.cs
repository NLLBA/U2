using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DifficultyStatusLabel : MonoBehaviour
{
    private UILabel label;

    private void Awake()
    {
        label = GetComponent<UILabel>();
    }

    private void Update()
    {
        if (label != null)
        {
            label.text = "当前难度：" + GameDataMgr.Instance.DifficultyName();
        }
    }
}
