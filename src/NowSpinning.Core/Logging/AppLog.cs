using System.Text.Json;

namespace NowSpinning.Core.Logging;

public static class AppLog
{
    private static readonly object Gate = new();
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NowSpinning", "Logs");

    public static void Write(string eventName, object? data = null)
    {
        try
        {
            var line = JsonSerializer.Serialize(new { timestamp = DateTimeOffset.Now, eventName, data });
            lock (Gate)
            {
                Directory.CreateDirectory(LogDirectory);
                File.AppendAllText(Path.Combine(LogDirectory, "nowspinning.jsonl"), line + Environment.NewLine);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
