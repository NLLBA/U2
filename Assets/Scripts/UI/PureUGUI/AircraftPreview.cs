using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

// The preview is composited inside the canvas. It never changes a scene camera or
// a canvas render mode, and never instantiates gameplay physics or effects.
[RequireComponent(typeof(RawImage))]
public sealed class AircraftPreview : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    public const int PreviewLayer = 8;
    private const float Elevation = 32f;
    private readonly List<Material> ownedMaterials = new List<Material>();
    private GameObject rig;
    private Transform pivot;
    private Camera previewCamera;
    private RenderTexture texture;
    private Bounds modelBounds;

    public Transform Pivot => pivot;
    public Camera PreviewCamera => previewCamera;
    public Bounds ModelBounds => modelBounds;

    public void SetAircraft(GameObject prefab)
    {
        Release();
        if (prefab == null) return;
        rig = new GameObject("AircraftPreviewRig");
        rig.transform.position = new Vector3(0f, -10000f, 0f);
        pivot = new GameObject("CenteredAircraftPivot").transform;
        pivot.SetParent(rig.transform, false);

        bool hasBounds = false;
        foreach (MeshFilter source in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            MeshRenderer sourceRenderer = source.GetComponent<MeshRenderer>();
            if (source.sharedMesh == null || sourceRenderer == null) continue;
            GameObject mesh = new GameObject(source.name, typeof(MeshFilter), typeof(MeshRenderer));
            mesh.layer = PreviewLayer;
            mesh.transform.SetParent(pivot, false);
            mesh.transform.localPosition = prefab.transform.InverseTransformPoint(source.transform.position);
            mesh.transform.localRotation = Quaternion.Inverse(prefab.transform.rotation) * source.transform.rotation;
            mesh.transform.localScale = source.transform.lossyScale;
            mesh.GetComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            MeshRenderer renderer = mesh.GetComponent<MeshRenderer>();
            var materials = new List<Material>();
            foreach (Material original in sourceRenderer.sharedMaterials)
            {
                if (original == null) continue;
                Texture albedo = original.HasProperty("_MainTex") ? original.mainTexture : null;
                Material material = new Material(Shader.Find(albedo != null ? "Unlit/Texture" : "Unlit/Color"));
                if (albedo != null) material.mainTexture = albedo;
                else material.color = original.HasProperty("_Color") ? original.color : Color.white;
                ownedMaterials.Add(material);
                materials.Add(material);
            }
            renderer.sharedMaterials = materials.ToArray();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            foreach (Vector3 corner in Corners(source.sharedMesh.bounds))
            {
                Vector3 point = pivot.InverseTransformPoint(mesh.transform.TransformPoint(corner));
                if (!hasBounds) { modelBounds = new Bounds(point, Vector3.zero); hasBounds = true; }
                else modelBounds.Encapsulate(point);
            }
        }
        foreach (Transform mesh in pivot) mesh.localPosition -= modelBounds.center;
        modelBounds.center = Vector3.zero;

        previewCamera = new GameObject("AircraftPreviewCamera", typeof(Camera)).GetComponent<Camera>();
        previewCamera.transform.SetParent(rig.transform, false);
        previewCamera.enabled = false;
        previewCamera.orthographic = true;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = Color.clear;
        previewCamera.cullingMask = 1 << PreviewLayer;
        previewCamera.allowHDR = false;
        previewCamera.allowMSAA = true;
        float distance = Mathf.Max(10f, modelBounds.size.magnitude * 2f);
        previewCamera.transform.localRotation = Quaternion.Euler(Elevation, 155f, 0f);
        previewCamera.transform.localPosition = -previewCamera.transform.forward * distance;
        previewCamera.nearClipPlane = 0.1f;
        previewCamera.farClipPlane = distance * 3f;
        ResetView();
        RenderNow();
    }

    public void ResetView() { SetYaw(0f); }

    public void SetYaw(float degrees)
    {
        if (pivot != null) pivot.localRotation = Quaternion.Euler(0f, degrees, 0f);
    }

    public void OnBeginDrag(PointerEventData eventData) { }

    public void OnDrag(PointerEventData eventData)
    {
        if (pivot == null) return;
        Canvas canvas = GetComponentInParent<Canvas>();
        float width = ((RectTransform)transform).rect.width * (canvas != null ? canvas.scaleFactor : 1f);
        SetYaw(pivot.localEulerAngles.y - eventData.delta.x / Mathf.Max(1f, width) * 240f);
    }

    private void LateUpdate() { RenderNow(); }

    public void RenderNow()
    {
        if (previewCamera == null || !isActiveAndEnabled) return;
        Rect frame = ((RectTransform)transform).rect;
        float aspect = Mathf.Max(0.1f, frame.width / Mathf.Max(1f, frame.height));
        int width = 1280;
        int height = Mathf.Clamp(Mathf.RoundToInt(width / aspect), 256, 1280);
        if (texture == null || texture.width != width || texture.height != height)
        {
            ReleaseTexture();
            texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) {
                name = "AircraftPreviewTexture", antiAliasing = 4
            };
            texture.Create();
            GetComponent<RawImage>().texture = texture;
            previewCamera.targetTexture = texture;
        }
        previewCamera.aspect = aspect;
        // Fit the complete yaw envelope once, so neither size nor position jumps
        // while dragging. Bounds are centered before any rotation is applied.
        Vector3 extents = modelBounds.extents;
        float radius = new Vector2(extents.x, extents.z).magnitude;
        float halfHeight = radius * Mathf.Sin(Elevation * Mathf.Deg2Rad) + extents.y * Mathf.Cos(Elevation * Mathf.Deg2Rad);
        previewCamera.orthographicSize = Mathf.Max(0.1f, Mathf.Max(halfHeight / 0.82f, radius / (aspect * 0.84f)));
        previewCamera.Render();
    }

    public Rect ProjectedModelBounds()
    {
        Vector2 min = Vector2.one * float.PositiveInfinity;
        Vector2 max = Vector2.one * float.NegativeInfinity;
        foreach (Vector3 corner in Corners(modelBounds))
        {
            Vector2 point = previewCamera.WorldToViewportPoint(pivot.TransformPoint(corner));
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static IEnumerable<Vector3> Corners(Bounds bounds)
    {
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
            yield return bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
    }

    private void ReleaseTexture()
    {
        if (texture == null) return;
        if (previewCamera != null) previewCamera.targetTexture = null;
        GetComponent<RawImage>().texture = null;
        texture.Release();
        Destroy(texture);
        texture = null;
    }

    private void Release()
    {
        ReleaseTexture();
        if (rig != null) { rig.SetActive(false); Destroy(rig); }
        foreach (Material material in ownedMaterials) Destroy(material);
        ownedMaterials.Clear();
        rig = null;
        pivot = null;
        previewCamera = null;
    }

    private void OnDisable() { Release(); }
    private void OnDestroy() { Release(); }
}
