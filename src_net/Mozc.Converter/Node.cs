using Mozc.Dictionary;

namespace Mozc.Converter;

// C++ src/converter/node.h 相当。ラティスの連結ノード。
// prev/next/constrained_prev で双方向リンク。class(参照型)で表現。
public sealed class Node
{
    public enum NodeType
    {
        NorNode, // normal
        BosNode, // beginning of sentence
        EosNode, // end of sentence
        ConNode, // constrained
        HisNode, // history
    }

    [Flags]
    public enum Attribute : uint
    {
        Default = 0,
        SystemDictionary = 1 << 0,
        UserDictionary = 1 << 1,
        NoVariantsExpansion = 1 << 2,
        StartsWithParticle = 1 << 4,
        SpellingCorrection = 1 << 5,
        PartiallyKeyConsumed = 1 << 7,
        SuffixDictionary = 1 << 8,
        KeyExpanded = 1 << 9,
    }

    public Node? Prev;
    public Node? Next;
    public Node? ConstrainedPrev;
    public ushort Rid;
    public ushort Lid;
    public ushort BeginPos;
    public ushort EndPos;
    public int Wcost;
    public int Cost;
    public NodeType Type = NodeType.NorNode;
    public Attribute Attributes = Attribute.Default;
    public string Key = string.Empty;
    public string Value = string.Empty;

    // C++ Node::InitFromToken 相当。
    public void InitFromToken(Token token)
    {
        Prev = null;
        Next = null;
        ConstrainedPrev = null;
        Rid = token.Rid;
        Lid = token.Lid;
        BeginPos = 0;
        EndPos = 0;
        Type = NodeType.NorNode;
        Wcost = token.Cost;
        Cost = 0;
        Attributes = Attribute.Default;
        if ((token.Attributes & Token.Attribute.SpellingCorrection) != 0)
        {
            Attributes |= Attribute.SpellingCorrection;
        }
        if ((token.Attributes & Token.Attribute.SuffixDictionary) != 0)
        {
            Attributes |= Attribute.SuffixDictionary;
        }
        if ((token.Attributes & Token.Attribute.UserDictionary) != 0)
        {
            Attributes |= Attribute.UserDictionary;
            Attributes |= Attribute.NoVariantsExpansion;
        }
        Key = token.Key;
        Value = token.Value;
    }
}
