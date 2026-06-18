using Google.Protobuf;
using Mozc.Converter;
using Mozc.Dictionary;
using Mozc.Engine;
using Mozc.Server;
using Mozc.Session;
using Xunit;
using Pb = Mozc.Commands;

namespace Mozc.Server.Tests;

// C++ ワイヤー互換 protobuf 経路の検証(commands.proto の Input/Output で疎通)。
public class ProtoBridgeTests
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
        var engine = new MozcEngine(new DataSetBuilder().Build(sources), "wa\tわ\nta\tた\nshi\tし");
        var km = new KeyMap();
        km.LoadFromString("Composition\tSpace\tConvertNext\nConversion\tEnter\tCommit");
        return new EngineServer(engine, km);
    }

    private static Pb.Output Call(EngineServer server, Pb.Input input)
        => Pb.Output.Parser.ParseFrom(server.HandleProtoRequest(input.ToByteArray()));

    [Fact]
    public void WireCompatible_FullFlow()
    {
        EngineServer server = Server();

        Pb.Output created = Call(server, new Pb.Input { Type = Pb.Input.Types.CommandType.CreateSession });
        ulong id = created.Id;
        Assert.True(created.Consumed);

        // ローマ字 w a t a s h i を key_code で送る。
        foreach (char c in "watashi")
        {
            Call(server, new Pb.Input
            {
                Type = Pb.Input.Types.CommandType.SendKey,
                Id = id,
                Key = new Pb.KeyEvent { KeyCode = c },
            });
        }

        // Space(special key)で変換。
        Pb.Output conv = Call(server, new Pb.Input
        {
            Type = Pb.Input.Types.CommandType.SendKey,
            Id = id,
            Key = new Pb.KeyEvent { SpecialKey = Pb.KeyEvent.Types.SpecialKey.Space },
        });
        Assert.NotNull(conv.CandidateWindow);
        Assert.Contains(conv.CandidateWindow.Candidate, c => c.Value == "私");

        // Enter で確定 → Result.value に 私。
        Pb.Output commit = Call(server, new Pb.Input
        {
            Type = Pb.Input.Types.CommandType.SendKey,
            Id = id,
            Key = new Pb.KeyEvent { SpecialKey = Pb.KeyEvent.Types.SpecialKey.Enter },
        });
        Assert.NotNull(commit.Result);
        Assert.Equal("私", commit.Result.Value);
    }

    [Fact]
    public void Preedit_IsCarriedInProtoOutput()
    {
        EngineServer server = Server();
        ulong id = Call(server, new Pb.Input { Type = Pb.Input.Types.CommandType.CreateSession }).Id;

        Pb.Output? last = null;
        foreach (char c in "watashi")
        {
            last = Call(server, new Pb.Input
            {
                Type = Pb.Input.Types.CommandType.SendKey,
                Id = id,
                Key = new Pb.KeyEvent { KeyCode = c },
            });
        }
        Assert.NotNull(last!.Preedit);
        Assert.Single(last.Preedit.Segment);
        Assert.Equal("わたし", last.Preedit.Segment[0].Value);
        Assert.Equal(3u, last.Preedit.Segment[0].ValueLength); // わ/た/し = 3 文字
    }

    [Fact]
    public void GarbageProtoRequest_ReturnsErrorOutput()
    {
        EngineServer server = Server();
        // 不正な protobuf でも例外にせず Output を返す(ErrorOccured は proto には載らないが parse 可能)。
        byte[] res = server.HandleProtoRequest(new byte[] { 0xFF, 0xFF, 0xFF });
        Pb.Output output = Pb.Output.Parser.ParseFrom(res);
        Assert.NotNull(output); // クラッシュしないこと
    }
}
