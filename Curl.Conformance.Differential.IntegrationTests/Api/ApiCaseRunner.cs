using System.Diagnostics;
using Curl.Api;
using Curl.Api.Codes;
using Curl.Api.Errors;
using Curl.Conformance.Differential.Api;
using Curl.Conformance.Differential.IntegrationTests.Host;

namespace Curl.Conformance.Differential.IntegrationTests.Api;

/// <summary>
/// Runs each <see cref="ApiDifferentialCase"/> through <c>Curl.Api</c> in process (tag <c>A</c>) and
/// its command line through the reference curl (tag <c>B</c>), one at a time since the loopback
/// server's state and wire log are shared, then normalises and compares (ADR-0790).
/// </summary>
internal sealed class ApiCaseRunner(DifferentialProcessRunner processRunner, string baseUrl, int controlPort, IReadOnlyList<string> reference)
{
    private const int ControlAttempts = 5;

    /// <summary>Runs every case and returns each one's outcome.</summary>
    public async Task<IReadOnlyList<DifferentialCaseOutcome>> RunAsync(IEnumerable<ApiDifferentialCase> cases)
    {
        // The control responder answers one request per connection, so a pooled connection would be reset.
        using var control = new HttpClient(new SocketsHttpHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(5) };
        control.DefaultRequestHeaders.ConnectionClose = true;
        var outcomes = new List<DifferentialCaseOutcome>();
        foreach (var apiCase in cases)
        {
            var differentialCase = apiCase.ToDifferentialCase();
            await FetchControlAsync(control, "/reset");
            processRunner.CreateWorkingDirectory(differentialCase, "A");
            var ours = await RunThroughApiAsync(apiCase.Request) with { Wire = await FetchControlAsync(control, "/log") };
            await FetchControlAsync(control, "/reset");
            var real = await processRunner.RunAsync(reference, differentialCase, "B") with { Wire = await FetchControlAsync(control, "/log") };
            outcomes.Add(DifferentialComparer.Compare(
                differentialCase,
                DifferentialOutputNormaliser.Normalise(ours, processRunner.WorkingDirectoryOf(differentialCase, "A")),
                DifferentialOutputNormaliser.Normalise(real, processRunner.WorkingDirectoryOf(differentialCase, "B"))));
        }

        return outcomes;
    }

    /// <summary>Sends <paramref name="request"/> on a new client and reads its whole body; a failure gives its <see cref="CurlCode"/> as the exit code.</summary>
    private async Task<DifferentialRunResult> RunThroughApiAsync(ApiDifferentialRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        using var timeout = new CancellationTokenSource(ApiDifferentialCase.Timeout);
        await using var client = CurlClient.Create();
        using var body = new MemoryStream();
        var responseCode = 0;
        int? exitCode = 0;
        try
        {
            var response = await client.SendAsync(ApiRequestMapping.ToCurlRequest(request, baseUrl), timeout.Token);
            responseCode = response.StatusCode;
            await using var stream = response.Body;
            await stream.CopyToAsync(body, timeout.Token);
        }
        catch (CurlException exception)
        {
            exitCode = (int)exception.Code;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            exitCode = null;
        }

        return new DifferentialRunResult(
            exitCode, ApiDifferentialOutput.Render(body.ToArray(), responseCode), [], new Dictionary<string, byte[]>(), [], stopwatch.Elapsed);
    }

    /// <summary>Fetches <paramref name="path"/> from the control port, trying again when the connection is reset, as <see cref="DifferentialCaseRunner"/> does.</summary>
    private async Task<byte[]> FetchControlAsync(HttpClient control, string path)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await control.GetByteArrayAsync(new Uri($"http://127.0.0.1:{controlPort}{path}"));
            }
            catch (HttpRequestException exception) when (attempt < ControlAttempts && exception.InnerException is IOException)
            {
            }
        }
    }
}
