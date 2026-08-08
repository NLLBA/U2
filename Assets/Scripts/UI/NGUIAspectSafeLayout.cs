using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(UIRoot))]
[DefaultExecutionOrder(-200)]
public sealed class NGUIAspectSafeLayout : MonoBehaviour
{
    public const int DefaultReferenceWidth = 1920;
    public const int DefaultReferenceHeight = 1080;

    [SerializeField] private Camera uiCamera;
    [SerializeField] private int referenceWidth = DefaultReferenceWidth;
    [SerializeField] private int referenceHeight = DefaultReferenceHeight;
    [SerializeField] private float referenceVerticalFieldOfView = 60f;
    [SerializeField] private float referenceOrthographicSize = 1f;
    [SerializeField] private bool respectScreenSafeArea = true;

    private UIRoot uiRoot;
    private int lastSurfaceWidth = -1;
    private int lastSurfaceHeight = -1;
    private Rect lastSafeArea = new Rect(-1f, -1f, -1f, -1f);
    private Rect lastCameraRect = new Rect(-1f, -1f, -1f, -1f);
    private RenderTexture lastTargetTexture;

    public Camera UiCamera => uiCamera;
    public int ReferenceWidth => referenceWidth;
    public int ReferenceHeight => referenceHeight;
    public float ReferenceVerticalFieldOfView => referenceVerticalFieldOfView;
    public bool RespectScreenSafeArea => respectScreenSafeArea;

    private void Awake()
    {
        CacheComponents();
        ApplyLayout();
    }

    private void OnEnable()
    {
        CacheComponents();
        ApplyLayout();
    }

    private void OnValidate()
    {
        referenceWidth = Mathf.Max(1, referenceWidth);
        referenceHeight = Mathf.Max(1, referenceHeight);
        referenceVerticalFieldOfView = Mathf.Clamp(referenceVerticalFieldOfView, 1f, 179f);
        referenceOrthographicSize = Mathf.Max(0.01f, referenceOrthographicSize);

        if (isActiveAndEnabled)
            ApplyLayout();
    }

    private void LateUpdate()
    {
        int width;
        int height;
        Rect safeArea;
        GetSurfaceMetrics(out width, out height, out safeArea);
        RenderTexture targetTexture = uiCamera != null ? uiCamera.targetTexture : null;
        if (width != lastSurfaceWidth || height != lastSurfaceHeight ||
            safeArea != lastSafeArea || lastTargetTexture != targetTexture ||
            (uiCamera != null && uiCamera.rect != lastCameraRect))
        {
            ApplyLayout();
        }
    }

    public void Configure(Camera camera, int width = DefaultReferenceWidth, int height = DefaultReferenceHeight)
    {
        uiCamera = camera;
        referenceWidth = Mathf.Max(1, width);
        referenceHeight = Mathf.Max(1, height);

        if (uiCamera != null)
        {
            if (uiCamera.orthographic)
                referenceOrthographicSize = Mathf.Max(0.01f, uiCamera.orthographicSize);
            else
            {
                float planeDistance = GetPerspectivePlaneDistance();
                referenceVerticalFieldOfView = planeDistance > 0.01f
                    ? CalculatePerspectiveFieldOfView(width, height, (float)width / height, planeDistance)
                    : Mathf.Clamp(uiCamera.fieldOfView, 1f, 179f);
            }
        }

        CacheComponents();
        ConfigureRoot();
    }

    public void ApplyLayout()
    {
        CacheComponents();
        if (uiRoot == null || uiCamera == null)
            return;

        ConfigureRoot();
        uiRoot.UpdateScale(false);

        int surfaceWidth;
        int surfaceHeight;
        Rect safeArea;
        GetSurfaceMetrics(out surfaceWidth, out surfaceHeight, out safeArea);
        if (surfaceWidth <= 0 || surfaceHeight <= 0 || safeArea.width <= 0f || safeArea.height <= 0f)
            return;

        Rect viewport = CalculateViewport(surfaceWidth, surfaceHeight, safeArea);
        uiCamera.rect = viewport;
        uiCamera.ResetAspect();

        float viewportAspect = safeArea.width / safeArea.height;
        int activeRootHeight = Mathf.Max(1, uiRoot.activeHeight);
        if (uiCamera.orthographic)
        {
            uiCamera.orthographicSize = CalculateOrthographicSize(
                referenceWidth,
                referenceHeight,
                activeRootHeight,
                viewportAspect,
                referenceOrthographicSize);
        }
        else
        {
            float planeDistance = GetPerspectivePlaneDistance();
            uiCamera.fieldOfView = planeDistance > 0.01f
                ? CalculatePerspectiveFieldOfView(
                    referenceWidth,
                    referenceHeight,
                    viewportAspect,
                    planeDistance)
                : CalculateVerticalFieldOfView(
                    referenceVerticalFieldOfView,
                    (float)referenceWidth / referenceHeight,
                    viewportAspect);
        }

        lastSurfaceWidth = surfaceWidth;
        lastSurfaceHeight = surfaceHeight;
        lastSafeArea = safeArea;
        lastCameraRect = viewport;
        lastTargetTexture = uiCamera.targetTexture;
    }

