using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BulletSource
{
    Normal,
    Boss,
}

public class BulletObject : MonoBehaviour, IPoolable
{
    private static List<BulletObject> activeBullets = new List<BulletObject>();

    private BulletInfo info;
    private float time;
    private bool isDead;
    private float speedRate = 1f;

    public BulletSource Source { get; private set; }
    public bool IsDead => isDead;

    private void OnEnable()
    {
        Register();
    }

    private void OnDestroy()
    {
        activeBullets.Remove(this);
    }

    private void OnDisable()
    {
        activeBullets.Remove(this);
    }

    public static void ClearAll()
    {
        BulletObject[] bullets = activeBullets.ToArray();
        for (int i = 0; i < bullets.Length; i++)
        {
            if (bullets[i] != null)
            {
                bullets[i].Dead();
            }
        }
    }

    public static int ClearBoss()
    {
        int count = 0;
        BulletObject[] bullets = activeBullets.ToArray();
        for (int i = 0; i < bullets.Length; i++)
        {
            if (bullets[i] != null && bullets[i].Source == BulletSource.Boss)
            {
                bullets[i].Dead(false);
                count++;
            }
        }

        return count;
    }

    public void Init(BulletInfo info, BulletSource source = BulletSource.Normal, float speedRate = 1f)
    {
        this.info = info;
        Source = source;
        speedRate = Mathf.Max(0f, speedRate);
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
            Invoke("DealyDestroy", info.life);
        }
    }

    private void AutoDestroy()
    {
        Dead();
    }

    public void Dead()
    {
        Dead(true);
    }

    public bool PlayerClear()
    {
        if (isDead)
            return false;

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.RegisterClear();
        }

        Dead();
        return true;
    }

    public void Dead(bool showFx)
    {
        if (isDead)
            return;

        isDead = true;
        CancelInvoke();

        if (showFx && info != null && !string.IsNullOrEmpty(info.deathFx))
        {
            ObjectPoolManager.Ensure().SpawnFx(
                info.deathFx,
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

    public void OnAcquire()
    {
        CancelInvoke();
        info = null;
        time = 0f;
        isDead = false;
        Source = BulletSource.Normal;
        speedRate = 1f;
        Register();
    }

    public void OnRelease()
    {
        CancelInvoke();
        info = null;
        time = 0f;
        isDead = true;
        Source = BulletSource.Normal;
        speedRate = 1f;
    }

    private void Register()
    {
        if (!activeBullets.Contains(this))
        {
            activeBullets.Add(this);
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

        float difficultyRate = Source == BulletSource.Boss
            ? speedRate
            : GameDataMgr.Instance.BulletSpeed();
        float speed = difficultyRate * PlayerObject.BulletRate;
        transform.Translate(Vector3.forward * info.moveRate * speed * Time.deltaTime);

        switch (info.type)
        {
            case 2:
                time += Time.deltaTime;
                transform.Translate(Vector3.right * Time.deltaTime * Mathf.Sin(time * info.turnSpeed) * info.sideSpeed);
                break;
            case 3:
                transform.rotation *= Quaternion.AngleAxis(info.turnSpeed * Time.deltaTime, Vector3.up);
                break;
            case 4:
                transform.rotation *= Quaternion.AngleAxis(-info.turnSpeed * Time.deltaTime, Vector3.up);
                break;
            case 5:
                if (PlayerObject.Instance != null)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        Quaternion.LookRotation(PlayerObject.Instance.transform.position - transform.position),
                        info.turnSpeed * Time.deltaTime);
                }
                break;
        }
    }
}
