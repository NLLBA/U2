using UnityEngine;
using UnityEngine.UI;

public class RankItem : MonoBehaviour
{
    public Text labRank;
    public Text labName;
    public Text labTime;

    private UGUIRankRowChrome rowChrome;
    private Text scoreLabel;
    private Text survivalLabel;
    private Text loadoutLabel;

    public void Init(int rank, RankInfo info)
    {
        if (info == null)
        {
            Init(rank, "Player", 0);
            return;
        }

        info.Normalize();
        SetIdentity(rank, info.name);
        SetRecord(info.score, info.runTime, info.difficulty, info.hero);
    }

    public void Init(int rank, string name, int time)
    {
        SetIdentity(rank, name);
        SetRecord(0, time, "普通", "历史记录");
    }

    private void SetIdentity(int rank, string name)
    {
        EnsureRowVisual(rank);
        if (labRank != null)
        {
            labRank.text = rank.ToString();
            labRank.color = RankColor(rank);
            labRank.fontStyle = rank <= 3 ? FontStyle.Bold : FontStyle.Normal;
        }
        if (labName != null)
        {
            labName.text = string.IsNullOrWhiteSpace(name) ? "Player" : name.Trim();
            labName.color = rank <= 3
                ? new Color(0.92f, 0.97f, 1f, 1f)
                : new Color(0.79f, 0.87f, 0.91f, 1f);
        }
    }

    private void SetRecord(int score, int time, string difficulty, string aircraft)
    {
        EnsureRecordLabels();
        scoreLabel.text = score + " 分";
        survivalLabel.text = FormatTime(time);
        loadoutLabel.text = (string.IsNullOrWhiteSpace(difficulty) ? "普通" : difficulty) +
            "  /  " + (string.IsNullOrWhiteSpace(aircraft) ? "Airplane1" : aircraft);
    }

    private void EnsureRecordLabels()
    {
        if (labTime != null)
            labTime.gameObject.SetActive(false);
        scoreLabel = EnsureDataLabel(scoreLabel, "UGUI_RankScore", new Vector2(-95f, 0f),
            new Vector2(120f, 48f));
        survivalLabel = EnsureDataLabel(survivalLabel, "UGUI_RankSurvival", new Vector2(55f, 0f),
            new Vector2(140f, 48f));
        loadoutLabel = EnsureDataLabel(loadoutLabel, "UGUI_RankLoadout", new Vector2(225f, 0f),
            new Vector2(300f, 48f));
    }

    private Text EnsureDataLabel(Text current, string name, Vector2 position, Vector2 size)
    {
        if (current == null)
            current = UGUIRuntime.Find<Text>(transform, name);
        if (current == null)
            current = UGUIRuntime.CreateText(transform, name, position,
                Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y), string.Empty, 17,
                new Color(0.61f, 0.73f, 0.78f, 1f), TextAnchor.MiddleLeft);
        current.rectTransform.anchoredPosition = position;
        current.rectTransform.sizeDelta = size;
        current.font = ChineseFontFallback.Font;
        current.fontSize = 17;
        current.fontStyle = FontStyle.Normal;
        current.alignment = TextAnchor.MiddleLeft;
        current.resizeTextForBestFit = false;
        current.color = new Color(0.61f, 0.73f, 0.78f, 1f);
        current.gameObject.SetActive(true);
        return current;
    }

    private void EnsureRowVisual(int rank)
    {
        if (rowChrome == null)
            rowChrome = GetComponent<UGUIRankRowChrome>();
        if (rowChrome == null)
            rowChrome = gameObject.AddComponent<UGUIRankRowChrome>();
        rowChrome.Configure(rank);
        rowChrome.raycastTarget = false;

        Image[] legacyBackgrounds = GetComponentsInChildren<Image>(true);
        for (int index = 0; index < legacyBackgrounds.Length; index++)
            legacyBackgrounds[index].enabled = false;
    }

    private static Color RankColor(int rank)
    {
        if (rank == 1) return new Color(1f, 0.78f, 0.28f, 1f);
        if (rank == 2) return new Color(0.64f, 0.86f, 0.96f, 1f);
        if (rank == 3) return new Color(0.94f, 0.57f, 0.30f, 1f);
        return new Color(0.24f, 0.82f, 0.88f, 1f);
    }

    private static string FormatTime(int time)
    {
        int safeTime = Mathf.Max(0, time);
        string text = string.Empty;
        if (safeTime / 3600 > 0)
        {
            text += safeTime / 3600 + "h";
        }

        if (safeTime % 3600 / 60 > 0 || text != string.Empty)
        {
            text += safeTime % 3600 / 60 + "m";
        }

        text += safeTime % 60 + "s";
        return text;
    }
}
