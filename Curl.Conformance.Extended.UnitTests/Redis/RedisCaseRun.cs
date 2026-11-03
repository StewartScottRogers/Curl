using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Redis;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.Redis;

/// <summary>Runs one Redis case: curl in process against a <see cref="RedisReferenceServer" /> (TCP or TLS), and checks what came of it.</summary>
internal static class RedisCaseRun
{
    /// <summary>The redis URI scheme's default port.</summary>
    internal const int DefaultPort = 6379;

    /// <summary>A RESP3 server's reply to <c>HELLO 3</c>: a map naming the protocol version.</summary>
    internal const string Hello3Reply = "%1\r\n+proto\r\n:3\r\n";

    private static readonly InProcessCurlRunner CurlRunner = InProcessCurl.RunAsync;

    /// <summary>Gets the known failures every Redis case runs under.</summary>
    internal static KnownFailures Known { get; } = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    /// <summary>A server that answers <c>HELLO</c> as RESP3 does and every other command with <paramref name="reply" />, one write per chunk.</summary>
    internal static Func<string, string[]> HelloThen(params string[] reply) => name => name == "HELLO" ? [Hello3Reply] : reply;

    /// <summary>Runs <c>curl -sS -m 10</c> with <paramref name="arguments" /> against a TCP server on <paramref name="port" />.</summary>
    internal static Task<CurlRunResult> RunOnPortAsync(Func<string, string[]> reply, int port, params string[] arguments) =>
        RunAsync(ReferenceRoute.Tcp(port, Server(reply)), arguments);

    /// <summary>Runs <c>curl -sS -m 10</c> with <paramref name="arguments" /> against a TCP server on port 6379.</summary>
    internal static Task<CurlRunResult> RunAsync(Func<string, string[]> reply, params string[] arguments) =>
        RunOnPortAsync(reply, DefaultPort, arguments);

    /// <summary>Runs <c>curl -sS -m 10 -k</c> with <paramref name="arguments" /> against a TLS server on port 6379.</summary>
    internal static async Task<CurlRunResult> RunTlsAsync(Func<string, string[]> reply, params string[] arguments)
    {
        using X509Certificate2 certificate = ReferenceCertificate.CreateSelfSigned("localhost");
        return await RunAsync(ReferenceRoute.Tls(DefaultPort, Server(reply), certificate), ["-k", .. arguments]);
    }

    /// <summary>A command as the RESP specification has a client send it: an array of bulk strings.</summary>
    internal static string Command(params string[] arguments) =>
        $"*{arguments.Length}\r\n" + string.Concat(arguments.Select(argument => $"${Encoding.Latin1.GetByteCount(argument)}\r\n{argument}\r\n"));

    /// <summary>Checks the run's exit code, standard output, standard error and the bytes curl sent on its one connection.</summary>
    internal static void AssertRun(CurlRunResult result, string requirement, int exitCode, string standardOutput, string standardError, string wire)
    {
        Assert.AreEqual(standardError.Replace("\n", Environment.NewLine, StringComparison.Ordinal), Encoding.Latin1.GetString(result.StandardError), $"{requirement}: standard error");
        Assert.AreEqual(exitCode, result.ExitCode, $"{requirement}: exit code");
        Assert.AreEqual(standardOutput, Encoding.Latin1.GetString(result.StandardOutput), $"{requirement}: standard output");
        Assert.AreEqual(wire, Encoding.Latin1.GetString(result.Exchanges.Single().ClientBytes), $"{requirement}: bytes curl sent");
    }

    private static RedisReferenceServer Server(Func<string, string[]> reply) =>
        new(command => [.. reply(Encoding.UTF8.GetString(command[0])).Select(Encoding.Latin1.GetBytes)]);

    private static Task<CurlRunResult> RunAsync(ReferenceRoute route, string[] arguments) =>
        ExtendedConformanceHarness.RunAsync(CurlRunner, [route], ["-sS", "-m", "10", .. arguments]);
}
