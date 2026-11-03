using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class AircraftHangarTests
{
    [UnityTest]
    public IEnumerator AllFiveAircraftStayCenteredAndFitThroughFullRotation()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/BeginScene.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        yield return null;
        yield return null;
        BeginPanel.Instance.btnBegin.onClick.Invoke();
        yield return null;
        ChoosePanel choose = ChoosePanel.Instance;
        AircraftPreview preview = choose.aircraftPreview;
        for (int aircraft = 0; aircraft < 5; aircraft++)
        {
            UGUIRuntime.Find<Button>(choose.transform, "AircraftSlot" + aircraft).onClick.Invoke();
            Assert.AreEqual(aircraft, GameDataMgr.Instance.heroIndex);
            Assert.IsNotNull(preview.Pivot);
            Assert.AreEqual(Vector3.zero, preview.ModelBounds.center);
            Assert.AreEqual(0, preview.Pivot.GetComponentsInChildren<Collider>().Length);
            Assert.AreEqual(0, preview.Pivot.GetComponentsInChildren<Rigidbody>().Length);
            for (int yaw = 0; yaw < 360; yaw += 15)
            {
                preview.SetYaw(yaw);
                preview.RenderNow();
                Rect projected = preview.ProjectedModelBounds();
                Assert.That(projected.center.x, Is.EqualTo(0.5f).Within(0.002f), "Horizontal orbit drift: " + aircraft);
                Assert.That(projected.center.y, Is.EqualTo(0.5f).Within(0.002f), "Vertical orbit drift: " + aircraft);
                Assert.Greater(projected.xMin, 0.05f);
                Assert.Less(projected.xMax, 0.95f);
                Assert.Greater(projected.yMin, 0.05f);
                Assert.Less(projected.yMax, 0.95f);
            }
            preview.OnDrag(new PointerEventData(EventSystem.current) { delta = new Vector2(40f, 0f) });
            Assert.AreNotEqual(0f, preview.Pivot.localEulerAngles.y);
            UGUIRuntime.Find<Button>(choose.transform, "ResetAircraftView").onClick.Invoke();
            Assert.That(preview.Pivot.localEulerAngles.y, Is.EqualTo(0f).Within(0.01f));
        }
        choose.btnClose.onClick.Invoke();
        yield return null;
        Assert.IsNull(preview.PreviewCamera);
        Assert.IsNull(preview.GetComponent<RawImage>().texture);
        Assert.IsNull(GameObject.Find("AircraftPreviewRig"));
        Assert.IsTrue(BeginPanel.Instance.gameObject.activeSelf);
        yield return new ExitPlayMode();
    }
}
