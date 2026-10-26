using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TrainingModeController : MonoBehaviour
{
    public static TrainingModeController Instance { get; private set; }

    private int step;
    private Vector3 startPosition;
    private bool subscribed;
    private bool coreSpawned;
    private BulletObject trainingCore;

    public static TrainingModeController Ensure(GameObject host)
    {
        if (Instance != null)
            return Instance;
        return host.AddComponent<TrainingModeController>();
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        BindPlayer();
        ShowStep();
    }

    private void OnDestroy()
    {
        UnbindPlayer();
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        BindPlayer();
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            StartCoroutine(ReturnToMenu());
            return;
        }

        if (PlayerObject.Instance == null)
            return;
        if (step == 0 && (PlayerObject.Instance.transform.position - startPosition).sqrMagnitude > 16f)
            Advance();
        else if (step == 3 && coreSpawned && (trainingCore == null || trainingCore.IsDead))
            Advance();
        else if (step >= 4 && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.JoystickButton7)))
            StartCoroutine(ReturnToMenu());
    }

    private void BindPlayer()
    {
        if (subscribed || PlayerObject.Instance == null)
            return;
        subscribed = true;
        startPosition = PlayerObject.Instance.transform.position;
        PlayerObject.Instance.ShieldActivated += OnShield;
        PlayerObject.Instance.SkillActivated += OnSkill;
    }

    private void UnbindPlayer()
    {
        if (!subscribed || PlayerObject.Instance == null)
            return;
        PlayerObject.Instance.ShieldActivated -= OnShield;
        PlayerObject.Instance.SkillActivated -= OnSkill;
        subscribed = false;
    }

    private void OnShield()
    {
        if (step == 1)
            Advance();
    }

    private void OnSkill()
    {
        if (step == 2)
            Advance();
    }

    private void Advance()
    {
        step++;
        ShowStep();
    }

    private void ShowStep()
    {
        if (GamePanel.Instance == null)
            return;
        if (step == 3 && !coreSpawned)
            SpawnTrainingCore();
        string text;
        switch (step)
        {
            case 0: text = "训练 1/4 · 使用 WASD / 左摇杆移动  ·  Tab 可跳过"; break;
            case 1: text = "训练 2/4 · 按 E / 手柄 A 开启护盾"; break;
            case 2: text = "训练 3/4 · 按 Q / 手柄 B 使用角色技能"; break;
            case 3: text = "训练 4/4 · 用鼠标点击屏幕上方的青色发光核心"; break;
            default: text = "[完成] 基础训练完成 · 按 Enter 返回主菜单"; break;
        }
        GamePanel.Instance.ShowCombatFeedback(text, new Color(0.2f, 0.9f, 1f), 999f);
    }

    private void SpawnTrainingCore()
    {
        coreSpawned = true;
        BulletInfo source = GameDataMgr.Instance.GetBullet(2);
        if (source == null || string.IsNullOrEmpty(source.asset) || Camera.main == null)
        {
            coreSpawned = false;
            return;
        }

        float distance = Mathf.Abs(Camera.main.transform.position.y);
        Vector3 position = Camera.main.ScreenToWorldPoint(new Vector3(Screen.width * 0.5f, Screen.height * 0.7f, distance));
        BulletInfo info = new BulletInfo
        {
            id = source.id,
            type = 1,
            moveRate = 0f,
            sideSpeed = 0f,
            turnSpeed = 0f,
            asset = source.asset,
            deathFx = source.deathFx,
            life = 999f,
        };
        GameObject core = ObjectPoolManager.Ensure().SpawnAsset(info.asset, position, Quaternion.identity);
        if (core == null)
        {
            coreSpawned = false;
            return;
        }
        trainingCore = core.GetComponent<BulletObject>();
        if (trainingCore == null)
        {
            trainingCore = core.AddComponent<BulletObject>();
            PooledObject pooled = core.GetComponent<PooledObject>();
            if (pooled != null)
                pooled.RefreshCallbacks();
        }
        trainingCore.Init(info, BulletSource.Normal, 0f, 1f);
        trainingCore.ConfigureInteraction(BulletInteraction.ClusterCore);
    }

    private IEnumerator ReturnToMenu()
    {
        yield return null;
        Time.timeScale = 1f;
        SceneManager.LoadScene("BeginScene");
    }
}
