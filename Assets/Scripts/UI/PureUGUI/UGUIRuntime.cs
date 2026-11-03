using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class UGUIRuntime
{
    private static readonly Regex LegacyColor = new Regex(@"\[([0-9a-fA-F]{6}(?:[0-9a-fA-F]{2})?)\]|\[(-)\]");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureEventSystem();
    }

    public static Canvas Canvas
    {
        get
        {
            Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
            for (int i = 0; i < canvases.Length; i++)
                if (canvases[i].name == "UICanvas" && canvases[i].gameObject.scene.IsValid() &&
                    canvases[i].gameObject.scene.isLoaded) return canvases[i];
            return UnityEngine.Object.FindObjectOfType<Canvas>(true);
        }
    }

    public static Transform Root
    {
        get
        {
            Canvas canvas = Canvas;
            if (canvas == null)
                return null;
            UGUIAspectSafeLayout layout = canvas.GetComponent<UGUIAspectSafeLayout>();
            RectTransform contentRoot = layout != null ? layout.ContentRoot : null;
            return contentRoot != null ? contentRoot : canvas.transform;
        }
    }

    public static Transform View(Component controller)
    {
        if (controller == null) return Root;
        return controller.transform;
    }

    public static GameObject ControlRoot(Selectable control)
    {
        if (control == null) return null;
        if (!(control is Button))
            return control.gameObject;
        if (control.GetComponentInChildren<Text>(true) != null)
            return control.gameObject;
        Transform parent = control.transform.parent;
        return parent != null ? parent.gameObject : control.gameObject;
    }

    public static Text Label(Button button)
    {
        if (button == null) return null;
        Text ownLabel = button.GetComponentInChildren<Text>(true);
        if (ownLabel != null) return ownLabel;
        GameObject root = ControlRoot(button);
        return root != null ? root.GetComponentInChildren<Text>(true) : null;
    }

    public static T Find<T>(Transform root, string name) where T : Component
    {
        if (root == null) return null;
        T[] components = root.GetComponentsInChildren<T>(true);
        for (int i = 0; i < components.Length; i++)
            if (components[i].name == name || components[i].transform.parent != null && components[i].transform.parent.name == name)
                return components[i];
        return null;
    }

    public static GameObject FindObject(Transform root, string name)
    {
        if (root == null) return null;
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
            if (transforms[i].name == name) return transforms[i].gameObject;
        return null;
    }

    public static RectTransform CreateRect(Transform parent, string name, Vector3 position, float width, float height)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition3D = position;
        rect.sizeDelta = new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height));
        return rect;
    }

    public static Image CreateImage(Transform parent, string name, Vector3 position, int width, int height,
        Image template, Sprite sprite, Color color, bool raycastTarget)
    {
        RectTransform rect = CreateRect(parent, name, position, width, height);
        Image image = rect.gameObject.AddComponent<Image>();
        if (template != null)
        {
            image.sprite = template.sprite;
            image.material = template.material;
            image.type = template.type;
            image.preserveAspect = template.preserveAspect;
        }
        if (sprite != null) image.sprite = sprite;
        image.type = image.sprite != null && image.sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : image.type;
        image.color = color;
        image.raycastTarget = raycastTarget;
        return image;
    }

    public static Text CreateText(Transform parent, string name, Vector3 position, int width, int height,
        string value, int size, Color color, TextAnchor alignment, Text template = null)
    {
        RectTransform rect = CreateRect(parent, name, position, width, height);
        Text text = rect.gameObject.AddComponent<Text>();
        // Runtime-created screens should share one readable font. Cloning the
        // legacy scene font made every dynamic panel inherit the oversized display
        // face and produced the mismatched typography seen in the redesigned UI.
        text.font = ChineseFontFallback.Font;
        if (template != null)
        {
            text.fontStyle = FontStyle.Normal;
            text.material = template.material;
            text.lineSpacing = template.lineSpacing;
        }
        text.text = ConvertLegacyRichText(value);
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.supportRichText = true;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Min(8, size);
        text.resizeTextMaxSize = size;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    public static Button CreateButton(Transform parent, string name, Vector3 position, int width, int height,
        string label, Color labelColor, Button template = null, Text textTemplate = null)
    {
        Image templateImage = template != null ? template.targetGraphic as Image : null;
        Image image = CreateImage(parent, name, position, width, height, templateImage, null, Color.white, true);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        if (template != null) button.colors = template.colors;
        Text text = CreateText(image.transform, name + "Label", Vector3.zero, width - 24, height - 8,
            label, textTemplate != null ? textTemplate.fontSize : 20, labelColor, TextAnchor.MiddleCenter, textTemplate);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(12f, 4f);
        text.rectTransform.offsetMax = new Vector2(-12f, -4f);
        UGUIVisualSystem.Style(button);
        return button;
    }

    public static InputField CreateInput(Transform parent, string name, Vector3 position, int width, int height,
        string placeholderText, string initialValue, int limit, Image template, Text textTemplate)
    {
        Image image = CreateImage(parent, name, position, width, height, template, null, Color.white, true);
        Text text = CreateText(image.transform, name + "Text", Vector3.zero, width - 36, height - 8,
            initialValue, textTemplate != null ? textTemplate.fontSize : 20, Color.white, TextAnchor.MiddleLeft, textTemplate);
        StretchWithPadding(text.rectTransform, 18f, 4f);
        Text placeholder = CreateText(image.transform, name + "Placeholder", Vector3.zero, width - 36, height - 8,
            placeholderText, text.fontSize, new Color(0.55f, 0.65f, 0.7f, 1f), TextAnchor.MiddleLeft, textTemplate);
        StretchWithPadding(placeholder.rectTransform, 18f, 4f);
        InputField input = image.gameObject.AddComponent<InputField>();
        input.targetGraphic = image;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.characterLimit = limit;
        input.SetTextWithoutNotify(initialValue ?? string.Empty);
        return input;
    }

    public static void StretchWithPadding(RectTransform rect, float horizontal, float vertical)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(horizontal, vertical);
        rect.offsetMax = new Vector2(-horizontal, -vertical);
    }

    public static void SetSize(Component component, float width, float height)
    {
        RectTransform rect = component != null ? component.transform as RectTransform : null;
        if (rect != null) rect.sizeDelta = new Vector2(width, height);
    }

    public static void SetPosition(Component component, Vector3 position)
    {
        RectTransform rect = component != null ? component.transform as RectTransform : null;
        if (rect != null) rect.anchoredPosition3D = position;
    }

    public static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    public static string ConvertLegacyRichText(string source)
    {
        int open = 0;
        string result = LegacyColor.Replace(source ?? string.Empty, match =>
        {
            if (match.Groups[1].Success)
            {
                open++;
                return "<color=#" + match.Groups[1].Value + ">";
            }
            if (open == 0) return string.Empty;
            open--;
            return "</color>";
        });
        while (open-- > 0) result += "</color>";
        return result;
    }

    public static void SetText(Text label, string value)
    {
        if (label == null)
            return;
        string converted = ConvertLegacyRichText(value);
        if (label.text != converted)
            label.text = converted;
    }
}
