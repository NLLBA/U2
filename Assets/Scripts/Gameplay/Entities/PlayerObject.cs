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

    public static PlayerObject Instance => instance;
    public static float BulletRate => bulletRate;

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
    private PlayerShieldView shieldView;

    private void Awake()
    {
        instance = this;
        bulletRate = 1f;
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
        HideShield();

        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.GameOver();
        }
        else
        {
            Time.timeScale = 0f;
        }

        GameOverPanel.Instance.Showme();
        ScorePanelPresenter.ShowFinalScore(GameOverPanel.Instance);
    }

    public void Wound()
    {
        if (isDead || isShield)
            return;

        nowHp--;
        SoundManager.Play(SoundCue.PlayerHit);
        GamePanel.Instance.ChangeHP(nowHp);

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
        Vector3 lastPos = transform.position;
        transform.Translate(Vector3.forward * vertical * moveRate * Time.deltaTime);
        transform.Translate(
            Vector3.right * horizontal * moveRate * Time.deltaTime,
            Space.World);

        if (Camera.main == null)
            return;

        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
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
        if (!Input.GetMouseButtonDown(0) || Camera.main == null)
            return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        int bulletLayer = LayerMask.NameToLayer("Bullent");
        RaycastHit hitInfo;
        if (!Physics.Raycast(ray, out hitInfo, 1000f, 1 << bulletLayer))
            return;

        BulletObject bullet = hitInfo.transform.GetComponent<BulletObject>();
        if (bullet != null)
        {
            bullet.PlayerClear();
        }
    }

    private void UpdateSkills()
    {
        float skillCd = GameDataMgr.Instance.SkillCd();
        if (Input.GetKeyDown(KeyCode.E) && Time.time >= shieldReady)
        {
            isShield = true;
            shieldEnd = Time.time + GameDataMgr.Instance.ShieldTime();
            shieldReady = Time.time + skillCd;
            ShowShield();
            SoundManager.Play(SoundCue.Skill);
        }

        if (Input.GetKeyDown(KeyCode.Q) && Time.time >= skillReady)
        {
            UseHeroSkill();
            skillReady = Time.time + SkillCooldown();
            SoundManager.Play(SoundCue.Skill);
        }
    }

    private void UseHeroSkill()
    {
        switch (SkillType())
        {
            case 1:
                BulletObject.ClearAll();
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
            BulletObject.ClearAll();
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
        int index = SkillType() - 1;
        return GameDataMgr.Instance.SkillCd() + SkillCdBonus[index];
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