    public static float CalculateVerticalFieldOfView(
        float referenceFieldOfView,
        float referenceAspect,
        float viewportAspect)
    {
        referenceFieldOfView = Mathf.Clamp(referenceFieldOfView, 1f, 179f);
        referenceAspect = Mathf.Max(0.01f, referenceAspect);
        viewportAspect = Mathf.Max(0.01f, viewportAspect);
        if (viewportAspect >= referenceAspect)
            return referenceFieldOfView;

        float referenceHalfHeight = Mathf.Tan(referenceFieldOfView * 0.5f * Mathf.Deg2Rad);
        float fittedHalfHeight = referenceHalfHeight * referenceAspect / viewportAspect;
        return Mathf.Atan(fittedHalfHeight) * 2f * Mathf.Rad2Deg;
    }

    public static int CalculateFitHeight(int width, int height, float viewportAspect)
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        viewportAspect = Mathf.Max(0.01f, viewportAspect);
        return Mathf.RoundToInt(Mathf.Max(height, width / viewportAspect));
    }

    public static float CalculatePerspectiveFieldOfView(
        int width,
        int height,
        float viewportAspect,
        float referencePlaneDistance)
    {
        int fittedHeight = CalculateFitHeight(width, height, viewportAspect);
        float distance = Mathf.Max(0.01f, referencePlaneDistance);
        float fieldOfView = Mathf.Atan(fittedHeight * 0.5f / distance) * 2f * Mathf.Rad2Deg;
        return Mathf.Clamp(fieldOfView, 1f, 179f);
    }

    public static float CalculateOrthographicSize(
        int width,
        int height,
        int activeRootHeight,
        float viewportAspect,
        float referenceSize)
    {
        int fittedHeight = CalculateFitHeight(width, height, viewportAspect);
        return Mathf.Max(0.01f, referenceSize) * fittedHeight / Mathf.Max(1f, activeRootHeight);
    }

    public static Rect ClampSafeArea(int width, int height, Rect safeArea)
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);

        if (!IsFinite(safeArea.xMin) || !IsFinite(safeArea.xMax) ||
            !IsFinite(safeArea.yMin) || !IsFinite(safeArea.yMax))
        {
            return new Rect(0f, 0f, width, height);
        }

        float xMin = Mathf.Clamp(Mathf.Min(safeArea.xMin, safeArea.xMax), 0f, width);
        float xMax = Mathf.Clamp(Mathf.Max(safeArea.xMin, safeArea.xMax), 0f, width);
        float yMin = Mathf.Clamp(Mathf.Min(safeArea.yMin, safeArea.yMax), 0f, height);
        float yMax = Mathf.Clamp(Mathf.Max(safeArea.yMin, safeArea.yMax), 0f, height);

        if (xMax - xMin < 1f || yMax - yMin < 1f)
            return new Rect(0f, 0f, width, height);

        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    public static Rect CalculateViewport(int width, int height, Rect safeArea)
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        Rect clampedSafeArea = ClampSafeArea(width, height, safeArea);
        return new Rect(
            clampedSafeArea.x / width,
            clampedSafeArea.y / height,
            clampedSafeArea.width / width,
            clampedSafeArea.height / height);
    }

    private void CacheComponents()
    {
        if (uiRoot == null)
            uiRoot = GetComponent<UIRoot>();
        if (uiCamera == null)
            uiCamera = GetComponentInChildren<Camera>(true);
    }

    private void ConfigureRoot()
    {
        if (uiRoot == null)
            return;

        uiRoot.scalingStyle = UIRoot.Scaling.Constrained;
        uiRoot.manualWidth = referenceWidth;
        uiRoot.manualHeight = referenceHeight;
        uiRoot.fitWidth = true;
        uiRoot.fitHeight = true;
    }

    private float GetPerspectivePlaneDistance()
    {
        if (uiCamera == null)
            return 0f;

        Vector3 cameraPosition = transform.InverseTransformPoint(uiCamera.transform.position);
        Vector3 cameraForward = transform.InverseTransformDirection(uiCamera.transform.forward);
        if (cameraForward.sqrMagnitude < 0.0001f)
            return 0f;

        cameraForward.Normalize();
        return Vector3.Dot(-cameraPosition, cameraForward);
    }

    private void GetSurfaceMetrics(out int width, out int height, out Rect safeArea)
    {
        RenderTexture targetTexture = uiCamera != null ? uiCamera.targetTexture : null;
        if (targetTexture != null)
        {
            width = targetTexture.width;
            height = targetTexture.height;
            safeArea = new Rect(0f, 0f, width, height);
            return;
        }

        width = Screen.width;
        height = Screen.height;
        safeArea = respectScreenSafeArea
            ? Screen.safeArea
            : new Rect(0f, 0f, width, height);
        safeArea = ClampSafeArea(width, height, safeArea);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
