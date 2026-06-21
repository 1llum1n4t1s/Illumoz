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
    // per-request の一時 config を適用している間、退避しておく「永続(恒久)config」。
    // non-null の間は Serialize(=保存)がこちらを返し、一時設定がプロファイルへ焼き付くのを防ぐ。
    private Pb.Config? _persistent;

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

    // protobuf binary でシリアライズ。一時 config 適用中(BeginTransient 後)は永続値を返す
    // (ProcessExit 等の保存が一時 config を焼き付けないように)。保存・GET_CONFIG 応答の両方に使う。
    public byte[] Serialize()
    {
        lock (_lock)
        {
            return (_persistent ?? _config).ToByteArray();
        }
    }

    // per-request の一時 config 適用を開始する。現在の永続 config を退避する(多重呼び出しは無害)。
    // 以降の SetConfig は実効(評価用)の _config だけを変え、保存は退避した永続値を使う。
    public void BeginTransient()
    {
        lock (_lock)
        {
            _persistent ??= _config.Clone();
        }
    }

    // 一時 config 適用を終了し、実効 config を退避した永続値へ戻す。
    public void EndTransient()
    {
        lock (_lock)
        {
            if (_persistent != null)
            {
                _config = _persistent;
                _persistent = null;
            }
        }
    }

    // 保存は AtomicFile(temp→rename)で行い、保存中の異常終了でも設定全損を防ぐ。
    public void Save(string path) => Mozc.Base.AtomicFile.WriteAllBytes(path, Serialize());

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
