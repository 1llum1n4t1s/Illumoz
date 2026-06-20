namespace Mozc.Converter;

// C++ src/converter/nbest_generator.{h,cc} 相当(A* 後ろ向き探索による N-best 列挙)。
// Viterbi 後の lattice 上で、begin_node〜end_node 区間の候補をコスト昇順に列挙する。
// node.cost を A* のヒューリスティック h(x) に用いる(Viterbi 済みなので厳密)。
//
// 注入点(本家では PosMatcher / CandidateFilter / SuggestionFilter に依存):
//  - isFunctional: pos_matcher.IsFunctional(id) 相当(content_key/value 分割用)。
//  - filter: CandidateFilter 相当の品質ゲート(重複除去・コスト閾値等)。本家移植は後続。
// 未対応: FillInnerSegmentInfo(realtime 用, candidate_mode 既定 NONE), constrained_prev,
//         BUILD_FROM_ONLY_FIRST_INNER_SEGMENT。
public sealed class NBestGenerator
{
    public enum BoundaryCheckMode { Strict, OnlyMid, OnlyEdge }

    private enum BoundaryCheckResult { Valid, ValidWeakConnected, Invalid }

    private const int CostDiff = 3453;            // log prob of 1/1000
    private const int WeakConnectedPenalty = 3453;
    private const int InvalidPenaltyCost = 100000;
    private const int MaxTrial = 500;

    private sealed class QueueElement
    {
        public Node Node = null!;
        public QueueElement? Next;
        public int Fx;          // f(x) = h(x)+g(x)
        public int Gx;          // g(x)
        public int StructureGx;
        public int WGx;
    }

    private readonly Connector _connector;
    private readonly Segmenter _segmenter;
    private readonly Lattice _lattice;
    private readonly Func<ushort, bool> _isFunctional;
    private readonly CandidateFilter _filter;

    // .NET の PriorityQueue は同値優先度で取り出し順が非安定。C++ 側ヒープと
    // 決定的に候補順を一致させるため、コスト＋挿入連番の複合キーで安定化する。
    private readonly struct StablePriority : global::System.IComparable<StablePriority>
    {
        public int Cost { get; }
        public long Sequence { get; }
        public StablePriority(int cost, long sequence)
        {
            Cost = cost;
            Sequence = sequence;
        }
        public int CompareTo(StablePriority other)
        {
            int c = Cost.CompareTo(other.Cost);
            return c != 0 ? c : Sequence.CompareTo(other.Sequence);
        }
    }

    private readonly PriorityQueue<QueueElement, StablePriority> _agenda = new();
    private long _sequence;
    private readonly List<Node> _topNodes = new();
    private Node _beginNode = null!;
    private Node _endNode = null!;
    private BoundaryCheckMode _mode;
    private bool _viterbiResultChecked;
    private CandidateFilter.Options _options = new();
    private string _originalKey = string.Empty;

    public NBestGenerator(Connector connector, Segmenter segmenter, Lattice lattice,
        Func<ushort, bool> isFunctional, CandidateFilter filter)
    {
        _connector = connector;
        _segmenter = segmenter;
        _lattice = lattice;
        _isFunctional = isFunctional;
        _filter = filter;
    }

    public void Reset(Node beginNode, Node endNode, BoundaryCheckMode mode = BoundaryCheckMode.Strict,
        CandidateFilter.Options? options = null, string originalKey = "")
    {
        _agenda.Clear();
        _sequence = 0;
        _topNodes.Clear();
        _filter.Reset();
        _viterbiResultChecked = false;
        _mode = mode;
        _beginNode = beginNode;
        _endNode = endNode;
        _options = options ?? new CandidateFilter.Options();
        _originalKey = originalKey;

        foreach (Node node in _lattice.BeginNodes(endNode.BeginPos))
        {
            if (ReferenceEquals(node, endNode) ||
                (node.Lid != endNode.Lid &&
                 global::System.Math.Abs(node.Cost - endNode.Cost) <= CostDiff &&
                 !ReferenceEquals(node.Prev, endNode.Prev)))
            {
                Push(new QueueElement { Node = node, Next = null, Fx = node.Cost });
            }
        }
    }

