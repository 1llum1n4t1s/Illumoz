using Google.Protobuf;
using Pb = Mozc.Config;

namespace Mozc.Server;

// C++ src/config/config_handler.cc 相当(中核スライス)。Config(protobuf)を保持し、
// 既定値の付与・取得・更新・ファイル永続化(config1.db 相当・protobuf binary)を行う。
// AOT 安全(JSON 不使用、protobuf binary のみ)。
public sealed class ConfigManager
{
    private readonly object _lock = new();
    private Pb.Config _config;

    public ConfigManager() => _config = CreateDefault();

    // C++ ConfigHandler::GetDefaultConfig 相当の主要既定値。
    public static Pb.Config CreateDefault() => new()
    {
        PreeditMethod = Pb.Config.Types.PreeditMethod.Roman,
        SessionKeymap = Pb.Config.Types.SessionKeymap.Msime,
        PunctuationMethod = Pb.Config.Types.PunctuationMethod.ToutenKuten,
        SymbolMethod = Pb.Config.Types.SymbolMethod.CornerBracketMiddleDot,
        SpaceCharacterForm = Pb.Config.Types.FundamentalCharacterForm.FundamentalInputMode,
        HistoryLearningLevel = Pb.Config.Types.HistoryLearningLevel.DefaultHistory,
        SelectionShortcut = Pb.Config.Types.SelectionShortcut.Shortcut123456789,
        UseKeyboardToChangePreeditMethod = false,
        IncognitoMode = false,
    };

    // 現在の設定のコピーを返す(GetConfig)。
    public Pb.Config GetConfig()
    {
        lock (_lock)
        {
            return _config.Clone();
        }
    }

    // 設定を差し替える(SetConfig)。version 等は呼び出し側責務。
    public void SetConfig(Pb.Config config)
    {
        lock (_lock)
        {
            _config = config.Clone();
        }
    }

    // protobuf binary でシリアライズ。
    public byte[] Serialize()
    {
        lock (_lock)
        {
            return _config.ToByteArray();
        }
    }

    public void Save(string path) => global::System.IO.File.WriteAllBytes(path, Serialize());

    // バイト列から読み込む。壊れていれば既定値のまま false。
    public bool Load(byte[] data)
    {
        try
        {
            Pb.Config parsed = Pb.Config.Parser.ParseFrom(data);
            lock (_lock)
            {
                _config = parsed;
            }
            return true;
        }
        catch (InvalidProtocolBufferException)
        {
            return false;
        }
    }

    public bool LoadFile(string path)
        => global::System.IO.File.Exists(path) && Load(global::System.IO.File.ReadAllBytes(path));
}
