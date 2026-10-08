using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public class GameDataMgr
{
    private static GameDataMgr instance = new GameDataMgr();

    public static GameDataMgr Instance => instance;

    public MusicData musicData;
    public RankData rankData;
    public RoleData roleData;
    public BulletData bulletData;
    public FireData fireData;
    public DifficultyData difficultyData;
    public LevelData levelData;

    public int heroIndex = 0;
    public int difficultyIndex = 1;

    private GameDataMgr()
    {
        musicData = XmlDataMgr.Instance.Load(typeof(MusicData), "MusicData") as MusicData;
        rankData = XmlDataMgr.Instance.Load(typeof(RankData), "RankData") as RankData;
        roleData = XmlDataMgr.Instance.Load(typeof(RoleData), "RoleData") as RoleData;
        bulletData = XmlDataMgr.Instance.Load(typeof(BulletData), "BulletData") as BulletData;
        fireData = XmlDataMgr.Instance.Load(typeof(FireData), "FireData") as FireData;
        difficultyData = XmlDataMgr.Instance.Load(typeof(DifficultyData), "DifficultyData") as DifficultyData;
        levelData = XmlDataMgr.Instance.Load(typeof(LevelData), "LevelData") as LevelData;

        InitDefaults();
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

        if (difficultyData.difficulties.Count == 0)
        {
            difficultyData.difficulties.Add(NewDifficulty(1, "简单", 2, 0.75f, 1.35f, 1.25f, 4f, 8f));
            difficultyData.difficulties.Add(NewDifficulty(2, "普通", 0, 1f, 1f, 1f, 3f, 10f));
            difficultyData.difficulties.Add(NewDifficulty(3, "困难", -1, 1.25f, 0.75f, 0.8f, 2f, 13f));
        }

        if (levelData.levels.Count == 0)
        {
            levelData.levels.Add(NewLevel(1, "第一关-训练航线", 0f, 1f, 1f, 1f, "1,2"));
            levelData.levels.Add(NewLevel(2, "第二关-密集弹幕", 30f, 1.15f, 0.85f, 0.9f, "1,2,3"));
            levelData.levels.Add(NewLevel(3, "第三关-追踪压制", 60f, 1.35f, 0.7f, 0.75f, "2,3,4"));
            levelData.levels.Add(NewLevel(4, "第四关-极限挑战", 100f, 1.6f, 0.55f, 0.6f, "1,2,3,4"));
        }

        SortRanks();
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
        XmlDataMgr.Instance.Save(musicData, "MusicData");
    }

    public void SetMusic(bool isOpen)
    {
        musicData.musicOn = isOpen;
        if (BKMusic.Instance != null)
        {
            BKMusic.Instance.SetOpen(isOpen);
        }
    }

    public void SetSound(bool isOpen)
    {
        musicData.soundOn = isOpen;
    }

    public void SetMusicVolume(float value)
    {
        musicData.musicVolume = value;
        if (BKMusic.Instance != null)
        {
            BKMusic.Instance.SetMusicVolume(value);
        }
    }

    public void SetSoundVolume(float value)
    {
        musicData.soundVolume = value;
    }

    public void AddRank(string name, int time)
    {
        RankInfo rankInfo = new RankInfo();
        rankInfo.name = NormalizePilotName(name);
        rankInfo.runTime = Mathf.Max(0, time);
        rankInfo.time = rankInfo.runTime;
        rankInfo.difficulty = DifficultyName();
        rankInfo.date = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.FinishScore();
            rankInfo.score = ScoreManager.Instance.TotalScore;
            rankInfo.runTime = Mathf.FloorToInt(ScoreManager.Instance.RunTime);
            rankInfo.time = rankInfo.runTime;
            rankInfo.bosses = ScoreManager.Instance.BossRounds;
            rankInfo.clears = ScoreManager.Instance.ClearCount;
        }

        rankInfo.Normalize();
        rankData.ranks.Add(rankInfo);

        SortRanks();

        XmlDataMgr.Instance.Save(rankData, "RankData");
    }

    private static string NormalizePilotName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Player";

        return name.Trim();
    }

    public void SortRanks()
    {
        if (rankData == null)
        {
            rankData = new RankData();
        }

        rankData.Sort(20);
    }

    public RoleInfo GetHero()
    {
        if (roleData.roles.Count == 0)
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
        difficultyIndex = Mathf.Clamp(difficultyId, 1, difficultyData.difficulties.Count);
    }

    public DifficultyInfo GetDifficulty()
    {
        for (int i = 0; i < difficultyData.difficulties.Count; i++)
        {
            if (difficultyData.difficulties[i].id == difficultyIndex)
                return difficultyData.difficulties[i];
        }

        return difficultyData.difficulties[0];
    }

    public LevelInfo GetLevel()
    {
        float nowTime = 0f;
        if (GamePanel.Instance != null)
        {
            nowTime = GamePanel.Instance.nowTime;
        }

        LevelInfo result = levelData.levels[0];
        for (int i = 0; i < levelData.levels.Count; i++)
        {
            if (nowTime >= levelData.levels[i].beginTime)
            {
                result = levelData.levels[i];
            }
        }

        return result;
    }

    public string DifficultyName()
    {
        return GetDifficulty().name;
    }

    public string LevelName()
    {
        return GetLevel().name;
    }

    public int HpBonus()
    {
        return GetDifficulty().hpBonus;
    }

    public float ShieldTime()
    {
        return GetDifficulty().shieldTime;
    }

    public float SkillCd()
    {
        return GetDifficulty().skillCd;
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
        List<FireInfo> source = fireData.fires;
        if (source.Count == 0)
            return null;

        List<int> ids = ParseIds(GetLevel().fireIds);
        List<FireInfo> candidates = new List<FireInfo>();

        for (int i = 0; i < source.Count; i++)
        {
            if (ids.Count == 0 || ids.Contains(source[i].id))
            {
                candidates.Add(source[i]);
            }
        }

        if (candidates.Count == 0)
        {
            candidates = source;
        }

        return candidates[Random.Range(0, candidates.Count)];
    }

    public BulletInfo GetBullet(int id)
    {
        for (int i = 0; i < bulletData.bullets.Count; i++)
        {
            if (bulletData.bullets[i].id == id)
                return bulletData.bullets[i];
        }

        return bulletData.bullets.Count > 0 ? bulletData.bullets[0] : null;
    }

    private List<int> ParseIds(string ids)
    {
        List<int> result = new List<int>();
        if (string.IsNullOrEmpty(ids))
            return result;

        string[] strs = ids.Split(',');
        for (int i = 0; i < strs.Length; i++)
        {
            int id;
            if (int.TryParse(strs[i], out id))
            {
                result.Add(id);
            }
        }

        return result;
    }
}