    // expandSize 件まで segment に候補を詰める。
    public void SetCandidates(Segment segment, int expandSize)
    {
        while (segment.CandidatesSize < expandSize)
        {
            Candidate candidate = segment.PushBackCandidate();
            if (!Next(candidate))
            {
                segment.PopBackCandidate();
                break;
            }
        }
    }

    public bool Next(Candidate candidate)
    {
        if (!_viterbiResultChecked)
        {
            _viterbiResultChecked = true;
            switch (InsertTopResult(candidate))
            {
                case CandidateFilter.ResultType.GoodCandidate:
                    return true;
                case CandidateFilter.ResultType.StopEnumeration:
                    return false;
            }
        }

        int numTrials = 0;
        while (_agenda.Count > 0)
        {
            QueueElement top = _agenda.Dequeue();
            Node rnode = top.Node;

            if (numTrials++ > MaxTrial)
            {
                return false;
            }

            if (rnode.EndPos == _beginNode.EndPos)
            {
                switch (MakeCandidateFromElement(top, candidate))
                {
                    case CandidateFilter.ResultType.GoodCandidate:
                        return true;
                    case CandidateFilter.ResultType.StopEnumeration:
                        return false;
                }
                continue;
            }

            QueueElement? bestLeftElm = null;
            bool isRightEdge = rnode.BeginPos == _endNode.BeginPos;
            bool isLeftEdge = rnode.BeginPos == _beginNode.EndPos;
            bool isEdge = isRightEdge || isLeftEdge;

            foreach (Node lnode in _lattice.EndNodes(rnode.BeginPos))
            {
                bool isValidPosition = !(lnode.BeginPos < _beginNode.EndPos &&
                                         _beginNode.EndPos < lnode.EndPos);
                if (!isValidPosition)
                {
                    continue;
                }

                bool isValidCost = (lnode.Cost - _beginNode.Cost) <= CostDiff;
                if (isLeftEdge && !isValidCost)
                {
                    continue;
                }

                bool canOmitSearch = lnode.Rid == _beginNode.Rid && !ReferenceEquals(lnode, _beginNode);
                if (isLeftEdge && canOmitSearch)
                {
                    continue;
                }

                BoundaryCheckResult boundaryResult = BoundaryCheck(lnode, rnode, isEdge);
                if (boundaryResult == BoundaryCheckResult.Invalid)
                {
                    continue;
                }

                int transitionCost = GetTransitionCost(lnode, rnode);
                int costDiff, structureCostDiff, wcostDiff;
                if (isRightEdge)
                {
                    costDiff = transitionCost + (rnode.Cost - _endNode.Cost);
                    structureCostDiff = 0;
                    wcostDiff = 0;
                }
                else if (isLeftEdge)
                {
                    costDiff = transitionCost + rnode.Wcost + (lnode.Cost - _beginNode.Cost);
                    structureCostDiff = 0;
                    wcostDiff = rnode.Wcost;
                }
                else
                {
                    costDiff = transitionCost + rnode.Wcost;
                    structureCostDiff = transitionCost;
                    wcostDiff = transitionCost + rnode.Wcost;
                }

                if (boundaryResult == BoundaryCheckResult.ValidWeakConnected)
                {
                    costDiff += WeakConnectedPenalty;
                    structureCostDiff += WeakConnectedPenalty / 2;
                    wcostDiff += WeakConnectedPenalty / 2;
                }

                int gx = costDiff + top.Gx;
                int fx = lnode.Cost + gx;
                int structureGx = structureCostDiff + top.StructureGx;
                int wGx = wcostDiff + top.WGx;

                if (isLeftEdge)
                {
                    if (bestLeftElm == null || bestLeftElm.Fx > fx)
                    {
                        bestLeftElm = new QueueElement
                        {
                            Node = lnode, Next = top, Fx = fx, Gx = gx,
                            StructureGx = structureGx, WGx = wGx,
                        };
                    }
                }
                else
                {
                    Push(new QueueElement
                    {
                        Node = lnode, Next = top, Fx = fx, Gx = gx,
                        StructureGx = structureGx, WGx = wGx,
                    });
                }
            }

            if (bestLeftElm != null)
            {
                Push(bestLeftElm);
            }
        }
        return false;
    }

