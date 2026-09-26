namespace Curl.Cli;

/// <summary>
/// Asks the person at the console for a password. <see cref="CommandLineParser"/> calls it when
/// <c>-u</c> / <c>--user</c> names a user with no password, as curl 8.21.0 does; injected so the
/// parser never touches the console and tests can answer for the user.
/// </summary>
public interface IPasswordPrompt
{
    /// <summary>Shows <paramref name="prompt"/> and returns the password typed in answer.</summary>
    /// <param name="prompt">The prompt text, curl's exactly, with no trailing space or line terminator.</param>
    /// <returns>The password typed, without its line terminator; empty when nothing was typed.</returns>
    string ReadPassword(string prompt);
}
