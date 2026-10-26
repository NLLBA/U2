using UnityEngine;

public static class CampaignProgress
{
    private const string Prefix = "U2.Campaign.";
    private const string SchemaVersionKey = Prefix + "SchemaVersion";
    private const int CurrentSchemaVersion = 2;
    private const string NetworkWinsKey = Prefix + "NetworkWins";
    private const string NetworkLossesKey = Prefix + "NetworkLosses";
    private const string NetworkDrawsKey = Prefix + "NetworkDraws";
    private const string NetworkBestKey = Prefix + "NetworkBestScore";

    static CampaignProgress()
    {
        EnsureSchema();
    }

    public static int MaxUnlocked => GameDataMgr.MaxLevelId;
    public static int NetworkWins => Mathf.Max(0, PlayerPrefs.GetInt(NetworkWinsKey, 0));
    public static int NetworkLosses => Mathf.Max(0, PlayerPrefs.GetInt(NetworkLossesKey, 0));
    public static int NetworkDraws => Mathf.Max(0, PlayerPrefs.GetInt(NetworkDrawsKey, 0));
    public static int NetworkBestScore => Mathf.Max(0, PlayerPrefs.GetInt(NetworkBestKey, 0));

    public static bool IsUnlocked(int levelId)
    {
        return levelId >= 1 && levelId <= MaxUnlocked;
    }

    public static int Stars(int levelId)
    {
        return Mathf.Clamp(PlayerPrefs.GetInt(LevelKey(levelId, "Stars"), 0), 0, 3);
    }

    public static int BestScore(int levelId)
    {
        return Mathf.Max(0, PlayerPrefs.GetInt(LevelKey(levelId, "BestScore"), 0));
    }

    public static int BestTime(int levelId)
    {
        return Mathf.Max(0, PlayerPrefs.GetInt(LevelKey(levelId, "BestTime"), 0));
    }

    public static int ChallengeMask(int levelId)
    {
        return PlayerPrefs.GetInt(LevelKey(levelId, "Challenges"), 0) & 7;
    }

    public static int ChallengeCount(int levelId)
    {
        int mask = ChallengeMask(levelId);
        int count = 0;
        for (int bit = 0; bit < 3; bit++)
            if ((mask & (1 << bit)) != 0)
                count++;
        return count;
    }

    public static void CompleteLevel(int levelId, int stars, int score, int runTime, int challengeMask = 0)
    {
        EnsureSchema();
        levelId = Mathf.Clamp(levelId, 1, GameDataMgr.MaxLevelId);
        PlayerPrefs.SetInt(LevelKey(levelId, "Stars"), Mathf.Max(Stars(levelId), Mathf.Clamp(stars, 1, 3)));
        PlayerPrefs.SetInt(LevelKey(levelId, "Challenges"), ChallengeMask(levelId) | (challengeMask & 7));
        RecordAttempt(levelId, score, runTime);
        PlayerPrefs.Save();
    }

    public static void RecordAttempt(int levelId, int score, int runTime)
    {
        EnsureSchema();
        levelId = Mathf.Clamp(levelId, 1, GameDataMgr.MaxLevelId);
        PlayerPrefs.SetInt(LevelKey(levelId, "BestScore"), Mathf.Max(BestScore(levelId), Mathf.Max(0, score)));
        PlayerPrefs.SetInt(LevelKey(levelId, "BestTime"), Mathf.Max(BestTime(levelId), Mathf.Max(0, runTime)));
        PlayerPrefs.Save();
    }

    public static void RecordNetworkResult(bool won, bool draw, int score)
    {
        EnsureSchema();
        string key = draw ? NetworkDrawsKey : (won ? NetworkWinsKey : NetworkLossesKey);
        PlayerPrefs.SetInt(key, Mathf.Max(0, PlayerPrefs.GetInt(key, 0)) + 1);
        PlayerPrefs.SetInt(NetworkBestKey, Mathf.Max(NetworkBestScore, Mathf.Max(0, score)));
        PlayerPrefs.Save();
    }

    public static string StarsText(int levelId)
    {
        int earned = Stars(levelId);
        return new string('★', earned) + new string('☆', 3 - earned);
    }

    public static int SchemaVersion
    {
        get
        {
            EnsureSchema();
            return PlayerPrefs.GetInt(SchemaVersionKey, CurrentSchemaVersion);
        }
    }

    public static void ResetProgress()
    {
        for (int level = 1; level <= GameDataMgr.MaxLevelId; level++)
        {
            PlayerPrefs.DeleteKey(LevelKey(level, "Stars"));
            PlayerPrefs.DeleteKey(LevelKey(level, "BestScore"));
            PlayerPrefs.DeleteKey(LevelKey(level, "BestTime"));
            PlayerPrefs.DeleteKey(LevelKey(level, "Challenges"));
        }

        PlayerPrefs.DeleteKey(NetworkWinsKey);
        PlayerPrefs.DeleteKey(NetworkLossesKey);
        PlayerPrefs.DeleteKey(NetworkDrawsKey);
        PlayerPrefs.DeleteKey(NetworkBestKey);
        PlayerPrefs.SetInt(SchemaVersionKey, CurrentSchemaVersion);
        PlayerPrefs.Save();
    }

    private static void EnsureSchema()
    {
        int version = PlayerPrefs.GetInt(SchemaVersionKey, 0);
        if (version < CurrentSchemaVersion)
        {
            // Version 2 formalizes the previously implicit campaign keys. The
            // existing values remain valid and are intentionally preserved.
            PlayerPrefs.SetInt(SchemaVersionKey, CurrentSchemaVersion);
            PlayerPrefs.Save();
        }
    }

    private static string LevelKey(int levelId, string suffix)
    {
        return Prefix + "Level" + Mathf.Clamp(levelId, 1, GameDataMgr.MaxLevelId) + "." + suffix;
    }
}
