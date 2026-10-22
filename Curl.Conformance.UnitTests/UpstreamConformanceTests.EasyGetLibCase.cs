using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Conformance.LibTest;
using Curl.Engine;
using Curl.Testing;

namespace Curl.Conformance;

public sealed partial class UpstreamConformanceTests
{
    // A sws case whose <tool> is a test-only program doing an easy GET of its URL to stdout through the ratchet's
    // CreateEngine, so the managed engine's own transports reach the sws emulation (BL-2558, ADR-0658). Its data is
    // nocheck: how a lib case's <reply><data> is judged against the program's stdout is BL-2560's.
    private const string EasyGetLibCase =
        "<testcase>\n<reply>\n<data crlf=\"headers\" nocheck=\"yes\">\nHTTP/1.1 200 OK\nContent-Length: 6\nConnection: close\n\n-foo-\n</data>\n</reply>\n"
        + "<client>\n<server>\nhttp\n</server>\n<tool>\nlib%TESTNUMBER\n</tool>\n<name>\neasy GET through the engine\n</name>\n"
        + "<command>\nhttp://%HOSTIP:%HTTPPORT/%TESTNUMBER\n</command>\n</client>\n"
        + "<verify>\n<protocol crlf=\"yes\">\nGET /%TESTNUMBER HTTP/1.1\nHost: %HOSTIP:%HTTPPORT\nUser-Agent: lib%TESTNUMBER\nAccept: */*\n\n</protocol>\n"
        + "<stdout>\n-foo-\n</stdout>\n<errorcode>\n0\n</errorcode>\n</verify>\n</testcase>\n";

    [TestMethod]
    public async Task CreateEngine_EasyGetLibTestOnAnSwsCase_MatchesTheCaseProtocolAndStdout()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        EasyGetLibTest program = new("lib9558");
        UpstreamCaseRunner runner = new(
            _ => throw new AssertFailedException("curl must not run for a lib case"),
            Platform,
            TimeProvider.System,
            TimeLimit,
            libTests: new UpstreamLibTests(new LibTestPrograms([program]), CreateEngine));
        DirectoryInfo logDirectory = Directory.CreateDirectory(Path.Combine(LogFolder, $"test9558-{Guid.NewGuid():N}"));
        UpstreamCaseOutcome outcome;
        try
        {
            outcome = await runner.RunAsync(9558, Encoding.Latin1.GetBytes(EasyGetLibCase), logDirectory.FullName);
        }
        finally
        {
            DeleteOrKeepLogDirectory(logDirectory);
        }

        diagnostics.Act("outcome detail", outcome.Detail);
        diagnostics.Assert("outcome kind", UpstreamCaseOutcomeKind.Passed, outcome.Kind);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
        Assert.IsInstanceOfType<ManagedCurlEngine>(program.Engine);
    }

    // Upstream's lib500 at its simplest: one easy GET of the URL, the body written to stdout, the perform's code returned.
    // It sets CURLOPT_USERAGENT to its own name, since the engine still sends the tool's curl/8.22.0 without one (BL-2559).
    private sealed class EasyGetLibTest(string programName) : ILibTest
    {
        public ICurlEngine? Engine { get; private set; }

        public string ProgramName => programName;

        public async Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
        {
            Engine = context.Engine;
            await using ICurlEngine engine = context.Engine!;
            await using IEasyTransfer easy = engine.CreateEasy();
            MemoryStream body = new();
            easy.Options = new EasyOptions
            {
                Network = new EasyNetworkOptions { Url = context.Url },
                Http = new EasyHttpOptions { UserAgent = programName },
                Callbacks = new EasyCallbackOptions { Write = data => WriteTo(body, data) },
            };
            CurlCode code = await easy.PerformAsync(cancellationToken);
            await context.StandardOutput.WriteAsync(body.ToArray(), cancellationToken);
            return LibTestResult.FromCurlCode(code);
        }

        private static long WriteTo(MemoryStream body, ReadOnlySpan<byte> data)
        {
            body.Write(data);
            return data.Length;
        }
    }
}
