using UnityEngine;

// Keeps the aircraft readable when it passes behind a corner HUD group.
public sealed class CombatHudVisibility : MonoBehaviour
{
    [SerializeField] private RectTransform[] regions;
    [SerializeField, Range(0.05f, 1f)] private float overlapAlpha = 0.16f;

    public RectTransform[] Regions => regions;

    public void Configure(RectTransform[] protectedRegions)
    {
        regions = protectedRegions;
        EnsureGroups();
    }

    private void Awake() { EnsureGroups(); }

    private void EnsureGroups()
    {
        if (regions == null) return;
        foreach (RectTransform region in regions)
            if (region != null && region.GetComponent<CanvasGroup>() == null)
                region.gameObject.AddComponent<CanvasGroup>();
    }

    private void LateUpdate()
    {
        if (regions == null || PlayerObject.Instance == null || Camera.main == null) return;
        Vector2 screenPoint = Camera.main.WorldToScreenPoint(PlayerObject.Instance.transform.position);
        Canvas canvas = GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;
        float speed = 7f * Time.unscaledDeltaTime;
        foreach (RectTransform region in regions)
        {
            if (region == null) continue;
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(region, screenPoint, uiCamera, out local);
            Rect expanded = region.rect;
            expanded.xMin -= 34f;
            expanded.xMax += 34f;
            expanded.yMin -= 34f;
            expanded.yMax += 34f;
            CanvasGroup group = region.GetComponent<CanvasGroup>();
            float target = expanded.Contains(local) ? overlapAlpha : 1f;
            group.alpha = Mathf.MoveTowards(group.alpha, target, speed);
        }
    }
}
