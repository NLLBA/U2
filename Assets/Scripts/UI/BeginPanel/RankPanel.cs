using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RankPanel : BasePanel<RankPanel>
{
    private const int DisplayRankCount = 20;
    private static readonly string[] DemoPilotNames =
    {
        "Nova-01", "星港巡弋者", "Vega", "青穹", "Orion",
        "白隼", "Pulse", "天际线", "Astra", "流光",
        "Echo-7", "晨星", "Helix", "远航者", "Comet",
        "苍蓝", "Vector", "夜莺", "Solaris", "归航"
    };

    public Button btnClose;
    public ScrollRect scrollView;

    private readonly List<RankItem> items = new List<RankItem>();

    public override void Init()
    {
        btnClose.onClick.AddListener(Hideme);
        Hideme();
    }

    public override void Showme()
    {
        Transform view = UGUIRuntime.View(this);
        foreach (Text label in view.GetComponentsInChildren<Text>(true))
        {
            if (label.name == "lblTitle")
                label.text = "无尽模式排行榜";
        }

        if (scrollView == null)
        {
            base.Showme();
            StabilizeTypography(view);
            return;
        }

        GameDataMgr.Instance.SortRanks();
        List<RankInfo> ranks = BuildDisplayRanks(GameDataMgr.Instance.rankData.ranks);
        LayoutPanelChrome(view);
        EnsureColumnHeader(view);
        RectTransform viewportRect = scrollView.viewport != null
            ? scrollView.viewport
            : scrollView.transform as RectTransform;
        if (viewportRect != null)
        {
            viewportRect.anchorMin = viewportRect.anchorMax = new Vector2(0.5f, 0.5f);
            viewportRect.pivot = new Vector2(0.5f, 0.5f);
            viewportRect.anchoredPosition = new Vector2(0f, -61f);
            viewportRect.sizeDelta = new Vector2(980f, 410f);
            if (viewportRect.GetComponent<RectMask2D>() == null)
                viewportRect.gameObject.AddComponent<RectMask2D>();
            Image inputSurface = viewportRect.GetComponent<Image>();
            if (inputSurface == null)
                inputSurface = viewportRect.gameObject.AddComponent<Image>();
            inputSurface.sprite = null;
            inputSurface.type = Image.Type.Simple;
            inputSurface.color = new Color(0f, 0f, 0f, 0.001f);
            inputSurface.raycastTarget = true;
        }
        Scrollbar scrollbar = EnsureVerticalScrollbar(view, viewportRect);

        RectTransform contentRect = scrollView.content;
        if (contentRect != null)
        {
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            float viewportHeight = viewportRect != null ? viewportRect.rect.height : 420f;
            contentRect.sizeDelta = new Vector2(0f, Mathf.Max(viewportHeight, ranks.Count * 58f + 12f));
        }

        for (int i = 0; i < ranks.Count; i++)
        {
            if (items.Count > i)
            {
                items[i].gameObject.SetActive(true);
                items[i].Init(i + 1, ranks[i]);
                LayoutRow(items[i].transform as RectTransform, i);
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
            Transform content = scrollView.content != null ? scrollView.content : scrollView.transform;
            row.transform.SetParent(content, false);
            RectTransform rowRect = row.transform as RectTransform;
            LayoutRow(rowRect, i);

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

        scrollView.verticalNormalizedPosition = 1f;
        scrollView.enabled = true;
        scrollView.vertical = true;
        scrollView.horizontal = false;
        scrollView.verticalScrollbar = scrollbar;
        scrollView.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        scrollView.verticalScrollbarSpacing = 0f;

        // Finish every layout and font decision while the panel is still hidden.
        // This prevents best-fit text from visibly growing after the first rendered frame.
        base.Showme();
        StabilizeTypography(view);
        Canvas.ForceUpdateCanvases();
    }

    private static void StabilizeTypography(Transform view)
    {
        if (view == null)
            return;
        foreach (Text label in view.GetComponentsInChildren<Text>(true))
        {
            label.resizeTextForBestFit = false;
            label.rectTransform.localScale = Vector3.one;
        }
    }

    private static List<RankInfo> BuildDisplayRanks(IList<RankInfo> savedRanks)
    {
        List<RankInfo> display = new List<RankInfo>(DisplayRankCount);
        if (savedRanks != null)
        {
            for (int i = 0; i < savedRanks.Count && display.Count < DisplayRankCount; i++)
                if (savedRanks[i] != null)
                    display.Add(savedRanks[i]);
        }

        int firstDemoScore = display.Count == 0
            ? 12800
            : Mathf.Max(1, display[display.Count - 1].score - 1);
        int scoreStep = Mathf.Max(1, firstDemoScore / Mathf.Max(1, DisplayRankCount - display.Count));
        int demoIndex = 0;
        while (display.Count < DisplayRankCount)
        {
            int slot = display.Count;
            display.Add(new RankInfo
            {
                name = DemoPilotNames[demoIndex % DemoPilotNames.Length],
                score = Mathf.Max(0, firstDemoScore - demoIndex * scoreStep),
                runTime = Mathf.Max(42, 385 - demoIndex * 11),
                time = Mathf.Max(42, 385 - demoIndex * 11),
                difficulty = demoIndex % 3 == 0 ? "困难" : (demoIndex % 3 == 1 ? "普通" : "简单"),
                mode = "无尽",
                hero = "Airplane" + (slot % 5 + 1),
                version = "UI预览",
                bosses = Mathf.Max(0, 5 - demoIndex / 4),
                waves = Mathf.Max(1, 18 - demoIndex),
                clears = Mathf.Max(12, 168 - demoIndex * 7),
                date = "展示数据"
            });
            demoIndex++;
        }
        return display;
    }

    private void LayoutPanelChrome(Transform view)
    {
        // Keep the title, table and footer inside one centered dialog with balanced margins.
        SetNamedRectPosition(view, "lblTitle", new Vector2(0f, 305f));
        SetNamedRectPosition(view, "Polish_RankSubtitle", new Vector2(0f, 263f));
        SetNamedRectPosition(view, "Polish_HeaderLine", new Vector2(0f, 225f));
        SetNamedRectPosition(view, "btnClose", new Vector2(0f, -330f));
        SetNamedRectPosition(view, "Polish_RankHeader", new Vector2(-8f, 170f));
        // The migrated button has a fixed-size visual beneath its layout root.
        // Resize that visual too so its border and hit area stay in the footer.
        RectTransform closeVisual = btnClose != null ? btnClose.transform as RectTransform : null;
        if (closeVisual != null)
            closeVisual.sizeDelta = new Vector2(220f, 52f);

        GameObject dialog = UGUIRuntime.FindObject(view, "Polish_Dialog");
        Image surface = dialog != null ? dialog.GetComponentInChildren<Image>(true) : null;
        if (surface != null)
            surface.rectTransform.sizeDelta = new Vector2(1120f, 760f);
    }

    private static void SetNamedRectPosition(Transform view, string name, Vector2 position)
    {
        GameObject target = UGUIRuntime.FindObject(view, name);
        RectTransform rect = target != null ? target.transform as RectTransform : null;
        if (rect != null)
            rect.anchoredPosition = position;
    }

    private static void EnsureColumnHeader(Transform view)
    {
        Text template = UGUIRuntime.Find<Text>(view, "lblTitle");
        // Rows are offset eight units left to reserve space for the scrollbar.
        EnsureHeaderLabel(view, "UGUI_RankHeaderRank", "排名", new Vector2(-422f, 170f), 72f,
            TextAnchor.MiddleCenter, template);
        EnsureHeaderLabel(view, "UGUI_RankHeaderPilot", "飞行员", new Vector2(-248f, 170f), 220f,
            TextAnchor.MiddleLeft, template);
        EnsureHeaderLabel(view, "UGUI_RankHeaderScore", "总分", new Vector2(-103f, 170f), 120f,
            TextAnchor.MiddleLeft, template);
        EnsureHeaderLabel(view, "UGUI_RankHeaderTime", "生存时间", new Vector2(47f, 170f), 140f,
            TextAnchor.MiddleLeft, template);
        EnsureHeaderLabel(view, "UGUI_RankHeaderLoadout", "难度 / 战机", new Vector2(217f, 170f), 300f,
            TextAnchor.MiddleLeft, template);
    }

    private static void EnsureHeaderLabel(Transform view, string name, string value, Vector2 position,
        float width, TextAnchor alignment, Text template)
    {
        Text label = UGUIRuntime.Find<Text>(view, name);
        if (label == null)
            label = UGUIRuntime.CreateText(view, name, position, Mathf.RoundToInt(width), 38,
                value, 16, new Color(0.56f, 0.73f, 0.79f, 1f), alignment, template);
        label.rectTransform.anchoredPosition = position;
        label.rectTransform.sizeDelta = new Vector2(width, 38f);
        label.text = value;
        label.font = ChineseFontFallback.Font;
        label.fontSize = 16;
        label.fontStyle = FontStyle.Normal;
        label.alignment = alignment;
        label.resizeTextForBestFit = false;
    }

    private static void LayoutRow(RectTransform row, int index)
    {
        if (row == null)
            return;

        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.sizeDelta = new Vector2(-54f, 52f);
        row.anchoredPosition = new Vector2(-8f, -(index * 58f + 4f));
        row.localScale = Vector3.one;
    }

    private static Scrollbar EnsureVerticalScrollbar(Transform view, RectTransform viewport)
    {
        if (view == null || viewport == null)
            return null;

        GameObject root = UGUIRuntime.FindObject(view, "UGUI_RankScrollbar");
        if (root == null)
        {
            root = new GameObject("UGUI_RankScrollbar", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(UGUIScrollbarTrackChrome), typeof(Scrollbar));
            root.transform.SetParent(view, false);
        }

        RectTransform rootRect = root.transform as RectTransform;
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = viewport.anchoredPosition + new Vector2(472f, 0f);
        rootRect.sizeDelta = new Vector2(14f, viewport.rect.height - 20f);

        UGUIScrollbarTrackChrome track = root.GetComponent<UGUIScrollbarTrackChrome>();
        track.color = Color.white;
        track.raycastTarget = true;

        Transform slidingTransform = root.transform.Find("SlidingArea");
        RectTransform slidingArea;
        if (slidingTransform == null)
        {
            GameObject sliding = new GameObject("SlidingArea", typeof(RectTransform));
            sliding.transform.SetParent(root.transform, false);
            slidingArea = sliding.transform as RectTransform;
        }
        else
        {
            slidingArea = slidingTransform as RectTransform;
        }
        slidingArea.anchorMin = Vector2.zero;
        slidingArea.anchorMax = Vector2.one;
        slidingArea.offsetMin = new Vector2(2f, 6f);
        slidingArea.offsetMax = new Vector2(-2f, -6f);

        Transform handleTransform = slidingArea.Find("Handle");
        UGUIScrollbarHandleChrome handle;
        if (handleTransform == null)
        {
            GameObject handleObject = new GameObject("Handle", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(UGUIScrollbarHandleChrome));
            handleObject.transform.SetParent(slidingArea, false);
            handle = handleObject.GetComponent<UGUIScrollbarHandleChrome>();
        }
        else
        {
            handle = handleTransform.GetComponent<UGUIScrollbarHandleChrome>();
            if (handle == null)
                handle = handleTransform.gameObject.AddComponent<UGUIScrollbarHandleChrome>();
        }
        RectTransform handleRect = handle.rectTransform;
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.offsetMin = Vector2.zero;
        handleRect.offsetMax = Vector2.zero;
        handle.color = Color.white;
        handle.raycastTarget = true;

        Scrollbar scrollbar = root.GetComponent<Scrollbar>();
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handle;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.numberOfSteps = 0;
        scrollbar.interactable = true;
        scrollbar.SetValueWithoutNotify(1f);
        root.SetActive(true);
        return scrollbar;
    }
}
