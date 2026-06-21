using System.Security.Cryptography;
using Mozc.Base;
using Mozc.Storage;

namespace Mozc.DataGen;

// 2 つの mozc.data(DataSet)を節単位で比較するゴールデンハーネス。
// C++/Bazel 製 mozc.data が用意できたら C# 生成物とのバイト一致検証に使う。
// 現状は C# 生成の決定性検証(同一ソース→同一バイト)にも使える。
public static class MozcDataComparer
{
    public sealed record SectionDiff(string Name, bool InA, bool InB, int SizeA, int SizeB, bool BytesEqual);

    public sealed record Report(bool Identical, IReadOnlyList<SectionDiff> Sections)
    {
        public IEnumerable<SectionDiff> Mismatches => Sections.Where(s => !(s.InA && s.InB && s.BytesEqual));
    }

    public static Report Compare(byte[] a, byte[] b)
    {
        var ra = new DataSetReader();
        var rb = new DataSetReader();
        ra.Init(a, MozcConstants.DataSetMagicOss);
        rb.Init(b, MozcConstants.DataSetMagicOss);

        var names = new SortedSet<string>(global::System.StringComparer.Ordinal);
        foreach (string n in ra.Names) names.Add(n);
        foreach (string n in rb.Names) names.Add(n);

        var diffs = new List<SectionDiff>();
        bool identical = true;
        foreach (string name in names)
        {
            bool inA = ra.TryGet(name, out ReadOnlyMemory<byte> ca);
            bool inB = rb.TryGet(name, out ReadOnlyMemory<byte> cb);
            bool eq = inA && inB && ca.Span.SequenceEqual(cb.Span);
            diffs.Add(new SectionDiff(name, inA, inB, inA ? ca.Length : 0, inB ? cb.Length : 0, eq));
            if (!(inA && inB && eq))
            {
                identical = false;
            }
        }
        return new Report(identical, diffs);
    }

    public static string Sha1(byte[] data)
        => global::System.Convert.ToHexString(SHA1.HashData(data)).ToLowerInvariant();

    // 人間可読のサマリ(CI ログ用)。
    public static string Format(Report report)
    {
        var sb = new global::System.Text.StringBuilder();
        sb.AppendLine(report.Identical ? "IDENTICAL" : "DIFFERENT");
        foreach (SectionDiff d in report.Sections)
        {
            string status = d.InA && d.InB ? (d.BytesEqual ? "ok" : "DIFF") : (d.InA ? "only-A" : "only-B");
            sb.AppendLine($"  [{status}] {d.Name} a={d.SizeA} b={d.SizeB}");
        }
        return sb.ToString();
    }
}
