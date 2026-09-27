using System.Text;

namespace Curl.Conformance;

/// <summary>
/// A test case's <c>&lt;reply&gt;&lt;postcmd&gt;</c> read the way <c>sws_send_doc</c> in upstream's
/// <c>tests/server/sws.c</c> at <c>curl-8_21_0</c> reads it after sending a reply: each line is
/// a word and a number, and <c>wait N</c> sleeps N seconds before the server goes on; any other
/// line is ignored.
/// </summary>
internal static class SwsPostReplyCommands
{
    /// <summary>How long sws sleeps after a reply: the sum of every <c>wait N</c>, a negative N counting as none.</summary>
    /// <param name="body">The <c>&lt;postcmd&gt;</c> body, after the test file's expansion.</param>
    /// <returns>The time to wait after each reply.</returns>
    public static TimeSpan ReadWaitAfterReply(ReadOnlySpan<byte> body)
    {
        long seconds = 0;
        foreach (string line in Encoding.Latin1.GetString(body).Split('\n'))
        {
            seconds += Math.Max(0, WaitSeconds(line.Split([' ', '\t', '\r'], StringSplitOptions.RemoveEmptyEntries)));
        }

        return TimeSpan.FromSeconds(seconds);
    }

    // sscanf "%31s %d": a word of up to 31 characters, then a number.
    private static int WaitSeconds(string[] words) =>
        words.Length > 1 && words[0] == "wait" ? SwsServerCommands.LeadingInteger(words[1]) ?? 0 : 0;
}
