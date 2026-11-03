using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class CombatUISceneUpgrade
{
    private static readonly Color Ink = new Color(0.90f, 0.95f, 0.98f);
    private static readonly Color Muted = new Color(0.48f, 0.64f, 0.72f);
    private static readonly Color Cyan = new Color(0.20f, 0.86f, 0.94f);
    private static readonly Color Orange = new Color(1f, 0.58f, 0.24f);
    private static readonly Color Red = new Color(1f, 0.31f, 0.25f);

    [MenuItem("Tools/U2/重建战斗与结算 UI")]
    public static void Upgrade()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Single);
        int before = Count(scene);
        BuildHud(Object.FindObjectOfType<GamePanel>(true));
        BuildPause(Object.FindObjectOfType<QuitPanel>(true));
        BuildGameOver(Object.FindObjectOfType<GameOverPanel>(true));
        HangarSceneUpgrade.ValidateReferences(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string capture = project.EndsWith("ValidationProject", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFullPath(Path.Combine(project, "..", "Captures"))
            : Path.Combine(project, "UIReview", "Captures");
        Directory.CreateDirectory(capture);
        File.WriteAllText(Path.Combine(capture, "combat-ui-cleanup.txt"),
            "GameScene: " + before + " -> " + Count(scene) + " objects\n" +
            "Rebuilt: combat HUD, pause modal and endless-run result modal.\n" +
            "Old panel children were deleted rather than hidden.\n");
    }

    public static void RunBatch()
    {
        try { Upgrade(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    private static int Count(Scene scene)
    {
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
            count += root.GetComponentsInChildren<Transform>(true).Length;
        return count;
    }

    private static void BuildHud(GamePanel panel)
    {
        Transform root = ResetView(panel, true);
        RectTransform mission = Surface(root, "CombatMission", new Vector2(-700f, 458f), 480, 118,
            CombatGraphic.Shape.Hud, Cyan).rectTransform;
        Text(mission, "MissionKicker", 0, 38, 424, 20, "任务 / MISSION", 13, Cyan);
        panel.labLevel = Text(mission, "MissionTitle", 0, 7, 424, 42, "当前任务", 21, Ink);
        panel.labDifficulty = Text(mission, "MissionStatus", 0, -36, 424, 24, "难度与得分", 14, Muted);

        RectTransform timer = Surface(root, "CombatTimer", new Vector2(0f, 486f), 196, 82,
            CombatGraphic.Shape.Timer, Cyan).rectTransform;
        Text(timer, "TimerCaption", 0, 20, 160, 20, "生存时间", 13, Muted, TextAnchor.MiddleCenter);
        panel.labTime = Text(timer, "TimerValue", 0, -11, 170, 42, "0s", 31, Ink, TextAnchor.MiddleCenter);

        panel.btnBack = Button(root, "CombatPause", 900, 486, 56, 56, "Ⅱ", Cyan, false);

        RectTransform encounterPanel = Surface(root, "CombatEncounter", new Vector2(0f, 392f), 650, 78,
            CombatGraphic.Shape.Hud, Cyan).rectTransform;
        Text encounter = Text(encounterPanel, "labEncounter", 0, 15, 606, 38,
            "战术阶段", 16, Cyan, TextAnchor.MiddleCenter);
        Text challenges = Text(encounterPanel, "labChallengeProgress", 0, -24, 606, 22,
            string.Empty, 13, Muted, TextAnchor.MiddleCenter);
        challenges.gameObject.SetActive(false);

        panel.labBossWarning = Text(root, "CombatNotice", 0, 305, 940, 76,
            string.Empty, 27, Cyan, TextAnchor.MiddleCenter);
        Text feedback = Text(root, "CombatFeedback", 0, 245, 780, 46,
            string.Empty, 21, Ink, TextAnchor.MiddleCenter);
        feedback.gameObject.SetActive(false);

        RectTransform health = Surface(root, "CombatHealth", new Vector2(-700f, -493f), 500, 82,
            CombatGraphic.Shape.Hud, Cyan).rectTransform;
        Text healthCaption = Text(health, "CombatHealthCaption", -2, 21, 440, 22,
            "机体完整度   05 / 10", 15, Ink);
        panel.hpObjs.Clear();
        for (int i = 0; i < 10; i++)
        {
            Image segment = UGUIRuntime.CreateImage(health, "HealthSegment" + i,
                new Vector3(-198f + i * 44f, -17f), 38, 13, null, null,
                new Color(0.22f, 0.82f, 0.88f), false);
            panel.hpObjs.Add(segment.gameObject);
        }

        RectTransform skill = Surface(root, "CombatSkills", new Vector2(700f, -493f), 500, 82,
            CombatGraphic.Shape.Hud, Cyan).rectTransform;
        panel.labSkill = Text(skill, "SkillStatus", 0, 23, 440, 22,
            "E 护盾可用    Q 战机技能可用", 15, Ink, TextAnchor.MiddleCenter);
        Surface(skill, "ShieldKey", new Vector2(-145f, -18f), 44, 32,
            CombatGraphic.Shape.Keycap, Cyan);
        panel.labShieldIcon = Text(skill, "ShieldIcon", -145, -18, 40, 28,
            "E", 18, Cyan, TextAnchor.MiddleCenter);
        Text(skill, "ShieldCaption", -65, -18, 100, 28, "防御护盾", 14, Muted, TextAnchor.MiddleLeft);
        Surface(skill, "AbilityKey", new Vector2(95f, -18f), 44, 32,
            CombatGraphic.Shape.Keycap, Orange);
        panel.labSkillIcon = Text(skill, "AbilityIcon", 95, -18, 40, 28,
            "Q", 18, Orange, TextAnchor.MiddleCenter);
        Text(skill, "AbilityCaption", 184, -18, 118, 28, "战机技能", 14, Muted, TextAnchor.MiddleLeft);

        panel.ConfigureHud(encounter, challenges, feedback, healthCaption);
        CombatHudVisibility visibility = panel.gameObject.GetComponent<CombatHudVisibility>();
        if (visibility == null) visibility = panel.gameObject.AddComponent<CombatHudVisibility>();
        visibility.Configure(new[] { mission, timer, encounterPanel, health, skill,
            panel.labBossWarning.rectTransform, feedback.rectTransform });
    }

    private static void BuildPause(QuitPanel panel)
    {
        Transform root = ResetView(panel, true);
        Scrim(root);
        RectTransform dialog = Surface(root, "PauseDialog", Vector2.zero, 760, 460,
            CombatGraphic.Shape.Dialog, Cyan).rectTransform;
        Text(dialog, "PauseKicker", 0, 180, 620, 22, "飞行中断 / PAUSED", 13, Cyan,
            TextAnchor.MiddleCenter);
        Text(dialog, "PauseTitle", 0, 136, 620, 48, "任务暂停", 34, Ink,
            TextAnchor.MiddleCenter);
        Text(dialog, "PauseSubtitle", 0, 100, 620, 24,
            "战斗计时与弹幕已暂停", 15, Muted, TextAnchor.MiddleCenter);
        Line(dialog, "PauseDivider", 0, 72, 640, 1, new Color(0.18f, 0.35f, 0.42f));

        panel.btnClose = Button(dialog, "PauseContinue", 0, 28, 640, 56, "继续任务", Cyan, false);
        panel.btnRestart = Button(dialog, "PauseRestart", -162, -40, 304, 50, "重新开始", Cyan, false);
        Button levelSelect = Button(dialog, "PauseLevelSelect", 162, -40, 304, 50, "返回选关", Cyan, false);
        Button music = Button(dialog, "PauseMusic", -162, -104, 304, 50, "音乐  已开启", Cyan, false);
        panel.btnSure = Button(dialog, "PauseMainMenu", 162, -104, 304, 50, "返回主菜单", Red, true);
        Text status = Text(dialog, "PauseNavigationStatus", 0, -174, 640, 24,
            "按 Esc 可继续任务", 14, Muted, TextAnchor.MiddleCenter);
        panel.ConfigureAdditionalButtons(levelSelect, music, status);
    }

    private static void BuildGameOver(GameOverPanel panel)
    {
        Transform root = ResetView(panel, true);
        Scrim(root);
        RectTransform dialog = Surface(root, "GameOverDialog", Vector2.zero, 900, 690,
            CombatGraphic.Shape.Dialog, Red).rectTransform;
        Text(dialog, "GameOverKicker", 0, 270, 720, 24, "飞行记录 / RUN COMPLETE", 14, Muted,
            TextAnchor.MiddleCenter);
        Text(dialog, "GameOverTitle", 0, 220, 720, 52, "行动结束", 38, Red, TextAnchor.MiddleCenter);
        panel.labTime = Text(dialog, "GameOverSummary", 0, 66, 720, 230,
            "正在整理作战记录", 20, Ink, TextAnchor.MiddleCenter);
        Text(dialog, "PilotCaption", 0, -74, 640, 22, "飞行员代号", 14, Muted, TextAnchor.MiddleCenter);
        panel.inputName = Input(dialog, "PilotNameInput", 0, -118, 520, 54);
        panel.btnRestart = Button(dialog, "GameOverRestart", -165, -221, 310, 60, "再次出击", Cyan, false);
        panel.btnSure = Button(dialog, "GameOverMenu", 165, -221, 310, 60, "保存并返回", Cyan, false);
        Text(dialog, "GameOverHint", 0, -284, 700, 22,
            "作战记录会保存到本地排行榜", 13, Muted, TextAnchor.MiddleCenter);
    }

    private static Transform ResetView(Component controller, bool active)
    {
        if (controller == null) throw new InvalidOperationException("Missing controller: " + typeof(Component).Name);
        Transform view = UGUIRuntime.View(controller);
        for (int i = view.childCount - 1; i >= 0; i--) Object.DestroyImmediate(view.GetChild(i).gameObject);
        RectTransform rect = view as RectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(1920f, 1080f);
        view.gameObject.SetActive(active);
        return view;
    }

    private static void Scrim(Transform root)
    {
        UGUIRuntime.CreateImage(root, "UGUI_ModalScrim", Vector3.zero, 4000, 2400,
            null, null, new Color(0f, 0.012f, 0.022f, 0.72f), true);
    }

    private static CombatGraphic Surface(Transform parent, string name, Vector2 position, int width, int height,
        CombatGraphic.Shape shape, Color tint)
    {
        RectTransform rect = UGUIRuntime.CreateRect(parent, name, position, width, height);
        CombatGraphic graphic = rect.gameObject.AddComponent<CombatGraphic>();
        graphic.Configure(shape, tint);
        graphic.raycastTarget = shape == CombatGraphic.Shape.Dialog;
        return graphic;
    }

    private static Button Button(Transform parent, string name, float x, float y, int width, int height,
        string label, Color tint, bool danger)
    {
        CombatGraphic graphic = Surface(parent, name, new Vector2(x, y), width, height,
            danger ? CombatGraphic.Shape.DangerButton : CombatGraphic.Shape.Button, tint);
        graphic.raycastTarget = true;
        Button button = graphic.gameObject.AddComponent<Button>();
        button.targetGraphic = graphic;
        button.colors = new ColorBlock { normalColor = Color.white,
            highlightedColor = new Color(1.18f, 1.18f, 1.18f),
            pressedColor = new Color(0.68f, 0.80f, 0.84f), selectedColor = Color.white,
            disabledColor = new Color(0.38f, 0.42f, 0.46f, 0.55f), colorMultiplier = 1f, fadeDuration = 0.08f };
        Text(button.transform, name + "Label", 0, 0, width - 24, height - 8,
            label, height > 58 ? 21 : 18, Ink, TextAnchor.MiddleCenter);
        return button;
    }

    private static InputField Input(Transform parent, string name, float x, float y, int width, int height)
    {
        CombatGraphic graphic = Surface(parent, name, new Vector2(x, y), width, height,
            CombatGraphic.Shape.Stat, Cyan);
        graphic.raycastTarget = true;
        Text placeholder = Text(graphic.transform, "PilotNamePlaceholder", 0, 0, width - 36, height - 8,
            "输入飞行员代号", 20, Muted, TextAnchor.MiddleCenter);
        Text value = Text(graphic.transform, "PilotNameText", 0, 0, width - 36, height - 8,
            "Player", 22, Ink, TextAnchor.MiddleCenter);
        InputField input = graphic.gameObject.AddComponent<InputField>();
        input.targetGraphic = graphic;
        input.textComponent = value;
        input.placeholder = placeholder;
        input.characterLimit = 16;
        input.SetTextWithoutNotify("Player");
        return input;
    }

    private static Text Text(Transform parent, string name, float x, float y, int width, int height,
        string value, int size, Color tint, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        Text text = UGUIRuntime.CreateText(parent, name, new Vector3(x, y), width, height,
            value, size, tint, alignment);
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(10, size - 6);
        text.resizeTextMaxSize = size;
        return text;
    }

    private static void Line(Transform parent, string name, float x, float y, int width, int height, Color color)
    {
        UGUIRuntime.CreateImage(parent, name, new Vector3(x, y), width, height, null, null, color, false);
    }
}
