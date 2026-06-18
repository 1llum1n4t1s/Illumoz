using System.Runtime.InteropServices;

namespace Mozc.Base;

// C++ src/base/system_util.cc の UserProfileDirectory 解決と、
// ipc_path_manager.cc の GetIPCKeyFileName 相当を C# 化。
// AOT 安全(リフレクション/動的生成なし)。
public static class MozcPaths
{
    private static string? _overrideProfileDir;

    // テスト用: プロファイルディレクトリを差し替える(C++ の SetUserProfileDirectory 相当)。
    public static void OverrideUserProfileDirectory(string? dir) => _overrideProfileDir = dir;

    // SystemUtil::GetUserProfileDirectory 相当。
    //   Windows: %LOCALAPPDATA%\Mozc
    //   macOS:   ~/Library/Application Support/Mozc
    //   Linux:   $HOME/.mozc が存在すればそれ、なければ $XDG_CONFIG_HOME/mozc、なければ $HOME/.config/mozc
    public static string GetUserProfileDirectory()
    {
        if (_overrideProfileDir is not null)
        {
            return _overrideProfileDir;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, MozcConstants.ProductNameInEnglish);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            string home = GetHome();
            return Path.Combine(home, "Library", "Application Support", MozcConstants.ProductNameInEnglish);
        }

        // Linux / その他 POSIX
        string homeDir = GetHome();
        string oldDir = Path.Combine(homeDir, ".mozc");
        if (Directory.Exists(oldDir))
        {
            return oldDir;
        }

        string xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? string.Empty;
        if (!string.IsNullOrEmpty(xdg))
        {
            return Path.Combine(xdg, "mozc");
        }

        return Path.Combine(homeDir, ".config", "mozc");
    }

    // ipc_path_manager.cc: GetIPCKeyFileName。
    //   <UserProfileDir>/<""(Win) または "."(POSIX)><name>.ipc
    public static string GetIpcKeyFileName(string name)
    {
        string prefix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? string.Empty : ".";
        string basename = $"{prefix}{name}.ipc";
        return Path.Combine(GetUserProfileDirectory(), basename);
    }

    // user:// 論理パス解決(config1.db / user_dictionary.db 等)。
    public static string ResolveUserPath(string fileName)
        => Path.Combine(GetUserProfileDirectory(), fileName);

    private static string GetHome()
    {
        string home = Environment.GetEnvironmentVariable("HOME") ?? string.Empty;
        if (!string.IsNullOrEmpty(home))
        {
            return home;
        }
        // フォールバック(.NET の UserProfile)
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
}
