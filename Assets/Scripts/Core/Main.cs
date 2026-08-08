using UnityEngine;

public class Main : MonoBehaviour
{
    void Start()
    {
        Time.timeScale = 1;
        ObjectPoolManager.EnsureInstance(gameObject);
        SoundManager.EnsureInstance();
        GameFlowManager.EnsureInstance(gameObject);
        DifficultyDirector.EnsureInstance(gameObject);
        ScoreManager.EnsureInstance(gameObject);
        BossController.EnsureInstance(gameObject);

        RoleInfo info = GameDataMgr.Instance.GetNowSelHeroInfo();
        GameObject prefab = Resources.Load<GameObject>(info.resName);
        if (prefab == null)
        {
            Debug.LogError("Player resource not found: " + info.resName);
            return;
        }

        GameObject obj = Instantiate(prefab);
        PlayerObject playerObj = obj.GetComponent<PlayerObject>();
        if (playerObj == null)
        {
            playerObj = obj.AddComponent<PlayerObject>();
        }

        int hp = Mathf.Max(1, info.hp + GameDataMgr.Instance.GetPlayerHpAdd());
        playerObj.speed = info.speed * 20;
        playerObj.maxHp = hp;
        playerObj.nowHp = hp;
        playerObj.roundSpeed = 20;

        GamePanel.Instance.ChangeHP(hp);
    }
}
