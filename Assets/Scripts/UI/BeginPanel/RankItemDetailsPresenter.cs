using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RankItem))]
public class RankItemDetailsPresenter : MonoBehaviour
{
    private RankItem rankItem;
    private int lastSignature = int.MinValue;

    private void Awake()
    {
        rankItem = GetComponent<RankItem>();
        ConfigureLabels();
    }

    private void LateUpdate()
    {
        if (rankItem == null || rankItem.labRank == null)
            return;

        int shownRank;
        if (!int.TryParse(rankItem.labRank.text, out shownRank))
            return;

        List<RankInfo> entries = GameDataMgr.Instance.rankData.rankInfos;
        int index = shownRank - 1;
        if (index < 0 || index >= entries.Count || entries[index] == null)
            return;

        RankInfo info = entries[index];
        info.Normalize();
        int signature = GetSignature(shownRank, info);
        if (signature == lastSignature)
            return;

        lastSignature = signature;
        rankItem.labRank.text = shownRank.ToString();
        rankItem.labName.text = info.name;
        rankItem.labTime.text = "分数 " + info.finalScore + "  生存 " + FormatTime(info.survivalTime) +
            "  " + info.difficulty +
            "\nBoss " + info.bossRounds + "  清除 " + info.clearedBullets +
            "  " + (string.IsNullOrEmpty(info.recordTime) ? "历史记录" : info.recordTime);
    }

    private void ConfigureLabels()
    {
        if (rankItem == null)
            return;

        ConfigureLabel(rankItem.labRank, 70, 50, 24, -410f,
            NGUIText.Alignment.Center, UIWidget.Pivot.Center);
        ConfigureLabel(rankItem.labName, 220, 50, 22, -350f,
            NGUIText.Alignment.Left, UIWidget.Pivot.Left);
        ConfigureLabel(rankItem.labTime, 560, 54, 17, -95f,
            NGUIText.Alignment.Left, UIWidget.Pivot.Left);
    }

    private static void ConfigureLabel(UILabel label, int width, int height, int fontSize, float x,
        NGUIText.Alignment alignment, UIWidget.Pivot pivot)
    {
        if (label == null)
            return;

        label.SetAnchor((Transform)null);
        label.width = width;
        label.height = height;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.pivot = pivot;
        label.spacingX = 0;
        label.spacingY = 1;
        label.overflowMethod = UILabel.Overflow.ShrinkContent;
        Vector3 position = label.transform.localPosition;
        position.x = x;
        position.y = 0f;
        label.transform.localPosition = position;
    }

    private static int GetSignature(int rank, RankInfo info)
    {
        unchecked
        {
            int hash = rank;
            hash = hash * 397 ^ info.finalScore;
            hash = hash * 397 ^ info.survivalTime;
            hash = hash * 397 ^ info.bossRounds;
            hash = hash * 397 ^ info.clearedBullets;
            hash = hash * 397 ^ (info.name != null ? info.name.GetHashCode() : 0);
            hash = hash * 397 ^ (info.recordTime != null ? info.recordTime.GetHashCode() : 0);
            return hash;
        }
    }

    private static string FormatTime(int totalSeconds)
    {
        int safeTime = Mathf.Max(0, totalSeconds);
        int hours = safeTime / 3600;
        int minutes = safeTime % 3600 / 60;
        int seconds = safeTime % 60;

        if (hours > 0)
            return hours + "h" + minutes + "m" + seconds + "s";
        if (minutes > 0)
            return minutes + "m" + seconds + "s";
        return seconds + "s";
    }
}
