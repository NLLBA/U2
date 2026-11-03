using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class UIPolishUGUIClickTests
{
    [Test]
    public void NativeButtonInvokesOneListenerPerClick()
    {
        GameObject root = new GameObject("ButtonTest", typeof(RectTransform), typeof(Image), typeof(Button));
        try
        {
            Button button = root.GetComponent<Button>();
            int clicks = 0;
            button.onClick.AddListener(() => clicks++);
            button.onClick.Invoke();
            Assert.AreEqual(1, clicks);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void RuntimeFactoriesCreateNativeSelectableControls()
    {
        GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        try
        {
            Button button = UGUIRuntime.CreateButton(canvasObject.transform, "Action", Vector3.zero,
                240, 60, "Action", Color.white);
            InputField input = UGUIRuntime.CreateInput(canvasObject.transform, "Name", Vector3.zero,
                320, 60, "Name", "Player", 16, null, null);
            Assert.IsNotNull(button.targetGraphic);
            Assert.IsNotNull(UGUIRuntime.Label(button));
            Assert.AreEqual("Player", input.text);
            Assert.IsNotNull(input.textComponent);
        }
        finally
        {
            Object.DestroyImmediate(canvasObject);
        }
    }
}
