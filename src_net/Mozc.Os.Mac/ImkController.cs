using Mozc.Client;
using Pb = Mozc.Commands;

namespace Mozc.Os.Mac;

// C++ src/mac/mozc_imk_input_controller.mm 相当のロジック層。ObjC の IMKInputController
// サブクラス(極薄 native)が handleEvent/commitComposition から forward する本体。
// キーは ImeClient 経由で mozc_server に送る。
public sealed class ImkController
{
    private readonly ImeClient _client;

    public ImkController(global::System.Func<byte[], byte[]> transport)
        => _client = new ImeClient(transport);

    public ImeState HandleCharacter(char c) => _client.SendCharacter(c);

    public ImeState HandleSpecialKey(Pb.KeyEvent.Types.SpecialKey s) => _client.SendSpecialKey(s);

    // 修飾キー(Ctrl/Shift/Alt)を含む完全なキーイベントをそのまま送る。
    public ImeState HandleKeyEvent(Pb.KeyEvent key) => _client.SendKey(key);

    public ImeState SelectCandidate(int index) => _client.SubmitCandidate(index);

    public void CommitComposition() => _client.SendSpecialKey(Pb.KeyEvent.Types.SpecialKey.Enter);

    public void Deactivate() => _client.Shutdown();
}
