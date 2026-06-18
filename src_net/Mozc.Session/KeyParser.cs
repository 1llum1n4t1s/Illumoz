using System.Text;

namespace Mozc.Session;

// C++ src/composer/key_parser.cc の ParseKey 相当。"Ctrl @" / "Shift Enter" / "a" 等を
// KeyEvent に解釈する。空白区切りトークン: 単一文字=key_code、修飾語=modifier、其他=special。
public static class KeyParser
{
    private static readonly Dictionary<string, ModifierKey> ModifierMap = new()
    {
        ["ctrl"] = ModifierKey.Ctrl, ["control"] = ModifierKey.Ctrl,
        ["leftctrl"] = ModifierKey.Ctrl, ["rightctrl"] = ModifierKey.Ctrl,
        ["alt"] = ModifierKey.Alt, ["option"] = ModifierKey.Alt, ["meta"] = ModifierKey.Alt,
        ["super"] = ModifierKey.Alt, ["hyper"] = ModifierKey.Alt,
        ["leftalt"] = ModifierKey.Alt, ["rightalt"] = ModifierKey.Alt,
        ["shift"] = ModifierKey.Shift, ["leftshift"] = ModifierKey.Shift,
        ["rightshift"] = ModifierKey.Shift,
        ["caps"] = ModifierKey.Caps,
    };

    private static readonly Dictionary<string, SpecialKey> SpecialMap = new()
    {
        ["on"] = SpecialKey.On, ["off"] = SpecialKey.Off,
        ["left"] = SpecialKey.Left, ["down"] = SpecialKey.Down, ["up"] = SpecialKey.Up,
        ["right"] = SpecialKey.Right, ["enter"] = SpecialKey.Enter, ["return"] = SpecialKey.Enter,
        ["esc"] = SpecialKey.Escape, ["escape"] = SpecialKey.Escape,
        ["delete"] = SpecialKey.Del, ["del"] = SpecialKey.Del,
        ["bs"] = SpecialKey.Backspace, ["backspace"] = SpecialKey.Backspace,
        ["henkan"] = SpecialKey.Henkan, ["muhenkan"] = SpecialKey.Muhenkan,
        ["kana"] = SpecialKey.Kana, ["hiragana"] = SpecialKey.Kana, ["katakana"] = SpecialKey.Katakana,
        ["eisu"] = SpecialKey.Eisu, ["home"] = SpecialKey.Home, ["end"] = SpecialKey.End,
        ["space"] = SpecialKey.Space, ["ascii"] = SpecialKey.TextInput, ["textinput"] = SpecialKey.TextInput,
        ["tab"] = SpecialKey.Tab, ["pageup"] = SpecialKey.PageUp, ["pagedown"] = SpecialKey.PageDown,
        ["insert"] = SpecialKey.Insert, ["hankaku"] = SpecialKey.Hankaku, ["zenkaku"] = SpecialKey.Hankaku,
        ["hankaku/zenkaku"] = SpecialKey.Hankaku, ["kanji"] = SpecialKey.Kanji,
        ["f1"] = SpecialKey.F1, ["f2"] = SpecialKey.F2, ["f3"] = SpecialKey.F3, ["f4"] = SpecialKey.F4,
        ["f5"] = SpecialKey.F5, ["f6"] = SpecialKey.F6, ["f7"] = SpecialKey.F7, ["f8"] = SpecialKey.F8,
        ["f9"] = SpecialKey.F9, ["f10"] = SpecialKey.F10, ["f11"] = SpecialKey.F11, ["f12"] = SpecialKey.F12,
        ["numpad0"] = SpecialKey.Numpad0, ["numpad1"] = SpecialKey.Numpad1, ["numpad2"] = SpecialKey.Numpad2,
        ["numpad3"] = SpecialKey.Numpad3, ["numpad4"] = SpecialKey.Numpad4, ["numpad5"] = SpecialKey.Numpad5,
        ["numpad6"] = SpecialKey.Numpad6, ["numpad7"] = SpecialKey.Numpad7, ["numpad8"] = SpecialKey.Numpad8,
        ["numpad9"] = SpecialKey.Numpad9,
        ["undefinedkey"] = SpecialKey.UndefinedKey,
    };

    public static bool TryParse(string keyString, out KeyEvent keyEvent)
    {
        keyEvent = new KeyEvent();
        string[] tokens = keyString.Split(' ', global::System.StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return false;
        }
        foreach (string token in tokens)
        {
            // 単一コードポイント(ASCII 記号含む)は key_code。
            var runes = new List<global::System.Text.Rune>();
            foreach (global::System.Text.Rune r in token.EnumerateRunes())
            {
                runes.Add(r);
            }
            if (runes.Count == 1 && !ModifierMap.ContainsKey(token.ToLowerInvariant())
                && !SpecialMap.ContainsKey(token.ToLowerInvariant()))
            {
                if (keyEvent.KeyCode.HasValue)
                {
                    return false; // 複数キーコードは非対応
                }
                keyEvent.KeyCode = runes[0].Value;
                continue;
            }
            string lower = token.ToLowerInvariant();
            if (ModifierMap.TryGetValue(lower, out ModifierKey mod))
            {
                keyEvent.Modifiers.Add(mod);
                continue;
            }
            if (SpecialMap.TryGetValue(lower, out SpecialKey sk))
            {
                if (keyEvent.Special.HasValue)
                {
                    return false;
                }
                keyEvent.Special = sk;
                continue;
            }
            return false; // 未知キー
        }
        return true;
    }
}
