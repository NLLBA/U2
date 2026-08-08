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
        data.NormalizeAndSort();
        Assert.AreEqual(1, data.rankInfos.Count);

        RankInfo info = data.rankInfos[0];
        Assert.AreEqual("OldPlayer", info.name);
        Assert.AreEqual(0, info.finalScore);
        Assert.AreEqual(88, info.survivalTime);
        Assert.AreEqual(88, info.time);
        Assert.AreEqual("普通", info.difficulty);
        Assert.AreEqual(0, info.bossRounds);
        Assert.AreEqual(0, info.clearedBullets);
        Assert.AreEqual(string.Empty, info.recordTime);
    }

    [Test]
    public void NewRankFieldsRoundTripThroughXml()
    {
        RankData source = new RankData();
        source.rankInfos.Add(new RankInfo
        {
            name = "Pilot",
            finalScore = 4321,
            survivalTime = 156,
            difficulty = "困难",
            bossRounds = 2,
            clearedBullets = 17,
            recordTime = "2026-08-02 20:30:00",
        });
        source.NormalizeAndSort();

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
        result.NormalizeAndSort();
        Assert.AreEqual(4321, result.rankInfos[0].finalScore);
        Assert.AreEqual("困难", result.rankInfos[0].difficulty);
        Assert.AreEqual("2026-08-02 20:30:00", result.rankInfos[0].recordTime);
    }

    [Test]
    public void RankingSortsByScoreThenSurvivalTime()
    {
        RankData data = new RankData();
        data.rankInfos.Add(CreateRank("ShortTie", 100, 50));
        data.rankInfos.Add(CreateRank("Winner", 200, 10));
        data.rankInfos.Add(CreateRank("LongTie", 100, 70));

        data.NormalizeAndSort();

        Assert.AreEqual("Winner", data.rankInfos[0].name);
        Assert.AreEqual("LongTie", data.rankInfos[1].name);
        Assert.AreEqual("ShortTie", data.rankInfos[2].name);
    }

    [Test]
    public void RankingKeepsOnlyTwentyEntries()
    {
        RankData data = new RankData();
        for (int i = 0; i < 25; i++)
        {
            data.rankInfos.Add(CreateRank("Player" + i, i, i));
        }

        data.NormalizeAndSort(20);

        Assert.AreEqual(20, data.rankInfos.Count);
        Assert.AreEqual(24, data.rankInfos[0].finalScore);
        Assert.AreEqual(5, data.rankInfos[19].finalScore);
    }

    private static RankInfo CreateRank(string name, int score, int survivalTime)
    {
        return new RankInfo
        {
            name = name,
            finalScore = score,
            survivalTime = survivalTime,
            difficulty = "普通",
        };
    }
}
