using Mozc.Converter;

namespace Mozc.Rewriter;

// C++ src/rewriter/rewriter_interface.h 相当(中核)。変換候補の後処理。
public interface IRewriter
{
    // Segments を書き換えたら true。
    bool Rewrite(Segments segments);
}

// C++ rewriter.cc の RewriterImpl 相当(簡略)。登録順に各 rewriter を適用。
public sealed class RewriterMerger : IRewriter
{
    private readonly List<IRewriter> _rewriters = new();

    public void AddRewriter(IRewriter rewriter) => _rewriters.Add(rewriter);

    // 登録済み rewriter 一覧(config 連携で特定の rewriter を探すのに使う)。
    public IReadOnlyList<IRewriter> Rewriters => _rewriters;

    public bool Rewrite(Segments segments)
    {
        bool result = false;
        foreach (IRewriter rewriter in _rewriters)
        {
            result |= rewriter.Rewrite(segments);
        }
        return result;
    }
}

// テスト可能にするための時刻ソース(C++ Clock 相当)。
public interface IClock
{
    global::System.DateTime Now { get; }
}

public sealed class SystemClock : IClock
{
    public global::System.DateTime Now => global::System.DateTime.Now;
}

public sealed class FixedClock : IClock
{
    public FixedClock(global::System.DateTime now) => Now = now;
    public global::System.DateTime Now { get; }
}
