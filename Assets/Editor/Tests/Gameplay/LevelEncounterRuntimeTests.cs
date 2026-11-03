using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class LevelEncounterRuntimeTests
{
    private int previousLevel;
    private int previousDifficulty;
    private int previousHero;
    private bool previousEndless;

    [SetUp]
    public void SetUp()
    {
        previousLevel = GameDataMgr.Instance.selectedLevelId;
        previousDifficulty = GameDataMgr.Instance.difficultyIndex;
        previousHero = GameDataMgr.Instance.heroIndex;
        previousEndless = GameDataMgr.Instance.IsEndless;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = 1f;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            yield return new ExitPlayMode();
        GameDataMgr.Instance.SetLevel(previousLevel);
        GameDataMgr.Instance.SetDifficulty(previousDifficulty);
        GameDataMgr.Instance.heroIndex = previousHero;
        if (previousEndless)
            GameDataMgr.Instance.SelectEndless();
    }

    [UnityTest]
    public IEnumerator AllFiveLevelsSpawnDistinctWavesAndShowTheirFeature()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        int[] expectedCounts = { 18, 12, 12, 8, 16 };
        string[] titles = { "移动航道", "核心识别", "诱导突围", "能量接力", "核心反击" };

        for (int levelId = 1; levelId <= 5; levelId++)
        {
            yield return LoadLevel(levelId);
            GameFlowManager flow = GameFlowManager.Instance;
            LevelEncounterDirector encounter = LevelEncounterDirector.Instance;
            flow.Advance(2.1f);
            encounter.Tick(flow.RunTime);
            yield return null;

            Assert.IsTrue(encounter.HasWarning, "Missing warning in level " + levelId);
            Assert.IsTrue(encounter.GetComponentsInChildren<LineRenderer>().Any(line => line.enabled));
            Text hud = GameObject.Find("labEncounter").GetComponent<Text>();
            StringAssert.Contains(titles[levelId - 1], hud.text);
            Assert.Greater(hud.fontSize, 14);
            CaptureIfRequested("Level" + levelId + "Warning.png");

            flow.Advance(1.2f);
            encounter.Tick(flow.RunTime);
            BulletObject[] bullets = ActiveBullets();
            Assert.AreEqual(expectedCounts[levelId - 1], bullets.Length, "Wrong wave in level " + levelId);
            Assert.AreEqual(1, encounter.WavesFired);
            Assert.AreEqual(bullets.Length, encounter.BulletsFired);
            Assert.IsFalse(encounter.HasWarning);

            if (levelId == 1)
            {
                foreach (BulletObject bullet in bullets)
                    Assert.GreaterOrEqual(Mathf.Abs(Camera.main.WorldToViewportPoint(bullet.transform.position).x - 0.5f), 0.12f);
            }
            if (levelId == 2)
            {
                Assert.IsTrue(bullets.All(bullet => bullet.ChainRadius > 0f));
                Assert.AreEqual(1, bullets.Count(bullet => bullet.Interaction == BulletInteraction.ClusterCore));
                Assert.AreEqual(11, bullets.Count(bullet => bullet.Interaction == BulletInteraction.ClusterDecoy));
            }

            // Move actual pooled shots into the arena before capturing their formation.
            foreach (BulletObject bullet in bullets)
                bullet.transform.position += bullet.transform.forward * 45f;
            yield return null;
            CaptureIfRequested("Level" + levelId + "Wave.png");
            if (levelId == 2)
            {
                bullets.Single(bullet => bullet.Interaction == BulletInteraction.ClusterCore).PlayerClear();
                Assert.AreEqual(12, ScoreManager.Instance.ClearCount);
                Assert.AreEqual(1, ScoreManager.Instance.ClusterWavesCleared);
                Assert.AreEqual(0, ActiveBullets().Length);
            }
        }
    }

    [UnityTest]
    public IEnumerator WrongClusterTargetSplitsAndDoesNotCompleteTheWave()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        yield return LoadLevel(2);
        GameFlowManager flow = GameFlowManager.Instance;
        LevelEncounterDirector encounter = LevelEncounterDirector.Instance;
        flow.Advance(2.1f);
        encounter.Tick(flow.RunTime);
        flow.Advance(1.2f);
        encounter.Tick(flow.RunTime);

        BulletObject decoy = ActiveBullets().First(
            bullet => bullet.Interaction == BulletInteraction.ClusterDecoy);
        Assert.IsTrue(decoy.PlayerClear());
        Assert.AreEqual(15, ActiveBullets().Length);
        Assert.AreEqual(1, ScoreManager.Instance.ClearCount);
        Assert.AreEqual(0, ScoreManager.Instance.ClusterWavesCleared);

        BulletObject core = ActiveBullets().Single(
            bullet => bullet.Interaction == BulletInteraction.ClusterCore);
        Assert.IsTrue(core.PlayerClear());
        Assert.AreEqual(4, ActiveBullets().Length, "Only the four split punishment bullets should remain.");
        Assert.AreEqual(1, ScoreManager.Instance.ClusterWavesCleared);
    }

    [UnityTest]
    public IEnumerator HardDifficultyCombinesTheFirstCorridorWithASideSweep()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        yield return LoadLevel(1);
        GameDataMgr.Instance.SetDifficulty(3);
        GameFlowManager flow = GameFlowManager.Instance;
        LevelEncounterDirector encounter = LevelEncounterDirector.Instance;
        flow.Advance(2.1f);
        encounter.Tick(flow.RunTime);
        flow.Advance(1.2f);
        encounter.Tick(flow.RunTime);

        BulletObject[] bullets = ActiveBullets();
        Assert.Greater(bullets.Length, 27);
        Assert.IsTrue(bullets.Any(bullet => Mathf.Abs(bullet.transform.forward.x) > 0.9f),
            "Hard mode should add a horizontal sweep from the first wave.");
    }

    [UnityTest]
    public IEnumerator HardDifficultyAddsFakeWarningsAndDoubleCoresInTheSecondPhase()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        yield return LoadLevel(2);
        GameDataMgr.Instance.SetDifficulty(3);
        for (int i = 1; i <= 9; i++)
            ScoreManager.Instance.RegisterClusterWave(1000 + i);

        LevelEncounterDirector encounter = LevelEncounterDirector.Instance;
        Assert.AreEqual(2, encounter.CurrentPhase);
        typeof(LevelEncounterDirector).GetProperty("WavesFired").GetSetMethod(true)
            .Invoke(encounter, new object[] { 2 });
        GameFlowManager.Instance.Advance(2.1f);
        encounter.Tick(GameFlowManager.Instance.RunTime);
        LineRenderer feint = GameObject.Find("EncounterWarning9").GetComponent<LineRenderer>();
        Assert.IsTrue(feint.enabled);
        Assert.Less(feint.startColor.a, 0.5f);

        GameFlowManager.Instance.Advance(1.2f);
        encounter.Tick(GameFlowManager.Instance.RunTime);
        BulletObject[] bullets = ActiveBullets();
        Assert.AreEqual(2, bullets.Count(bullet => bullet.Interaction == BulletInteraction.ClusterCore));
        Assert.AreEqual(18, bullets.Length);
    }

    [UnityTest]
    public IEnumerator RepairHeroAcceleratesRelayWithoutMakingItRequired()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        yield return LoadLevel(4);
        GameDataMgr.Instance.heroIndex = 1;
        SignalRelayController relay = SignalRelayController.Instance;

        PlayerObject.Instance.transform.position = relay.EnergySourcePosition;
        relay.Tick(1.6f);
        Assert.AreEqual(1f, relay.CarriedEnergy, 0.001f);
        PlayerObject.Instance.transform.position = relay.CurrentBeaconPosition;
        relay.Tick(3.2f);
        Assert.AreEqual(1, relay.ActivatedBeacons);
        Assert.AreEqual("维修协同", PlayerObject.ActiveStageAffinityText());
    }

    [UnityTest]
    public IEnumerator TechnicalRatingUsesHitsMistakesAndComboInsteadOfHealthAlone()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        yield return LoadLevel(1);
        PlayerObject.Instance.nowHp = PlayerObject.Instance.maxHp;
        for (int pass = 0; pass < 5; pass++)
            ScoreManager.Instance.RegisterCorridorPass();

        CampaignMissionController mission = CampaignMissionController.Instance;
        Assert.AreEqual(3, mission.CalculateStars());
        Assert.AreEqual(7, mission.CalculateChallengeMask());

        ScoreManager.Instance.RegisterPlayerHit();
        Assert.AreEqual(2, mission.CalculateStars(),
            "Normal difficulty should allow one hit for two stars but never for three stars.");
        ScoreManager.Instance.RegisterPlayerHit();
        Assert.AreEqual(1, mission.CalculateStars());
        Assert.AreEqual(CampaignMissionController.NoMistakeChallenge, mission.CalculateChallengeMask());
    }

    [UnityTest]
    public IEnumerator FailureAdvicePrioritizesRepeatedClusterMistakes()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        yield return LoadLevel(2);
        ScoreManager.Instance.RegisterMistake();
        ScoreManager.Instance.RegisterMistake();

        StringAssert.Contains("误点过多", CampaignMissionController.Instance.ResultAdvice());
        StringAssert.Contains("呼吸发光", CampaignMissionController.Instance.ResultAdvice());
    }

    [UnityTest]
    public IEnumerator HeroScreenClearAwardsAtMostOneClusterWave()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        yield return LoadLevel(2);
        GameFlowManager flow = GameFlowManager.Instance;
        LevelEncounterDirector encounter = LevelEncounterDirector.Instance;

        flow.Advance(2.1f);
        encounter.Tick(flow.RunTime);
        flow.Advance(1.2f);
        encounter.Tick(flow.RunTime);
        flow.Advance(3f);
        encounter.Tick(flow.RunTime);
        flow.Advance(1f);
        encounter.Tick(flow.RunTime);
        Assert.AreEqual(24, ActiveBullets().Length);

        BulletObject.ClearByPlayer();
        Assert.AreEqual(0, ActiveBullets().Length);
        Assert.AreEqual(1, ScoreManager.Instance.ClusterWavesCleared);
    }

    [UnityTest]
    public IEnumerator RelayMissionRequiresEnergyDeliveryToAllThreeBeacons()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        yield return LoadLevel(4);
        SignalRelayController relay = SignalRelayController.Instance;
        Assert.IsNotNull(relay);

        for (int beacon = 0; beacon < 3; beacon++)
        {
            PlayerObject.Instance.transform.position = relay.EnergySourcePosition;
            relay.Tick(2f);
            Assert.AreEqual(1f, relay.CarriedEnergy, 0.001f);
            PlayerObject.Instance.transform.position = relay.CurrentBeaconPosition;
            relay.Tick(4f);
            Assert.AreEqual(beacon + 1, relay.ActivatedBeacons);
            Assert.AreEqual(beacon + 1, ScoreManager.Instance.RelayBeacons);
        }

        yield return null;
        Assert.AreEqual(CampaignMissionState.Victory, CampaignMissionController.Instance.State);
        Assert.AreEqual(GameFlowState.MissionComplete, GameFlowManager.Instance.State);
    }

    [UnityTest]
    public IEnumerator StormRecoveryCancelsWarningsAndPausePreservesTheirClock()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        yield return LoadLevel(5);
        GameFlowManager flow = GameFlowManager.Instance;
        LevelEncounterDirector encounter = LevelEncounterDirector.Instance;

        flow.Advance(11.5f);
        encounter.Tick(flow.RunTime);
        Assert.IsTrue(encounter.HasWarning);
        flow.Pause();
        flow.Advance(20f);
        yield return new WaitForSecondsRealtime(0.1f);
        Assert.AreEqual(11.5f, flow.RunTime, 0.001f);
        Assert.IsTrue(encounter.HasWarning);
        Assert.AreEqual(0, encounter.WavesFired);

        flow.Resume();
        Time.timeScale = 0f;
        flow.Advance(0.8f);
        encounter.Tick(flow.RunTime);
        Assert.IsFalse(encounter.HasWarning, "An old warning must not fire inside the recovery window.");
        Assert.AreEqual(0, ActiveBullets().Length);
        flow.Advance(1.8f);
        encounter.Tick(flow.RunTime);
        Assert.IsTrue(encounter.HasWarning);
        Assert.AreEqual(LevelAttack.Crossfire, encounter.CurrentAttack);
        flow.Advance(0.9f);
        encounter.Tick(flow.RunTime);
        Assert.AreEqual(8, ActiveBullets().Length);

        flow.Advance(11.1f);
        encounter.Tick(flow.RunTime);
        Assert.AreEqual(LevelAttack.LockOn, encounter.CurrentAttack);
        Assert.IsTrue(encounter.HasWarning);
    }

    [UnityTest]
    public IEnumerator ClusterMissionCountsChainsAndHeroScreenClear()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        yield return LoadLevel(2);
        GameFlowManager flow = GameFlowManager.Instance;
        LevelEncounterDirector encounter = LevelEncounterDirector.Instance;
        string[] keys = { "U2.Campaign.MaxUnlocked", "U2.Campaign.Level2.Stars",
            "U2.Campaign.Level2.BestScore", "U2.Campaign.Level2.BestTime",
            "U2.Campaign.Level2.Challenges" };
        bool[] hadKeys = keys.Select(PlayerPrefs.HasKey).ToArray();
        int[] savedValues = keys.Select(key => PlayerPrefs.GetInt(key)).ToArray();
        try
        {
            int targetWaves = CampaignMissionController.Instance.TargetValue();
            Assert.AreEqual(16, targetWaves);
            int expectedClears = 0;
            for (int wave = 0; wave < targetWaves; wave++)
            {
                flow.Advance(3f);
                encounter.Tick(flow.RunTime);
                flow.Advance(1f);
                encounter.Tick(flow.RunTime);
                int activeCount = ActiveBullets().Length;
                int phase = LevelEncounterDirector.PhaseForProgress((float)wave / targetWaves);
                int eliteBonus = phase >= 3 && (wave + 1) % 4 == 0 ? 4 : 0;
                Assert.AreEqual(12 + (phase - 1) * 2 + eliteBonus, activeCount,
                    "Later mission phases should add more decoys around the core.");
                if (wave < targetWaves - 1)
                {
                    // Use the same collider and layer as the player's click raycast.
                    BulletObject target = ActiveBullets().Single(
                        bullet => bullet.Interaction == BulletInteraction.ClusterCore);
                    Ray ray = Camera.main.ScreenPointToRay(Camera.main.WorldToScreenPoint(target.transform.position));
                    Physics.SyncTransforms();
                    RaycastHit hit;
                    Assert.IsTrue(Physics.Raycast(ray, out hit, 1000f, 1 << LayerMask.NameToLayer("Bullent")));
                    Assert.IsTrue(hit.transform.GetComponent<BulletObject>().PlayerClear());
                }
                else
                {
                    int previousHero = GameDataMgr.Instance.heroIndex;
                    try
                    {
                        GameDataMgr.Instance.heroIndex = 0;
                        typeof(PlayerObject).GetMethod("UseHeroSkill", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(PlayerObject.Instance, null);
                    }
                    finally { GameDataMgr.Instance.heroIndex = previousHero; }
                }
                expectedClears += activeCount;
                Assert.AreEqual(expectedClears, ScoreManager.Instance.ClearCount);
                Assert.AreEqual(wave + 1, ScoreManager.Instance.ClusterWavesCleared);
                yield return null;
            }
            Assert.AreEqual(CampaignMissionState.Victory, CampaignMissionController.Instance.State);
            Assert.AreEqual(GameFlowState.MissionComplete, flow.State);
        }
        finally
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (hadKeys[i]) PlayerPrefs.SetInt(keys[i], savedValues[i]);
                else PlayerPrefs.DeleteKey(keys[i]);
            }
            PlayerPrefs.Save();
        }
    }

    [UnityTest]
    public IEnumerator LockOnShotsStopSteeringAfterTheirEscapeWindow()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        yield return LoadLevel(3);
        GameFlowManager flow = GameFlowManager.Instance;
        LevelEncounterDirector encounter = LevelEncounterDirector.Instance;
        flow.Advance(2f);
        encounter.Tick(flow.RunTime);
        flow.Advance(1.2f);
        encounter.Tick(flow.RunTime);
        BulletObject shot = ActiveBullets()[0];
        Time.timeScale = 1f;
        float stopTime = Time.time + 1.35f;
        while (Time.time < stopTime)
            yield return null;
        Assert.IsFalse(shot.IsDead);
        Vector3 heading = shot.transform.forward;
        PlayerObject.Instance.transform.position = shot.transform.position + Vector3.Cross(heading, Vector3.up) * 100f;
        stopTime = Time.time + 0.25f;
        while (Time.time < stopTime)
            yield return null;
        Assert.Less(Vector3.Angle(heading, shot.transform.forward), 0.01f,
            "After 1.2 seconds the shot must keep its escapeable straight heading.");
    }

    [UnityTest]
    public IEnumerator FifthMissionRequiresReflectedCoresAndDoesNotSubmitToLeaderboard()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        yield return LoadLevel(5);
        string[] keys = { "U2.Campaign.Level5.Stars", "U2.Campaign.Level5.BestScore",
            "U2.Campaign.Level5.BestTime", "U2.Campaign.Level5.Challenges" };
        bool[] hadKeys = keys.Select(PlayerPrefs.HasKey).ToArray();
        int[] savedValues = keys.Select(key => PlayerPrefs.GetInt(key)).ToArray();
        int rankCount = GameDataMgr.Instance.rankData.ranks.Count;
        try
        {
            GameFlowManager flow = GameFlowManager.Instance;
            Assert.IsFalse(GameDataMgr.Instance.IsEndless);
            flow.Advance(45f);
            yield return null;
            Assert.AreEqual(GameFlowState.BossSurvival, flow.State);
            Assert.AreEqual(CampaignMissionState.Running, CampaignMissionController.Instance.State);
            int target = CampaignMissionController.Instance.TargetValue();
            Assert.AreEqual(9, target);
            int shieldTarget = BossController.Instance.ShieldHitsRequired;
            for (int hit = 0; hit < shieldTarget; hit++)
                BossController.Instance.RegisterReflectedHit(PlayerObject.Instance.transform.position);
            Assert.IsTrue(BossController.Instance.ShieldBroken);
            Assert.IsTrue(BossController.Instance.RageVisualActive);
            Assert.IsTrue(BKMusic.Instance.CombatIntensity);
            typeof(BossController).GetMethod("SpawnBullet", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(BossController.Instance, new object[]
                {
                    1, Vector3.back, BulletInteraction.Normal, 1f
                });
            Assert.IsTrue(ActiveBullets().Any(bullet => bullet.RageVisual),
                "Bullets spawned after the shield break should use the rage color.");
            for (int hit = shieldTarget; hit < target; hit++)
                BossController.Instance.RegisterReflectedHit(PlayerObject.Instance.transform.position);
            yield return null;
            Assert.AreEqual(target, ScoreManager.Instance.ReflectedCores);
            Assert.Greater(ScoreManager.Instance.MechanicBonusScore, 0);
            Assert.AreEqual(GameFlowState.MissionComplete, flow.State);
            Assert.AreEqual(CampaignMissionState.Victory, CampaignMissionController.Instance.State);
            Assert.AreEqual(3, CampaignMissionController.Instance.EarnedStars);
            Assert.AreEqual(7, CampaignMissionController.Instance.EarnedChallengeMask);
            Assert.AreEqual(7, CampaignProgress.ChallengeMask(5));
            Assert.IsTrue(ScoreManager.Instance.Finished);
            Assert.IsNotNull(GameObject.Find("CampaignMissionResult"));
            GameObject statistics = GameObject.Find("MissionResultMechanics");
            Assert.IsNotNull(statistics);
            StringAssert.Contains("反弹 " + target, statistics.GetComponent<Text>().text);
            StringAssert.Contains("机制奖励", statistics.GetComponent<Text>().text);
            StringAssert.Contains("零受击", GameObject.Find("MissionResultChallenges").GetComponent<Text>().text);
            Assert.IsNull(GameObject.Find("MissionNextButton"));
            GameDataMgr.Instance.AddRank("CampaignMustNotRank", 110);
            Assert.AreEqual(rankCount, GameDataMgr.Instance.rankData.ranks.Count);
            float completedAt = flow.RunTime;
            flow.Advance(100f);
            Assert.AreEqual(completedAt, flow.RunTime, 0.001f);
        }
        finally
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (hadKeys[i]) PlayerPrefs.SetInt(keys[i], savedValues[i]);
                else PlayerPrefs.DeleteKey(keys[i]);
            }
            PlayerPrefs.Save();
        }
    }

    private static IEnumerator LoadLevel(int levelId)
    {
        BulletObject.ClearAll();
        GameDataMgr.Instance.SetDifficulty(2);
        GameDataMgr.Instance.SetLevel(levelId);
        SceneManager.LoadScene("GameScene");
        yield return null;
        float deadline = Time.realtimeSinceStartup + 15f;
        while (LevelEncounterDirector.Instance == null || PlayerObject.Instance == null ||
            CampaignMissionController.Instance == null)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "GameScene did not initialize.");
            yield return null;
        }
        Time.timeScale = 1f;
        float warmupEnd = Time.time + 0.2f;
        while (Time.time < warmupEnd)
            yield return null;
        Time.timeScale = 0f;
        BulletObject.ClearAll();
        GameFlowManager.Instance.Reset();
        ScoreManager.Instance.Reset();
        LevelEncounterDirector.Instance.Tick(0f);
        PlayerObject.Instance.nowHp = 100;
    }

    private static BulletObject[] ActiveBullets()
    {
        return UnityEngine.Object.FindObjectsOfType<BulletObject>().Where(bullet => !bullet.IsDead).ToArray();
    }

    private static void CaptureIfRequested(string filename)
    {
        if (Environment.GetEnvironmentVariable("U2_CAPTURE_LEVEL_FEATURES") != "1")
            return;
        RenderTexture target = new RenderTexture(1600, 900, 24);
        RenderTexture previousActive = RenderTexture.active;
        Texture2D image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            Camera[] cameras = Camera.allCameras.OrderBy(camera => camera.depth).ToArray();
            foreach (Camera camera in cameras)
            {
                RenderTexture previousTarget = camera.targetTexture;
                float previousAspect = camera.aspect;
                try
                {
                    camera.targetTexture = target;
                    camera.aspect = 16f / 9f;
                    UGUIAspectSafeLayout layout = UnityEngine.Object.FindObjectOfType<UGUIAspectSafeLayout>();
                    if (layout != null && layout.UiCamera == camera)
                    {
                        layout.Apply();
                        Canvas.ForceUpdateCanvases();
                    }
                    camera.Render();
                }
                finally
                {
                    camera.targetTexture = previousTarget;
                    camera.aspect = previousAspect;
                }
            }
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Logs/LevelFeatures");
            File.WriteAllBytes("Logs/LevelFeatures/" + filename, image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previousActive;
            UnityEngine.Object.Destroy(image);
            target.Release();
            UnityEngine.Object.Destroy(target);
        }
    }
}
