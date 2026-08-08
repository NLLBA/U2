using System.Collections.Generic;
using System.Xml.Serialization;

public class RankData
{
    public List<RankInfo> rankInfos = new List<RankInfo>();

    public void NormalizeAndSort(int maxEntries = 20)
    {
        if (rankInfos == null)
        {
            rankInfos = new List<RankInfo>();
        }

        for (int i = rankInfos.Count - 1; i >= 0; i--)
        {
            if (rankInfos[i] == null)
            {
                rankInfos.RemoveAt(i);
                continue;
            }

            rankInfos[i].Normalize();
        }

        rankInfos.Sort(CompareRankInfo);

        int safeMaxEntries = System.Math.Max(0, maxEntries);
        if (rankInfos.Count > safeMaxEntries)
        {
            rankInfos.RemoveRange(safeMaxEntries, rankInfos.Count - safeMaxEntries);
        }
    }

    public static int CompareRankInfo(RankInfo left, RankInfo right)
    {
        if (ReferenceEquals(left, right))
            return 0;
        if (left == null)
            return 1;
        if (right == null)
            return -1;

        int scoreCompare = right.finalScore.CompareTo(left.finalScore);
        if (scoreCompare != 0)
            return scoreCompare;

        return right.survivalTime.CompareTo(left.survivalTime);
    }
}

public class RankInfo
{
    [XmlAttribute]
    public string name;

    // Kept for XML files written by versions before the score system.
    [XmlAttribute]
    public int time;

    [XmlAttribute]
    public int finalScore;

    [XmlAttribute]
    public int survivalTime;

    [XmlAttribute]
    public string difficulty;

    [XmlAttribute]
    public int bossRounds;

    [XmlAttribute]
    public int clearedBullets;

    [XmlAttribute]
    public string recordTime;

    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "Player";
        }

        time = System.Math.Max(0, time);
        finalScore = System.Math.Max(0, finalScore);
        survivalTime = survivalTime > 0 ? survivalTime : time;
        survivalTime = System.Math.Max(0, survivalTime);
        time = survivalTime;
        difficulty = string.IsNullOrWhiteSpace(difficulty) ? "普通" : difficulty;
        bossRounds = System.Math.Max(0, bossRounds);
        clearedBullets = System.Math.Max(0, clearedBullets);
        recordTime = recordTime ?? string.Empty;
    }
}
