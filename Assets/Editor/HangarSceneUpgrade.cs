using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Explicit migration: it replaces scene content, so it is never run on import.
public static class HangarSceneUpgrade
{
    private static readonly Color Ink = new Color(0.90f, 0.95f, 0.98f);
    private static readonly Color Muted = new Color(0.52f, 0.65f, 0.73f);
    private static readonly Color Cyan = new Color(0.24f, 0.85f, 0.93f);

    public static void RunBatch()
    {
        try { Upgrade(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    [MenuItem("Tools/U2/更新机库并清理旧场景 UI")]
    public static void Upgrade()
    {
        var report = new StringBuilder();
        foreach (string name in new[] { "BeginScene", "GameScene" })
        {
            string path = "Assets/Scenes/" + name + ".unity";
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int before = SceneTransforms(scene).Length;
            if (name == "BeginScene") BuildHangar(Object.FindObjectOfType<ChoosePanel>(true));
            RemoveObsoleteVisuals(scene);
            ValidateReferences(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            report.AppendLine(name + ": " + before + " → " + SceneTransforms(scene).Length + " objects");
        }
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string captureDirectory = project.EndsWith("ValidationProject", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFullPath(Path.Combine(project, "..", "Captures"))
            : Path.Combine(project, "UIReview", "Captures");
        Directory.CreateDirectory(captureDirectory);
        File.WriteAllText(Path.Combine(captureDirectory, "hangar-cleanup.txt"), report.ToString());
        AssetDatabase.SaveAssets();
        Debug.Log(report.ToString());
    }

    private static Transform[] SceneTransforms(Scene scene)
    {
        return scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
    }

    private static void RemoveObsoleteVisuals(Scene scene)
    {
        var obsolete = new HashSet<string> { "Polish_SettingsVolumeHeader", "Polish_SettingsToggleHeader",
            "Polish_RankColumnRank", "Polish_RankColumnPlayer", "Polish_RankColumnRecord", "Polish_StageStatus",
            "Polish_HealthAccent", "Polish_SkillAccent", "labSkillHelp", "bkSet", "bkRank",
            "UGUI_RankSummary", "UGUI_RankColumnHeader" };
        foreach (Transform t in SceneTransforms(scene))
        {
            if (t == null) continue;
            // The black legacy sprite backing is superseded by Polish_Dialog.
            if (obsolete.Contains(t.name) || t.name == "spSK" || t.name == "bk")
                Object.DestroyImmediate(t.gameObject);
        }
        foreach (SettingPanel settings in SceneTransforms(scene).Select(t => t.GetComponent<SettingPanel>()).Where(p => p != null).ToArray())
        {
            foreach (string name in new[] { "labMusic", "labSound", "UGUI_MusicDescription", "UGUI_SoundDescription", "UGUI_MusicState", "UGUI_SoundState" })
                DeleteChild(settings.transform, name);
            foreach (Slider slider in new[] { settings.sliderMusic, settings.sliderSound })
            {
                if (slider == null || slider.transform.Find("UGUI_FillArea") != null) continue;
                slider.targetGraphic = null;
                slider.fillRect = null;
                slider.handleRect = null;
                RemoveLegacyControlGraphics(slider);
            }
            foreach (Toggle toggle in new[] { settings.toggleMusic, settings.toggleSound })
            {
                if (toggle == null || toggle.transform.Find("UGUI_ToggleTrack") != null) continue;
                toggle.targetGraphic = null;
                toggle.graphic = null;
                RemoveLegacyControlGraphics(toggle);
            }
        }
        foreach (RankPanel rank in SceneTransforms(scene).Select(t => t.GetComponent<RankPanel>()).Where(p => p != null).ToArray())
            foreach (string path in new[] { "polLeft", "spBk/labRank", "spBk/labName", "spBk/labTime", "spBk/ImageVisual" })
                DeleteChild(rank.transform, path);
    }

    private static void DeleteChild(Transform parent, string path)
    {
        Transform child = parent.Find(path);
        if (child != null) Object.DestroyImmediate(child.gameObject);
    }

    private static void RemoveLegacyControlGraphics(Selectable control)
    {
        // The current slider/toggle renderer creates its own graphics. Remove the
        // superseded NGUI sprites rather than keeping disabled copies in scenes.
        for (int i = control.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(control.transform.GetChild(i).gameObject);
        foreach (BaseMeshEffect effect in control.GetComponents<BaseMeshEffect>()) Object.DestroyImmediate(effect);
        foreach (Graphic graphic in control.GetComponents<Graphic>()) Object.DestroyImmediate(graphic);
        CanvasRenderer renderer = control.GetComponent<CanvasRenderer>();
        if (renderer != null) Object.DestroyImmediate(renderer);
    }

    public static void ValidateReferences(Scene scene)
    {
        foreach (Transform t in SceneTransforms(scene))
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) != 0)
                throw new InvalidOperationException("Missing script: " + t.name);
            foreach (MonoBehaviour component in t.GetComponents<MonoBehaviour>())
            {
                if (component == null) continue;
                var property = new SerializedObject(component).GetIterator();
                while (property.NextVisible(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference &&
                        property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                        throw new InvalidOperationException("Broken reference: " + t.name + "." + property.propertyPath);
            }
        }
    }

    private static void BuildHangar(ChoosePanel choose)
    {
        Transform view = UGUIRuntime.View(choose);
        for (int i = view.childCount - 1; i >= 0; i--) Object.DestroyImmediate(view.GetChild(i).gameObject);
        RectTransform surface = Graphic(view, "HangarSurface", Vector2.zero, 1560, 880, HangarGraphic.Shape.Surface, Cyan).rectTransform;
        choose.btnClose = Button(surface, "btnClose", -706, 355, 52, 48, "←");
        Text(surface, "HangarHeading", -485, 368, 350, 46, "选择战机", 34, Ink);
        Text(surface, "HangarKicker", -485, 330, 350, 24, "作战机库  /  AIRCRAFT HANGAR", 14, Muted);
        Text(surface, "HangarReady", 590, 355, 260, 26, "●  机库已就绪", 17, Cyan, TextAnchor.MiddleRight);
        Line(surface, "HeaderRule", 0, 301, 1456, 1, new Color(0.14f, 0.27f, 0.34f));
        Line(surface, "ColumnRule", 275, -30, 1, 610, new Color(0.14f, 0.27f, 0.34f));

        Text(surface, "AircraftDesignation", -482, 257, 390, 24, "STRIKE CRAFT / 01", 14, Cyan);
        choose.labHero = Text(surface, "AircraftName", -482, 211, 390, 58, "苍穹", 44, Ink);
        Text(surface, "AircraftClass", -482, 167, 390, 30, "均衡突击机", 18, Muted);
        Text(surface, "AircraftCounter", 120, 247, 180, 26, "01  /  05", 18, Muted, TextAnchor.MiddleRight);
        Graphic(surface, "PreviewStage", new Vector2(-225, -35), 850, 360, HangarGraphic.Shape.Stage, Cyan);
        RectTransform preview = UGUIRuntime.CreateRect(surface, "AircraftPreview", new Vector2(-225, -14), 790, 380);
        RawImage raw = preview.gameObject.AddComponent<RawImage>();
        raw.raycastTarget = true;
        choose.aircraftPreview = preview.gameObject.AddComponent<AircraftPreview>();
        choose.btnLeft = Button(surface, "btnLeft", -705, -24, 46, 56, "‹");
        choose.btnRight = Button(surface, "btnRight", 223, -24, 46, 56, "›");
        Text(surface, "OrbitInstruction", -415, -234, 400, 26, "拖动战机可旋转查看", 15, Muted);
        Button(surface, "ResetAircraftView", 100, -234, 160, 32, "重置视角");

        Text(surface, "SpecificationHeading", 508, 249, 386, 34, "机体性能", 24, Ink);
        Meter(surface, "Durability", "耐久", "机体装甲", 183, new Color(0.23f, 0.85f, 0.65f));
        Meter(surface, "Mobility", "机动", "飞行速度", 105, new Color(0.25f, 0.69f, 1f));
        Meter(surface, "Firepower", "火力", "弹幕威力", 27, new Color(1f, 0.62f, 0.29f));
        Line(surface, "AbilityRule", 508, -28, 386, 1, new Color(0.14f, 0.27f, 0.34f));
        Text(surface, "AbilityName", 508, -65, 386, 32, "爆破清屏", 23, Cyan);
        Text(surface, "AbilityDescription", 508, -107, 386, 46, "", 18, Ink);
        Text(surface, "AbilityCooldown", 508, -150, 386, 26, "", 16, Muted);
        Text(surface, "AircraftAffinity", 508, -208, 386, 62, "", 17, Ink);
        Text(surface, "AircraftRecommendation", 508, -257, 386, 26, "", 15, Muted);
        Text(surface, "SelectedAircraft", 508, -310, 386, 24, "", 15, Muted, TextAnchor.MiddleCenter);
        choose.btnStart = Button(surface, "btnStart", 508, -359, 386, 62, "确认战机  →", true);

        for (int i = 0; i < 5; i++)
        {
            Button slot = Button(surface, "AircraftSlot" + i, -607 + i * 183, -338, 171, 104, "");
            slot.GetComponent<HangarGraphic>().Configure(HangarGraphic.Shape.Slot, Cyan);
            Object.DestroyImmediate(slot.GetComponentInChildren<Text>().gameObject);
            Text(slot.transform, "AircraftNumber", -53, 28, 34, 22, "0" + (i + 1), 14, Cyan);
            Text(slot.transform, "AircraftCallsign", 2, -1, 140, 32, ChoosePanel.AircraftNames[i], 24, Ink);
            Text(slot.transform, "AircraftType", 2, -30, 145, 24, ChoosePanel.AircraftClasses[i], 14, Muted);
        }
    }

    private static void Meter(Transform parent, string key, string label, string description, float y, Color tint)
    {
        Text(parent, key + "Name", 363, y + 4, 96, 30, label, 20, Ink);
        Text(parent, key + "Explanation", 504, y + 4, 170, 26, description, 14, Muted);
        Text(parent, key + "Value", 653, y + 4, 96, 30, "04 / 10", 20, tint, TextAnchor.MiddleRight);
        Graphic(parent, key + "Meter", new Vector2(508, y - 25), 386, 7, HangarGraphic.Shape.Meter, tint);
    }

    private static HangarGraphic Graphic(Transform parent, string name, Vector2 position, int width, int height, HangarGraphic.Shape shape, Color tint)
    {
        RectTransform rect = UGUIRuntime.CreateRect(parent, name, position, width, height);
        HangarGraphic graphic = rect.gameObject.AddComponent<HangarGraphic>();
        graphic.Configure(shape, tint);
        graphic.raycastTarget = shape == HangarGraphic.Shape.Surface;
        return graphic;
    }

    private static Button Button(Transform parent, string name, float x, float y, int width, int height, string label, bool primary = false)
    {
        HangarGraphic graphic = Graphic(parent, name, new Vector2(x, y), width, height, HangarGraphic.Shape.Button, Cyan);
        graphic.raycastTarget = true;
        graphic.SetSelected(primary);
        Button button = graphic.gameObject.AddComponent<Button>();
        button.targetGraphic = graphic;
        button.colors = new ColorBlock { normalColor = Color.white, highlightedColor = new Color(1.18f, 1.18f, 1.18f),
            pressedColor = new Color(0.65f, 0.85f, 0.91f), selectedColor = Color.white,
            disabledColor = new Color(0.45f, 0.5f, 0.55f), colorMultiplier = 1f, fadeDuration = 0.12f };
        Text(button.transform, "ButtonLabel", 0, 0, width - 16, height - 4, label, height > 40 ? 23 : 15, Ink, TextAnchor.MiddleCenter);
        return button;
    }

    private static Text Text(Transform parent, string name, float x, float y, int width, int height, string value, int size, Color tint, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        Text text = UGUIRuntime.CreateText(parent, name, new Vector2(x, y), width, height, value, size, tint, alignment);
        text.resizeTextForBestFit = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        return text;
    }

    private static void Line(Transform parent, string name, float x, float y, int width, int height, Color tint)
    {
        UGUIRuntime.CreateImage(parent, name, new Vector2(x, y), width, height, null, null, tint, false);
    }
}
