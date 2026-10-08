using UnityEngine;

public class RankItem : MonoBehaviour
{
    public UILabel labRank;
    public UILabel labName;
    public UILabel labTime;

    public void Init(int rank, RankInfo info)
    {
        if (info == null)
        {
            Init(rank, "Player", 0);
            return;
        }

        info.Normalize();
        SetIdentity(rank, info.name);
        if (labTime != null)
            labTime.text = BuildRecord(info);
    }

    public void Init(int rank, string name, int time)
    {
        SetIdentity(rank, name);

        if (labTime != null)
            labTime.text = FormatTime(time);
    }

    private void SetIdentity(int rank, string name)
    {
        if (labRank != null)
            labRank.text = rank.ToString();
        if (labName != null)
            labName.text = string.IsNullOrWhiteSpace(name) ? "Player" : name.Trim();
    }

    private static string BuildRecord(RankInfo info)
    {
        string date = string.IsNullOrEmpty(info.date) ? "历史记录" : info.date;
        return "分数 " + info.score + "  生存 " + FormatTime(info.runTime) +
            "  " + info.difficulty +
            "\nBoss " + info.bosses + "  清除 " + info.clears + "  " + date;
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
