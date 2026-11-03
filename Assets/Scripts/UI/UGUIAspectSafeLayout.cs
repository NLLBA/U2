using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(-200)]
public sealed class UGUIAspectSafeLayout : MonoBehaviour
{
    public const int DefaultWidth = 1920;
    public const int DefaultHeight = 1080;
    private const string AdaptiveRootName = "UGUI_AdaptiveRoot";

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

    private Canvas canvas;
    private CanvasScaler canvasScaler;
    private RectTransform adaptiveRoot;
    private int lastWidth = -1;
    private int lastHeight = -1;
    private Rect lastSafe = new Rect(-1f, -1f, -1f, -1f);

    public Camera UiCamera => uiCamera;
    public int RefWidth => refWidth;
    public int RefHeight => refHeight;
    public float ReferenceFov => refFov;
    public bool UseSafeArea => useSafeArea;
    public RectTransform ContentRoot
    {
        get
        {
            Cache();
            SetupRoot();
            if (!Application.isPlaying || canvas == null)
                return canvas != null ? canvas.transform as RectTransform : null;
            EnsureAdaptiveRoot();
            return adaptiveRoot;
        }
    }

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
        if (Application.isPlaying)
        {
            EnsureAdaptiveRoot();
            AdoptCanvasChildren();
        }

        int width;
        int height;
        Rect safeArea;
        GetSurface(out width, out height, out safeArea);
        if (width != lastWidth || height != lastHeight ||
            safeArea != lastSafe)
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
        if (canvas == null)
            return;

        SetupRoot();

        int surfaceWidth;
        int surfaceHeight;
        Rect safeArea;
        GetSurface(out surfaceWidth, out surfaceHeight, out safeArea);
        if (surfaceWidth <= 0 || surfaceHeight <= 0 || safeArea.width <= 0f || safeArea.height <= 0f)
            return;

        if (Application.isPlaying)
        {
            EnsureAdaptiveRoot();
            AdoptCanvasChildren();
            LayoutAdaptiveRoot(surfaceWidth, surfaceHeight, safeArea);
        }

        lastWidth = surfaceWidth;
        lastHeight = surfaceHeight;
        lastSafe = safeArea;
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

    public static float ExpandScale(int surfaceWidth, int surfaceHeight, int width, int height)
    {
        surfaceWidth = Mathf.Max(1, surfaceWidth);
        surfaceHeight = Mathf.Max(1, surfaceHeight);
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        return Mathf.Max(0.0001f, Mathf.Min(
            (float)surfaceWidth / width,
            (float)surfaceHeight / height));
    }

    public static Vector2 ExpandedCanvasSize(int surfaceWidth, int surfaceHeight, int width, int height)
    {
        float scale = ExpandScale(surfaceWidth, surfaceHeight, width, height);
        return new Vector2(surfaceWidth / scale, surfaceHeight / scale);
    }

    public static float SafeContentScale(
        int surfaceWidth,
        int surfaceHeight,
        Rect safeArea,
        int width = DefaultWidth,
        int height = DefaultHeight)
    {
        Rect safeRect = ClampSafeArea(surfaceWidth, surfaceHeight, safeArea);
        float canvasScale = ExpandScale(surfaceWidth, surfaceHeight, width, height);
        float availableWidth = safeRect.width / canvasScale;
        float availableHeight = safeRect.height / canvasScale;
        return Mathf.Clamp01(Mathf.Min(availableWidth / Mathf.Max(1, width),
            availableHeight / Mathf.Max(1, height)));
    }

    public static Vector2 SafeContentOffset(
        int surfaceWidth,
        int surfaceHeight,
        Rect safeArea,
        int width = DefaultWidth,
        int height = DefaultHeight)
    {
        Rect safeRect = ClampSafeArea(surfaceWidth, surfaceHeight, safeArea);
        float canvasScale = ExpandScale(surfaceWidth, surfaceHeight, width, height);
        Vector2 screenCenter = new Vector2(surfaceWidth * 0.5f, surfaceHeight * 0.5f);
        return (safeRect.center - screenCenter) / canvasScale;
    }

