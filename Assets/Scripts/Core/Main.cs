using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Main : MonoBehaviour
{
    void Start()
    {
        Time.timeScale = 1;
        ObjectPoolManager.Ensure(gameObject);
        SoundManager.Ensure();
        GameFlowManager.Ensure(gameObject);
        DifficultyDirector.Ensure(gameObject);
        ScoreManager.Ensure(gameObject);
        BossController.Ensure(gameObject);

        RoleInfo info = GameDataMgr.Instance.GetHero();
        GameObject prefab = Resources.Load<GameObject>(info.asset);
        if (prefab == null)
        {
            Debug.LogError("Player resource not found: " + info.asset);
            return;
        }

        GameObject obj = Instantiate(prefab);
        PlayerObject playerObj = obj.GetComponent<PlayerObject>();
        if (playerObj == null)
        {
            playerObj = obj.AddComponent<PlayerObject>();
        }

        int hp = Mathf.Max(1, info.hp + GameDataMgr.Instance.HpBonus());
        playerObj.speed = info.speed * 20;
        playerObj.maxHp = hp;
        playerObj.nowHp = hp;
        playerObj.turnSpeed = 20;

        GamePanel.Instance.ChangeHP(hp);
    }
}
