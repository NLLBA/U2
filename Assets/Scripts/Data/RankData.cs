using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Xml.Serialization;

public class RankData
{
    public const int CurrentSchemaVersion = 2;

    [XmlAttribute("schemaVersion")]
    public int schemaVersion = CurrentSchemaVersion;

    [XmlArray("rankInfos")]
    public List<RankInfo> ranks = new List<RankInfo>();

    public void Sort(int maxCount = 20)
    {
        if (schemaVersion < CurrentSchemaVersion)
            schemaVersion = CurrentSchemaVersion;
        if (ranks == null)
        {
            ranks = new List<RankInfo>();
        }

        for (int i = ranks.Count - 1; i >= 0; i--)
        {
            if (ranks[i] == null)
            {
                ranks.RemoveAt(i);
                continue;
            }

            ranks[i].Normalize();
        }

        ranks.Sort(CompareRanks);

        int limit = System.Math.Max(0, maxCount);
        if (ranks.Count > limit)
        {
            ranks.RemoveRange(limit, ranks.Count - limit);
        }
    }

    public static int CompareRanks(RankInfo left, RankInfo right)
    {
        if (ReferenceEquals(left, right))
            return 0;
        if (left == null)
            return 1;
        if (right == null)
            return -1;

        int scoreCompare = right.score.CompareTo(left.score);
        if (scoreCompare != 0)
            return scoreCompare;

        int waveCompare = right.waves.CompareTo(left.waves);
        if (waveCompare != 0)
            return waveCompare;

        return right.runTime.CompareTo(left.runTime);
    }
}

public class RankInfo
{
    [XmlAttribute]
    public string name;

    // 旧版排行榜只保存 time，读取时仍需保留这个字段。
    [XmlAttribute]
    public int time;

    [XmlAttribute("finalScore")]
    public int score;

    [XmlAttribute("survivalTime")]
    public int runTime;

    [XmlAttribute]
    public string difficulty;

    [XmlAttribute]
    public string mode;

    [XmlAttribute]
    public string hero;

    [XmlAttribute("gameVersion")]
    public string version;

    [XmlAttribute("bossRounds")]
    public int bosses;

    [XmlAttribute("highestWave")]
    public int waves;

    [XmlAttribute("clearedBullets")]
    public int clears;

    [XmlAttribute("recordTime")]
    public string date;

    public void Normalize()
    {
        name = string.IsNullOrWhiteSpace(name) ? "Player" : name.Trim();

        time = System.Math.Max(0, time);
        score = System.Math.Max(0, score);
        runTime = runTime > 0 ? runTime : time;
        runTime = System.Math.Max(0, runTime);
        time = runTime;
        difficulty = string.IsNullOrWhiteSpace(difficulty) ? "普通" : difficulty;
        mode = string.IsNullOrWhiteSpace(mode) ? "无尽" : mode;
        hero = string.IsNullOrWhiteSpace(hero) ? "角色" : hero;
        version = string.IsNullOrWhiteSpace(version) ? "历史版本" : version;
        bosses = System.Math.Max(0, bosses);
        waves = System.Math.Max(0, waves);
        clears = System.Math.Max(0, clears);
        date = date ?? string.Empty;
    }
}
