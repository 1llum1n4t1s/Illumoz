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
        // 既定 SelectionShortcut=123456789 で先頭候補に shortcut "1" が付く。
        Assert.Equal("1", conv.CandidateWindow.Candidate[0].Annotation.Shortcut);

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
    public void CandidateWindow_AssignsSequentialIds_AndPosition()
    {
        EngineServer server = Server();
        ulong id = Call(server, new Pb.Input { Type = Pb.Input.Types.CommandType.CreateSession }).Id;
        foreach (char c in "watashi")
        {
            Call(server, new Pb.Input
            {
                Type = Pb.Input.Types.CommandType.SendKey,
                Id = id,
                Key = new Pb.KeyEvent { KeyCode = c },
            });
        }
        Pb.Output conv = Call(server, new Pb.Input
        {
            Type = Pb.Input.Types.CommandType.SendKey,
            Id = id,
            Key = new Pb.KeyEvent { SpecialKey = Pb.KeyEvent.Types.SpecialKey.Space },
        });
        Assert.NotNull(conv.CandidateWindow);
        // 各候補の id は行インデックスと一致する(SELECT_CANDIDATE の id とずれない)。
        for (int i = 0; i < conv.CandidateWindow.Candidate.Count; i++)
        {
            Assert.Equal(i, conv.CandidateWindow.Candidate[i].Id);
        }
        // position(required)は変換中も常に設定される。
        Assert.True(conv.CandidateWindow.HasPosition);
    }

    [Fact]
    public void DirectInput_KeyString_CommitsImmediately_InPrecomposition()
    {
        EngineServer server = Server();
        ulong id = Call(server, new Pb.Input { Type = Pb.Input.Types.CommandType.CreateSession }).Id;

        // input_style=DIRECT_INPUT + key_string(ソフトキーボードの直接テキスト)。
        // precomposition では key_string を即時確定する(preedit/composition にしない)。
        Pb.Output output = Call(server, new Pb.Input
        {
            Type = Pb.Input.Types.CommandType.SendKey,
            Id = id,
            Key = new Pb.KeyEvent
            {
                KeyCode = 'a',
                KeyString = "あ",
                InputStyle = Pb.KeyEvent.Types.InputStyle.DirectInput,
            },
        });
        Assert.True(output.Consumed);
        Assert.NotNull(output.Result);
        Assert.Equal("あ", output.Result.Value);
        Assert.Null(output.Preedit); // preedit ではなく Result として出力される
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

        // 入力中サジェストが SUGGESTION カテゴリの候補窓として載る。
        Assert.NotNull(last.CandidateWindow);
        Assert.Equal(Pb.Category.Suggestion, last.CandidateWindow.Category);
        Assert.Contains(last.CandidateWindow.Candidate, c => c.Value == "私");
        // サジェスト候補にも shortcut が付く(既定 123456789)。
        Assert.Equal("1", last.CandidateWindow.Candidate[0].Annotation.Shortcut);
    }

    [Fact]
    public void SendCommand_SubmitCandidate_Commits()
    {
        EngineServer server = Server();
        ulong id = Call(server, new Pb.Input { Type = Pb.Input.Types.CommandType.CreateSession }).Id;
        foreach (char c in "watashi")
        {
            Call(server, new Pb.Input
            {
                Type = Pb.Input.Types.CommandType.SendKey, Id = id,
                Key = new Pb.KeyEvent { KeyCode = c },
            });
        }
        Call(server, new Pb.Input
        {
            Type = Pb.Input.Types.CommandType.SendKey, Id = id,
            Key = new Pb.KeyEvent { SpecialKey = Pb.KeyEvent.Types.SpecialKey.Space },
        });
        // SEND_COMMAND: SUBMIT_CANDIDATE で 0 番候補を選択し確定。
        Pb.Output o = Call(server, new Pb.Input
        {
            Type = Pb.Input.Types.CommandType.SendCommand,
            Id = id,
            Command = new Pb.SessionCommand
            {
                Type = Pb.SessionCommand.Types.CommandType.SubmitCandidate,
                Id = 0,
            },
        });
        Assert.NotNull(o.Result);
        Assert.Equal("私", o.Result.Value);
    }

    [Fact]
    public void ExplicitNoSpecialKey_OnPrintableKey_StillInputsCharacter()
    {
        // NO_SPECIALKEY(enum 既定値)を明示シリアライズしても、key_code の印字キーは
        // 特殊キー扱いせず通常入力できる(Special=UndefinedKey にすると 'w''a' が落ちる)。
        EngineServer server = Server();
        ulong id = Call(server, new Pb.Input { Type = Pb.Input.Types.CommandType.CreateSession }).Id;
        Pb.Output? last = null;
        foreach (char c in "wa")
        {
            last = Call(server, new Pb.Input
            {
                Type = Pb.Input.Types.CommandType.SendKey,
                Id = id,
                Key = new Pb.KeyEvent
                {
                    KeyCode = c,
                    SpecialKey = Pb.KeyEvent.Types.SpecialKey.NoSpecialkey,
                },
            });
        }
        // "wa"→わ。NO_SPECIALKEY を特殊キー扱いしていれば印字キーが落ち preedit は空になる。
        Assert.NotNull(last!.Preedit);
        Assert.Equal("わ", last.Preedit.Segment[0].Value);
    }

    [Fact]
    public void SendCommand_Undo_RestoresPreviousCommit()
    {
        // ツールバー/クライアント駆動の UNDO(protobuf SEND_COMMAND)で直前確定を復元する。
        EngineServer server = Server();
        ulong id = Call(server, new Pb.Input { Type = Pb.Input.Types.CommandType.CreateSession }).Id;
        foreach (char c in "watashi")
        {
            Call(server, new Pb.Input
            {
                Type = Pb.Input.Types.CommandType.SendKey, Id = id,
                Key = new Pb.KeyEvent { KeyCode = c },
            });
        }
        Call(server, new Pb.Input
        {
            Type = Pb.Input.Types.CommandType.SendKey, Id = id,
            Key = new Pb.KeyEvent { SpecialKey = Pb.KeyEvent.Types.SpecialKey.Space },
        });
        Pb.Output commit = Call(server, new Pb.Input
        {
            Type = Pb.Input.Types.CommandType.SendKey, Id = id,
            Key = new Pb.KeyEvent { SpecialKey = Pb.KeyEvent.Types.SpecialKey.Enter },
        });
        Assert.Equal("私", commit.Result.Value);

        Pb.Output undo = Call(server, new Pb.Input
        {
            Type = Pb.Input.Types.CommandType.SendCommand,
            Id = id,
            Command = new Pb.SessionCommand { Type = Pb.SessionCommand.Types.CommandType.Undo },
        });
        Assert.True(undo.Consumed);
        // 確定が取り消され、読みが preedit に戻る。
        Assert.NotNull(undo.Preedit);
        Assert.Equal("わたし", undo.Preedit.Segment[0].Value);
    }

    [Fact]
    public void DecodeInput_PasswordField_SuppressesSuggestionAndMarksPrivate()
    {
        // context.input_field_type=PASSWORD を落とさず、サジェスト抑止 + 秘匿モードとして伝える
        // (履歴学習と中間サジェストで秘密が漏れるのを防ぐ)。
        var proto = new Pb.Input
        {
            Type = Pb.Input.Types.CommandType.SendKey,
            Context = new Pb.Context { InputFieldType = Pb.Context.Types.InputFieldType.Password },
        };
        Input decoded = ProtoBridge.DecodeInput(proto.ToByteArray());
        Assert.True(decoded.IsPasswordField);
        Assert.True(decoded.SuppressSuggestion);
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
