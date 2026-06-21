using System.Text;
using Mozc.Dictionary;

namespace Mozc.Converter;

// C++ src/converter/immutable_converter.cc の変換フロー(会話変換の中核スライス)。
// Convert: ラティス構築 → Viterbi → MakeSegments(最良経路を文法境界で分割し
// 各セグメントに NBestGenerator を回して候補を詰める)。
// 未対応(後続): 予測(PredictionViterbi)/realtime(SINGLE_SEGMENT)/履歴セグメント/
//   FIXED_BOUNDARY 制約/人名 resegmentation/number・unknown 文字種ノード/
//   InsertDummyCandidates/逆変換の空白グルーピング。
public sealed class ImmutableConverter
{
    private const int MaxNBestExpand = 512;

    private readonly DictionaryBase _dictionary;
    private readonly Connector _connector;
    private readonly Segmenter _segmenter;
    private readonly PosMatcher _posMatcher;
    private readonly CandidateFilter _filter;

    public ImmutableConverter(DictionaryBase dictionary, Connector connector, Segmenter segmenter,
        PosMatcher posMatcher, CandidateFilter filter)
    {
        _dictionary = dictionary;
        _connector = connector;
        _segmenter = segmenter;
        _posMatcher = posMatcher;
        _filter = filter;
    }

    // キーを変換し、候補付き Segments を返す(会話変換 / 履歴・制約なし)。
    public Segments Convert(string key, int maxCandidatesSize = 30)
    {
        var segments = new Segments();
        Segment initial = segments.AddSegment();
        initial.SetKey(key); // FREE, 全キー1セグメント

        var lattice = new Lattice();
        lattice.SetKey(key);
        LatticeBuilder.PopulateFromDictionary(lattice, _dictionary);

        if (!new Viterbi(_connector).Forward(lattice))
        {
            // ラティスが繋がらない場合は空(候補なし)。
            return segments;
        }

        MakeSegments(lattice, segments, key, maxCandidatesSize);
        return segments;
    }

    // 各セグメントのキー(バイト位置)→所属セグメント index の表(末尾に番兵)。
    private static ushort[] MakeGroup(Segments segments)
    {
        var group = new List<ushort>();
        for (int i = 0; i < segments.SegmentsSize; i++)
        {
            int bytes = Encoding.UTF8.GetByteCount(segments.GetSegment(i).Key);
            for (int j = 0; j < bytes; j++)
            {
                group.Add((ushort)i);
            }
        }
        group.Add((ushort)segments.SegmentsSize);
        return group.ToArray();
    }

    private void MakeSegments(Lattice lattice, Segments segments, string key, int maxCandidatesSize)
    {
        ushort[] group = MakeGroup(segments);
        int oldConversionSize = segments.ConversionSegmentsSize;

        // key の UTF-8 エンコードはセグメント毎に再実行せず一度だけ行い引き回す。
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        InsertCandidates(lattice, segments, group, keyBytes, maxCandidatesSize);

        // 既存(変換前)セグメントを削除し、新規セグメントのみ残す。
        if (oldConversionSize > 0)
        {
            segments.EraseSegments(segments.HistorySegmentsSize, oldConversionSize);
        }
    }

    private void InsertCandidates(Lattice lattice, Segments segments, ushort[] group,
        byte[] keyBytes, int maxCandidatesSize)
    {
        // 履歴ノードは無いので prev = BOS から開始。
        Node prev = lattice.BosNode;
        int expandSize = global::System.Math.Clamp(maxCandidatesSize, 1, MaxNBestExpand);

        var nbest = new NBestGenerator(_connector, _segmenter, lattice, _posMatcher.IsFunctional, _filter);

        // original_key = 変換セグメントのキー連結(初期は全キー)。
        var originalKeyBuilder = new StringBuilder();
        foreach (Segment s in segments.ConversionSegments)
        {
            originalKeyBuilder.Append(s.Key);
        }
        string originalKey = originalKeyBuilder.ToString();

        int beginPos = -1;
        var options = new CandidateFilter.Options { RequestType = CandidateFilter.RequestType.Conversion };

        for (Node? node = prev.Next; node != null && node.Next != null; node = node.Next)
        {
            if (beginPos == -1)
            {
                beginPos = node.BeginPos;
            }
            if (!IsSegmentEndNode(node, group))
            {
                continue;
            }

            // 新セグメント生成(キーはバイト範囲 [beginPos, node.EndPos))。
            Segment segment = segments.AddSegment();
            segment.ClearCandidates();
            segment.SetKey(SubstringByBytes(keyBytes, beginPos, node.EndPos - beginPos));
            segment.Type = segments.GetSegment(group[node.BeginPos]).Type;

            nbest.Reset(prev, node.Next, NBestGenerator.BoundaryCheckMode.Strict, options, originalKey);
            nbest.SetCandidates(segment, expandSize);

            if (node.Type == Node.NodeType.ConNode)
            {
                segment.Type = Segment.SegmentType.FixedValue;
            }

            beginPos = -1;
            prev = node;
        }
    }

    private bool IsSegmentEndNode(Node node, ushort[] group)
    {
        if (node.Next!.Type == Node.NodeType.EosNode)
        {
            return true;
        }
        // 別グループ境界(強制境界)。
        if (group[node.BeginPos] != group[node.Next.BeginPos])
        {
            return true;
        }
        if (node.Type == Node.NodeType.ConNode)
        {
            return true;
        }
        return _segmenter.IsBoundary(node, node.Next, false);
    }

    // keyBytes の UTF-8 バイト範囲 [startByte, startByte+lenBytes) を文字列で返す。
    private static string SubstringByBytes(byte[] keyBytes, int startByte, int lenBytes)
    {
        return Encoding.UTF8.GetString(keyBytes, startByte, lenBytes);
    }
}