    private void Push(QueueElement elm) => _agenda.Enqueue(elm, new StablePriority(elm.Fx, _sequence++));

    private CandidateFilter.ResultType InsertTopResult(Candidate candidate)
    {
        if (!MakeCandidateFromBestPath(candidate))
        {
            return CandidateFilter.ResultType.StopEnumeration;
        }
        return _filter.FilterCandidate(_options, _originalKey, candidate, _topNodes, _topNodes);
    }

    private bool MakeCandidateFromBestPath(Candidate candidate)
    {
        _topNodes.Clear();
        int totalWcost = 0;
        for (Node? node = _beginNode.Next; node != null && !ReferenceEquals(node, _endNode); node = node.Next)
        {
            if (!ReferenceEquals(node, _beginNode.Next))
            {
                if (IsBetweenAlphabetKeys(_topNodes[^1], node))
                {
                    return false;
                }
                totalWcost += node.Wcost;
            }
            _topNodes.Add(node);
        }
        if (_topNodes.Count == 0)
        {
            return false;
        }

        int cost = (_endNode.Cost - _endNode.Wcost) - _beginNode.Cost;
        int structureCost = _endNode.Prev!.Cost - _beginNode.Next!.Cost - totalWcost;
        int wcost = _endNode.Prev!.Cost - _beginNode.Next!.Cost + _beginNode.Next!.Wcost;

        MakeCandidate(candidate, cost, structureCost, wcost, _topNodes);
        return true;
    }

    private CandidateFilter.ResultType MakeCandidateFromElement(QueueElement element, Candidate candidate)
    {
        if (element.Next == null)
        {
            return CandidateFilter.ResultType.BadCandidate;
        }
        var nodes = new List<Node>();
        for (QueueElement? elm = element.Next; elm.Next != null; elm = elm.Next)
        {
            nodes.Add(elm.Node);
        }
        if (nodes.Count == 0)
        {
            return CandidateFilter.ResultType.BadCandidate;
        }
        MakeCandidate(candidate, element.Gx, element.StructureGx, element.WGx, nodes);
        return _filter.FilterCandidate(_options, _originalKey, candidate, _topNodes, nodes);
    }

    private void MakeCandidate(Candidate candidate, int cost, int structureCost, int wcost,
        IReadOnlyList<Node> nodes)
    {
        candidate.Clear();
        candidate.Lid = nodes[0].Lid;
        candidate.Rid = nodes[^1].Rid;
        candidate.Cost = cost;
        candidate.StructureCost = structureCost;
        candidate.Wcost = wcost;

        // 文字列連結は StringBuilder で1パス構築する(string += はノードごとに全コピー = O(L²))。
        var keyBuilder = new global::System.Text.StringBuilder();
        var valueBuilder = new global::System.Text.StringBuilder();
        var contentKeyBuilder = new global::System.Text.StringBuilder();
        var contentValueBuilder = new global::System.Text.StringBuilder();
        bool isFunctional = false;
        foreach (Node node in nodes)
        {
            if (!isFunctional && !_isFunctional(node.Lid))
            {
                contentKeyBuilder.Append(node.Key);
                contentValueBuilder.Append(node.Value);
            }
            else
            {
                isFunctional = true;
            }
            keyBuilder.Append(node.Key);
            valueBuilder.Append(node.Value);

            if ((node.Attributes & Node.Attribute.SpellingCorrection) != 0)
            {
                candidate.Attributes |= Candidate.Attribute.SpellingCorrection;
            }
            if ((node.Attributes & Node.Attribute.NoVariantsExpansion) != 0)
            {
                candidate.Attributes |= Candidate.Attribute.NoVariantsExpansion;
            }
            if ((node.Attributes & Node.Attribute.UserDictionary) != 0)
            {
                candidate.Attributes |= Candidate.Attribute.UserDictionary;
            }
            if ((node.Attributes & Node.Attribute.SuffixDictionary) != 0)
            {
                candidate.Attributes |= Candidate.Attribute.SuffixDictionary;
            }
            if ((node.Attributes & Node.Attribute.KeyExpanded) != 0)
            {
                candidate.Attributes |= Candidate.Attribute.KeyExpandedInDictionary;
            }
        }

        candidate.Key = keyBuilder.ToString();
        candidate.Value = valueBuilder.ToString();
        candidate.ContentKey = contentKeyBuilder.ToString();
        candidate.ContentValue = contentValueBuilder.ToString();

        if (candidate.ContentKey.Length == 0 || candidate.ContentValue.Length == 0)
        {
            candidate.ContentKey = candidate.Key;
            candidate.ContentValue = candidate.Value;
        }
        // FillInnerSegmentInfo は realtime 用(candidate_mode NONE のため省略, 後続)。
    }

