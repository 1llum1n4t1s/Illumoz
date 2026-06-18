using Mozc.DataGen;

// CLI: src/data ソース → mozc.data。Bazel genrule の代替(MSBuild pre-build から呼べる)。
// 使い方:
//   Mozc.DataGen --out mozc.data --id-def id.def --connection conn.txt --conn-special 0 \
//     --pos-matcher-rule pos_matcher_rule.def --segmenter-rule segmenter.def \
//     --boundary boundary.def --special-pos special_pos.def --dict d00.txt --dict d01.txt ...
//     [--symbol symbol.tsv --single-kanji single_kanji.tsv --emoji emoji_data.tsv]
static class Program
{
    static int Main(string[] args)
    {
        string outPath = string.Empty;
        var dicts = new List<string>();
        string connection = string.Empty, idDef = string.Empty, specialPos = string.Empty;
        string posRule = string.Empty, segRule = string.Empty, boundary = string.Empty;
        string symbol = string.Empty, singleKanji = string.Empty, emoji = string.Empty;
        int connSpecial = 0;

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            string Next() => ++i < args.Length ? args[i] : string.Empty;
            switch (a)
            {
                case "--out": outPath = Next(); break;
                case "--dict": dicts.Add(Next()); break;
                case "--connection": connection = Next(); break;
                case "--conn-special": int.TryParse(Next(), out connSpecial); break;
                case "--id-def": idDef = Next(); break;
                case "--special-pos": specialPos = Next(); break;
                case "--pos-matcher-rule": posRule = Next(); break;
                case "--segmenter-rule": segRule = Next(); break;
                case "--boundary": boundary = Next(); break;
                case "--symbol": symbol = Next(); break;
                case "--single-kanji": singleKanji = Next(); break;
                case "--emoji": emoji = Next(); break;
                default:
                    global::System.Console.Error.WriteLine($"unknown arg: {a}");
                    return 2;
            }
        }

        if (outPath.Length == 0)
        {
            global::System.Console.Error.WriteLine("--out is required");
            return 2;
        }

        byte[] data = DataGenerator.Generate(new DataGenerator.FileSources
        {
            DictionaryFiles = dicts,
            ConnectionFile = connection,
            ConnectionSpecialPosSize = connSpecial,
            IdDefFile = idDef,
            SpecialPosFile = specialPos,
            PosMatcherRuleFile = posRule,
            SegmenterRuleFile = segRule,
            BoundaryDefFile = boundary,
            SymbolFile = symbol,
            SingleKanjiFile = singleKanji,
            EmojiFile = emoji,
        });
        global::System.IO.File.WriteAllBytes(outPath, data);
        global::System.Console.WriteLine($"wrote {data.Length} bytes to {outPath}");
        return 0;
    }
}
