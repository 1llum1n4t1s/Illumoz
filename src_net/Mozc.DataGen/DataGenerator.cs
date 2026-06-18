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
    }

    public static byte[] Generate(FileSources sources)
    {
        var dict = new List<string>();
        foreach (string f in sources.DictionaryFiles)
        {
            dict.AddRange(ReadLines(f));
        }

        var builderSources = new DataSetBuilder.Sources
        {
            DictionaryLines = dict,
            ConnectionLines = ReadLines(sources.ConnectionFile),
            ConnectionSpecialPosSize = sources.ConnectionSpecialPosSize,
            IdDefLines = ReadLines(sources.IdDefFile),
            SpecialPosLines = ReadLinesOrEmpty(sources.SpecialPosFile),
            PosMatcherRuleLines = ReadLines(sources.PosMatcherRuleFile),
            SegmenterRuleLines = ReadLinesOrEmpty(sources.SegmenterRuleFile),
            BoundaryDefLines = ReadLinesOrEmpty(sources.BoundaryDefFile),
        };
        return new DataSetBuilder().Build(builderSources);
    }

    private static IReadOnlyList<string> ReadLines(string path)
        => path.Length == 0 ? global::System.Array.Empty<string>() : global::System.IO.File.ReadAllLines(path);

    private static IReadOnlyList<string> ReadLinesOrEmpty(string path)
        => path.Length == 0 || !global::System.IO.File.Exists(path)
            ? global::System.Array.Empty<string>()
            : global::System.IO.File.ReadAllLines(path);
}