    private BoundaryCheckResult BoundaryCheck(Node lnode, Node rnode, bool isEdge)
    {
        if (rnode.Type == Node.NodeType.ConNode || lnode.Type == Node.NodeType.ConNode)
        {
            return BoundaryCheckResult.Valid;
        }
        if (IsBetweenAlphabetKeys(lnode, rnode))
        {
            return BoundaryCheckResult.Invalid;
        }
        return _mode switch
        {
            BoundaryCheckMode.Strict => CheckStrict(lnode, rnode, isEdge),
            BoundaryCheckMode.OnlyMid => CheckOnlyMid(lnode, rnode, isEdge),
            BoundaryCheckMode.OnlyEdge => CheckOnlyEdge(lnode, rnode, isEdge),
            _ => BoundaryCheckResult.Invalid,
        };
    }

    private BoundaryCheckResult CheckStrict(Node lnode, Node rnode, bool isEdge)
    {
        bool isBoundary = lnode.Type == Node.NodeType.HisNode ||
                          _segmenter.IsBoundary(lnode, rnode, false);
        return isEdge != isBoundary ? BoundaryCheckResult.Invalid : BoundaryCheckResult.Valid;
    }

    private BoundaryCheckResult CheckOnlyMid(Node lnode, Node rnode, bool isEdge)
    {
        bool isBoundary = lnode.Type == Node.NodeType.HisNode ||
                          _segmenter.IsBoundary(lnode, rnode, false);
        if (!isEdge && isBoundary)
        {
            return BoundaryCheckResult.Invalid;
        }
        if (isEdge && !isBoundary)
        {
            return BoundaryCheckResult.ValidWeakConnected;
        }
        return BoundaryCheckResult.Valid;
    }

    private BoundaryCheckResult CheckOnlyEdge(Node lnode, Node rnode, bool isEdge)
    {
        bool isBoundary = lnode.Type == Node.NodeType.HisNode ||
                          _segmenter.IsBoundary(lnode, rnode, true);
        return isEdge != isBoundary ? BoundaryCheckResult.Invalid : BoundaryCheckResult.Valid;
    }

    private int GetTransitionCost(Node lnode, Node rnode)
    {
        if (rnode.ConstrainedPrev != null && !ReferenceEquals(lnode, rnode.ConstrainedPrev))
        {
            return InvalidPenaltyCost;
        }
        return _connector.GetTransitionCost(lnode.Rid, rnode.Lid);
    }

    private static bool IsBetweenAlphabetKeys(Node left, Node right)
        => left.Key.Length > 0 && right.Key.Length > 0 &&
           IsAsciiAlpha(left.Key[^1]) && IsAsciiAlpha(right.Key[0]);

    private static bool IsAsciiAlpha(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z';
}
