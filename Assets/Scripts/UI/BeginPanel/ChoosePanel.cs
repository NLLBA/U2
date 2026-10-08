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

    public override void Init()
    {
        btnClose.onClick.Add(new EventDelegate(() =>
        {
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
            if (DifficultyPanel.Instance != null)
            {
                DifficultyPanel.Instance.Showme();
                Hideme();
            }
            else
            {
                Debug.LogError("DifficultyPanel not found. Run Tools/U2/搭建毕设UI first.");
            }
        }));

        Hideme();
    }

    private void UpdateHero()
    {
        RoleInfo info = GameDataMgr.Instance.GetHero();
        if (labHero != null)
            labHero.text = HeroName(info);
        DestroyHero();

        GameObject prefab = Resources.Load<GameObject>(info.asset);
        if (prefab == null)
        {
            Debug.LogError("Hero resource not found: " + info.asset);
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
    }

    public void ShowCurrent()
    {
        base.Showme();
        UpdateHero();
    }

    public override void Hideme()
    {
        base.Hideme();
        DestroyHero();
    }

    private void Update()
    {
        if (heroPos == null)
            return;

        time += Time.deltaTime;
        heroPos.Translate(Vector3.up * Mathf.Sin(time) * 0.0001f, Space.World);

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
}
