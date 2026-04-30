// FILE: src/GreatEmailApp.Core/Updates/UpdateInstaller.cs
// Created: 2026-04-30 | Revised: 2026-04-30 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed
//
// Downloads a release zip, extracts it to a staging folder, writes a
// throwaway .cmd that waits for this process to exit, mirrors the staged
// files over AppContext.BaseDirectory, then re-launches the new exe.
// We hand the swap off to cmd.exe because the running .exe locks itself —
// the cmd outlives our process and can replace the binary.

using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using GreatEmailApp.Core.Services;

namespace GreatEmailApp.Core.Updates;

public interface IUpdateInstaller
{
    /// <summary>
    /// Downloads + stages the update and kicks off the swap-and-restart cmd.
    /// On success the caller MUST call <see cref="Action"/> Application.Current.Shutdown()
    /// promptly — the cmd waits ~3s for the parent to exit before copying.
    /// </summary>
    Task<Result<bool>> DownloadAndApplyAsync(UpdateInfo info, IProgress<double>? progress = null, CancellationToken ct = default);
}

public sealed class UpdateInstaller : IUpdateInstaller
{
    private readonly HttpClient _http;
    private readonly string _installDir;
    private readonly string _exeName;

    public UpdateInstaller(string? installDir = null, string? exeName = null, HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        _installDir = installDir ?? AppContext.BaseDirectory.TrimEnd('\\', '/');
        _exeName    = exeName    ?? "GreatEmailApp.exe";
    }

    public async Task<Result<bool>> DownloadAndApplyAsync(
        UpdateInfo info, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        try
        {
            var stagingRoot = Path.Combine(Path.GetTempPath(), $"TGEA-update-{info.Version}");
            if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, recursive: true);
            Directory.CreateDirectory(stagingRoot);

            var zipPath = Path.Combine(stagingRoot, "package.zip");
            var unpackDir = Path.Combine(stagingRoot, "unpacked");

            // 1. Download
            using (var resp = await _http.GetAsync(info.ZipDownloadUrl,
                    HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
            {
                resp.EnsureSuccessStatusCode();
                var total = resp.Content.Headers.ContentLength ?? info.ZipSizeBytes ?? -1L;
                using var src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                using var dst = File.Create(zipPath);
                var buf = new byte[81920];
                long read = 0; int n;
                while ((n = await src.ReadAsync(buf, ct).ConfigureAwait(false)) > 0)
                {
                    await dst.WriteAsync(buf.AsMemory(0, n), ct).ConfigureAwait(false);
                    read += n;
                    if (total > 0) progress?.Report((double)read / total);
                }
            }

            // 2. Extract. Some publishers wrap the build in a top-level folder; if
            // exactly one directory shakes out, treat its contents as the payload.
            ZipFile.ExtractToDirectory(zipPath, unpackDir);
            var payloadDir = unpackDir;
            var entries = Directory.GetFileSystemEntries(unpackDir);
            if (entries.Length == 1 && Directory.Exists(entries[0]))
                payloadDir = entries[0];

            // 3. Write the swap script alongside the staged payload.
            var scriptPath = Path.Combine(stagingRoot, "apply-update.cmd");
            var logPath    = Path.Combine(stagingRoot, "apply-update.log");
            var script = BuildSwapScript(payloadDir, _installDir, _exeName, logPath);
            await File.WriteAllTextAsync(scriptPath, script, new UTF8Encoding(false), ct).ConfigureAwait(false);

            // 4. Fire-and-forget the script. cmd.exe stays alive after our exit.
            Process.Start(new ProcessStartInfo
            {
                FileName        = "cmd.exe",
                Arguments       = $"/c \"\"{scriptPath}\"\"",
                CreateNoWindow  = true,
                UseShellExecute = false,
                WorkingDirectory = stagingRoot,
            });

            return Result.Ok(true);
        }
        catch (Exception ex)
        {
            return Result.Fail<bool>($"Update install failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// The script does four things: (1) wait for our exe to release its file
    /// lock, (2) robocopy /MIR the new files over the install dir — but exclude
    /// the user's appsettings.json so per-machine config survives, (3) launch
    /// the new exe, (4) self-delete.
    /// </summary>
    private static string BuildSwapScript(string sourceDir, string installDir, string exeName, string logPath)
    {
        // /MIR mirrors source → dest (deletes orphaned files in dest). /XO skips
        // overwriting newer files in dest. /R:5 /W:1 keeps retry storms short.
        // /XF appsettings.local.json: leave any local override alone.
        // We deliberately do NOT exclude appsettings.json from copy — it ships
        // with the build and changing it across versions is intentional.
        return $@"@echo off
setlocal
set ""LOG={logPath}""
echo [%date% %time%] update script start >> ""%LOG%""
echo Source: {sourceDir} >> ""%LOG%""
echo Target: {installDir} >> ""%LOG%""

rem Wait up to ~10s for the running exe to exit + release its file lock.
for /l %%i in (1,1,20) do (
    tasklist /fi ""IMAGENAME eq {exeName}"" | find /i ""{exeName}"" >nul && (
        timeout /t 1 /nobreak >nul
    ) || (
        goto :ready
    )
)
:ready
echo [%date% %time%] mirroring >> ""%LOG%""
robocopy ""{sourceDir}"" ""{installDir}"" /MIR /R:5 /W:1 /XF appsettings.local.json /NFL /NDL /NP >> ""%LOG%""
set RC=%ERRORLEVEL%
echo [%date% %time%] robocopy rc=%RC% >> ""%LOG%""

rem robocopy success: rc < 8. 0=no copy, 1=copied, 2=extra, 3=copied+extra.
if %RC% GEQ 8 (
    echo [%date% %time%] FAILED — robocopy returned %RC% >> ""%LOG%""
    exit /b %RC%
)

echo [%date% %time%] launching {exeName} >> ""%LOG%""
start """" ""{installDir}\{exeName}""

rem Best-effort self-cleanup. Run from %TEMP% so we don't sit in the install dir.
(goto) 2>nul & rmdir /s /q ""{Path.GetDirectoryName(logPath)}""
";
    }
}
