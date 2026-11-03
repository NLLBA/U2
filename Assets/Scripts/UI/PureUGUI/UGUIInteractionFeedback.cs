using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class UGUIInteractionFeedback : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler
{
    private Selectable selectable;
    private RectTransform motionTarget;
    private Outline outline;
    private bool allowScale;
    private Vector3 restScale = Vector3.one;
    private bool hovered;
    private bool pressed;
    private bool selected;

    private void Awake()
    {
        Cache();
    }

    private void OnEnable()
    {
        Cache();
        ApplyImmediate();
    }

    private void OnDisable()
    {
        hovered = false;
        pressed = false;
        selected = false;
        if (motionTarget != null)
            motionTarget.localScale = restScale;
    }

    private void Update()
    {
        if (selectable == null || motionTarget == null)
            return;

        float factor = 1f;
        if (allowScale && selectable.interactable)
        {
            // Keep the response tactile without changing layout geometry. A larger
            // scale makes rows and modal panels appear to shake, especially when a
            // Toggle resolves its parent as the old control root.
            if (pressed)
                factor = 0.985f;
        }

        float blend = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
        motionTarget.localScale = Vector3.Lerp(motionTarget.localScale, restScale * factor, blend);

        if (outline != null)
        {
            float alpha = !selectable.interactable ? 0.04f :
                (pressed ? 0.5f : (hovered || selected ? 0.34f : 0f));
            Color color = new Color(0.2f, 0.92f, 1f, alpha);
            outline.effectColor = Color.Lerp(outline.effectColor, color, blend);
        }
    }

    public void OnPointerEnter(PointerEventData eventData) { hovered = true; }
    public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; }
    public void OnPointerDown(PointerEventData eventData) { pressed = true; }
    public void OnPointerUp(PointerEventData eventData) { pressed = false; }
    public void OnSelect(BaseEventData eventData) { selected = true; }
    public void OnDeselect(BaseEventData eventData) { selected = false; pressed = false; }

    private void Cache()
    {
        selectable = GetComponent<Selectable>();
        if (selectable == null)
            return;

        allowScale = selectable is Button;
        // Toggles, sliders and input fields must never animate their parent. Their
        // parent is often the whole dialog or settings canvas.
        GameObject root = allowScale ? UGUIRuntime.ControlRoot(selectable) : selectable.gameObject;
        motionTarget = root != null ? root.transform as RectTransform : transform as RectTransform;
        if (motionTarget != null && restScale == Vector3.one)
            restScale = motionTarget.localScale;

        Graphic graphic = selectable.targetGraphic;
        if (graphic == null)
            return;

        outline = graphic.GetComponent<Outline>();
        if (outline == null)
            outline = graphic.gameObject.AddComponent<Outline>();
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;
    }

    private void ApplyImmediate()
    {
        if (motionTarget != null)
            motionTarget.localScale = restScale;
        if (outline != null)
            outline.effectColor = new Color(0.2f, 0.92f, 1f, 0.16f);
    }
}
