using System.Xml.Serialization;

[XmlRoot("CombatTuningData")]
public class CombatTuningData
{
    [XmlAttribute] public float hudDynamicRefreshSeconds = 0.1f;
    [XmlAttribute] public float hudClockRefreshSeconds = 1f;
    [XmlAttribute] public float scoreRefreshSeconds = 0.1f;

    [XmlAttribute] public float endlessWaveSeconds = 12f;
    [XmlAttribute] public float endlessCampWarningSeconds = 7f;
    [XmlAttribute] public float endlessCampStrikeSeconds = 10f;
    [XmlAttribute] public int endlessCampStartWave = 5;
    [XmlAttribute] public int endlessTier2Wave = 6;
    [XmlAttribute] public int endlessTier3Wave = 13;
    [XmlAttribute] public int endlessTier4Wave = 21;
    [XmlAttribute] public int endlessBackgroundFinalWave = 25;
    [XmlAttribute] public float endlessBossStart = 90f;
    [XmlAttribute] public float endlessBossWarning = 3f;
    [XmlAttribute] public float endlessBossDuration = 30f;
    [XmlAttribute] public float endlessBossGap = 90f;

    [XmlAttribute] public float bossNormalFireScale = 0.25f;
    [XmlAttribute] public float bossAttackInterval = 1.2f;
    [XmlAttribute] public float bossVisualScale = 2.5f;
    [XmlAttribute] public float bossPhase2 = 0.25f;
    [XmlAttribute] public float bossPhase3 = 0.5f;
    [XmlAttribute] public float bossPhase4 = 0.75f;
    [XmlAttribute] public float fifthBossPhaseSplit = 0.5f;
    [XmlAttribute] public float fifthBossProtectionSeconds = 1.5f;
    [XmlAttribute] public int bossExtraCrossRound = 2;
    [XmlAttribute] public int bossExtraHomingRound = 3;
    [XmlAttribute] public int bossNormalCoreCadence = 2;
    [XmlAttribute] public int bossRageCoreCadence = 3;

    [XmlAttribute] public int bossFanBase = 3;
    [XmlAttribute] public int bossFanPerRound = 2;
    [XmlAttribute] public int bossFanPerDifficulty = 2;
    [XmlAttribute] public int bossFanMax = 17;
    [XmlAttribute] public int bossSpiralBase = 4;
    [XmlAttribute] public int bossSpiralPerRound = 1;
    [XmlAttribute] public int bossSpiralPerDifficulty = 2;
    [XmlAttribute] public int bossSpiralMax = 14;
    [XmlAttribute] public int bossCrossBase = 4;
    [XmlAttribute] public int bossCrossAdvanced = 8;
    [XmlAttribute] public int bossCrossPerDifficulty = 2;
    [XmlAttribute] public int bossHomingBase = 2;
    [XmlAttribute] public int bossHomingPerRound = 1;
    [XmlAttribute] public int bossHomingPerDifficulty = 1;
    [XmlAttribute] public int bossHomingMax = 8;
    [XmlAttribute] public int bossRingBase = 6;
    [XmlAttribute] public int bossRingPerRound = 2;
    [XmlAttribute] public int bossRingPerDifficulty = 2;
    [XmlAttribute] public int bossRingMax = 18;
    [XmlAttribute] public int bossShieldEasy = 3;
    [XmlAttribute] public int bossShieldNormal = 4;
    [XmlAttribute] public int bossShieldHard = 5;

    public void Normalize()
    {
        hudDynamicRefreshSeconds = Clamp(hudDynamicRefreshSeconds, 0.05f, 1f);
        hudClockRefreshSeconds = Clamp(hudClockRefreshSeconds, 0.25f, 2f);
        scoreRefreshSeconds = Clamp(scoreRefreshSeconds, 0.05f, 1f);
        endlessWaveSeconds = Max(1f, endlessWaveSeconds);
        endlessCampWarningSeconds = Max(1f, endlessCampWarningSeconds);
        endlessCampStrikeSeconds = Max(endlessCampWarningSeconds + 0.5f, endlessCampStrikeSeconds);
        endlessCampStartWave = Max(1, endlessCampStartWave);
        endlessTier2Wave = Max(2, endlessTier2Wave);
        endlessTier3Wave = Max(endlessTier2Wave + 1, endlessTier3Wave);
        endlessTier4Wave = Max(endlessTier3Wave + 1, endlessTier4Wave);
        endlessBackgroundFinalWave = Max(endlessTier4Wave, endlessBackgroundFinalWave);
        endlessBossStart = Max(1f, endlessBossStart);
        endlessBossWarning = Clamp(endlessBossWarning, 0f, endlessBossStart);
        endlessBossDuration = Max(1f, endlessBossDuration);
        endlessBossGap = Max(endlessBossDuration + endlessBossWarning + 1f, endlessBossGap);
        bossNormalFireScale = Clamp(bossNormalFireScale, 0f, 1f);
        bossAttackInterval = Max(0.1f, bossAttackInterval);
        bossVisualScale = Max(1f, bossVisualScale);
        bossPhase2 = Clamp(bossPhase2, 0.05f, 0.85f);
        bossPhase3 = Clamp(bossPhase3, bossPhase2 + 0.05f, 0.9f);
        bossPhase4 = Clamp(bossPhase4, bossPhase3 + 0.05f, 0.95f);
        fifthBossPhaseSplit = Clamp(fifthBossPhaseSplit, 0.1f, 0.9f);
        fifthBossProtectionSeconds = Max(0f, fifthBossProtectionSeconds);
        bossExtraCrossRound = Max(1, bossExtraCrossRound);
        bossExtraHomingRound = Max(1, bossExtraHomingRound);
        bossNormalCoreCadence = Max(1, bossNormalCoreCadence);
        bossRageCoreCadence = Max(1, bossRageCoreCadence);
        bossFanMax = Max(1, bossFanMax);
        bossSpiralMax = Max(1, bossSpiralMax);
        bossHomingMax = Max(1, bossHomingMax);
        bossRingMax = Max(1, bossRingMax);
        bossShieldEasy = Max(1, bossShieldEasy);
        bossShieldNormal = Max(1, bossShieldNormal);
        bossShieldHard = Max(1, bossShieldHard);
    }

    private static float Clamp(float value, float min, float max)
    {
        return value < min ? min : (value > max ? max : value);
    }

    private static float Max(float left, float right) { return left > right ? left : right; }
    private static int Max(int left, int right) { return left > right ? left : right; }
}
