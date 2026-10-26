using System.Collections.Generic;
using UnityEngine;

public sealed class GameConfigService
{
    private readonly Dictionary<int, BulletInfo> bullets = new Dictionary<int, BulletInfo>();
    private readonly Dictionary<int, FireInfo> fires = new Dictionary<int, FireInfo>();
    private readonly Dictionary<int, DifficultyInfo> difficulties = new Dictionary<int, DifficultyInfo>();
    private readonly Dictionary<int, LevelInfo> levels = new Dictionary<int, LevelInfo>();
    private readonly Dictionary<int, List<FireInfo>> levelFires = new Dictionary<int, List<FireInfo>>();
    private readonly Dictionary<FireInfo, Vector2Int> fireBulletRanges = new Dictionary<FireInfo, Vector2Int>();
    private readonly List<FireInfo> allFires = new List<FireInfo>();
    private BulletInfo firstBullet;
    private DifficultyInfo firstDifficulty;
    private LevelInfo firstLevel;

    public void Rebuild(BulletData bulletData, FireData fireData, DifficultyData difficultyData, LevelData levelData)
    {
        bullets.Clear();
        fires.Clear();
        difficulties.Clear();
        levels.Clear();
        levelFires.Clear();
        fireBulletRanges.Clear();
        allFires.Clear();
        firstBullet = null;
        firstDifficulty = null;
        firstLevel = null;

        if (bulletData != null && bulletData.bullets != null)
        {
            for (int i = 0; i < bulletData.bullets.Count; i++)
            {
                BulletInfo item = bulletData.bullets[i];
                if (item == null)
                    continue;
                if (firstBullet == null)
                    firstBullet = item;
                bullets[item.id] = item;
            }
        }

        if (fireData != null && fireData.fires != null)
        {
            for (int i = 0; i < fireData.fires.Count; i++)
            {
                FireInfo item = fireData.fires[i];
                if (item == null)
                    continue;
                fires[item.id] = item;
                allFires.Add(item);
                int min;
                int max;
                if (TryParseRange(item.ids, out min, out max))
                    fireBulletRanges[item] = new Vector2Int(min, max);
            }
        }

        if (difficultyData != null && difficultyData.difficulties != null)
        {
            for (int i = 0; i < difficultyData.difficulties.Count; i++)
            {
                DifficultyInfo item = difficultyData.difficulties[i];
                if (item == null)
                    continue;
                if (firstDifficulty == null)
                    firstDifficulty = item;
                difficulties[item.id] = item;
            }
        }

        if (levelData != null && levelData.levels != null)
        {
            for (int i = 0; i < levelData.levels.Count; i++)
            {
                LevelInfo item = levelData.levels[i];
                if (item == null)
                    continue;
                if (firstLevel == null)
                    firstLevel = item;
                levels[item.id] = item;
                levelFires[item.id] = BuildFireCandidates(item.fireIds);
            }
        }
    }

    public BulletInfo Bullet(int id)
    {
        BulletInfo item;
        return bullets.TryGetValue(id, out item) ? item : firstBullet;
    }

    public DifficultyInfo Difficulty(int id)
    {
        DifficultyInfo item;
        return difficulties.TryGetValue(id, out item) ? item : firstDifficulty;
    }

    public LevelInfo Level(int id)
    {
        LevelInfo item;
        return levels.TryGetValue(id, out item) ? item : firstLevel;
    }

    public FireInfo RandomFire(int levelId)
    {
        List<FireInfo> candidates;
        if (!levelFires.TryGetValue(levelId, out candidates) || candidates.Count == 0)
            candidates = allFires;
        return candidates.Count == 0 ? null : candidates[Random.Range(0, candidates.Count)];
    }

    public BulletInfo RandomBullet(FireInfo fire)
    {
        Vector2Int range;
        if (fire == null || !fireBulletRanges.TryGetValue(fire, out range))
            return firstBullet;
        return Bullet(Random.Range(range.x, range.y + 1));
    }

    public IEnumerable<string> ResourcePathsForRun(int levelId, int heroIndex, RoleData roleData)
    {
        HashSet<string> paths = new HashSet<string>();
        if (roleData != null && roleData.roles != null && roleData.roles.Count > 0)
        {
            RoleInfo role = roleData.roles[Mathf.Clamp(heroIndex, 0, roleData.roles.Count - 1)];
            AddPath(paths, role != null ? role.asset : null);
        }

        List<FireInfo> candidates;
        if (!levelFires.TryGetValue(levelId, out candidates) || candidates.Count == 0)
            candidates = allFires;
        for (int i = 0; i < candidates.Count; i++)
        {
            int min;
            int max;
            if (!TryParseRange(candidates[i].ids, out min, out max))
                continue;
            for (int id = min; id <= max; id++)
            {
                BulletInfo bullet = Bullet(id);
                if (bullet == null)
                    continue;
                AddPath(paths, bullet.asset);
                AddPath(paths, bullet.deathFx);
            }
        }

        return paths;
    }

    private List<FireInfo> BuildFireCandidates(string ids)
    {
        List<FireInfo> result = new List<FireInfo>();
        if (string.IsNullOrWhiteSpace(ids))
        {
            result.AddRange(allFires);
            return result;
        }

        string[] values = ids.Split(',');
        for (int i = 0; i < values.Length; i++)
        {
            int id;
            FireInfo item;
            if (int.TryParse(values[i], out id) && fires.TryGetValue(id, out item))
                result.Add(item);
        }
        return result;
    }

    private static bool TryParseRange(string value, out int min, out int max)
    {
        min = 0;
        max = -1;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        string[] parts = value.Split(',');
        if (parts.Length == 0 || !int.TryParse(parts[0], out min))
            return false;
        max = min;
        if (parts.Length > 1 && !int.TryParse(parts[1], out max))
            max = min;
        if (max < min)
        {
            int swap = min;
            min = max;
            max = swap;
        }
        return true;
    }

    private static void AddPath(HashSet<string> paths, string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
            paths.Add(path);
    }
}

public sealed class GameSelectionService
{
    public int HeroIndex;
    public int DifficultyId = 2;
    public int LevelId = 1;
    public bool IsEndless { get; private set; }
    public bool IsTraining { get; private set; }

    public void SelectLevel(int levelId)
    {
        IsEndless = false;
        IsTraining = false;
        LevelId = Mathf.Clamp(levelId, 1, GameDataMgr.MaxLevelId);
    }

    public void SelectEndless()
    {
        IsEndless = true;
        IsTraining = false;
    }

    public void SelectTraining()
    {
        IsEndless = false;
        IsTraining = true;
        LevelId = 1;
    }
}
