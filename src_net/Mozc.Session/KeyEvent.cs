using System.Text;

namespace Mozc.Session;

// C++ commands::KeyEvent::SpecialKey の主要部。
public enum SpecialKey
{
    On, Off, Left, Down, Up, Right, Enter, Escape, Del, Backspace,
    Henkan, Muhenkan, Kana, Katakana, Eisu, Home, End, Space, TextInput,
    Tab, PageUp, PageDown, Insert, Hankaku, Kanji,
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
    Numpad0, Numpad1, Numpad2, Numpad3, Numpad4,
    Numpad5, Numpad6, Numpad7, Numpad8, Numpad9,
    UndefinedKey,
}

// キーマップ照合に使う正規化済み修飾キー(左右は基本キーに畳む)。
public enum ModifierKey { Ctrl, Shift, Alt, Caps }

// C++ commands::KeyEvent の中核(KeyCode=単一文字 / SpecialKey / Modifiers)。
public sealed class KeyEvent
{
    public int? KeyCode { get; set; }
    public SpecialKey? Special { get; set; }
    public HashSet<ModifierKey> Modifiers { get; } = new();

    // 照合用の正規化シグネチャ(parse 結果と実イベントで一致させる)。
    public string Signature()
    {
        var sb = new StringBuilder();
        if (Modifiers.Contains(ModifierKey.Ctrl)) sb.Append("Ctrl+");
        if (Modifiers.Contains(ModifierKey.Alt)) sb.Append("Alt+");
        if (Modifiers.Contains(ModifierKey.Shift)) sb.Append("Shift+");
        if (Modifiers.Contains(ModifierKey.Caps)) sb.Append("Caps+");
        if (Special.HasValue)
        {
            sb.Append("special:").Append(Special.Value);
        }
        else if (KeyCode.HasValue)
        {
            sb.Append("code:").Append(KeyCode.Value);
        }
        return sb.ToString();
    }
}
