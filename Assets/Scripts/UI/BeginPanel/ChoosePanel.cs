using System.Collections.Generic;
using UnityEngine;

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
    public UILabel heroNameLabel;

    private GameObject ariPlaneobj;
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
            --GameDataMgr.Instance.nowSelHeroIndex;
            if (GameDataMgr.Instance.nowSelHeroIndex < 0)
            {
                GameDataMgr.Instance.nowSelHeroIndex = GameDataMgr.Instance.roleData.roleDatas.Count - 1;
            }
            ChangeNowHero();
        }));

        btnRight.onClick.Add(new EventDelegate(() =>
        {
            ++GameDataMgr.Instance.nowSelHeroIndex;
            if (GameDataMgr.Instance.nowSelHeroIndex >= GameDataMgr.Instance.roleData.roleDatas.Count)
            {
                GameDataMgr.Instance.nowSelHeroIndex = 0;
            }
            ChangeNowHero();
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

    private void ChangeNowHero()
    {
        RoleInfo info = GameDataMgr.Instance.GetNowSelHeroInfo();
        if (heroNameLabel != null)
            heroNameLabel.text = GetHeroDisplayName(info);
        Destroyobj();

        GameObject prefab = Resources.Load<GameObject>(info.resName);
        if (prefab == null)
        {
            Debug.LogError("Hero resource not found: " + info.resName);
            return;
        }

        ariPlaneobj = Instantiate(prefab);
        ariPlaneobj.transform.SetParent(heroPos, false);
        ariPlaneobj.transform.localPosition = Vector3.zero;
        ariPlaneobj.transform.localRotation = Quaternion.identity;
        ariPlaneobj.transform.localScale = Vector3.one * info.scale;
        ariPlaneobj.layer = LayerMask.NameToLayer("UI");

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

    public static string GetHeroDisplayName(RoleInfo info)
    {
        if (info == null || string.IsNullOrEmpty(info.resName))
            return "UNKNOWN AIRCRAFT";

        string normalized = info.resName.Replace('\\', '/');
        int separator = normalized.LastIndexOf('/');
        return separator >= 0 ? normalized.Substring(separator + 1) : normalized;
    }

    private void Destroyobj()
    {
        if (ariPlaneobj != null)
        {
            Destroy(ariPlaneobj.gameObject);
            ariPlaneobj = null;
        }
    }

    public override void Showme()
    {
        base.Showme();
        GameDataMgr.Instance.nowSelHeroIndex = 0;
        ChangeNowHero();
    }

    public void ShowmeKeepingSelection()
    {
        base.Showme();
        ChangeNowHero();
    }

    public override void Hideme()
    {
        base.Hideme();
        Destroyobj();
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
