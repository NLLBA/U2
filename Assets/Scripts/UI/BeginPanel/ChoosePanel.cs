using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class ChoosePanel : BasePanel<ChoosePanel>
{
    public UIButton btnClose;
    public UIButton btnLeft;
    public UIButton btnRight;
    public UIButton btnStart;
    public Transform heroPos;
    public List<GameObject> hpObjs;
    public List<GameObject> speedObjs;
    public List<GameObject> volumeObjs;
    public Camera uis;
    [FormerlySerializedAs("heroNameLabel")]
    public UILabel labHero;

    private GameObject heroObj;
    private float time;
    private bool isSel;
    private string startButtonText;
    private UILabel heroDetailLabel;
    private Vector3 heroBaseLocalPosition;
    private bool hasHeroBasePosition;

    private const float HeroBobAmplitude = 4f;
    private const float HeroBobSpeed = 1.5f;

    public override void Init()
    {
        CacheStartButtonText();
        CacheHeroBasePosition();
        BuildHeroDetails();
        btnClose.onClick.Add(new EventDelegate(() =>
        {
            if (IsNetworkSelection())
                NetworkBattleSession.Instance.Leave();
            Hideme();
            BeginPanel.Instance.Showme();
        }));

        btnLeft.onClick.Add(new EventDelegate(() =>
        {
            --GameDataMgr.Instance.heroIndex;
            if (GameDataMgr.Instance.heroIndex < 0)
            {
                GameDataMgr.Instance.heroIndex = GameDataMgr.Instance.roleData.roles.Count - 1;
            }
            UpdateHero();
        }));

        btnRight.onClick.Add(new EventDelegate(() =>
        {
            ++GameDataMgr.Instance.heroIndex;
            if (GameDataMgr.Instance.heroIndex >= GameDataMgr.Instance.roleData.roles.Count)
            {
                GameDataMgr.Instance.heroIndex = 0;
            }
            UpdateHero();
        }));

        btnStart.onClick.Add(new EventDelegate(() =>
        {
            if (IsNetworkSelection())
            {
                NetworkBattleSession.Instance.ConfirmCharacter();
                return;
            }
            if (DifficultyPanel.Instance != null)
            {
                DifficultyPanel.Instance.Showme();
                Hideme();
            }
            else
            {
                GameLog.Error(GameLogCategory.Data, "DIFFICULTY_PANEL_MISSING",
                    "DifficultyPanel not found. Run Tools/U2/搭建毕设UI first.");
            }
        }));

        Hideme();
    }

    private void UpdateHero()
    {
        RoleInfo info = GameDataMgr.Instance.GetHero();
        if (labHero != null)
        {
            labHero.transform.localPosition = new Vector3(-170f, 242f, 0f);
            labHero.width = 390;
            labHero.height = 44;
            labHero.maxLineCount = 1;
            labHero.overflowMethod = UILabel.Overflow.ShrinkContent;
            labHero.text = HeroName(info);
        }
        if (heroDetailLabel != null)
        {
            float cooldown = PlayerObject.HeroSkillCooldown(GameDataMgr.Instance.heroIndex,
                GameDataMgr.Instance.SkillCd());
            heroDetailLabel.text = PlayerObject.StageAffinityText(GameDataMgr.Instance.heroIndex) +
                "\n" + PlayerObject.RecommendedLevelText(GameDataMgr.Instance.heroIndex) + " · " +
                PlayerObject.OperationDifficultyText(GameDataMgr.Instance.heroIndex) +
                "\n技能冷却：" + cooldown.ToString("0.#") + "秒";
        }
        DestroyHero();

        GameObject prefab = Resources.Load<GameObject>(info.asset);
        if (prefab == null)
        {
            GameLog.Error(GameLogCategory.Data, "HERO_RESOURCE_MISSING", info.asset);
            return;
        }

        heroObj = Instantiate(prefab);
        heroObj.transform.SetParent(heroPos, false);
        heroObj.transform.localPosition = Vector3.zero;
        heroObj.transform.localRotation = Quaternion.identity;
        heroObj.transform.localScale = Vector3.one * info.scale;
        heroObj.layer = LayerMask.NameToLayer("UI");

        for (int i = 0; i < 10; i++)
        {
            if (i < hpObjs.Count)
                hpObjs[i].SetActive(i < info.hp);
            if (i < speedObjs.Count)
                speedObjs[i].SetActive(i < info.speed);
            if (i < volumeObjs.Count)
                volumeObjs[i].SetActive(i < info.volume);
        }
    }

    public static string HeroName(RoleInfo info)
    {
        if (info == null || string.IsNullOrEmpty(info.asset))
            return "UNKNOWN AIRCRAFT";

        string normalized = info.asset.Replace('\\', '/');
        int separator = normalized.LastIndexOf('/');
        return separator >= 0 ? normalized.Substring(separator + 1) : normalized;
    }

    private void BuildHeroDetails()
    {
        if (labHero == null)
            return;

        Transform backdropTransform = transform.Find("HeroDetailBackdrop");
        if (backdropTransform == null)
        {
            Transform nameplate = transform.Find("Polish_HeroNameplate");
            if (nameplate != null)
            {
                GameObject backdropObject = Instantiate(nameplate.gameObject, transform, false);
                backdropObject.name = "HeroDetailBackdrop";
                backdropTransform = backdropObject.transform;
            }
        }

        if (backdropTransform != null)
        {
            backdropTransform.localPosition = new Vector3(500f, -155f, 0f);
            UI2DSprite backdrop = backdropTransform.GetComponent<UI2DSprite>();
            if (backdrop != null)
            {
                backdrop.width = 420;
                backdrop.height = 94;
                backdrop.color = new Color(backdrop.color.r, backdrop.color.g, backdrop.color.b, 0.82f);
            }
        }

        Transform detailTransform = transform.Find("HeroDetailLabel");
        if (detailTransform == null)
        {
            GameObject detailObject = Instantiate(labHero.gameObject, transform, false);
            detailObject.name = "HeroDetailLabel";
            detailTransform = detailObject.transform;
        }

        detailTransform.localPosition = new Vector3(500f, -155f, 0f);
        heroDetailLabel = detailTransform.GetComponent<UILabel>();
        heroDetailLabel.width = 390;
        heroDetailLabel.height = 82;
        heroDetailLabel.fontSize = 17;
        heroDetailLabel.maxLineCount = 3;
        heroDetailLabel.overflowMethod = UILabel.Overflow.ShrinkContent;
    }

    private void CacheHeroBasePosition()
    {
        if (heroPos == null || hasHeroBasePosition)
            return;

        heroBaseLocalPosition = heroPos.localPosition;
        hasHeroBasePosition = true;
    }

    private void DestroyHero()
    {
        if (heroObj != null)
        {
            Destroy(heroObj.gameObject);
            heroObj = null;
        }
    }

    public override void Showme()
    {
        base.Showme();
        GameDataMgr.Instance.heroIndex = 0;
        UpdateHero();
        RefreshNetworkControls();
    }

    public void ShowCurrent()
    {
        CacheStartButtonText();
        base.Showme();
        UpdateHero();
        RefreshNetworkControls();
    }

    public override void Hideme()
    {
        if (heroPos != null && hasHeroBasePosition)
            heroPos.localPosition = heroBaseLocalPosition;
        time = 0f;
        base.Hideme();
        DestroyHero();
    }

    private void Update()
    {
        RefreshNetworkControls();
        if (heroPos == null)
            return;

        CacheHeroBasePosition();
        time += Time.deltaTime;
        Vector3 bobbedPosition = heroBaseLocalPosition;
        bobbedPosition.y += Mathf.Sin(time * HeroBobSpeed) * HeroBobAmplitude;
        heroPos.localPosition = bobbedPosition;

        if (Input.GetMouseButtonDown(0) && uis != null)
        {
            if (Physics.Raycast(uis.ScreenPointToRay(Input.mousePosition), 1000, 1 << LayerMask.NameToLayer("UI")))
            {
                isSel = true;
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            isSel = false;
        }

        if (Input.GetMouseButton(0) && isSel)
        {
            heroPos.rotation *= Quaternion.AngleAxis(Input.GetAxis("Mouse X") * 20, Vector3.up);
        }
    }

    private static bool IsNetworkSelection()
    {
        return NetworkBattleSession.Instance != null &&
            NetworkBattleSession.Instance.State == NetworkBattleSession.BattleState.SelectingCharacter;
    }

    private void RefreshNetworkControls()
    {
        CacheStartButtonText();
        bool selecting = IsNetworkSelection();
        bool confirmed = selecting && NetworkBattleSession.Instance.LocalReady;
        btnLeft.isEnabled = !confirmed;
        btnRight.isEnabled = !confirmed;
        btnStart.isEnabled = !confirmed;
        UILabel label = btnStart.GetComponentInChildren<UILabel>();
        if (label != null)
            label.text = selecting ? (confirmed ? "已确认，等待对手" : "确认角色") : startButtonText;
    }

    private void CacheStartButtonText()
    {
        if (startButtonText != null)
            return;
        UILabel label = btnStart.GetComponentInChildren<UILabel>();
        startButtonText = label != null ? label.text : "下一步";
    }
}
