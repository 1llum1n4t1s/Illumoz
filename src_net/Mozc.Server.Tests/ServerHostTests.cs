using Google.Protobuf;
using Mozc.Converter;
using Mozc.Dictionary;
using Mozc.Server;
using Mozc.Server.Host;
using Mozc.Session;
using Xunit;
using Pb = Mozc.Commands;

namespace Mozc.Server.Tests;

public class ServerHostTests
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

    [Fact]
    public void CreateFromBytes_BuildsWorkingServer()
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
        byte[] data = new DataSetBuilder().Build(sources);

        EngineServer server = ServerHost.CreateFromBytes(
            data, "wa\tわ\nta\tた\nshi\tし",
            "Composition\tSpace\tConvertNext\nConversion\tEnter\tCommit");

        Pb.Output created = Pb.Output.Parser.ParseFrom(
            server.HandleProtoRequest(new Pb.Input { Type = Pb.Input.Types.CommandType.CreateSession }.ToByteArray()));
        Assert.True(created.Id > 0);
    }
}
