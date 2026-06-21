namespace Mozc.Converter;

// C6: gen_segmenter_bitarray.cc 相当。is_boundary(rid,lid) の NxN 真偽行列を
// l_table/r_table(重複行・列のマージ)で圧縮し、Segmenter が読む形式
// (l_num=compressed_lsize, l_table[lsize+1], r_table[rsize+1], bitarray)を生成する。
// インデックス式・番兵(rid==lsize||lid==rsize→1)・stride(rid + lsize*lid)・
// StateTable の初出順 id 付与を C++ と厳密一致させる。
public static class SegmenterBitarrayGenerator
{
    public sealed class Result
    {
        public int CompressedLSize;
        public int CompressedRSize;
        public ushort[] LTable = global::System.Array.Empty<ushort>(); // 長さ lsize+1, rid で参照
        public ushort[] RTable = global::System.Array.Empty<ushort>(); // 長さ rsize+1, lid で参照
        public byte[] Bitarray = global::System.Array.Empty<byte>();
    }

    public static Result Generate(int lsize, int rsize, Func<int, int, bool> isBoundary)
    {
        // (lsize+1)*(rsize+1) の一時行列。index = rid + lsize*lid(C++ 厳密。番兵で上書きされる
        // エイリアスも含めて同一挙動)。番兵: rid==lsize または lid==rsize は 1。
        var array = new byte[(lsize + 1) * (rsize + 1)];
        for (int rid = 0; rid <= lsize; rid++)
        {
            for (int lid = 0; lid <= rsize; lid++)
            {
                int index = rid + lsize * lid;
                if (rid == lsize || lid == rsize)
                {
                    array[index] = 1;
                }
                else
                {
                    array[index] = (byte)(isBoundary(rid, lid) ? 1 : 0);
                }
            }
        }

        // 左状態(行)の重複除去。
        var ltable = new StateTable(lsize + 1);
        for (int rid = 0; rid <= lsize; rid++)
        {
            var buf = new byte[rsize + 1];
            for (int lid = 0; lid <= rsize; lid++)
            {
                buf[lid] = array[rid + lsize * lid];
            }
            ltable.Add(rid, buf);
        }

        // 右状態(列)の重複除去。
        var rtable = new StateTable(rsize + 1);
        for (int lid = 0; lid <= rsize; lid++)
        {
            var buf = new byte[lsize + 1];
            for (int rid = 0; rid <= lsize; rid++)
            {
                buf[rid] = array[rid + lsize * lid];
            }
            rtable.Add(lid, buf);
        }

        rtable.Build();
        ltable.Build();
        int cL = ltable.CompressedSize;
        int cR = rtable.CompressedSize;

        var bitarray = new byte[(cL * cR + 7) / 8];
        for (int rid = 0; rid <= lsize; rid++)
        {
            for (int lid = 0; lid <= rsize; lid++)
            {
                int index = rid + lsize * lid;
                int cindex = ltable.Id(rid) + cL * rtable.Id(lid);
                if (array[index] > 0)
                {
                    bitarray[cindex >> 3] |= (byte)(1 << (cindex & 7));
                }
            }
        }

        return new Result
        {
            CompressedLSize = cL,
            CompressedRSize = cR,
            LTable = ltable.Table,
            RTable = rtable.Table,
            Bitarray = bitarray,
        };
    }

    // C++ StateTable 相当。行/列のバイト列に初出順で id を割り当てる。
    private sealed class StateTable
    {
        private readonly byte[]?[] _idArray;
        private ushort[] _compressed = global::System.Array.Empty<ushort>();

        public StateTable(int size) => _idArray = new byte[]?[size];

        public int CompressedSize { get; private set; }
        public ushort[] Table => _compressed;

        public void Add(int id, byte[] str) => _idArray[id] = str;

        public void Build()
        {
            _compressed = new ushort[_idArray.Length];
            ushort id = 0;
            var dup = new Dictionary<string, ushort>();
            for (int i = 0; i < _idArray.Length; i++)
            {
                string key = global::System.Convert.ToBase64String(_idArray[i]!);
                if (dup.TryGetValue(key, out ushort existing))
                {
                    _compressed[i] = existing;
                }
                else
                {
                    _compressed[i] = id;
                    dup[key] = id;
                    id++;
                }
            }
            CompressedSize = dup.Count;
        }

        public ushort Id(int id) => _compressed[id];
    }
}
