using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RankItem))]
public class RankItemDetailsPresenter : MonoBehaviour
{
    private RankItem rankItem;

    private void Awake()
    {
        rankItem = GetComponent<RankItem>();
        RectTransform row = transform as RectTransform;
        if (row != null)
            row.sizeDelta = new Vector2(930f, 52f);
        SetupLabels();
    }

    // RankPanel owns row data. Reading the global list by visible rank every frame
    // made rows change after sorting or reopening the panel.

    private void SetupLabels()
    {
        if (rankItem == null)
            return;

        SetupLabel(rankItem.labRank, 72, 48, 21, -414f, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
        SetupLabel(rankItem.labName, 220, 48, 19, -350f, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f));
        SetupLabel(rankItem.labTime, 580, 48, 17, -92f, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f));
    }

    private static void SetupLabel(Text label, int width, int height, int fontSize, float x,
        TextAnchor alignment, Vector2 pivot)
    {
        if (label == null)
            return;

        RectTransform rect = label.rectTransform;
        RectTransform container = rect.parent as RectTransform;
        if (container != null)
        {
            container.anchorMin = container.anchorMax = new Vector2(0.5f, 0.5f);
            container.pivot = pivot;
            container.anchoredPosition = new Vector2(x, 0f);
            container.sizeDelta = new Vector2(width, height);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }
        else
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.pivot = pivot;
        }
        label.font = ChineseFontFallback.Font;
        label.fontStyle = FontStyle.Normal;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.resizeTextForBestFit = false;
    }

}
