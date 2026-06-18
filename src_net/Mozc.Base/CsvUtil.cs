using System.Collections.Generic;
using System.Text;

namespace Mozc.Base;

// C++ src/base/util.cc の Util::SplitCSV 相当。CSV 1 行をフィールドへ分割する。
// 各フィールド先頭の空白/タブはスキップ。ダブルクオートで囲んだフィールドは
// 内部の "" を 1 つの " にアンエスケープする。末尾がカンマなら空フィールドを足す。
public static class CsvUtil
{
    public static List<string> SplitCsv(string input)
    {
        var output = new List<string>();
        int i = 0;
        int n = input.Length;

        while (i < n)
        {
            // 先頭の空白/タブをスキップ。
            while (i < n && (input[i] == ' ' || input[i] == '\t'))
            {
                i++;
            }

            var field = new StringBuilder();
            if (i < n && input[i] == '"')
            {
                i++; // 開きクオート。
                while (i < n)
                {
                    if (input[i] == '"')
                    {
                        i++;
                        if (i >= n || input[i] != '"')
                        {
                            break; // 閉じクオート。
                        }
                        // "" → " (エスケープ)。
                    }
                    field.Append(input[i]);
                    i++;
                }
                // 次のカンマまで読み飛ばす。
                while (i < n && input[i] != ',')
                {
                    i++;
                }
            }
            else
            {
                int start = i;
                while (i < n && input[i] != ',')
                {
                    i++;
                }
                field.Append(input.Substring(start, i - start));
            }

            output.Add(field.ToString());

            // 末尾がカンマなら空フィールドを 1 つ追加。
            if (i == n - 1 && input[i] == ',')
            {
                output.Add(string.Empty);
            }
            i++; // カンマをスキップ。
        }
        return output;
    }
}
