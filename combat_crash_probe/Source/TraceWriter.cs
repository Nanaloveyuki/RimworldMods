using System;
using System.IO;
using System.Text;

namespace CombatCrashProbe;

internal static class TraceWriter
{
    private const long MaxBytes = 16 * 1024 * 1024;
    private static readonly object Gate = new();
    internal static string LogPath { get; private set; } = "";

    internal static void Initialize(string directory)
    {
        Directory.CreateDirectory(directory);
        LogPath = Path.Combine(directory, $"combat-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Environment.ProcessId}.log");
        Write("SESSION " + DateTimeOffset.Now.ToString("O") + " pid=" + Environment.ProcessId);
    }

    internal static void Write(string message)
    {
        lock (Gate)
        {
            try
            {
                if (LogPath.Length == 0) return;
                if (File.Exists(LogPath) && new FileInfo(LogPath).Length >= MaxBytes)
                    File.Move(LogPath, LogPath + ".previous", overwrite: true);
                using var file = new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                using var writer = new StreamWriter(file, new UTF8Encoding(false), leaveOpen: true);
                writer.WriteLine(DateTimeOffset.Now.ToString("O") + " " + message);
                writer.Flush();
                file.Flush(flushToDisk: true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
