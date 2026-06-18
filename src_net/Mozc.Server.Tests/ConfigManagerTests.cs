using Mozc.Server;
using Xunit;
using Pb = Mozc.Config;

namespace Mozc.Server.Tests;

public class ConfigManagerTests
{
    [Fact]
    public void Default_HasExpectedValues()
    {
        var m = new ConfigManager();
        Pb.Config c = m.GetConfig();
        Assert.Equal(Pb.Config.Types.PreeditMethod.Roman, c.PreeditMethod);
        Assert.Equal(Pb.Config.Types.SessionKeymap.Msime, c.SessionKeymap);
        Assert.Equal(Pb.Config.Types.HistoryLearningLevel.DefaultHistory, c.HistoryLearningLevel);
    }

    [Fact]
    public void SetGet_RoundTrip()
    {
        var m = new ConfigManager();
        Pb.Config c = m.GetConfig();
        c.IncognitoMode = true;
        c.PreeditMethod = Pb.Config.Types.PreeditMethod.Kana;
        m.SetConfig(c);

        Pb.Config back = m.GetConfig();
        Assert.True(back.IncognitoMode);
        Assert.Equal(Pb.Config.Types.PreeditMethod.Kana, back.PreeditMethod);
    }

    [Fact]
    public void GetConfig_ReturnsCopy()
    {
        var m = new ConfigManager();
        Pb.Config c = m.GetConfig();
        c.IncognitoMode = true; // 取得物への変更は内部状態に波及しない。
        Assert.False(m.GetConfig().IncognitoMode);
    }

    [Fact]
    public void Serialize_Load_RoundTrip()
    {
        var m = new ConfigManager();
        Pb.Config c = m.GetConfig();
        c.SymbolMethod = Pb.Config.Types.SymbolMethod.SquareBracketSlash;
        m.SetConfig(c);
        byte[] bytes = m.Serialize();

        var m2 = new ConfigManager();
        Assert.True(m2.Load(bytes));
        Assert.Equal(Pb.Config.Types.SymbolMethod.SquareBracketSlash, m2.GetConfig().SymbolMethod);
    }

    [Fact]
    public void SaveLoadFile_RoundTrip()
    {
        string id = global::System.Guid.NewGuid().ToString("N");
        string path = global::System.IO.Path.Combine(global::System.IO.Path.GetTempPath(), $"mozc_cfg_{id}.db");
        try
        {
            var m = new ConfigManager();
            Pb.Config c = m.GetConfig();
            c.IncognitoMode = true;
            m.SetConfig(c);
            m.Save(path);

            var m2 = new ConfigManager();
            Assert.True(m2.LoadFile(path));
            Assert.True(m2.GetConfig().IncognitoMode);
        }
        finally
        {
            if (global::System.IO.File.Exists(path)) { global::System.IO.File.Delete(path); }
        }
    }
}
