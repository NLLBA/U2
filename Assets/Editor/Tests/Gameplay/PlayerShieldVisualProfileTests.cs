using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;
using NUnit.Framework;
using UnityEngine;

public class PlayerShieldVisualProfileTests
{
    [Test]
    public void FiveProfilesUseDistinctVisualLanguages()
    {
        HashSet<string> shellColors = new HashSet<string>();
        HashSet<int> ringCounts = new HashSet<int>();
        HashSet<ShieldRotationPattern> rotationPatterns = new HashSet<ShieldRotationPattern>();
        HashSet<ShieldPulsePattern> pulsePatterns = new HashSet<ShieldPulsePattern>();
        HashSet<int> energyNodeCounts = new HashSet<int>();

        Assert.AreEqual(5, ShieldVisualProfileCatalog.Count);
        for (int styleId = 1; styleId <= ShieldVisualProfileCatalog.Count; styleId++)
        {
            ShieldVisualProfile profile = ShieldVisualProfileCatalog.Get(styleId);
            Assert.AreEqual(styleId, profile.StyleId);
            shellColors.Add(ColorKey(profile.ShellColor));
            ringCounts.Add(profile.RingCount);
            rotationPatterns.Add(profile.RotationPattern);
            pulsePatterns.Add(profile.PulsePattern);
            energyNodeCounts.Add(profile.EnergyNodeCount);
        }

        Assert.AreEqual(5, shellColors.Count);
        Assert.AreEqual(5, ringCounts.Count);
        Assert.AreEqual(5, rotationPatterns.Count);
        Assert.AreEqual(5, pulsePatterns.Count);
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
        Assert.AreEqual(5, roleData.roleDatas.Count);
        for (int i = 0; i < roleData.roleDatas.Count; i++)
        {
            Assert.AreEqual(i + 1, roleData.roleDatas[i].shieldStyle);
            Assert.AreEqual("Airplane/Airplane" + (i + 1), roleData.roleDatas[i].resName);
        }
    }

    [Test]
    public void PulsePatternsRemainWithinNormalizedRange()
    {
        for (int styleId = 1; styleId <= ShieldVisualProfileCatalog.Count; styleId++)
        {
            ShieldVisualProfile profile = ShieldVisualProfileCatalog.Get(styleId);
            for (int sample = 0; sample <= 40; sample++)
            {
                float pulse = ShieldVisualProfileCatalog.EvaluatePulse(
                    profile.PulsePattern,
                    sample * 0.05f,
                    profile.PulseFrequency,
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
        int originalAircraftIndex = GameDataMgr.Instance.nowSelHeroIndex;
        GameObject aircraft = new GameObject("ShieldVisualStructureTest");

        try
        {
            GameDataMgr.Instance.nowSelHeroIndex = aircraftIndex;
            PlayerObject player = aircraft.AddComponent<PlayerObject>();
            MethodInfo createShield = typeof(PlayerObject).GetMethod(
                "CreateShieldView",
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
            GameDataMgr.Instance.nowSelHeroIndex = originalAircraftIndex;
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
