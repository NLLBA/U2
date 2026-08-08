using UnityEngine;

public enum E_Pos_Type
{
    TopLeft,
    Top,
    TopRight,
    Left,
    Right,
    BottomLeft,
    Bottom,
    BottomRight,
}

public class FireObject : MonoBehaviour
{
    private static float globalIntensityScale = 1f;

    public static float GlobalIntensityScale => globalIntensityScale;

    public E_Pos_Type type;

    private FireInfo fireInfo;
    private int nowNum;
    private float nowCD;
    private float nowDelay;
    private BulletInfo nowBulletInfo;
    private float changeAngle;
    private Vector3 screenPos;
    private Vector3 initDir;
    private Vector3 nowDir;

    void Update()
    {
        if (PlayerObject.Instance != null && PlayerObject.Instance.isDead)
            return;

        UpdatePos();

        float fireDeltaTime = Time.deltaTime * globalIntensityScale;
        if (fireDeltaTime <= 0f)
            return;

        ResetFireInfo(fireDeltaTime);
        UpdateFire(fireDeltaTime);
    }

    public static void SetGlobalIntensityScale(float scale)
    {
        globalIntensityScale = Mathf.Clamp01(scale);
    }

    private void UpdatePos()
    {
        if (Camera.main == null)
            return;

        screenPos.z = 293;
        switch (type)
        {
            case E_Pos_Type.TopLeft:
                screenPos.x = 0;
                screenPos.y = Screen.height;
                initDir = Vector3.right;
                break;
            case E_Pos_Type.Top:
                screenPos.x = Screen.width / 2;
                screenPos.y = Screen.height;
                initDir = Vector3.right;
                break;
            case E_Pos_Type.TopRight:
                screenPos.x = Screen.width;
                screenPos.y = Screen.height;
                initDir = Vector3.left;
                break;
            case E_Pos_Type.Left:
                screenPos.x = 0;
                screenPos.y = Screen.height / 2;
                initDir = Vector3.right;
                break;
            case E_Pos_Type.Right:
                screenPos.x = Screen.width;
                screenPos.y = Screen.height / 2;
                initDir = Vector3.left;
                break;
            case E_Pos_Type.BottomLeft:
                screenPos.x = 0;
                screenPos.y = 0;
                initDir = Vector3.right;
                break;
            case E_Pos_Type.Bottom:
                screenPos.x = Screen.width / 2;
                screenPos.y = 0;
                initDir = Vector3.right;
                break;
            case E_Pos_Type.BottomRight:
                screenPos.x = Screen.width;
                screenPos.y = 0;
                initDir = Vector3.left;
                break;
        }

        transform.position = Camera.main.ScreenToWorldPoint(screenPos);
    }

    private void ResetFireInfo(float deltaTime)
    {
        if (nowCD != 0 && nowNum != 0)
            return;

        if (fireInfo != null)
        {
            nowDelay -= deltaTime;
            if (nowDelay > 0)
                return;
        }

        fireInfo = GameDataMgr.Instance.GetRandomFireInfo();
        if (fireInfo == null)
            return;

        nowNum = fireInfo.num;
        nowCD = GameDataMgr.Instance.GetFireCD(fireInfo.cd);
        nowDelay = GameDataMgr.Instance.GetFireDelay(fireInfo.delay);

        string[] strs = fireInfo.ids.Split(',');
        int beginID = int.Parse(strs[0]);
        int endID = int.Parse(strs[1]);
        int randomBulletID = Random.Range(beginID, endID + 1);
        nowBulletInfo = GameDataMgr.Instance.GetBulletInfoById(randomBulletID);

        if (fireInfo.type == 2)
        {
            switch (type)
            {
                case E_Pos_Type.TopLeft:
                case E_Pos_Type.TopRight:
                case E_Pos_Type.BottomLeft:
                case E_Pos_Type.BottomRight:
                    changeAngle = 90f / (nowNum + 1);
                    break;
                case E_Pos_Type.Top:
                case E_Pos_Type.Left:
                case E_Pos_Type.Right:
                case E_Pos_Type.Bottom:
                    changeAngle = 180f / (nowNum + 1);
                    break;
            }
        }
    }

    private void UpdateFire(float deltaTime)
    {
        if (nowBulletInfo == null)
            return;

        if (nowCD == 0 && nowNum == 0)
            return;

        nowCD -= deltaTime;
        if (nowCD > 0)
            return;

        GameObject bullet;
        BulletObject bulletObj;
        switch (fireInfo.type)
        {
            case 1:
                bullet = CreateBullet(out bulletObj);
                if (bullet == null)
                    return;

                bullet.transform.position = transform.position;
                bullet.transform.rotation = Quaternion.LookRotation(PlayerObject.Instance.transform.position - transform.position);

                --nowNum;
                nowCD = nowNum == 0 ? 0 : GameDataMgr.Instance.GetFireCD(fireInfo.cd);
                break;
            case 2:
                if (fireInfo.cd == 0)
                {
                    for (int i = 0; i < nowNum; i++)
                    {
                        bullet = CreateBullet(out bulletObj);
                        if (bullet == null)
                            continue;

                        nowDir = Quaternion.AngleAxis(changeAngle * i, Vector3.up) * initDir;
                        bullet.transform.position = GetSpreadSpawnPosition(nowDir, i, nowNum);
                        bullet.transform.rotation = Quaternion.LookRotation(nowDir);
                    }

                    nowCD = nowNum = 0;
                }
                else
                {
                    bullet = CreateBullet(out bulletObj);
                    if (bullet == null)
                        return;

                    int index = fireInfo.num - nowNum;
                    nowDir = Quaternion.AngleAxis(changeAngle * index, Vector3.up) * initDir;
                    bullet.transform.position = GetSpreadSpawnPosition(nowDir, index, fireInfo.num);
                    bullet.transform.rotation = Quaternion.LookRotation(nowDir);

                    --nowNum;
                    nowCD = nowNum == 0 ? 0 : GameDataMgr.Instance.GetFireCD(fireInfo.cd);
                }
                break;
        }
    }

    private Vector3 GetSpreadSpawnPosition(Vector3 dir, int index, int count)
    {
        Vector3 forwardOffset = dir.normalized * 2.8f;
        Vector3 sideDir = Vector3.Cross(Vector3.up, dir.normalized);

        if (sideDir.sqrMagnitude < 0.001f)
        {
            sideDir = Vector3.right;
        }
        else
        {
            sideDir.Normalize();
        }

        float centerIndex = (count - 1) * 0.5f;
        float sideOffset = (index - centerIndex) * 0.55f;

        return transform.position + forwardOffset + sideDir * sideOffset;
    }

    private GameObject CreateBullet(out BulletObject bulletObj)
    {
        bulletObj = null;
        ObjectPoolManager pool = ObjectPoolManager.EnsureInstance();
        GameObject bullet = pool.SpawnResource(
            nowBulletInfo.resName,
            transform.position,
            Quaternion.identity);
        if (bullet == null)
            return null;

        bulletObj = bullet.GetComponent<BulletObject>();
        if (bulletObj == null)
        {
            bulletObj = bullet.AddComponent<BulletObject>();
        }

        bulletObj.InitInfo(nowBulletInfo);
        return bullet;
    }
}
