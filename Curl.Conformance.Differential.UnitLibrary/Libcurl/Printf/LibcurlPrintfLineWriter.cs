using System.Globalization;

namespace Curl.Conformance.Differential.Libcurl.Printf;

/// <summary>Writes one scenario's result line as the tester's <c>printftest.py</c> and <c>LibTest</c> did, without the index.</summary>
public static class LibcurlPrintfLineWriter
{
    /// <summary>The size of the buffer both libraries format into (the <c>maxlength</c>).</summary>
    public const int BufferSize = 4096;

    /// <summary>The line of a scenario not run against the reference build (<see cref="LibcurlPrintfScenario.CrashesReference"/>).</summary>
    public const string NotRun = "NOT RUN crashes the reference build";

    /// <summary>Writes a call's return value and the bytes it formatted.</summary>
    /// <param name="count">What <c>curl_msnprintf</c> returned.</param>
    /// <param name="buffer">The buffer it formatted into.</param>
    /// <returns><c>count HEX</c>: the hex of the first <paramref name="count"/> bytes, at most the buffer's length less one.</returns>
    public static string Result(int count, ReadOnlySpan<byte> buffer) =>
        string.Create(CultureInfo.InvariantCulture, $"{count} {Convert.ToHexString(buffer[..Math.Clamp(count, 0, Math.Max(buffer.Length - 1, 0))])}");

    /// <summary>Writes a call that threw.</summary>
    /// <param name="exception">What it threw.</param>
    /// <returns><c>EXC</c> and the exception's type name.</returns>
    public static string Failure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return $"EXC {exception.GetType().Name}";
    }
}
