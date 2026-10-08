using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum FirePos
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
    private static float intensity = 1f;

    public static float Intensity => intensity;

    public FirePos type;

    private FireInfo fireData;
    private int count;
    private float gap;
    private float delay;
    private BulletInfo bulletData;
    private float angleStep;
    private Vector3 screenPos;
    private Vector3 baseDir;
    private Vector3 dir;

    void Update()
    {
        if (PlayerObject.Instance != null && PlayerObject.Instance.isDead)
            return;

        UpdatePos();

        float delta = Time.deltaTime * intensity;
        if (delta <= 0f)
            return;

        ResetFire(delta);
        Shoot(delta);
    }

    public static void SetIntensity(float scale)
    {
        intensity = Mathf.Clamp01(scale);
    }

    private void UpdatePos()
    {
        if (Camera.main == null)
            return;

        screenPos.z = 293;
        switch (type)
        {
            case FirePos.TopLeft:
                screenPos.x = 0;
                screenPos.y = Screen.height;
                baseDir = Vector3.right;
                break;
            case FirePos.Top:
                screenPos.x = Screen.width / 2;
                screenPos.y = Screen.height;
                baseDir = Vector3.right;
                break;
            case FirePos.TopRight:
                screenPos.x = Screen.width;
                screenPos.y = Screen.height;
                baseDir = Vector3.left;
                break;
            case FirePos.Left:
                screenPos.x = 0;
                screenPos.y = Screen.height / 2;
                baseDir = Vector3.right;
                break;
            case FirePos.Right:
                screenPos.x = Screen.width;
                screenPos.y = Screen.height / 2;
                baseDir = Vector3.left;
                break;
            case FirePos.BottomLeft:
                screenPos.x = 0;
                screenPos.y = 0;
                baseDir = Vector3.right;
                break;
            case FirePos.Bottom:
                screenPos.x = Screen.width / 2;
                screenPos.y = 0;
                baseDir = Vector3.right;
                break;
            case FirePos.BottomRight:
                screenPos.x = Screen.width;
                screenPos.y = 0;
                baseDir = Vector3.left;
                break;
        }

        transform.position = Camera.main.ScreenToWorldPoint(screenPos);
    }

    private void ResetFire(float deltaTime)
    {
        if (gap != 0 && count != 0)
            return;

        if (fireData != null)
        {
            delay -= deltaTime;
            if (delay > 0)
                return;
        }

        fireData = GameDataMgr.Instance.RandomFire();
        if (fireData == null)
            return;

        count = fireData.num;
        gap = GameDataMgr.Instance.FireGap(fireData.cd);
        delay = GameDataMgr.Instance.FireDelay(fireData.delay);

        string[] strs = fireData.ids.Split(',');
        int minId = int.Parse(strs[0]);
        int maxId = int.Parse(strs[1]);
        int bulletId = Random.Range(minId, maxId + 1);
        bulletData = GameDataMgr.Instance.GetBullet(bulletId);

        if (fireData.type == 2)
        {
            switch (type)
            {
                case FirePos.TopLeft:
                case FirePos.TopRight:
                case FirePos.BottomLeft:
                case FirePos.BottomRight:
                    angleStep = 90f / (count + 1);
                    break;
                case FirePos.Top:
                case FirePos.Left:
                case FirePos.Right:
                case FirePos.Bottom:
                    angleStep = 180f / (count + 1);
                    break;
            }
        }
    }

    private void Shoot(float deltaTime)
    {
        if (bulletData == null)
            return;

        if (gap == 0 && count == 0)
            return;

        gap -= deltaTime;
        if (gap > 0)
            return;

        GameObject bullet;
        BulletObject bulletObj;
        switch (fireData.type)
        {
            case 1:
                bullet = NewBullet(out bulletObj);
                if (bullet == null)
                    return;

                bullet.transform.position = transform.position;
                bullet.transform.rotation = Quaternion.LookRotation(PlayerObject.Instance.transform.position - transform.position);

                --count;
                gap = count == 0 ? 0 : GameDataMgr.Instance.FireGap(fireData.cd);
                break;
            case 2:
                if (fireData.cd == 0)
                {
                    for (int i = 0; i < count; i++)
                    {
                        bullet = NewBullet(out bulletObj);
                        if (bullet == null)
                            continue;

                        dir = Quaternion.AngleAxis(angleStep * i, Vector3.up) * baseDir;
                        bullet.transform.position = SpreadPos(dir, i, count);
                        bullet.transform.rotation = Quaternion.LookRotation(dir);
                    }

                    gap = count = 0;
                }
                else
                {
                    bullet = NewBullet(out bulletObj);
                    if (bullet == null)
                        return;

                    int index = fireData.num - count;
                    dir = Quaternion.AngleAxis(angleStep * index, Vector3.up) * baseDir;
                    bullet.transform.position = SpreadPos(dir, index, fireData.num);
                    bullet.transform.rotation = Quaternion.LookRotation(dir);

                    --count;
                    gap = count == 0 ? 0 : GameDataMgr.Instance.FireGap(fireData.cd);
                }
                break;
        }
    }

    private Vector3 SpreadPos(Vector3 dir, int index, int count)
    {
        Vector3 front = dir.normalized * 2.8f;
        Vector3 side = Vector3.Cross(Vector3.up, dir.normalized);

        if (side.sqrMagnitude < 0.001f)
        {
            side = Vector3.right;
        }
        else
        {
            side.Normalize();
        }

        float center = (count - 1) * 0.5f;
        float offset = (index - center) * 0.55f;

        return transform.position + front + side * offset;
    }

    private GameObject NewBullet(out BulletObject bulletObj)
    {
        bulletObj = null;
        ObjectPoolManager pool = ObjectPoolManager.Ensure();
        GameObject bullet = pool.SpawnAsset(
            bulletData.asset,
            transform.position,
            Quaternion.identity);
        if (bullet == null)
            return null;

        bulletObj = bullet.GetComponent<BulletObject>();
        if (bulletObj == null)
        {
            bulletObj = bullet.AddComponent<BulletObject>();
        }

        bulletObj.Init(bulletData);
        return bullet;
    }
}
