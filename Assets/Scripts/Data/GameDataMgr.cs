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

    public int nowSelHeroIndex = 0;
    public int nowDifficultyIndex = 1;

    private GameDataMgr()
    {
        musicData = XmlDataMgr.Instance.LoadData(typeof(MusicData), "MusicData") as MusicData;
        rankData = XmlDataMgr.Instance.LoadData(typeof(RankData), "RankData") as RankData;
        roleData = XmlDataMgr.Instance.LoadData(typeof(RoleData), "RoleData") as RoleData;
        bulletData = XmlDataMgr.Instance.LoadData(typeof(BulletData), "BulletData") as BulletData;
        fireData = XmlDataMgr.Instance.LoadData(typeof(FireData), "FireData") as FireData;
        difficultyData = XmlDataMgr.Instance.LoadData(typeof(DifficultyData), "DifficultyData") as DifficultyData;
        levelData = XmlDataMgr.Instance.LoadData(typeof(LevelData), "LevelData") as LevelData;

        InitDefaultData();
    }

    private void InitDefaultData()
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

        if (difficultyData.difficultyInfos.Count == 0)
        {
            difficultyData.difficultyInfos.Add(CreateDifficulty(1, "简单", 2, 0.75f, 1.35f, 1.25f, 4f, 8f));
            difficultyData.difficultyInfos.Add(CreateDifficulty(2, "普通", 0, 1f, 1f, 1f, 3f, 10f));
            difficultyData.difficultyInfos.Add(CreateDifficulty(3, "困难", -1, 1.25f, 0.75f, 0.8f, 2f, 13f));
        }

        if (levelData.levelInfos.Count == 0)
        {
            levelData.levelInfos.Add(CreateLevel(1, "第一关-训练航线", 0f, 1f, 1f, 1f, "1,2"));
            levelData.levelInfos.Add(CreateLevel(2, "第二关-密集弹幕", 30f, 1.15f, 0.85f, 0.9f, "1,2,3"));
            levelData.levelInfos.Add(CreateLevel(3, "第三关-追踪压制", 60f, 1.35f, 0.7f, 0.75f, "2,3,4"));
            levelData.levelInfos.Add(CreateLevel(4, "第四关-极限挑战", 100f, 1.6f, 0.55f, 0.6f, "1,2,3,4"));
        }

        NormalizeRankData();
    }

    private DifficultyInfo CreateDifficulty(int id, string name, int hpAdd, float bulletSpeedMul, float fireCdMul, float fireDelayMul, float shieldTime, float skillCD)
    {
        DifficultyInfo info = new DifficultyInfo();
        info.id = id;
        info.name = name;
        info.hpAdd = hpAdd;
        info.bulletSpeedMul = bulletSpeedMul;
        info.fireCdMul = fireCdMul;
        info.fireDelayMul = fireDelayMul;
        info.shieldTime = shieldTime;
        info.skillCD = skillCD;
        return info;
    }

    private LevelInfo CreateLevel(int id, string name, float beginTime, float bulletSpeedMul, float fireCdMul, float fireDelayMul, string fireIds)
    {
        LevelInfo info = new LevelInfo();
        info.id = id;
        info.name = name;
        info.beginTime = beginTime;
        info.bulletSpeedMul = bulletSpeedMul;
        info.fireCdMul = fireCdMul;
        info.fireDelayMul = fireDelayMul;
        info.fireIds = fireIds;
        return info;
    }

    public void SaveMusic()
    {
        XmlDataMgr.Instance.SaveData(musicData, "MusicData");
    }

    public void SetMusicIsOpen(bool isOpen)
    {
        musicData.musicIsopen = isOpen;
        if (BKMusic.Instance != null)
        {
            BKMusic.Instance.SetBKMusicIsOpen(isOpen);
        }
    }

    public void SetSoundIsOpen(bool isOpen)
    {
        musicData.SoundIsopen = isOpen;
    }

    public void SetMusicValue(float value)
    {
        musicData.musicvalue = value;
        if (BKMusic.Instance != null)
        {
            BKMusic.Instance.SetMusicValue(value);
        }
    }

    public void SetSoundValue(float value)
    {
        musicData.soundValue = value;
    }

    public void AddRankData(string name, int time)
    {
        RankInfo rankInfo = new RankInfo();
        rankInfo.name = string.IsNullOrEmpty(name) ? "Player" : name;
        rankInfo.survivalTime = Mathf.Max(0, time);
        rankInfo.time = rankInfo.survivalTime;
        rankInfo.difficulty = GetDifficultyName();
        rankInfo.recordTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.FinalizeScore();
            rankInfo.finalScore = ScoreManager.Instance.FinalScore;
            rankInfo.survivalTime = Mathf.FloorToInt(ScoreManager.Instance.SurvivalTime);
            rankInfo.time = rankInfo.survivalTime;
            rankInfo.bossRounds = ScoreManager.Instance.CompletedBossRounds;
            rankInfo.clearedBullets = ScoreManager.Instance.ManualClearedBullets;
        }

        rankInfo.Normalize();
        rankData.rankInfos.Add(rankInfo);

        NormalizeRankData();

        XmlDataMgr.Instance.SaveData(rankData, "RankData");
    }

    public void NormalizeRankData()
    {
        if (rankData == null)
        {
            rankData = new RankData();
        }

        rankData.NormalizeAndSort(20);
    }

    public RoleInfo GetNowSelHeroInfo()
    {
        if (roleData.roleDatas.Count == 0)
        {
            RoleInfo info = new RoleInfo();
            info.hp = 4;
            info.speed = 6;
            info.volume = 5;
            info.resName = "Airplane/Airplane1";
            info.scale = 13f;
            info.shieldStyle = 1;
            return info;
        }

        nowSelHeroIndex = Mathf.Clamp(nowSelHeroIndex, 0, roleData.roleDatas.Count - 1);
        return roleData.roleDatas[nowSelHeroIndex];
    }

    public void SetDifficulty(int difficultyId)
    {
        nowDifficultyIndex = Mathf.Clamp(difficultyId, 1, difficultyData.difficultyInfos.Count);
    }

    public DifficultyInfo GetNowDifficultyInfo()
    {
        for (int i = 0; i < difficultyData.difficultyInfos.Count; i++)
        {
            if (difficultyData.difficultyInfos[i].id == nowDifficultyIndex)
                return difficultyData.difficultyInfos[i];
        }

        return difficultyData.difficultyInfos[0];
    }

    public LevelInfo GetCurrentLevelInfo()
    {
        float nowTime = 0f;
        if (GamePanel.Instance != null)
        {
            nowTime = GamePanel.Instance.nowTime;
        }

        LevelInfo result = levelData.levelInfos[0];
        for (int i = 0; i < levelData.levelInfos.Count; i++)
        {
            if (nowTime >= levelData.levelInfos[i].beginTime)
            {
                result = levelData.levelInfos[i];
            }
        }

        return result;
    }

    public string GetDifficultyName()
    {
        return GetNowDifficultyInfo().name;
    }

    public string GetLevelName()
    {
        return GetCurrentLevelInfo().name;
    }

    public int GetPlayerHpAdd()
    {
        return GetNowDifficultyInfo().hpAdd;
    }

    public float GetShieldTime()
    {
        return GetNowDifficultyInfo().shieldTime;
    }

    public float GetSkillCD()
    {
        return GetNowDifficultyInfo().skillCD;
    }

    public float GetBulletSpeedMul()
    {
        float multiplier = GetNowDifficultyInfo().bulletSpeedMul * GetCurrentLevelInfo().bulletSpeedMul;
        if (DifficultyDirector.Instance != null)
        {
            multiplier = DifficultyDirector.Instance.ApplyNormalBulletSpeed(multiplier);
        }

        return multiplier;
    }

    public float GetFireCD(float cd)
    {
        if (cd <= 0f)
            return cd;

        float multiplier = GetNowDifficultyInfo().fireCdMul * GetCurrentLevelInfo().fireCdMul;
        if (DifficultyDirector.Instance != null)
        {
            multiplier = DifficultyDirector.Instance.ApplyNormalFireInterval(multiplier);
        }

        float value = cd * multiplier;
        return Mathf.Max(0.08f, value);
    }

    public float GetFireDelay(float delay)
    {
        if (delay <= 0f)
            return delay;

        float multiplier = GetNowDifficultyInfo().fireDelayMul * GetCurrentLevelInfo().fireDelayMul;
        if (DifficultyDirector.Instance != null)
        {
            multiplier = DifficultyDirector.Instance.ApplyNormalFireInterval(multiplier);
        }

        float value = delay * multiplier;
        return Mathf.Max(0.5f, value);
    }

    public float GetBossBulletSpeedMul()
    {
        float multiplier = GetNowDifficultyInfo().bulletSpeedMul;
        if (DifficultyDirector.Instance != null)
        {
            multiplier = DifficultyDirector.Instance.ApplyBossBulletSpeed(multiplier);
        }

        return multiplier;
    }

    public float GetBossFireInterval(float interval)
    {
        if (interval <= 0f)
            return interval;

        float multiplier = GetNowDifficultyInfo().fireCdMul;
        if (DifficultyDirector.Instance != null)
        {
            multiplier = DifficultyDirector.Instance.ApplyBossFireInterval(multiplier);
        }

        return Mathf.Max(0.16f, interval * multiplier);
    }

    public FireInfo GetRandomFireInfo()
    {
        List<FireInfo> source = fireData.fireInfoList;
        if (source.Count == 0)
            return null;

        List<int> ids = ParseIds(GetCurrentLevelInfo().fireIds);
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

    public BulletInfo GetBulletInfoById(int id)
    {
        for (int i = 0; i < bulletData.bulletInfoList.Count; i++)
        {
            if (bulletData.bulletInfoList[i].id == id)
                return bulletData.bulletInfoList[i];
        }

        return bulletData.bulletInfoList.Count > 0 ? bulletData.bulletInfoList[0] : null;
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
