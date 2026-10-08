using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(UIRoot))]
[DefaultExecutionOrder(-200)]
public sealed class NGUIAspectSafeLayout : MonoBehaviour
{
    public const int DefaultWidth = 1920;
    public const int DefaultHeight = 1080;

    [SerializeField] private Camera uiCamera;
    [FormerlySerializedAs("referenceWidth")]
    [SerializeField] private int refWidth = DefaultWidth;
    [FormerlySerializedAs("referenceHeight")]
    [SerializeField] private int refHeight = DefaultHeight;
    [FormerlySerializedAs("referenceVerticalFieldOfView")]
    [SerializeField] private float refFov = 60f;
    [FormerlySerializedAs("referenceOrthographicSize")]
    [SerializeField] private float refSize = 1f;
    [FormerlySerializedAs("respectScreenSafeArea")]
    [SerializeField] private bool useSafeArea = true;

    private UIRoot uiRoot;
    private int lastWidth = -1;
    private int lastHeight = -1;
    private Rect lastSafe = new Rect(-1f, -1f, -1f, -1f);
    private Rect lastRect = new Rect(-1f, -1f, -1f, -1f);
    private RenderTexture lastTexture;

    public Camera UiCamera => uiCamera;
    public int RefWidth => refWidth;
    public int RefHeight => refHeight;
    public float ReferenceFov => refFov;
    public bool UseSafeArea => useSafeArea;

    private void Awake()
    {
        Cache();
        Apply();
    }

    private void OnEnable()
    {
        Cache();
        Apply();
    }

    private void OnValidate()
    {
        refWidth = Mathf.Max(1, refWidth);
        refHeight = Mathf.Max(1, refHeight);
        refFov = Mathf.Clamp(refFov, 1f, 179f);
        refSize = Mathf.Max(0.01f, refSize);

        if (isActiveAndEnabled)
            Apply();
    }

    private void LateUpdate()
    {
        int width;
        int height;
        Rect safeArea;
        GetSurface(out width, out height, out safeArea);
        RenderTexture targetTexture = uiCamera != null ? uiCamera.targetTexture : null;
        if (width != lastWidth || height != lastHeight ||
            safeArea != lastSafe || lastTexture != targetTexture ||
            (uiCamera != null && uiCamera.rect != lastRect))
        {
            Apply();
        }
    }

    public void Configure(Camera camera, int width = DefaultWidth, int height = DefaultHeight)
    {
        uiCamera = camera;
        refWidth = Mathf.Max(1, width);
        refHeight = Mathf.Max(1, height);

        if (uiCamera != null)
        {
            if (uiCamera.orthographic)
                refSize = Mathf.Max(0.01f, uiCamera.orthographicSize);
            else
            {
                float planeDistance = PlaneDistance();
                refFov = planeDistance > 0.01f
                    ? PerspectiveFov(width, height, (float)width / height, planeDistance)
                    : Mathf.Clamp(uiCamera.fieldOfView, 1f, 179f);
            }
        }

        Cache();
        SetupRoot();
    }

    public void Apply()
    {
        Cache();
        if (uiRoot == null || uiCamera == null)
            return;

        SetupRoot();
        uiRoot.UpdateScale(false);

        int surfaceWidth;
        int surfaceHeight;
        Rect safeArea;
        GetSurface(out surfaceWidth, out surfaceHeight, out safeArea);
        if (surfaceWidth <= 0 || surfaceHeight <= 0 || safeArea.width <= 0f || safeArea.height <= 0f)
            return;

        Rect viewport = Viewport(surfaceWidth, surfaceHeight, safeArea);
        uiCamera.rect = viewport;
        uiCamera.ResetAspect();

        float viewportAspect = safeArea.width / safeArea.height;
        int rootHeight = Mathf.Max(1, uiRoot.activeHeight);
        if (uiCamera.orthographic)
        {
            uiCamera.orthographicSize = OrthoSize(
                refWidth,
                refHeight,
                rootHeight,
                viewportAspect,
                refSize);
        }
        else
        {
            float planeDistance = PlaneDistance();
            uiCamera.fieldOfView = planeDistance > 0.01f
                ? PerspectiveFov(
                    refWidth,
                    refHeight,
                    viewportAspect,
                    planeDistance)
                : VerticalFov(
                    refFov,
                    (float)refWidth / refHeight,
                    viewportAspect);
        }

        lastWidth = surfaceWidth;
        lastHeight = surfaceHeight;
        lastSafe = safeArea;
        lastRect = viewport;
        lastTexture = uiCamera.targetTexture;
    }

