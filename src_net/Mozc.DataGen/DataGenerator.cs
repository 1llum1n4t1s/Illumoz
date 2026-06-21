using Mozc.Converter;

namespace Mozc.DataGen;

// src/data のソースファイル群から mozc.data を生成する(Bazel genrule 代替の中核)。
public static class DataGenerator
{
    public sealed class FileSources
    {
        // 複数の辞書テキスト(dictionary00.txt..09.txt)を結合する。
        public IReadOnlyList<string> DictionaryFiles { get; init; } = global::System.Array.Empty<string>();
        public string ConnectionFile { get; init; } = string.Empty;
        public int ConnectionSpecialPosSize { get; init; }
        public string IdDefFile { get; init; } = string.Empty;
        public string SpecialPosFile { get; init; } = string.Empty;
        public string PosMatcherRuleFile { get; init; } = string.Empty;
        public string SegmenterRuleFile { get; init; } = string.Empty;
        public string BoundaryDefFile { get; init; } = string.Empty;
        // 記号/単漢字/絵文字の tsv(任意)。指定されれば mozc.data にセクション梱包する。
        public string SymbolFile { get; init; } = string.Empty;
        public string SingleKanjiFile { get; init; } = string.Empty;
        public string EmojiFile { get; init; } = string.Empty;
    }

    public static byte[] Generate(FileSources sources)
    {
        var dict = new List<string>();
        foreach (string f in sources.DictionaryFiles)
        {
            dict.AddRange(ReadLines(f));
        }

        IReadOnlyList<string> specialPosLines = ReadLinesOrEmpty(sources.SpecialPosFile);
        // --conn-special 未指定(0)時は special_pos.def の実エントリ数から導出する。
        // (連接/境界テーブルを pos_size + special_pos_size で正しくサイズするため)。
        int connectionSpecialPosSize = sources.ConnectionSpecialPosSize > 0
            ? sources.ConnectionSpecialPosSize
            : CountSpecialPos(specialPosLines);

        var builderSources = new DataSetBuilder.Sources
        {
            DictionaryLines = dict,
            ConnectionLines = ReadLines(sources.ConnectionFile),
            ConnectionSpecialPosSize = connectionSpecialPosSize,
            IdDefLines = ReadLines(sources.IdDefFile),
            SpecialPosLines = specialPosLines,
            PosMatcherRuleLines = ReadLines(sources.PosMatcherRuleFile),
            SegmenterRuleLines = ReadLinesOrEmpty(sources.SegmenterRuleFile),
            BoundaryDefLines = ReadLinesOrEmpty(sources.BoundaryDefFile),
            SymbolTsv = ReadTextOrEmpty(sources.SymbolFile),
            SingleKanjiTsv = ReadTextOrEmpty(sources.SingleKanjiFile),
            EmojiTsv = ReadTextOrEmpty(sources.EmojiFile),
        };
        return new DataSetBuilder().Build(builderSources);
    }

    // special_pos.def の実エントリ数(空行/'#' コメント行を除く)。
    // PosMatcherDataGenerator.ParsePosDatabase の付番ロジックと一致させる。
    private static int CountSpecialPos(IReadOnlyList<string> lines)
    {
        int count = 0;
        foreach (string raw in lines)
        {
            string t = raw.Trim();
            if (t.Length == 0 || t.StartsWith('#'))
            {
                continue; // 行頭コメント / 空行 / 空白のみ行をスキップ(ParsePosDatabase と一致)。
            }
            count++;
        }
        return count;
    }

    private static IReadOnlyList<string> ReadLines(string path)
        => path.Length == 0 ? global::System.Array.Empty<string>() : global::System.IO.File.ReadAllLines(path);

    private static IReadOnlyList<string> ReadLinesOrEmpty(string path)
        => path.Length == 0 || !global::System.IO.File.Exists(path)
            ? global::System.Array.Empty<string>()
            : global::System.IO.File.ReadAllLines(path);

    private static string ReadTextOrEmpty(string path)
        => path.Length == 0 || !global::System.IO.File.Exists(path)
            ? string.Empty
            : global::System.IO.File.ReadAllText(path);
}
