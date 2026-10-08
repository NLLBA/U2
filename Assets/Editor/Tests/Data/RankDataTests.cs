using System.IO;
using System.Xml.Serialization;
using NUnit.Framework;

public class RankDataTests
{
    [Test]
    public void OldXmlWithoutScoreFieldsGetsSafeDefaults()
    {
        const string oldXml =
            "<RankData><rankInfos><RankInfo name=\"OldPlayer\" time=\"88\" /></rankInfos></RankData>";
        XmlSerializer serializer = new XmlSerializer(typeof(RankData));
        RankData data;

        using (StringReader reader = new StringReader(oldXml))
        {
            data = serializer.Deserialize(reader) as RankData;
        }

        Assert.IsNotNull(data);
        data.Sort();
        Assert.AreEqual(1, data.ranks.Count);

        RankInfo info = data.ranks[0];
        Assert.AreEqual("OldPlayer", info.name);
        Assert.AreEqual(0, info.score);
        Assert.AreEqual(88, info.runTime);
        Assert.AreEqual(88, info.time);
        Assert.AreEqual("普通", info.difficulty);
        Assert.AreEqual(0, info.bosses);
        Assert.AreEqual(0, info.clears);
        Assert.AreEqual(string.Empty, info.date);
    }

    [Test]
    public void NewRankFieldsRoundTripThroughXml()
    {
        RankData source = new RankData();
        source.ranks.Add(new RankInfo
        {
            name = "Pilot",
            score = 4321,
            runTime = 156,
            difficulty = "困难",
            bosses = 2,
            clears = 17,
            date = "2026-08-02 20:30:00",
        });
        source.Sort();

        XmlSerializer serializer = new XmlSerializer(typeof(RankData));
        string xml;
        using (StringWriter writer = new StringWriter())
        {
            serializer.Serialize(writer, source);
            xml = writer.ToString();
        }

        StringAssert.Contains("finalScore=\"4321\"", xml);
        StringAssert.Contains("survivalTime=\"156\"", xml);
        StringAssert.Contains("bossRounds=\"2\"", xml);
        StringAssert.Contains("clearedBullets=\"17\"", xml);

        RankData result;
        using (StringReader reader = new StringReader(xml))
        {
            result = serializer.Deserialize(reader) as RankData;
        }

        Assert.IsNotNull(result);
        result.Sort();
        Assert.AreEqual(4321, result.ranks[0].score);
        Assert.AreEqual("困难", result.ranks[0].difficulty);
        Assert.AreEqual("2026-08-02 20:30:00", result.ranks[0].date);
    }

    [Test]
    public void RankingSortsByScoreThenSurvivalTime()
    {
        RankData data = new RankData();
        data.ranks.Add(CreateRank("ShortTie", 100, 50));
        data.ranks.Add(CreateRank("Winner", 200, 10));
        data.ranks.Add(CreateRank("LongTie", 100, 70));

        data.Sort();

        Assert.AreEqual("Winner", data.ranks[0].name);
        Assert.AreEqual("LongTie", data.ranks[1].name);
        Assert.AreEqual("ShortTie", data.ranks[2].name);
    }

    [Test]
    public void RankingKeepsOnlyTwentyEntries()
    {
        RankData data = new RankData();
        for (int i = 0; i < 25; i++)
        {
            data.ranks.Add(CreateRank("Player" + i, i, i));
        }

        data.Sort(20);

        Assert.AreEqual(20, data.ranks.Count);
        Assert.AreEqual(24, data.ranks[0].score);
        Assert.AreEqual(5, data.ranks[19].score);
    }

    [Test]
    public void NormalizeTrimsPilotNameAndUsesDefaultForBlankInput()
    {
        RankInfo named = new RankInfo { name = "  Ace  " };
        RankInfo blank = new RankInfo { name = " \t" };

        named.Normalize();
        blank.Normalize();

        Assert.AreEqual("Ace", named.name);
        Assert.AreEqual("Player", blank.name);
    }

    [Test]
    public void SortNormalizesNamesLoadedFromLegacyXml()
    {
        RankData data = new RankData();
        data.ranks.Add(new RankInfo { name = "  LegacyPilot  ", time = 12 });

        data.Sort();

        Assert.AreEqual("LegacyPilot", data.ranks[0].name);
        Assert.AreEqual(12, data.ranks[0].runTime);
    }

    private static RankInfo CreateRank(string name, int score, int runTime)
    {
        return new RankInfo
        {
            name = name,
            score = score,
            runTime = runTime,
            difficulty = "普通",
        };
    }
}
