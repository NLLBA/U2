using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class PlayerObject : MonoBehaviour
{
    private static PlayerObject instance;
    private static float bulletRate = 1f;
    private static readonly string[] SkillNames =
    {
        "爆破清屏", "紧急维修", "电磁干扰", "能量脉冲", "疾航推进",
    };
    private static readonly string[] SkillIcons = { "爆", "修", "扰", "脉", "速" };
    private static readonly float[] SkillCdBonus = { 2f, 3f, 1f, 4f, 0f };
    private static readonly int[] AffinityLevels = { 2, 4, 3, 5, 1 };

    public static PlayerObject Instance => instance;
    public static float BulletRate => bulletRate;
    public const float HitProtectionDuration = 0.7f;
    public int HitsTaken { get; private set; }
    public bool ShieldActive => isShield;
    public event Action Damaged;
    public event Action ShieldActivated;
    public event Action SkillActivated;

    public static bool HasStageAffinity(int levelId)
    {
        if (GameDataMgr.Instance.IsEndless || GameDataMgr.Instance.selectedLevelId != levelId)
            return false;
        NetworkBattleSession battle = NetworkBattleSession.Instance;
        if (battle != null && battle.IsBattle)
            return false;
        return AffinityLevelForHero(GameDataMgr.Instance.heroIndex) == levelId;
    }

    public static int AffinityLevelForHero(int heroIndex)
    {
        int index = Mathf.Clamp(heroIndex, 0, AffinityLevels.Length - 1);
        return AffinityLevels[index];
    }

    public static string StageAffinityText(int heroIndex)
    {
        switch (Mathf.Clamp(heroIndex, 0, 4))
        {
            case 0: return "第二关协同：弹簇连锁范围 +25%";
            case 1: return "第四关协同：取能/传输 +25%，受击损失减半";
            case 2: return "第三关协同：追踪锁定时间 -20%";
            case 3: return "第五关协同：反弹核心点击范围 +30%";
            default: return "第一关协同：基础航速 +12%";
        }
    }

    public static string ActiveStageAffinityText()
    {
        if (!HasStageAffinity(GameDataMgr.Instance.selectedLevelId))
            return string.Empty;
        switch (GameDataMgr.Instance.heroIndex)
        {
            case 0: return "爆破协同";
            case 1: return "维修协同";
            case 2: return "干扰协同";
            case 3: return "脉冲协同";
            default: return "疾航协同";
        }
    }

    public static string RecommendedLevelText(int heroIndex)
    {
        return "推荐关卡：第" + AffinityLevelForHero(heroIndex) + "关";
    }

    public static string OperationDifficultyText(int heroIndex)
    {
        switch (Mathf.Clamp(heroIndex, 0, 4))
        {
            case 1: return "操作难度：简单";
            case 4: return "操作难度：简单";
            case 0: return "操作难度：普通";
            case 2: return "操作难度：普通";
            default: return "操作难度：进阶";
        }
    }

    public static float HeroSkillCooldown(int heroIndex, float baseCooldown)
    {
        int index = Mathf.Clamp(heroIndex, 0, SkillCdBonus.Length - 1);
        return Mathf.Max(0f, baseCooldown) + SkillCdBonus[index];
    }

    public int nowHp;
    public int maxHp;
    public int speed;
    [FormerlySerializedAs("roundSpeed")]
    public int turnSpeed;
    public bool isDead;

    private bool isShield;
    private float shieldEnd;
    private float shieldReady;
    private float skillReady;
    private float slowEnd;
    private float pulseEnd;
    private float nextPulse;
    private float boostEnd;
    private float damageReady;
    private PlayerShieldView shieldView;
    private Camera sceneCamera;

    private void Awake()
    {
        instance = this;
        bulletRate = 1f;
        damageReady = 0f;
        HitsTaken = 0;
        sceneCamera = Camera.main;
        GetShield();
    }

    private void OnDestroy()
    {
        bulletRate = 1f;
        if (instance == this)
        {
            instance = null;
        }
    }

    public void Dead()
    {
        if (isDead)
            return;

        isDead = true;
        if (GameDataMgr.Instance.IsEndless && EndlessRunController.Instance != null)
            EndlessRunController.Instance.RegisterFailure("弹幕命中导致生命耗尽");
        HideShield();

        CampaignMissionController mission = CampaignMissionController.Instance;
        if (mission != null && mission.IsActiveCampaign)
        {
            mission.FailMission();
            return;
        }

        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.GameOver();
        }
        else
        {
            Time.timeScale = 0f;
        }

        NetworkBattleSession battle = NetworkBattleSession.Instance;
        if (battle != null && battle.IsPlaying)
        {
            battle.ReportLocalDeath();
            return;
        }

        GameOverPanel.Instance.Showme();
        ScorePanelPresenter.ShowFinalScore(GameOverPanel.Instance);
    }

    public void Wound()
    {
        if (isDead || isShield || Time.time < damageReady)
            return;

        damageReady = Time.time + HitProtectionDuration;
        HitsTaken++;
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.RegisterPlayerHit();
        nowHp--;
        SoundManager.Play(SoundCue.PlayerHit);
        GamePanel.Instance.ChangeHP(nowHp);
        Damaged?.Invoke();

        if (nowHp <= 0)
            Dead();
    }

    private void Update()
    {
        if (isDead)
            return;

        UpdateShield();
        UpdateSkill();
        UpdateMove();
        CheckClick();
        UpdateSkills();
        GetShield().Tick();
    }

    private void UpdateMove()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Quaternion targetRot = horizontal == 0f
            ? Quaternion.identity
            : Quaternion.AngleAxis(horizontal < 0f ? 20f : -20f, Vector3.forward);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            turnSpeed * Time.deltaTime);

        float moveRate = Time.time < boostEnd ? speed * 1.55f : speed;
        if (HasStageAffinity(1))
            moveRate *= 1.12f;
        Vector3 lastPos = transform.position;
        transform.Translate(Vector3.forward * vertical * moveRate * Time.deltaTime);
        transform.Translate(
            Vector3.right * horizontal * moveRate * Time.deltaTime,
            Space.World);

        if (sceneCamera == null)
            sceneCamera = Camera.main;
        if (sceneCamera == null)
            return;

        Vector3 screenPos = sceneCamera.WorldToScreenPoint(transform.position);
        if (screenPos.x <= 0f || screenPos.x >= Screen.width)
        {
            transform.position = new Vector3(
                lastPos.x,
                transform.position.y,
                transform.position.z);
        }

        if (screenPos.y <= 0f || screenPos.y >= Screen.height)
        {
            transform.position = new Vector3(
                transform.position.x,
                transform.position.y,
                lastPos.z);
        }
    }

    private void CheckClick()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        if (sceneCamera == null)
            sceneCamera = Camera.main;
        if (sceneCamera == null)
            return;

        Ray ray = sceneCamera.ScreenPointToRay(Input.mousePosition);
        int bulletLayer = LayerMask.NameToLayer("Bullent");
        RaycastHit hitInfo;
        if (Physics.Raycast(ray, out hitInfo, 1000f, 1 << bulletLayer))
        {
            BulletObject bullet = hitInfo.transform.GetComponentInParent<BulletObject>();
            if (bullet != null)
            {
                bullet.PlayerClear();
                return;
            }
        }

        // Mission cores get a forgiving screen-space click area without enlarging
        // their physical collision volume against the player.
        BulletObject.TryPlayerClearNearScreen(sceneCamera, Input.mousePosition);
    }

    private void UpdateSkills()
    {
        float skillCd = GameDataMgr.Instance.SkillCd();
        if ((Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.JoystickButton0)) && Time.time >= shieldReady)
        {
            isShield = true;
            shieldEnd = Time.time + GameDataMgr.Instance.ShieldTime();
            shieldReady = Time.time + skillCd;
            ShowShield();
            SoundManager.Play(SoundCue.Skill);
            ShieldActivated?.Invoke();
        }

        if ((Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.JoystickButton1)) && Time.time >= skillReady)
        {
            UseHeroSkill();
            skillReady = Time.time + SkillCooldown();
            SoundManager.Play(SoundCue.Skill);
            SkillActivated?.Invoke();
        }
    }

    private void UseHeroSkill()
    {
        switch (SkillType())
        {
            case 1:
                BulletObject.ClearByPlayer();
                break;
            case 2:
                nowHp = Mathf.Min(maxHp, nowHp + 2);
                GamePanel.Instance.ChangeHP(nowHp);
                break;
            case 3:
                slowEnd = Time.time + 5f;
                bulletRate = 0.35f;
                break;
            case 4:
                pulseEnd = Time.time + 4f;
                nextPulse = 0f;
                break;
            case 5:
                boostEnd = Time.time + 5f;
                break;
        }
    }

    private void UpdateSkill()
    {
        if (Time.time >= slowEnd)
        {
            bulletRate = 1f;
        }

        if (Time.time < pulseEnd && Time.time >= nextPulse)
        {
            BulletObject.ClearByPlayer();
            nextPulse = Time.time + 0.75f;
        }
    }

    private void UpdateShield()
    {
        if (!isShield || Time.time < shieldEnd)
            return;

        isShield = false;
        HideShield();
    }

    private int SkillType()
    {
        return Mathf.Clamp(GameDataMgr.Instance.heroIndex + 1, 1, 5);
    }

    private float SkillCooldown()
    {
        return HeroSkillCooldown(GameDataMgr.Instance.heroIndex, GameDataMgr.Instance.SkillCd());
    }

    public void GrantProtection(float duration)
    {
        damageReady = Mathf.Max(damageReady, Time.time + Mathf.Max(0f, duration));
    }

    public string SkillName()
    {
        return SkillNames[SkillType() - 1];
    }

    public string SkillIcon()
    {
        return SkillIcons[SkillType() - 1];
    }

    public string SkillText()
    {
        string shieldText;
        if (isShield)
            shieldText = "E 护盾中";
        else if (Time.time >= shieldReady)
            shieldText = "E 护盾可用";
        else
            shieldText = "E 护盾冷却 " + Mathf.CeilToInt(shieldReady - Time.time) + "s";

        string skillName = SkillName();
        string heroText = Time.time >= skillReady
            ? "Q " + skillName + " 可用"
            : "Q " + skillName + " 冷却 " + Mathf.CeilToInt(skillReady - Time.time) + "s";
        return shieldText + "    " + heroText;
    }

    public string ShieldIcon()
    {
        if (isShield || Time.time >= shieldReady)
            return "盾";

        return Mathf.CeilToInt(shieldReady - Time.time).ToString();
    }

    public string SkillIconText()
    {
        if (Time.time >= skillReady)
            return SkillIcon();

        return Mathf.CeilToInt(skillReady - Time.time).ToString();
    }

    private PlayerShieldView GetShield()
    {
        if (shieldView == null)
        {
            shieldView = GetComponent<PlayerShieldView>();
        }

        if (shieldView == null)
        {
            shieldView = gameObject.AddComponent<PlayerShieldView>();
        }

        return shieldView;
    }

    private void ShowShield()
    {
        GetShield().Show();
    }

    private void BuildShield()
    {
        GetShield().Build();
    }

    private void HideShield()
    {
        if (shieldView != null)
        {
            shieldView.Hide();
        }
    }
}
