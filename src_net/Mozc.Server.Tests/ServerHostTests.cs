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

    private static EngineServer Server()
    {
        var sources = new DataSetBuilder.Sources
        {
            DictionaryLines = new[] { "わたし\t1\t1\t100\t私" },
            ConnectionLines = new[] { "2", "0", "0", "0", "0" },
            IdDefLines = new[] { "0 BOS/EOS,*,*,*,*,*,*", "1 名詞,一般,*,*,*,*,*" },
            PosMatcherRuleLines = PosRules(),
        };
        return ServerHost.CreateFromBytes(new DataSetBuilder().Build(sources), "wa\tわ", "");
    }

    [Fact]
    public void DefaultProfileDir_EndsWithMozc()
    {
        string dir = ServerHost.DefaultProfileDir();
        Assert.True(dir.Length > 0);
        // 末尾は Mozc(Win/mac)または mozc(Linux)。
        string leaf = global::System.IO.Path.GetFileName(dir);
        Assert.True(leaf == "Mozc" || leaf == "mozc", $"unexpected leaf: {leaf}");
    }

    [Fact]
    public void SaveProfile_LoadProfile_RoundTrip()
    {
        string pid = global::System.Guid.NewGuid().ToString("N");
        string dir = global::System.IO.Path.Combine(
            global::System.IO.Path.GetTempPath(), $"mozc_prof_{pid}");
        try
        {
            EngineServer s1 = Server();
            s1.Handler.RegisterWord("もずく", "Mozc");
            s1.Handler.History.Learn("わたし", "私");
            Mozc.Config.Config c = s1.Config.GetConfig();
            c.PreeditMethod = Mozc.Config.Config.Types.PreeditMethod.Kana;
            // 文字形 LAST_FORM ルール + 記憶を設定。
            c.CharacterFormRules.Add(new Mozc.Config.Config.Types.CharacterFormRule
            {
                Group = "0",
                ConversionCharacterForm = Mozc.Config.Config.Types.CharacterForm.LastForm,
                PreeditCharacterForm = Mozc.Config.Config.Types.CharacterForm.LastForm,
            });
            s1.SetConfig(c);
            s1.ConversionFormManager.SetCharacterForm("0", Mozc.Base.CharacterForm.HalfWidth);
            ServerHost.SaveProfile(s1, dir);

            EngineServer s2 = Server();
            ServerHost.LoadProfile(s2, dir);
            Assert.Single(s2.Handler.UserDictionary.LookupExact("もずく"));
            Assert.NotEmpty(s2.Handler.History.Predict("わたし"));
            Assert.Equal(Mozc.Config.Config.Types.PreeditMethod.Kana, s2.Config.GetConfig().PreeditMethod);
            // LAST_FORM 記憶(半角)が復元される。
            Assert.Equal(Mozc.Base.CharacterForm.HalfWidth,
                s2.ConversionFormManager.GetCharacterForm("5"));
        }
        finally
        {
            if (global::System.IO.Directory.Exists(dir))
            {
                global::System.IO.Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void ApplyConfig_PropagatesIncognitoToCommandRewriter()
    {
        EngineServer s = Server();
        // 既定では incognito off。
        var rw = FindCommandRewriter(s);
        Assert.NotNull(rw);
        Assert.False(rw!.IncognitoMode);

        Mozc.Config.Config c = s.Config.GetConfig();
        c.IncognitoMode = true;
        c.PresentationMode = true;
        s.SetConfig(c); // ApplyConfig が走る

        Assert.True(rw.IncognitoMode);
        Assert.True(rw.PresentationMode);
    }

    [Fact]
    public void ApplyConfig_BuildsCharacterFormManagerFromRules()
    {
        EngineServer s = Server();
        Mozc.Config.Config c = s.Config.GetConfig();
        var rule = new Mozc.Config.Config.Types.CharacterFormRule
        {
            Group = "0",
            ConversionCharacterForm = Mozc.Config.Config.Types.CharacterForm.HalfWidth,
            PreeditCharacterForm = Mozc.Config.Config.Types.CharacterForm.FullWidth,
        };
        c.CharacterFormRules.Add(rule);
        s.SetConfig(c);

        // conversion 側は数字を半角化、preedit 側は全角化。
        Assert.Equal("123", s.ConversionFormManager.ConvertString("１２３"));
        Assert.Equal("１２３", s.PreeditFormManager.ConvertString("123"));
    }

    private static Mozc.Rewriter.CommandRewriter? FindCommandRewriter(EngineServer s)
    {
        if (s.Handler.Rewriter is not Mozc.Rewriter.RewriterMerger merger)
        {
            return null;
        }
        foreach (Mozc.Rewriter.IRewriter r in merger.Rewriters)
        {
            if (r is Mozc.Rewriter.CommandRewriter cmd)
            {
                return cmd;
            }
        }
        return null;
    }
}
