using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class ConfigurationAndMigrationTests
{
    private const string SchemaKey = "U2.Campaign.SchemaVersion";

    [Test]
    public void ConfigServiceBuildsStableIndexesAndLevelFireCandidates()
    {
        BulletInfo bullet = new BulletInfo { id = 7, asset = "Bullet/Test" };
        FireInfo first = new FireInfo { id = 1, ids = "7,7" };
        FireInfo second = new FireInfo { id = 2, ids = "7,7" };
        DifficultyInfo difficulty = new DifficultyInfo { id = 3, name = "Hard" };
        LevelInfo level = new LevelInfo { id = 5, fireIds = "2" };
        GameConfigService service = new GameConfigService();
        service.Rebuild(
            new BulletData { bullets = new List<BulletInfo> { bullet } },
            new FireData { fires = new List<FireInfo> { first, second } },
            new DifficultyData { difficulties = new List<DifficultyInfo> { difficulty } },
            new LevelData { levels = new List<LevelInfo> { level } });

        Assert.AreSame(bullet, service.Bullet(7));
        Assert.AreSame(difficulty, service.Difficulty(3));
        Assert.AreSame(level, service.Level(5));
        Assert.AreSame(second, service.RandomFire(5));
    }

    [Test]
    public void CombatTuningNormalizesInvalidThresholds()
    {
        CombatTuningData tuning = new CombatTuningData
        {
            hudDynamicRefreshSeconds = 0f,
            endlessTier2Wave = 20,
            endlessTier3Wave = 5,
            endlessTier4Wave = 2,
            bossPhase2 = 0.8f,
            bossPhase3 = 0.2f,
            bossPhase4 = 0.1f,
        };

        tuning.Normalize();

        Assert.GreaterOrEqual(tuning.hudDynamicRefreshSeconds, 0.05f);
        Assert.Greater(tuning.endlessTier3Wave, tuning.endlessTier2Wave);
        Assert.Greater(tuning.endlessTier4Wave, tuning.endlessTier3Wave);
        Assert.Greater(tuning.bossPhase3, tuning.bossPhase2);
        Assert.Greater(tuning.bossPhase4, tuning.bossPhase3);
    }

    [Test]
    public void CampaignSchemaMigrationIsIdempotentAndPreservesProgress()
    {
        int previousVersion = PlayerPrefs.GetInt(SchemaKey, -1);
        const string scoreKey = "U2.Campaign.Level2.BestScore";
        int previousScore = PlayerPrefs.GetInt(scoreKey, -1);
        try
        {
            PlayerPrefs.SetInt(SchemaKey, 0);
            PlayerPrefs.SetInt(scoreKey, 4321);

            Assert.AreEqual(2, CampaignProgress.SchemaVersion);
            Assert.AreEqual(4321, CampaignProgress.BestScore(2));
            Assert.AreEqual(2, CampaignProgress.SchemaVersion);
            Assert.AreEqual(4321, CampaignProgress.BestScore(2));
        }
        finally
        {
            RestoreInt(SchemaKey, previousVersion);
            RestoreInt(scoreKey, previousScore);
            PlayerPrefs.Save();
        }
    }

    [Test]
    public void LegacyRankSchemaUpgradesWithoutDroppingEntries()
    {
        RankData ranks = new RankData { schemaVersion = 0 };
        ranks.ranks.Add(new RankInfo { name = "Legacy", time = 15 });

        ranks.Sort();

        Assert.AreEqual(RankData.CurrentSchemaVersion, ranks.schemaVersion);
        Assert.AreEqual(1, ranks.ranks.Count);
        Assert.AreEqual(15, ranks.ranks[0].runTime);
    }

    private static void RestoreInt(string key, int value)
    {
        if (value < 0)
            PlayerPrefs.DeleteKey(key);
        else
            PlayerPrefs.SetInt(key, value);
    }
}
