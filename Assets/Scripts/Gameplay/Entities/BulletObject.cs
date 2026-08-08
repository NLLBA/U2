using System.Collections.Generic;
using UnityEngine;

public enum BulletSource
{
    Normal,
    Boss,
}

public class BulletObject : MonoBehaviour, IPoolableObject
{
    private static List<BulletObject> allBullets = new List<BulletObject>();

    private BulletInfo info;
    private float time;
    private bool isDead;
    private float fixedSpeedMultiplier = 1f;

    public BulletSource Source { get; private set; }
    public bool IsDead => isDead;

    private void OnEnable()
    {
        RegisterActiveBullet();
    }

    private void OnDestroy()
    {
        allBullets.Remove(this);
    }

    private void OnDisable()
    {
        allBullets.Remove(this);
    }

    public static void ClearAllBullets()
    {
        BulletObject[] bullets = allBullets.ToArray();
        for (int i = 0; i < bullets.Length; i++)
        {
            if (bullets[i] != null)
            {
                bullets[i].Dead();
            }
        }
    }

    public static int ClearBossBullets()
    {
        int clearedCount = 0;
        BulletObject[] bullets = allBullets.ToArray();
        for (int i = 0; i < bullets.Length; i++)
        {
            if (bullets[i] != null && bullets[i].Source == BulletSource.Boss)
            {
                bullets[i].Dead(false);
                clearedCount++;
            }
        }

        return clearedCount;
    }

    public void InitInfo(BulletInfo info, BulletSource source = BulletSource.Normal, float speedMultiplier = 1f)
    {
        this.info = info;
        Source = source;
        fixedSpeedMultiplier = Mathf.Max(0f, speedMultiplier);
        time = 0f;
        isDead = false;
        CancelInvoke();

        if (info == null)
        {
            Debug.LogError("Bullet info is null.");
            Dead(false);
            return;
        }

        if (Application.isPlaying)
        {
            Invoke("DealyDestroy", info.lifeTime);
        }
    }

    private void DealyDestroy()
    {
        Dead();
    }

    public void Dead()
    {
        Dead(true);
    }

    public bool ClearByPlayer()
    {
        if (isDead)
            return false;

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.RegisterManualBulletClear();
        }

        Dead();
        return true;
    }

    public void Dead(bool spawnEffect)
    {
        if (isDead)
            return;

        isDead = true;
        CancelInvoke();

        if (spawnEffect && info != null && !string.IsNullOrEmpty(info.deadEffRes))
        {
            ObjectPoolManager.EnsureInstance().SpawnEffect(
                info.deadEffRes,
                transform.position,
                Quaternion.identity,
                1f);
        }

        if (ObjectPoolManager.TryRelease(gameObject))
            return;

        if (Application.isPlaying)
        {
            Destroy(gameObject);
        }
        else
        {
            DestroyImmediate(gameObject);
        }
    }

    public void OnPoolAcquire()
    {
        CancelInvoke();
        info = null;
        time = 0f;
        isDead = false;
        Source = BulletSource.Normal;
        fixedSpeedMultiplier = 1f;
        RegisterActiveBullet();
    }

    public void OnPoolRelease()
    {
        CancelInvoke();
        info = null;
        time = 0f;
        isDead = true;
        Source = BulletSource.Normal;
        fixedSpeedMultiplier = 1f;
    }

    private void RegisterActiveBullet()
    {
        if (!allBullets.Contains(this))
        {
            allBullets.Add(this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isDead)
            return;

        if (other.gameObject.CompareTag("Player"))
        {
            PlayerObject obj = other.GetComponent<PlayerObject>();
            if (obj != null)
            {
                obj.Wound();
            }

            Dead();
        }
    }

    void Update()
    {
        if (info == null || isDead)
            return;

        float difficultySpeed = Source == BulletSource.Boss
            ? fixedSpeedMultiplier
            : GameDataMgr.Instance.GetBulletSpeedMul();
        float speedMul = difficultySpeed * PlayerObject.BulletSpeedScale;
        transform.Translate(Vector3.forward * info.forwardSpeed * speedMul * Time.deltaTime);

        switch (info.type)
        {
            case 2:
                time += Time.deltaTime;
                transform.Translate(Vector3.right * Time.deltaTime * Mathf.Sin(time * info.roundSpeed) * info.rightSpeed);
                break;
            case 3:
                transform.rotation *= Quaternion.AngleAxis(info.roundSpeed * Time.deltaTime, Vector3.up);
                break;
            case 4:
                transform.rotation *= Quaternion.AngleAxis(-info.roundSpeed * Time.deltaTime, Vector3.up);
                break;
            case 5:
                if (PlayerObject.Instance != null)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        Quaternion.LookRotation(PlayerObject.Instance.transform.position - transform.position),
                        info.roundSpeed * Time.deltaTime);
                }
                break;
        }
    }
}
