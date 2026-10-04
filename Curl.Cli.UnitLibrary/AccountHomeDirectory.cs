namespace Curl.Cli;

/// <summary>
/// The account's home directory from the user database, which curl searches last off Windows (for
/// <c>.curlrc</c> and <c>known_hosts</c>) and never on Windows. On Windows it is not looked up at
/// all: <see cref="Environment.GetFolderPath(Environment.SpecialFolder)"/> calls
/// <c>SHGetKnownFolderPath</c>, which loads <c>shell32.dll</c> and with it <c>user32.dll</c>,
/// <c>gdi32.dll</c> and <c>imm32.dll</c>, about a megabyte of working set real <c>curl.exe</c> never
/// maps (BL-1290).
/// </summary>
public static class AccountHomeDirectory
{
    /// <summary>This process's account home directory where curl reads it; <see langword="null"/> on Windows.</summary>
    public static string? ForProcess { get; } =
        ReadOffWindows(OperatingSystem.IsWindows(), () => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    /// <summary>Reads the account home directory unless running on Windows, where curl never searches it.</summary>
    /// <param name="isWindows">Whether the process runs on Windows.</param>
    /// <param name="readFromUserDatabase">Reads the directory from the user database; not called on Windows.</param>
    /// <returns>The directory; <see langword="null"/> on Windows.</returns>
    public static string? ReadOffWindows(bool isWindows, Func<string> readFromUserDatabase) =>
        isWindows ? null : readFromUserDatabase();
}
