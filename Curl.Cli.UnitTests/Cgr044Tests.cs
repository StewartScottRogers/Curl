namespace Curl.Cli;

/// <summary>
/// Pins CGR-044 (BL-3573): on Windows curl 8.22.0 reads the <c>-u</c> and <c>--proxy-user</c>
/// password with <c>_getch</c>, from the console input buffer, so a redirected standard input
/// (<c>&lt; pw.txt</c>, <c>&lt; /dev/null</c>) is never read and the prompt waits for the console.
/// <see cref="Console.ReadKey(bool)"/> threw on a redirected standard input instead, and the
/// password came back empty at once.
/// </summary>
[TestClass]
public sealed class Cgr044Tests
{
    [TestMethod]
    public void ForPlatform_OnWindows_ReadsKeysFromTheConsoleInputBuffer()
    {
        ConsolePasswordPrompt prompt = ConsolePasswordPrompt.ForPlatform(isWindows: true, TextWriter.Null);

        Assert.AreEqual<Func<ConsoleKeyInfo>>(WindowsConsolePasswordInput.ReadKey, prompt.KeyReader);
    }

    [TestMethod]
    public void ForPlatform_OffWindows_DoesNotReadTheWindowsConsoleInputBuffer()
    {
        ConsolePasswordPrompt prompt = ConsolePasswordPrompt.ForPlatform(isWindows: false, TextWriter.Null);

        Assert.AreNotEqual<Func<ConsoleKeyInfo>>(WindowsConsolePasswordInput.ReadKey, prompt.KeyReader);
    }
}