    private void Cache()
    {
        if (canvas == null)
            canvas = UGUIRuntime.Canvas;
        if (canvas != null && canvasScaler == null)
            canvasScaler = canvas.GetComponent<CanvasScaler>();
        if (canvas != null && adaptiveRoot == null)
        {
            Transform existing = canvas.transform.Find(AdaptiveRootName);
            adaptiveRoot = existing as RectTransform;
        }
        if (uiCamera == null)
            uiCamera = GetComponentInChildren<Camera>(true);
    }

    private void SetupRoot()
    {
        if (canvas == null)
            return;
        if (canvasScaler == null)
            canvasScaler = canvas.gameObject.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(refWidth, refHeight);
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
    }

    private void EnsureAdaptiveRoot()
    {
        if (canvas == null || adaptiveRoot != null || !Application.isPlaying)
            return;

        GameObject rootObject = new GameObject(AdaptiveRootName, typeof(RectTransform));
        rootObject.layer = canvas.gameObject.layer;
        adaptiveRoot = rootObject.GetComponent<RectTransform>();
        adaptiveRoot.SetParent(canvas.transform, false);
        adaptiveRoot.anchorMin = adaptiveRoot.anchorMax = new Vector2(0.5f, 0.5f);
        adaptiveRoot.pivot = new Vector2(0.5f, 0.5f);
        adaptiveRoot.anchoredPosition = Vector2.zero;
        adaptiveRoot.sizeDelta = new Vector2(refWidth, refHeight);
        AdoptCanvasChildren();
    }

    private void AdoptCanvasChildren()
    {
        if (canvas == null || adaptiveRoot == null)
            return;

        List<Transform> children = new List<Transform>();
        for (int i = 0; i < canvas.transform.childCount; i++)
        {
            Transform child = canvas.transform.GetChild(i);
            if (child != adaptiveRoot)
                children.Add(child);
        }
        for (int i = 0; i < children.Count; i++)
            children[i].SetParent(adaptiveRoot, false);

        for (int i = 0; i < adaptiveRoot.childCount; i++)
        {
            RectTransform childRect = adaptiveRoot.GetChild(i) as RectTransform;
            if (childRect == null)
                continue;
            Stretch(childRect);
            NormalizeScreenFillers(childRect);
        }
    }

    private static void NormalizeScreenFillers(RectTransform view)
    {
        RectTransform[] descendants = view.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            RectTransform filler = descendants[i];
            if (filler.name.IndexOf("Scrim", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            Stretch(filler);
            for (int childIndex = 0; childIndex < filler.childCount; childIndex++)
            {
                RectTransform visual = filler.GetChild(childIndex) as RectTransform;
                if (visual != null && visual.GetComponent<Graphic>() != null)
                    Stretch(visual);
            }
        }
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private void LayoutAdaptiveRoot(int surfaceWidth, int surfaceHeight, Rect safeArea)
    {
        if (adaptiveRoot == null)
            return;

        adaptiveRoot.anchorMin = adaptiveRoot.anchorMax = new Vector2(0.5f, 0.5f);
        adaptiveRoot.pivot = new Vector2(0.5f, 0.5f);
        adaptiveRoot.sizeDelta = ExpandedCanvasSize(
            surfaceWidth, surfaceHeight, refWidth, refHeight);
        adaptiveRoot.anchoredPosition = useSafeArea
            ? SafeContentOffset(surfaceWidth, surfaceHeight, safeArea, refWidth, refHeight)
            : Vector2.zero;
        float scale = useSafeArea
            ? SafeContentScale(surfaceWidth, surfaceHeight, safeArea, refWidth, refHeight)
            : 1f;
        adaptiveRoot.localScale = new Vector3(scale, scale, 1f);
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
