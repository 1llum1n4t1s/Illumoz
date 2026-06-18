using Mozc.Client;
using Pb = Mozc.Commands;

namespace Mozc.Os.Linux;

// C++ src/unix/ibus/engine.cc 相当のロジック層。ibus の signal(process-key-event 等)が
// 呼ぶ本体。native GObject stub から渡されたキーを ImeClient 経由で mozc_server に送る。
// DBus/GObject signal 接続(process-key-event/set-cursor-location)は実機で極薄 native。
public sealed class IbusEngineController
{
    private readonly ImeClient _client;

    public IbusEngineController(global::System.Func<byte[], byte[]> transport)
        => _client = new ImeClient(transport);

    public ImeState ProcessKeyEvent(uint keyval, bool isSpecial)
        => isSpecial
            ? _client.SendSpecialKey((Pb.KeyEvent.Types.SpecialKey)keyval)
            : _client.SendCharacter((char)keyval);

    public ImeState ProcessCharacter(char c) => _client.SendCharacter(c);

    public ImeState ProcessSpecial(Pb.KeyEvent.Types.SpecialKey s) => _client.SendSpecialKey(s);

    public void Disable() => _client.Shutdown();
}
