using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Main : MonoBehaviour
{
    void Start()
    {
        GameFlowManager.ResetGlobalTimeScale();
        NetworkBattleSession.Ensure();
        NetworkBattleUI.Ensure();
        ObjectPoolManager pool = ObjectPoolManager.Ensure(gameObject);
        SoundManager.Ensure();
        GameFlowManager flow = GameFlowManager.Ensure(gameObject);
        if (GameDataMgr.Instance.IsTraining)
        {
            LevelInfo training = new LevelInfo
            {
                id = 1,
                name = "训练模式",
                bossStart = 9999f,
                bossDuration = 30f,
                bossGap = 9999f,
            };
            flow.Configure(training);
        }
        else if (GameDataMgr.Instance.IsEndless)
            flow.ConfigureEndless();
        else
            flow.Configure(GameDataMgr.Instance.GetLevel());
        if (!GameDataMgr.Instance.IsTraining)
            DifficultyDirector.Ensure(gameObject);
        ScoreManager.Ensure(gameObject);
        if (!GameDataMgr.Instance.IsTraining)
            BossController.Ensure(gameObject);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameDataMgr.Instance.ValidateResourceReferences();
#endif

        RoleInfo info = GameDataMgr.Instance.GetHero();
        pool.PreloadAssets(GameDataMgr.Instance.ResourcesForCurrentRun());
        if (BossController.Instance != null)
            pool.PreloadAssets(BossController.Instance.ResourcePaths());
        GameObject prefab = pool.LoadAsset(info.asset);
        if (prefab == null)
        {
            GameLog.Error(GameLogCategory.Data, "PLAYER_RESOURCE_MISSING", info.asset);
            return;
        }

        GameObject obj = Instantiate(prefab);
        PlayerObject playerObj = obj.GetComponent<PlayerObject>();
        if (playerObj == null)
        {
            playerObj = obj.AddComponent<PlayerObject>();
        }

        int hp = GameDataMgr.Instance.PlayerHp(info);
        playerObj.speed = info.speed * 20;
        playerObj.maxHp = hp;
        playerObj.nowHp = hp;
        playerObj.turnSpeed = 20;

        GamePanel.Instance.ChangeHP(hp);
        if (!GameDataMgr.Instance.IsEndless && !GameDataMgr.Instance.IsTraining)
            LevelEncounterDirector.Ensure(gameObject);
        if (!GameDataMgr.Instance.IsEndless && !GameDataMgr.Instance.IsTraining && GameDataMgr.Instance.selectedLevelId == 4)
            SignalRelayController.Ensure(gameObject);
        if (GameDataMgr.Instance.IsEndless)
            EndlessRunController.Ensure(gameObject);
        if (GameDataMgr.Instance.IsTraining)
            TrainingModeController.Ensure(gameObject);
        CampaignMissionController.Ensure(gameObject);
        CampaignMissionUI.Ensure();
    }
}
