using System.Runtime.Versioning;
using Mozc.Converter;
using Mozc.Dictionary;
using Mozc.Engine;
using Mozc.Ipc;
using Mozc.Server;
using Mozc.Session;
using Xunit;

namespace Mozc.Server.Tests;

// EngineServer を実 NamedPipe サーバ越しにクライアントから叩く統合(C++ mozc_server 相当の疎通)。
[SupportedOSPlatform("windows")]
public class IpcIntegrationTests
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

    [Fact]
    public void FullConversion_OverNamedPipe()
    {
        if (!global::System.OperatingSystem.IsWindows())
        {
            return;
        }
        EngineServer engineServer = Server();
        string pipeName = "mozc.test." + global::System.Guid.NewGuid().ToString("N");
        using var ipc = new NamedPipeIpcServer(pipeName, engineServer.HandleRequest);
        ipc.Start();

        var client = new NamedPipeIpcClient(pipeName);
        global::System.TimeSpan to = global::System.TimeSpan.FromSeconds(5);

        Output Call(Input input)
            => CommandCodec.DecodeOutput(client.Call(CommandCodec.EncodeInput(input), to));

        ulong id = Call(new Input { Type = CommandType.CreateSession }).SessionId;
        foreach (char c in "watashi")
        {
            Call(new Input { Type = CommandType.SendKey, SessionId = id, KeyString = c.ToString() });
        }
        Output conv = Call(new Input { Type = CommandType.SendKey, SessionId = id, KeyString = "Space" });
        Assert.Equal("私", conv.Preedit);

        Output commit = Call(new Input { Type = CommandType.SendKey, SessionId = id, KeyString = "Enter" });
        Assert.Equal("私", commit.Result);
    }
}
