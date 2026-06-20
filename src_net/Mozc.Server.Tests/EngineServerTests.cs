using Google.Protobuf;
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

    private static string DataDir()
    {
        var dir = new global::System.IO.DirectoryInfo(global::System.AppContext.BaseDirectory);
        while (dir != null)
        {
            string c = global::System.IO.Path.Combine(dir.FullName, "src", "data");
            if (global::System.IO.Directory.Exists(global::System.IO.Path.Combine(c, "keymap")))
            {
                return c;
            }
            dir = dir.Parent;
        }
        return string.Empty;
    }

    [Fact]
    public void SetConfig_ReloadsPresetKeymapFromData()
    {
        string dataDir = DataDir();
        Assert.True(dataDir.Length > 0);

        var sources = new DataSetBuilder.Sources
        {
            DictionaryLines = new[] { "わたし\t1\t1\t100\t私" },
            ConnectionLines = new[] { "2", "0", "0", "0", "0" },
            IdDefLines = new[] { "0 BOS/EOS,*,*,*,*,*,*", "1 名詞,一般,*,*,*,*,*" },
            PosMatcherRuleLines = PosRules(),
        };
        var engine = new MozcEngine(new DataSetBuilder().Build(sources), RomanTable);
        var km = new KeyMap();
        km.LoadFromString("Composition\tSpace\tConvertNext");
        var server = new EngineServer(engine, km, dataDir: dataDir);

        // MSIME プリセットが読み込まれている(Composition+Backspace→Backspace)。
        Assert.Equal("Backspace", server.Handler.KeyMap.GetCommand("Composition", "Backspace"));

        // KOTOERI に切替 → keymap が再ロードされエントリ数が変わる。
        Mozc.Config.Config c = server.Config.GetConfig();
        c.SessionKeymap = Mozc.Config.Config.Types.SessionKeymap.Kotoeri;
        server.SetConfig(c);
        Assert.True(server.Handler.KeyMap.EntryCount > 50);
    }

    [Fact]
    public void SetConfig_KanaPreeditMethod_LoadsKanaTable()
    {
        string dataDir = DataDir();
        Assert.True(dataDir.Length > 0);
        var engine = new MozcEngine(new DataSetBuilder().Build(new DataSetBuilder.Sources
        {
            DictionaryLines = new[] { "わたし\t1\t1\t100\t私" },
            ConnectionLines = new[] { "2", "0", "0", "0", "0" },
            IdDefLines = new[] { "0 BOS/EOS,*,*,*,*,*,*", "1 名詞,一般,*,*,*,*,*" },
            PosMatcherRuleLines = PosRules(),
        }), RomanTable);
        var km = new KeyMap();
        km.LoadFromString("Composition\tSpace\tConvertNext");
        var server = new EngineServer(engine, km, dataDir: dataDir);

        // 既定(ROMAN): w a → わ
        ulong a = server.Handler.EvalCommand(new Input { Type = CommandType.CreateSession }).SessionId;
        server.Handler.EvalCommand(new Input { Type = CommandType.SendKey, SessionId = a, KeyString = "w" });
        Output ra = server.Handler.EvalCommand(new Input { Type = CommandType.SendKey, SessionId = a, KeyString = "a" });
        Assert.Equal("わ", ra.Preedit);

        // preedit_method=KANA へ切替 → 新セッションの composer はかな配列を使う。
        // かな配列は物理かなキー前提で ascii の w/a を わ へ変換しない(=ローマ字のままにならない)。
        Mozc.Config.Config c = server.Config.GetConfig();
        c.PreeditMethod = Mozc.Config.Config.Types.PreeditMethod.Kana;
        server.SetConfig(c);
        ulong b = server.Handler.EvalCommand(new Input { Type = CommandType.CreateSession }).SessionId;
        server.Handler.EvalCommand(new Input { Type = CommandType.SendKey, SessionId = b, KeyString = "w" });
        Output rb = server.Handler.EvalCommand(new Input { Type = CommandType.SendKey, SessionId = b, KeyString = "a" });
        Assert.NotEqual("わ", rb.Preedit);
    }

    [Fact]
    public void LastFormHistory_SurvivesUnrelatedConfigChange()
    {
        EngineServer server = Server();
        // 変換結果から半角 ASCII を学習(LAST_FORM の "A" グループ → HalfWidth を記憶)。
        server.ConversionFormManager.GuessAndSetCharacterForm("abc");
        Assert.Equal(Mozc.Base.CharacterForm.HalfWidth,
            server.ConversionFormManager.GetCharacterForm("A"));

        // 字形と無関係な設定変更(句読点方式)を適用する。
        Mozc.Config.Config c = server.Config.GetConfig();
        c.PunctuationMethod = Mozc.Config.Config.Types.PunctuationMethod.CommaPeriod;
        server.SetConfig(c);

        // ApplyConfig で ConversionFormManager のルールが作り直されても、学習した LAST_FORM の
        // 好み(_lastFormStorage)は保持される(無関係な設定変更で半角学習が消えない)。
        Assert.Equal(Mozc.Base.CharacterForm.HalfWidth,
            server.ConversionFormManager.GetCharacterForm("A"));
    }

    [Fact]
    public void Config_PunctuationMethod_AppliesToComposer()
    {
        EngineServer server = Server();
        // 既定(TOUTEN_KUTEN): "," → 、
        var d = server.Handler.Engine.CreateComposer();
        d.InsertCharacter(",");
        Assert.Equal("、", d.GetStringForPreedit());

        // COMMA_PERIOD: "," → ，, "." → ．
        Mozc.Config.Config c = server.Config.GetConfig();
        c.PunctuationMethod = Mozc.Config.Config.Types.PunctuationMethod.CommaPeriod;
        server.SetConfig(c);
        var cm = server.Handler.Engine.CreateComposer();
        cm.InsertCharacter(",");
        Assert.Equal("，", cm.GetStringForPreedit());
        var pd = server.Handler.Engine.CreateComposer();
        pd.InsertCharacter(".");
        Assert.Equal("．", pd.GetStringForPreedit());
    }

    [Fact]
    public void Config_SymbolMethod_AppliesToComposer()
    {
        EngineServer server = Server();
        // 既定(CORNER_BRACKET_MIDDLE_DOT): [→「 /→・
        var d = server.Handler.Engine.CreateComposer();
        d.InsertCharacter("[");
        Assert.Equal("「", d.GetStringForPreedit());

        // SQUARE_BRACKET_SLASH: [→［ /→／
        Mozc.Config.Config c = server.Config.GetConfig();
        c.SymbolMethod = Mozc.Config.Config.Types.SymbolMethod.SquareBracketSlash;
        server.SetConfig(c);
        var b = server.Handler.Engine.CreateComposer();
        b.InsertCharacter("[");
        Assert.Equal("［", b.GetStringForPreedit());
        var s = server.Handler.Engine.CreateComposer();
        s.InsertCharacter("/");
        Assert.Equal("／", s.GetStringForPreedit());
    }

    [Fact]
    public void Config_SpaceCharacterForm_FullWidth()
    {
        EngineServer server = Server();
        Mozc.Config.Config c = server.Config.GetConfig();
        c.SpaceCharacterForm = Mozc.Config.Config.Types.FundamentalCharacterForm.FundamentalFullWidth;
        server.SetConfig(c);

        var comp = server.Handler.Engine.CreateComposer();
        comp.InsertCharacter(" ");
        Assert.Equal("　", comp.GetStringForPreedit()); // 全角スペース
    }

    [Fact]
    public void SetConfig_AppliesCustomRomanTable()
    {
        EngineServer server = Server();
        // 既定表に無い "q" を カスタムで "あ" に割り当てる。
        Mozc.Config.Config c = server.Config.GetConfig();
        c.CustomRomanTable = ByteString.CopyFromUtf8("q\tあ");
        server.SetConfig(c);

        // 新規セッションで q→あ が効く(composer が再ロードされた)。
        var composer = server.Handler.Engine.CreateComposer();
        composer.InsertCharacter("q");
        Assert.Equal("あ", composer.GetStringForPreedit());
    }

    [Fact]
    public void UserDictionary_SaveLoad_AcrossHandlers()
    {
        string id = global::System.Guid.NewGuid().ToString("N");
        string path = global::System.IO.Path.Combine(global::System.IO.Path.GetTempPath(), $"mozc_ud_{id}.db");
        try
        {
            EngineServer s1 = Server();
            s1.Handler.UserDictionary.Add(
                new Mozc.Dictionary.UserDictionaryStorage.UserEntry("もずく", "Mozc", "名詞", ""));
            s1.Handler.SaveUserDictionary(path);

            EngineServer s2 = Server();
            Assert.True(s2.Handler.LoadUserDictionary(path));
            Assert.Single(s2.Handler.UserDictionary.LookupExact("もずく"));
        }
        finally
        {
            if (global::System.IO.File.Exists(path)) { global::System.IO.File.Delete(path); }
        }
    }

    [Fact]
    public void RegisterWord_ThenConversionIncludesIt()
    {
        EngineServer server = Server();
        Assert.True(server.Handler.RegisterWord("わたし", "ワタシ社"));

        Output create = RoundTrip(server, new Input { Type = CommandType.CreateSession });
        ulong sid = create.SessionId;
        foreach (char ch in "watashi")
        {
            RoundTrip(server, new Input { Type = CommandType.SendKey, SessionId = sid, KeyString = ch.ToString() });
        }
        Output conv = RoundTrip(server, new Input { Type = CommandType.SendKey, SessionId = sid, KeyString = "Space" });
        Assert.Contains("ワタシ社", conv.Candidates);
    }

    [Fact]
    public void UserDictionary_WordAppearsInSuggestions()
    {
        EngineServer server = Server();
        // ユーザー辞書に「わたし→Watashi」を登録。
        server.Handler.UserDictionary.Add(
            new Mozc.Dictionary.UserDictionaryStorage.UserEntry("わたし", "Watashi", "名詞", ""));

        Output create = RoundTrip(server, new Input { Type = CommandType.CreateSession });
        ulong sid = create.SessionId;
        Output? last = null;
        foreach (char ch in "watashi")
        {
            last = RoundTrip(server, new Input
            {
                Type = CommandType.SendKey, SessionId = sid, KeyString = ch.ToString(),
            });
        }
        Assert.NotNull(last);
        Assert.Contains("Watashi", last!.Suggestions); // ユーザー辞書語がサジェストに出る
    }

    [Fact]
    public void SendKey_ReturnsSuggestions_OverIpc()
    {
        EngineServer server = Server();
        Output create = RoundTrip(server, new Input { Type = CommandType.CreateSession });
        ulong sid = create.SessionId;

        Output? last = null;
        foreach (char c in "watashi")
        {
            last = RoundTrip(server, new Input
            {
                Type = CommandType.SendKey,
                SessionId = sid,
                KeyString = c.ToString(),
            });
        }
        // 入力中サジェストに「私」が IPC 経由で返る。
        Assert.NotNull(last);
        Assert.Contains("私", last!.Suggestions);
    }

    [Fact]
    public void GetConfig_SetConfig_OverIpc()
    {
        EngineServer server = Server();

        // GET_CONFIG: 既定 Config が返る。
        Output got = RoundTrip(server, new Input { Type = CommandType.GetConfig });
        Assert.True(got.Consumed);
        Mozc.Config.Config cfg = Mozc.Config.Config.Parser.ParseFrom(got.ConfigBytes);
        Assert.Equal(Mozc.Config.Config.Types.PreeditMethod.Roman, cfg.PreeditMethod);

        // SET_CONFIG: 変更して送信 → 反映される。
        cfg.PreeditMethod = Mozc.Config.Config.Types.PreeditMethod.Kana;
        Output set = RoundTrip(server, new Input
        {
            Type = CommandType.SetConfig,
            ConfigBytes = cfg.ToByteArray(),
        });
        Assert.True(set.Consumed);

        Output again = RoundTrip(server, new Input { Type = CommandType.GetConfig });
        Mozc.Config.Config cfg2 = Mozc.Config.Config.Parser.ParseFrom(again.ConfigBytes);
        Assert.Equal(Mozc.Config.Config.Types.PreeditMethod.Kana, cfg2.PreeditMethod);
    }

    [Fact]
    public void Config_DefaultEnablesLearning_NoHistoryDisables()
    {
        EngineServer server = Server();
        Assert.True(server.Handler.History.LearningEnabled); // 既定で学習有効

        Mozc.Config.Config c = server.Config.GetConfig();
        c.HistoryLearningLevel = Mozc.Config.Config.Types.HistoryLearningLevel.NoHistory;
        server.SetConfig(c);
        Assert.False(server.Handler.History.LearningEnabled); // NO_HISTORY で無効

        // 学習が無効なら Learn しても履歴は増えない。
        server.Handler.History.Learn("わたし", "私");
        Assert.Equal(0, server.Handler.History.Count);
    }

    [Fact]
    public void SyncData_InvokesOnSyncData_AndSucceeds()
    {
        EngineServer server = Server();
        int flushed = 0;
        server.OnSyncData = () => flushed++;

        Output o = RoundTrip(server, new Input { Type = CommandType.SyncData });
        Assert.True(o.Consumed);
        Assert.False(o.ErrorOccured);
        Assert.Equal(1, flushed); // SYNC_DATA で永続化フックが発火する。
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
