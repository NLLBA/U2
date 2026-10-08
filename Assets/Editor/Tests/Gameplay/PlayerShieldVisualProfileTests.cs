using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;
using NUnit.Framework;
using UnityEngine;

public class ShieldStyleTests
{
    [Test]
    public void FiveProfilesUseDistinctVisualLanguages()
    {
        HashSet<string> shellColors = new HashSet<string>();
        HashSet<int> ringCounts = new HashSet<int>();
        HashSet<ShieldRotation> rotations = new HashSet<ShieldRotation>();
        HashSet<ShieldPulse> pulses = new HashSet<ShieldPulse>();
        HashSet<int> energyNodeCounts = new HashSet<int>();

        Assert.AreEqual(5, ShieldStyles.Count);
        for (int styleId = 1; styleId <= ShieldStyles.Count; styleId++)
        {
            ShieldStyle style = ShieldStyles.Get(styleId);
            Assert.AreEqual(styleId, style.Id);
            shellColors.Add(ColorKey(style.ShellColor));
            ringCounts.Add(style.RingCount);
            rotations.Add(style.Rotation);
            pulses.Add(style.Pulse);
            energyNodeCounts.Add(style.NodeCount);
        }

        Assert.AreEqual(5, shellColors.Count);
        Assert.AreEqual(5, ringCounts.Count);
        Assert.AreEqual(5, rotations.Count);
        Assert.AreEqual(5, pulses.Count);
        Assert.AreEqual(5, energyNodeCounts.Count);
    }

    [Test]
    public void RoleConfigurationMapsAllAircraftToTheirShieldStyles()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "RoleData.xml");
        XmlSerializer serializer = new XmlSerializer(typeof(RoleData));
        RoleData roleData;

        using (FileStream stream = File.OpenRead(path))
        {
            roleData = serializer.Deserialize(stream) as RoleData;
        }

        Assert.IsNotNull(roleData);
        Assert.AreEqual(5, roleData.roles.Count);
        for (int i = 0; i < roleData.roles.Count; i++)
        {
            Assert.AreEqual(i + 1, roleData.roles[i].shieldId);
            Assert.AreEqual("Airplane/Airplane" + (i + 1), roleData.roles[i].asset);
        }
    }

    [Test]
    public void PulsePatternsRemainWithinNormalizedRange()
    {
        for (int styleId = 1; styleId <= ShieldStyles.Count; styleId++)
        {
            ShieldStyle style = ShieldStyles.Get(styleId);
            for (int sample = 0; sample <= 40; sample++)
            {
                float pulse = ShieldStyles.EvalPulse(
                    style.Pulse,
                    sample * 0.05f,
                    style.PulseRate,
                    0.17f);
                Assert.That(pulse, Is.InRange(0f, 1f));
            }
        }
    }

    [TestCase(0, 1, 3)]
    [TestCase(1, 2, 4)]
    [TestCase(2, 3, 6)]
    [TestCase(3, 4, 8)]
    [TestCase(4, 5, 10)]
    public void RuntimeVisualBuildsConfiguredRingsAndNonCollidingNodes(
        int aircraftIndex,
        int expectedRingCount,
        int expectedNodeCount)
    {
        int originalAircraftIndex = GameDataMgr.Instance.heroIndex;
        GameObject aircraft = new GameObject("ShieldVisualStructureTest");

        try
        {
            GameDataMgr.Instance.heroIndex = aircraftIndex;
            PlayerObject player = aircraft.AddComponent<PlayerObject>();
            MethodInfo createShield = typeof(PlayerObject).GetMethod(
                "BuildShield",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(createShield);
            createShield.Invoke(player, null);

            Transform shieldRoot = aircraft.transform.Find("ShieldViewRoot");
            Assert.IsNotNull(shieldRoot);
            Assert.AreEqual(
                expectedRingCount,
                shieldRoot.GetComponentsInChildren<LineRenderer>(true).Length);
            Assert.AreEqual(
                expectedNodeCount + 1,
                shieldRoot.GetComponentsInChildren<MeshRenderer>(true).Length,
                "Expected one shell mesh plus the configured energy nodes.");

            Collider[] visualColliders = shieldRoot.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < visualColliders.Length; i++)
            {
                Assert.IsFalse(visualColliders[i].enabled);
            }
        }
        finally
        {
            GameDataMgr.Instance.heroIndex = originalAircraftIndex;
            Object.DestroyImmediate(aircraft);
        }
    }

    private static string ColorKey(Color color)
    {
        return Mathf.RoundToInt(color.r * 1000f) + ":" +
            Mathf.RoundToInt(color.g * 1000f) + ":" +
            Mathf.RoundToInt(color.b * 1000f);
    }
}
