using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public class GameDataMgr
{
    public const int MaxLevelId = 5;
    public const string EndlessRankFile = "EndlessRankData";

    private static GameDataMgr instance = new GameDataMgr();

    public static GameDataMgr Instance => instance;

    public MusicData musicData;
    public RankData rankData;
    public RoleData roleData;
    public BulletData bulletData;
    public FireData fireData;
    public DifficultyData difficultyData;
    public LevelData levelData;
    public CombatTuningData combatTuning;

    private readonly GameConfigService configService = new GameConfigService();
    private readonly GameSelectionService selectionService = new GameSelectionService();
    private readonly AudioSettingsService audioSettingsService = new AudioSettingsService();
    private readonly LeaderboardService leaderboardService = new LeaderboardService();

    public GameConfigService Config => configService;
    public GameSelectionService Selection => selectionService;
    public CombatTuningData Tuning => combatTuning;
    public int heroIndex { get => selectionService.HeroIndex; set => selectionService.HeroIndex = value; }
    public int difficultyIndex { get => selectionService.DifficultyId; set => selectionService.DifficultyId = value; }
    public int selectedLevelId { get => selectionService.LevelId; set => selectionService.LevelId = value; }
    public bool IsEndless => selectionService.IsEndless;
    public bool IsTraining => selectionService.IsTraining;

    private GameDataMgr()
    {
        bool hadSavedMusicSettings = File.Exists(
            Path.Combine(Application.persistentDataPath, "MusicData.xml"));
        musicData = XmlDataMgr.Instance.Load(typeof(MusicData), "MusicData") as MusicData;
        // Keep old, unclassified records separate from the endless leaderboard.
        rankData = XmlDataMgr.Instance.Load(typeof(RankData), EndlessRankFile) as RankData;
        roleData = XmlDataMgr.Instance.Load(typeof(RoleData), "RoleData") as RoleData;
        bulletData = XmlDataMgr.Instance.Load(typeof(BulletData), "BulletData") as BulletData;
        fireData = XmlDataMgr.Instance.Load(typeof(FireData), "FireData") as FireData;
        difficultyData = XmlDataMgr.Instance.Load(typeof(DifficultyData), "DifficultyData") as DifficultyData;
        levelData = XmlDataMgr.Instance.Load(typeof(LevelData), "LevelData") as LevelData;
        combatTuning = XmlDataMgr.Instance.Load(typeof(CombatTuningData), "CombatTuningData") as CombatTuningData;

        InitDefaults();
        configService.Rebuild(bulletData, fireData, difficultyData, levelData);
        if (audioSettingsService.Upgrade(musicData, hadSavedMusicSettings))
        {
            audioSettingsService.Save(musicData);
        }
    }

    private void InitDefaults()
    {
        if (musicData == null)
            musicData = new MusicData();
        if (rankData == null)
            rankData = new RankData();
        if (roleData == null)
            roleData = new RoleData();
        if (bulletData == null)
            bulletData = new BulletData();
        if (fireData == null)
            fireData = new FireData();
        if (difficultyData == null)
            difficultyData = new DifficultyData();
        if (levelData == null)
            levelData = new LevelData();
        if (combatTuning == null)
            combatTuning = new CombatTuningData();
        combatTuning.Normalize();

        bool repairedLists = false;
        if (rankData.ranks == null) { rankData.ranks = new List<RankInfo>(); repairedLists = true; }
        if (roleData.roles == null) { roleData.roles = new List<RoleInfo>(); repairedLists = true; }
        if (bulletData.bullets == null) { bulletData.bullets = new List<BulletInfo>(); repairedLists = true; }
        if (fireData.fires == null) { fireData.fires = new List<FireInfo>(); repairedLists = true; }
        if (difficultyData.difficulties == null) { difficultyData.difficulties = new List<DifficultyInfo>(); repairedLists = true; }
        if (levelData.levels == null) { levelData.levels = new List<LevelInfo>(); repairedLists = true; }
        if (repairedLists)
            GameLog.Warning(GameLogCategory.Data, "REPAIRED", "detected null data lists and restored empty collections");

        if (difficultyData.difficulties.Count == 0)
        {
            difficultyData.difficulties.Add(NewDifficulty(1, "简单", 2, 0.75f, 1.35f, 1.25f, 4f, 8f));
            difficultyData.difficulties.Add(NewDifficulty(2, "普通", 0, 1f, 1f, 1f, 3f, 10f));
            difficultyData.difficulties.Add(NewDifficulty(3, "困难", -1, 1.25f, 0.75f, 0.8f, 2f, 13f));
        }

        if (levelData.levels.Count == 0)
        {
            levelData.levels.Add(NewLevel(1, "第一关-边境巡航", 0f, 1f, 1f, 1f, "1,2"));
            levelData.levels.Add(NewLevel(2, "第二关-弹幕清扫", 30f, 1.15f, 0.85f, 0.9f, "1,2,3"));
            levelData.levels.Add(NewLevel(3, "第三关-追踪压制", 60f, 1.35f, 0.7f, 0.75f, "2,3,4"));
            levelData.levels.Add(NewLevel(4, "第四关-精英拦截", 100f, 1.6f, 0.55f, 0.6f, "1,2,3,4"));
        }

        AddExtendedLevelsIfNeeded();
        // Keep the campaign capped at the fifth level, including when an older
        // LevelData.xml still contains entries from a previous six-level build.
        levelData.levels.RemoveAll(level => level == null || level.id > MaxLevelId);
        NormalizeLevelMissions();
        selectedLevelId = Mathf.Clamp(selectedLevelId, 1, MaxLevelId);
        SortRanks();
    }

    private void AddExtendedLevelsIfNeeded()
    {
        if (levelData == null || levelData.levels == null)
            return;

        if (!levelData.levels.Exists(level => level.id == 5))
        {
            levelData.levels.Add(NewLevel(5, "第五关-星环风暴", 150f, 1.7f, 0.48f, 0.52f, "2,3,4"));
        }
    }

    private void NormalizeLevelMissions()
    {
        foreach (LevelInfo level in levelData.levels)
        {
            if (level == null)
                continue;

            switch (level.id)
            {
                case 1:
                    level.name = "第一关-边境巡航";
                    ApplyMissionDefaults(level, "survive", 45, "穿过移动缺口，生存45秒", 9999f, 30f, 9999f);
                    level.featureTitle = "移动航道";
                    level.featureHint = "跟随移动缺口穿行；中后段会叠加左右横扫弹幕";
                    break;
                case 2:
                    level.name = "第二关-弹幕清扫";
                    ApplyMissionDefaults(level, "clear", 8, "引爆弹簇，完成8波连锁清扫", 9999f, 30f, 9999f);
                    level.featureTitle = "核心识别";
                    level.featureHint = "只点击青色核心可连锁清扫；误点外圈弹体会分裂";
                    break;
                case 3:
                    level.name = "第三关-追踪压制";
                    ApplyMissionDefaults(level, "dodge", 5, "成功诱导并躲开5轮锁定齐射", 9999f, 30f, 9999f);
                    level.featureTitle = "诱导突围";
                    level.featureHint = "锁定后换位并避开侧翼齐射；无伤脱离才计入进度";
                    break;
                case 4:
                    level.name = "第四关-信标接力";
                    ApplyMissionDefaults(level, "beacon", 3, "携带能量并激活3座信标", 9999f, 30f, 9999f);
                    level.featureTitle = "能量接力";
                    level.featureHint = "在中央蓝环取能，再送往橙色信标；受击会损失能量";
                    break;
                case 5:
                    level.name = "第五关-星环风暴";
                    level.bulletRate = 1.7f;
                    ApplyMissionDefaults(level, "reflect", 6, "反弹金色核心，击破护盾并压制BOSS", 45f, 30f, 35f);
                    level.featureTitle = "核心反击";
                    level.featureHint = "点击金色核心反弹攻击；破盾后BOSS会进入交叉追踪狂暴阶段";
                    break;
            }
        }
    }

    private static void ApplyMissionDefaults(LevelInfo level, string type, int target, string text,
        float bossStart, float bossDuration, float bossGap)
    {
        level.objectiveType = type;
        level.objectiveTarget = target;
        level.objectiveText = text;
        level.bossStart = bossStart;
        level.bossDuration = bossDuration;
        level.bossGap = bossGap;
    }

    private DifficultyInfo NewDifficulty(int id, string name, int hpBonus, float bulletRate, float fireRate, float delayRate, float shieldTime, float skillCd)
    {
        DifficultyInfo info = new DifficultyInfo();
        info.id = id;
        info.name = name;
        info.hpBonus = hpBonus;
        info.bulletRate = bulletRate;
        info.fireRate = fireRate;
        info.delayRate = delayRate;
        info.shieldTime = shieldTime;
        info.skillCd = skillCd;
        return info;
    }

    private LevelInfo NewLevel(int id, string name, float beginTime, float bulletRate, float fireRate, float delayRate, string fireIds)
    {
        LevelInfo info = new LevelInfo();
        info.id = id;
        info.name = name;
        info.beginTime = beginTime;
        info.bulletRate = bulletRate;
        info.fireRate = fireRate;
        info.delayRate = delayRate;
        info.fireIds = fireIds;
        return info;
    }

    public void SaveMusic()
    {
        audioSettingsService.Save(musicData);
    }

    public void SetMusic(bool isOpen)
    {
        audioSettingsService.SetMusic(musicData, isOpen);
    }

    public void SetSound(bool isOpen)
    {
        musicData.soundOn = isOpen;
    }

    public void SetMusicVolume(float value)
    {
        audioSettingsService.SetMusicVolume(musicData, value);
    }

    public void SetSoundVolume(float value)
    {
        musicData.soundVolume = value;
    }

    public void AddRank(string name, int time)
    {
        if (!IsEndless)
            return;

        if (rankData == null)
            rankData = new RankData();
        if (rankData.ranks == null)
            rankData.ranks = new List<RankInfo>();

        int scoreValue = 0;
        int runTime = Mathf.Max(0, time);
        int bosses = 0;
        int clears = 0;
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.FinishScore();
            scoreValue = ScoreManager.Instance.TotalScore;
            runTime = Mathf.FloorToInt(ScoreManager.Instance.RunTime);
            bosses = ScoreManager.Instance.BossRounds;
            clears = ScoreManager.Instance.ClearCount;
        }
        int waves = EndlessRunController.Instance != null ? EndlessRunController.Instance.CurrentWave : 0;
        RankInfo rankInfo = leaderboardService.CreateEndlessEntry(name, time, DifficultyName(),
            heroIndex, scoreValue, runTime, bosses, waves, clears);
        rankData.ranks.Add(rankInfo);

        SortRanks();
        leaderboardService.Save(rankData, EndlessRankFile);
    }

    public void SortRanks()
    {
        if (rankData == null)
        {
            rankData = new RankData();
        }
        if (rankData.ranks == null)
            rankData.ranks = new List<RankInfo>();

        leaderboardService.NormalizeAndSort(rankData, 20);
    }

    public RoleInfo GetHero()
    {
        if (roleData == null || roleData.roles == null || roleData.roles.Count == 0)
        {
            RoleInfo info = new RoleInfo();
            info.hp = 4;
            info.speed = 6;
            info.volume = 5;
            info.asset = "Airplane/Airplane1";
            info.scale = 13f;
            info.shieldId = 1;
            return info;
        }

        heroIndex = Mathf.Clamp(heroIndex, 0, roleData.roles.Count - 1);
        return roleData.roles[heroIndex];
    }

    public void SetDifficulty(int difficultyId)
    {
        if (difficultyData == null || difficultyData.difficulties == null || difficultyData.difficulties.Count == 0)
        {
            difficultyIndex = 1;
            return;
        }
        difficultyIndex = Mathf.Clamp(difficultyId, 1, difficultyData.difficulties.Count);
    }

    public DifficultyInfo GetDifficulty()
    {
        if (difficultyData == null || difficultyData.difficulties == null || difficultyData.difficulties.Count == 0)
            return NewDifficulty(2, "普通", 0, 1f, 1f, 1f, 3f, 10f);
        return configService.Difficulty(difficultyIndex) ?? difficultyData.difficulties[0];
    }

    public LevelInfo GetLevel()
    {
        return IsEndless ? GetLevel(GameFlowManager.Instance != null ? GameFlowManager.Instance.RunTime : 0f)
            : GetLevelById(selectedLevelId);
    }

    public void SetLevel(int levelId)
    {
        selectionService.SelectLevel(levelId);
    }

    public void SelectEndless()
    {
        selectionService.SelectEndless();
    }

    public void SelectTraining()
    {
        selectionService.SelectTraining();
    }

    public LevelInfo GetLevelById(int levelId)
    {
        LevelInfo indexed = configService.Level(levelId);
        if (indexed != null)
            return indexed;

        if (levelData != null && levelData.levels != null && levelData.levels.Count > 0)
        {
            for (int i = 0; i < levelData.levels.Count; i++)
            {
                if (levelData.levels[i] != null)
                    return levelData.levels[i];
            }
        }
        return NewLevel(1, "第一关-边境巡航", 0f, 1f, 1f, 1f, "1,2");
    }

    public LevelInfo GetLevel(float runTime)
    {
        LevelInfo result = GetLevelById(1);
        if (levelData == null || levelData.levels == null || levelData.levels.Count == 0)
            return result;
        for (int i = 0; i < levelData.levels.Count; i++)
        {
            if (levelData.levels[i] != null && levelData.levels[i].id <= MaxLevelId &&
                runTime >= levelData.levels[i].beginTime &&
                levelData.levels[i].beginTime >= result.beginTime)
            {
                result = levelData.levels[i];
            }
        }

        return result;
    }

    public LevelInfo GetNextLevel(float runTime)
    {
        LevelInfo current = GetLevel(runTime);
        LevelInfo next = null;
        foreach (LevelInfo level in levelData.levels)
        {
            if (level != null && level.id > current.id && level.id <= MaxLevelId &&
                level.beginTime > runTime && (next == null || level.beginTime < next.beginTime))
                next = level;
        }
        return next;
    }

    public string DifficultyName()
    {
        return GetDifficulty().name;
    }

    public string LevelName()
    {
        if (IsTraining)
            return "训练模式";
        LevelInfo level = GetLevel();
        return IsEndless ? "无尽模式 · 阶段 " + level.id : level.name;
    }

    public int HpBonus()
    {
        return GetDifficulty().hpBonus;
    }

    public int PlayerHp(RoleInfo role)
    {
        if (role == null)
            return GetDifficulty().id == 3 ? 2 : 1;

        int minimum = GetDifficulty().id == 3 ? 2 : 1;
        return Mathf.Max(minimum, role.hp + HpBonus());
    }

    public float ShieldTime()
    {
        return GetDifficulty().shieldTime;
    }

    public float SkillCd()
    {
        return GetDifficulty().skillCd;
    }

    public int EncounterCount(int easy, int normal, int hard)
    {
        switch (GetDifficulty().id)
        {
            case 1: return easy;
            case 3: return hard;
            default: return normal;
        }
    }

    public int EncounterDensityPercent()
    {
        return EncounterCount(100, 130, 165);
    }

    public float EncounterWarningTime(float baseTime)
    {
        float multiplier;
        switch (GetDifficulty().id)
        {
            case 1: multiplier = 1.15f; break;
            case 3: multiplier = 0.75f; break;
            default: multiplier = 1f; break;
        }

        return Mathf.Max(0.35f, baseTime * multiplier);
    }

    public int EncounterWarningPercent()
    {
        return EncounterCount(115, 100, 75);
    }

    public float EncounterTrackingTime()
    {
        switch (GetDifficulty().id)
        {
            case 1: return 1f;
            case 3: return 1.45f;
            default: return 1.2f;
        }
    }

    public int MissionTarget(LevelInfo level)
    {
        if (level == null)
            return 1;

        switch (level.objectiveType)
        {
            case "survive": return EncounterCount(45, 55, 65);
            case "clear": return EncounterCount(8, 16, 26);
            case "score": return EncounterCount(900, 1200, 1550);
            case "dodge": return EncounterCount(5, 8, 12);
            case "beacon": return 3;
            case "reflect": return EncounterCount(6, 9, 12);
            default: return Mathf.Max(1, level.objectiveTarget);
        }
    }

    public string MissionObjectiveText(LevelInfo level)
    {
        if (level == null)
            return string.Empty;

        int target = MissionTarget(level);
        switch (level.objectiveType)
        {
            case "survive": return "穿过移动缺口，生存" + target + "秒";
            case "clear": return "引爆弹簇，完成" + target + "波连锁清扫";
            case "score": return "躲开锁定齐射，任务积分达到" + target + "分";
            case "dodge": return "成功诱导并躲开" + target + "轮锁定齐射";
            case "beacon": return "携带能量并激活" + target + "座信标";
            case "reflect": return "风暴BOSS战：反弹" + target + "枚金色核心，击破护盾并压制BOSS";
            default: return level.objectiveText;
        }
    }

    public float BulletSpeed()
    {
        float rate = GetDifficulty().bulletRate * GetLevel().bulletRate;
        if (DifficultyDirector.Instance != null)
        {
            rate = DifficultyDirector.Instance.ScaleBullet(rate);
        }

        return rate;
    }

    public float FireGap(float cd)
    {
        if (cd <= 0f)
            return cd;

        float rate = GetDifficulty().fireRate * GetLevel().fireRate;
        if (DifficultyDirector.Instance != null)
        {
            rate = DifficultyDirector.Instance.ScaleFire(rate);
        }

        float value = cd * rate;
        return Mathf.Max(0.08f, value);
    }

    public float FireDelay(float delay)
    {
        if (delay <= 0f)
            return delay;

        float rate = GetDifficulty().delayRate * GetLevel().delayRate;
        if (DifficultyDirector.Instance != null)
        {
            rate = DifficultyDirector.Instance.ScaleFire(rate);
        }

        float value = delay * rate;
        return Mathf.Max(0.5f, value);
    }

    public float BossBulletSpeed()
    {
        float rate = GetDifficulty().bulletRate;
        if (DifficultyDirector.Instance != null)
        {
            rate = DifficultyDirector.Instance.ScaleBossBullet(rate);
        }

        return rate;
    }

    public float BossFireGap(float interval)
    {
        if (interval <= 0f)
            return interval;

        float rate = GetDifficulty().fireRate;
        if (DifficultyDirector.Instance != null)
        {
            rate = DifficultyDirector.Instance.ScaleBossFire(rate);
        }

        return Mathf.Max(0.16f, interval * rate);
    }

    public FireInfo RandomFire()
    {
        LevelInfo level = GetLevel();
        return configService.RandomFire(level != null ? level.id : 1);
    }

    public BulletInfo GetBullet(int id)
    {
        if (bulletData == null || bulletData.bullets == null)
            return null;
        return configService.Bullet(id);
    }

    public BulletInfo RandomBullet(FireInfo fire)
    {
        return configService.RandomBullet(fire);
    }

    public int ValidateResourceReferences()
    {
        int missing = 0;
        if (roleData != null && roleData.roles != null)
        {
            foreach (RoleInfo role in roleData.roles)
            {
                if (role == null || string.IsNullOrWhiteSpace(role.asset))
                    continue;
                if (Resources.Load<GameObject>(role.asset) == null)
                {
                    missing++;
                    GameLog.Error(GameLogCategory.Data, "RESOURCE_MISSING", "role: " + role.asset);
                }
            }
        }

        if (bulletData != null && bulletData.bullets != null)
        {
            foreach (BulletInfo bullet in bulletData.bullets)
            {
                if (bullet == null)
                    continue;
                if (!string.IsNullOrWhiteSpace(bullet.asset) && Resources.Load<GameObject>(bullet.asset) == null)
                {
                    missing++;
                    GameLog.Error(GameLogCategory.Data, "RESOURCE_MISSING", "bullet: " + bullet.asset);
                }
                if (!string.IsNullOrWhiteSpace(bullet.deathFx) && Resources.Load<GameObject>(bullet.deathFx) == null)
                {
                    missing++;
                    GameLog.Error(GameLogCategory.Data, "RESOURCE_MISSING", "bullet_fx: " + bullet.deathFx);
                }
            }
        }

        if (missing == 0)
            GameLog.Debug(GameLogCategory.Data, "all configured role and bullet resources are available");
        return missing;
    }

    public IEnumerable<string> ResourcesForCurrentRun()
    {
        HashSet<string> paths = new HashSet<string>(
            configService.ResourcePathsForRun(GetLevel().id, heroIndex, roleData));
        if (IsEndless && bulletData != null && bulletData.bullets != null)
        {
            for (int i = 0; i < bulletData.bullets.Count; i++)
            {
                BulletInfo bullet = bulletData.bullets[i];
                if (bullet == null)
                    continue;
                if (!string.IsNullOrWhiteSpace(bullet.asset))
                    paths.Add(bullet.asset);
                if (!string.IsNullOrWhiteSpace(bullet.deathFx))
                    paths.Add(bullet.deathFx);
            }
        }
        return paths;
    }

}
