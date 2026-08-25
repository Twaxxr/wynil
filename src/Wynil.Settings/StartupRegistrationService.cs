using Microsoft.Win32;

namespace NowSpinning.Settings;

public static class StartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static void SetEnabled(string applicationName, string executablePath, bool enabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true);
        if (enabled) key.SetValue(applicationName, $"\"{executablePath}\" --background", RegistryValueKind.String);
        else key.DeleteValue(applicationName, false);
    }
}
