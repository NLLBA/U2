using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RankItem))]
public class RankItemDetailsPresenter : MonoBehaviour
{
    private RankItem rankItem;

    private void Awake()
    {
        rankItem = GetComponent<RankItem>();
        SetupLabels();
    }

    // RankPanel owns row data. Reading the global list by visible rank every frame
    // made rows change after sorting or reopening the panel.

    private void SetupLabels()
    {
        if (rankItem == null)
            return;

        SetupLabel(rankItem.labRank, 70, 50, 24, -410f,
            NGUIText.Alignment.Center, UIWidget.Pivot.Center);
        SetupLabel(rankItem.labName, 220, 50, 22, -350f,
            NGUIText.Alignment.Left, UIWidget.Pivot.Left);
        SetupLabel(rankItem.labTime, 560, 54, 17, -95f,
            NGUIText.Alignment.Left, UIWidget.Pivot.Left);
    }

    private static void SetupLabel(UILabel label, int width, int height, int fontSize, float x,
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

}
