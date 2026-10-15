namespace Curl.Cli;

/// <summary>
/// The account's home directory from the user database, which curl searches last off Windows (for
/// <c>.curlrc</c> and <c>known_hosts</c>) and never on Windows. Off Windows it is curl's
/// <c>getpwuid(geteuid())-&gt;pw_dir</c>, read by <see cref="UserDatabaseHomeDirectory"/> and never
/// <c>$HOME</c>, so a <c>HOME</c> naming another directory still leaves the account's files found
/// (BL-2336). On Windows it is not looked up at all, as curl never searches it (BL-1290).
/// </summary>
public static class AccountHomeDirectory
{
    /// <summary>This process's account home directory where curl reads it; <see langword="null"/> on Windows.</summary>
    public static string? ForProcess { get; } =
        ReadOffWindows(OperatingSystem.IsWindows(), UserDatabaseHomeDirectory.ReadForEffectiveUser);

    /// <summary>Reads the account home directory unless running on Windows, where curl never searches it.</summary>
    /// <param name="isWindows">Whether the process runs on Windows.</param>
    /// <param name="readFromUserDatabase">
    /// Reads the directory from the user database, <see langword="null"/> when it has no entry; not called on Windows.
    /// </param>
    /// <returns>The directory; <see langword="null"/> on Windows or when the user database has no entry.</returns>
    public static string? ReadOffWindows(bool isWindows, Func<string?> readFromUserDatabase) =>
        isWindows ? null : readFromUserDatabase();
}
