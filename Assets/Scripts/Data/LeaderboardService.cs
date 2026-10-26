using UnityEngine;

public sealed class LeaderboardService
{
    public void NormalizeAndSort(RankData data, int limit = 20)
    {
        if (data != null)
            data.Sort(limit);
    }

    public void Save(RankData data, string fileName)
    {
        if (data != null)
            XmlDataMgr.Instance.Save(data, fileName);
    }

    public RankInfo CreateEndlessEntry(string pilotName, int fallbackTime, string difficulty,
        int heroIndex, int score, int runTime, int bosses, int waves, int clears)
    {
        RankInfo entry = new RankInfo
        {
            name = string.IsNullOrWhiteSpace(pilotName) ? "Player" : pilotName.Trim(),
            runTime = Mathf.Max(0, runTime > 0 ? runTime : fallbackTime),
            difficulty = difficulty,
            mode = "无尽",
            hero = "角色" + (heroIndex + 1),
            version = Application.version,
            date = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss",
                System.Globalization.CultureInfo.InvariantCulture),
            score = Mathf.Max(0, score),
            bosses = Mathf.Max(0, bosses),
            waves = Mathf.Max(0, waves),
            clears = Mathf.Max(0, clears),
        };
        entry.Normalize();
        return entry;
    }
}
