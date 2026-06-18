using Google.Protobuf;
using Mozc.Commands;
using Xunit;

namespace Mozc.Protocol.Tests;

// proto2 の group ワイヤー形式(start-group/end-group タグ)が C# 生成コードで
// 正しく往復することを固定するゴールデンテスト。
// 対象: GetReading(逆変換) 経路で使う Preedit.Segment(group, field 2) と
//       候補ウィンドウの CandidateWindow.Candidate(group, field 3)。
public class GroupRoundtripTests
{
    [Fact]
    public void PreeditSegmentGroup_Roundtrips()
    {
        var preedit = new Preedit
        {
            Cursor = 3,
            Segment =
            {
                new Preedit.Types.Segment { Value = "とうきょう", Key = "とうきょう", ValueLength = 5 },
                new Preedit.Types.Segment { Value = "東京", Key = "とうきょう", ValueLength = 2 },
            },
        };

        byte[] bytes = preedit.ToByteArray();
        Preedit parsed = Preedit.Parser.ParseFrom(bytes);

        Assert.Equal(preedit, parsed);
        Assert.Equal(2, parsed.Segment.Count);
        Assert.Equal("東京", parsed.Segment[1].Value);
        Assert.Equal("とうきょう", parsed.Segment[1].Key);
    }

    [Fact]
    public void PreeditSegmentGroup_UsesGroupWireTags()
    {
        var preedit = new Preedit
        {
            Segment = { new Preedit.Types.Segment { Value = "あ", Key = "あ", ValueLength = 1 } },
        };

        byte[] bytes = preedit.ToByteArray();

        // field 2, wire type 3 (start-group) = (2<<3)|3 = 19 (0x13)
        // field 2, wire type 4 (end-group)   = (2<<3)|4 = 20 (0x14)
        Assert.Contains((byte)19, bytes);
        Assert.Contains((byte)20, bytes);
        // length-delimited(通常メッセージ) = (2<<3)|2 = 18 を group には使っていないこと。
        // ※他フィールドで 18 が出る可能性はあるが、この最小ケースでは start/end group が主。
    }

    [Fact]
    public void CandidateWindowCandidateGroup_Roundtrips()
    {
        var window = new CandidateWindow
        {
            Candidate =
            {
                new CandidateWindow.Types.Candidate { Index = 0, Value = "東京", Id = 100 },
                new CandidateWindow.Types.Candidate { Index = 1, Value = "東亰", Id = 101 },
            },
        };

        byte[] bytes = window.ToByteArray();
        CandidateWindow parsed = CandidateWindow.Parser.ParseFrom(bytes);

        Assert.Equal(window, parsed);
        Assert.Equal(2, parsed.Candidate.Count);
        Assert.Equal("東亰", parsed.Candidate[1].Value);
    }
}
