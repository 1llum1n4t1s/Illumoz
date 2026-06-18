using Mozc.Converter;
using Mozc.Dictionary;
using Mozc.Engine;
using Mozc.Server;
using Mozc.Session;
using Xunit;

namespace Mozc.Server.Tests;

public class EngineServerTests
{
    private static List<string> PosRules()
    {
        var rules = new List<string> { "Functional ^助詞" };
        for (int i = 1; i < PosMatcher.RuleCount; i++)
        {
            rules.Add($"R{i} ^ZZ{i}");
        }
        return rules;
    }

    private const string RomanTable = "wa\tわ\nta\tた\nshi\tし";

    private static EngineServer Server()
    {
        var sources = new DataSetBuilder.Sources
        {
            DictionaryLines = new[] { "わたし\t1\t1\t100\t私" },
            ConnectionLines = new[] { "2", "0", "0", "0", "0" },
            ConnectionSpecialPosSize = 0,
            IdDefLines = new[] { "0 BOS/EOS,*,*,*,*,*,*", "1 名詞,一般,*,*,*,*,*" },
            SpecialPosLines = global::System.Array.Empty<string>(),
            PosMatcherRuleLines = PosRules(),
            SegmenterRuleLines = global::System.Array.Empty<string>(),
            BoundaryDefLines = global::System.Array.Empty<string>(),
        };
        var engine = new MozcEngine(new DataSetBuilder().Build(sources), RomanTable);
        var km = new KeyMap();
        km.LoadFromString("Composition\tSpace\tConvertNext\nConversion\tEnter\tCommit");
        return new EngineServer(engine, km);
    }

    private static Output RoundTrip(EngineServer server, Input input)
    {
        byte[] req = CommandCodec.EncodeInput(input);
        byte[] res = server.HandleRequest(req);
        return CommandCodec.DecodeOutput(res);
    }

    [Fact]
    public void Codec_RoundTrip_Output()
    {
        var output = new Output
        {
            SessionId = 7, Consumed = true, Preedit = "わたし", Result = "私",
            Candidates = new[] { "私", "渡し" },
        };
        Output back = CommandCodec.DecodeOutput(CommandCodec.EncodeOutput(output));
        Assert.Equal(7ul, back.SessionId);
        Assert.True(back.Consumed);
        Assert.Equal("わたし", back.Preedit);
        Assert.Equal("私", back.Result);
        Assert.Equal(new[] { "私", "渡し" }, back.Candidates);
    }

    [Fact]
    public void Server_FullFlow_OverBytes()
    {
        EngineServer server = Server();
        Output created = RoundTrip(server, new Input { Type = CommandType.CreateSession });
        ulong id = created.SessionId;
        Assert.False(created.ErrorOccured);

        foreach (char c in "watashi")
        {
            RoundTrip(server, new Input { Type = CommandType.SendKey, SessionId = id, KeyString = c.ToString() });
        }
        Output conv = RoundTrip(server, new Input { Type = CommandType.SendKey, SessionId = id, KeyString = "Space" });
        Assert.Equal("私", conv.Preedit);
        Assert.Contains("私", conv.Candidates);

        Output commit = RoundTrip(server, new Input { Type = CommandType.SendKey, SessionId = id, KeyString = "Enter" });
        Assert.Equal("私", commit.Result);
    }

    [Fact]
    public void Server_GarbageRequest_ReturnsError()
    {
        EngineServer server = Server();
        byte[] res = server.HandleRequest(new byte[] { 1, 2 }); // 不正なリクエスト
        Output output = CommandCodec.DecodeOutput(res);
        Assert.True(output.ErrorOccured);
    }
}
