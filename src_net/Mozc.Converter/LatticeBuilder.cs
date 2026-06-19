using System.Text;
using Mozc.Dictionary;

namespace Mozc.Converter;

// C++ src/converter/immutable_converter.cc の Lookup/MakeLattice の中核部分(最小版)。
// 各開始位置(コードポイント境界=UTF-8 バイトオフセット)で辞書を前方一致検索し、
// 得たトークンを Node に変換してラティスへ挿入する。
// number/unknown 文字種ノード・履歴セグメント・resegmentation は未対応(後続)。
public static class LatticeBuilder
{
    // 辞書の LookupPrefix でラティスを充填する。
    public static void PopulateFromDictionary(Lattice lattice, DictionaryBase dictionary)
    {
        string key = lattice.Key;
        int byteOffset = 0;
        int charIdx = 0;
        while (charIdx < key.Length)
        {
            // 妥当なサロゲート対(高位+低位)のみ 2 文字として扱う。
            bool validSurrogatePair = char.IsHighSurrogate(key[charIdx])
                && charIdx + 1 < key.Length
                && char.IsLowSurrogate(key[charIdx + 1]);
            int runeLen = validSurrogatePair ? 2 : 1;
            int pos = byteOffset;
            string sub = key.Substring(charIdx);

            dictionary.LookupPrefix(sub, new InlineDictionaryCallback
            {
                TokenHandler = (_, _, token) =>
                {
                    var node = new Node();
                    node.InitFromToken(token);
                    lattice.Insert(pos, node);
                    return DictionaryCallback.ResultType.TraverseContinue;
                },
            });

            byteOffset += Encoding.UTF8.GetByteCount(key.Substring(charIdx, runeLen));
            charIdx += runeLen;
        }
    }
}