    public static float VerticalFov(
        float refFov,
        float refAspect,
        float viewportAspect)
    {
        refFov = Mathf.Clamp(refFov, 1f, 179f);
        refAspect = Mathf.Max(0.01f, refAspect);
        viewportAspect = Mathf.Max(0.01f, viewportAspect);
        if (viewportAspect >= refAspect)
            return refFov;

        float refHalf = Mathf.Tan(refFov * 0.5f * Mathf.Deg2Rad);
        float fitHalf = refHalf * refAspect / viewportAspect;
        return Mathf.Atan(fitHalf) * 2f * Mathf.Rad2Deg;
    }

    public static int FitHeight(int width, int height, float viewportAspect)
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        viewportAspect = Mathf.Max(0.01f, viewportAspect);
        return Mathf.RoundToInt(Mathf.Max(height, width / viewportAspect));
    }

    public static float PerspectiveFov(
        int width,
        int height,
        float viewportAspect,
        float planeDepth)
    {
        int fitHeight = FitHeight(width, height, viewportAspect);
        float distance = Mathf.Max(0.01f, planeDepth);
        float fieldOfView = Mathf.Atan(fitHeight * 0.5f / distance) * 2f * Mathf.Rad2Deg;
        return Mathf.Clamp(fieldOfView, 1f, 179f);
    }

    public static float OrthoSize(
        int width,
        int height,
        int rootHeight,
        float viewportAspect,
        float referenceSize)
    {
        int fitHeight = FitHeight(width, height, viewportAspect);
        return Mathf.Max(0.01f, referenceSize) * fitHeight / Mathf.Max(1f, rootHeight);
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

    public static Rect Viewport(int width, int height, Rect safeArea)
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        Rect safeRect = ClampSafeArea(width, height, safeArea);
        return new Rect(
            safeRect.x / width,
            safeRect.y / height,
            safeRect.width / width,
            safeRect.height / height);
    }

    private void Cache()
    {
        if (uiRoot == null)
            uiRoot = GetComponent<UIRoot>();
        if (uiCamera == null)
            uiCamera = GetComponentInChildren<Camera>(true);
    }

    private void SetupRoot()
    {
        if (uiRoot == null)
            return;

        uiRoot.scalingStyle = UIRoot.Scaling.Constrained;
        uiRoot.manualWidth = refWidth;
        uiRoot.manualHeight = refHeight;
        uiRoot.fitWidth = true;
        uiRoot.fitHeight = true;
    }

    private float PlaneDistance()
    {
        if (uiCamera == null)
            return 0f;

        Vector3 cameraPos = transform.InverseTransformPoint(uiCamera.transform.position);
        Vector3 cameraDir = transform.InverseTransformDirection(uiCamera.transform.forward);
        if (cameraDir.sqrMagnitude < 0.0001f)
            return 0f;

        cameraDir.Normalize();
        return Vector3.Dot(-cameraPos, cameraDir);
    }

    private void GetSurface(out int width, out int height, out Rect safeArea)
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
        safeArea = useSafeArea
            ? Screen.safeArea
            : new Rect(0f, 0f, width, height);
        safeArea = ClampSafeArea(width, height, safeArea);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
