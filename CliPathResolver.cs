using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace AIPaste
{
    /// <summary>
    /// Resolves the path to a system-installed GitHub Copilot CLI executable.
    /// When this returns null, the caller falls back to the SDK's bundled
    /// copilot.exe under runtimes\win-x64\native\.
    ///
    /// Strategy (in order):
    ///   1. Explicit override from config (<see cref="GitHubCopilotConfig.CliPathOverride"/>).
    ///   2. PATH lookup via `where copilot` (filtered to .exe).
    ///   3. Winget package directory (%LOCALAPPDATA%\Microsoft\WinGet\Packages).
    ///   4. npm global install (%APPDATA%\npm\node_modules\@github\copilot-win32-x64).
    ///   5. Return null — caller uses the bundled binary.
    ///
    /// Resolved paths are cached for the lifetime of the process to avoid
    /// repeated filesystem probing.
    /// </summary>
    internal static class CliPathResolver
    {
        private static string? _cachedPath;
        private static bool _resolved;

        public static string? Resolve()
        {
            if (_resolved) return _cachedPath;
            _resolved = true;

            var cfg = ConfigManager.GetConfig().GitHubCopilot;

            // 1. Explicit override wins.
            if (!string.IsNullOrWhiteSpace(cfg.CliPathOverride))
            {
                if (File.Exists(cfg.CliPathOverride))
                {
                    _cachedPath = cfg.CliPathOverride;
                    return _cachedPath;
                }
                // Configured but missing — fall through to auto-discovery.
            }

            _cachedPath = DiscoverSystemCli();
            return _cachedPath;
        }

        public static void Invalidate()
        {
            _resolved = false;
            _cachedPath = null;
        }

        private static string? DiscoverSystemCli()
        {
            // a) PATH lookup via `where copilot`. Filter to .exe — winget/npm shims
            //    (.bat / .ps1 / .cmd) confuse the SDK process spawner.
            try
            {
                var psi = new ProcessStartInfo("where", "copilot")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var p = Process.Start(psi);
                if (p != null)
                {
                    var output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(2000);
                    if (p.ExitCode == 0)
                    {
                        var exe = output
                            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                            .FirstOrDefault(line => line.EndsWith("copilot.exe", StringComparison.OrdinalIgnoreCase)
                                                    && File.Exists(line));
                        if (!string.IsNullOrEmpty(exe)) return exe;
                    }
                }
            }
            catch { /* best-effort */ }

            // b) Winget package directory (stable per-user install path).
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
            {
                var wingetRoot = Path.Combine(localAppData, "Microsoft", "WinGet", "Packages");
                if (Directory.Exists(wingetRoot))
                {
                    try
                    {
                        var hit = Directory
                            .EnumerateDirectories(wingetRoot, "GitHub.Copilot*")
                            .Select(d => Path.Combine(d, "copilot.exe"))
                            .FirstOrDefault(File.Exists);
                        if (!string.IsNullOrEmpty(hit)) return hit;
                    }
                    catch { /* best-effort */ }
                }
            }

            // c) npm global install (`npm i -g @github/copilot`).
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(appData))
            {
                var npmExe = Path.Combine(appData, "npm", "node_modules", "@github", "copilot-win32-x64", "copilot.exe");
                if (File.Exists(npmExe)) return npmExe;
            }

            return null;
        }
    }
}
