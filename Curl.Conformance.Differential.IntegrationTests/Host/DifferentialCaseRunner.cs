namespace Curl.Conformance.Differential.IntegrationTests.Host;

/// <summary>
/// Runs cases through Curl (tag <c>A</c>) and the reference curl (tag <c>B</c>) one at a time,
/// since the loopback server's state and wire log are shared: resets the server, runs the
/// binary, fetches the wire log from the control port, then normalises and compares.
/// </summary>
internal sealed class DifferentialCaseRunner(
    DifferentialProcessRunner processRunner, int controlPort, IReadOnlyList<string> ours, IReadOnlyList<string> reference, Action? throwIfServerFaulted = null)
{
    private const int ControlAttempts = 5;

    /// <summary>Runs every case the reference build can run and returns each one's outcome.</summary>
    public async Task<IReadOnlyList<DifferentialCaseOutcome>> RunAsync(IEnumerable<DifferentialCase> cases)
    {
        // The control responder answers one request per connection, so a pooled connection would be reset.
        using var control = new HttpClient(new SocketsHttpHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(5) };
        control.DefaultRequestHeaders.ConnectionClose = true;
        var outcomes = new List<DifferentialCaseOutcome>();
        foreach (var differentialCase in cases.Where(candidate => candidate.RunsAgainstReferenceBuild))
        {
            var oursResult = await RunOneAsync(control, ours, differentialCase, "A");
            var realResult = await RunOneAsync(control, reference, differentialCase, "B");
            outcomes.Add(DifferentialComparer.Compare(differentialCase, oursResult, realResult));
        }

        return outcomes;
    }

    private async Task<DifferentialRunResult> RunOneAsync(HttpClient control, IReadOnlyList<string> command, DifferentialCase differentialCase, string tag)
    {
        throwIfServerFaulted?.Invoke();
        await FetchControlAsync(control, "/reset", differentialCase, tag);
        var result = await processRunner.RunAsync(command, differentialCase, tag);
        var wire = await FetchControlAsync(control, "/log", differentialCase, tag);
        throwIfServerFaulted?.Invoke();
        return DifferentialOutputNormaliser.Normalise(result with { Wire = wire }, processRunner.WorkingDirectoryOf(differentialCase, tag));
    }

    /// <summary>
    /// Fetches <paramref name="path"/> from the control port, trying again when the connection is
    /// reset: the control responder closes first, so its side of each connection sits in TIME_WAIT,
    /// and Windows can hand that ephemeral port to the next request, which is then reset before the
    /// responder reads it.
    /// </summary>
    private async Task<byte[]> FetchControlAsync(HttpClient control, string path, DifferentialCase differentialCase, string tag)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await control.GetByteArrayAsync(ControlUrl(path));
            }
            catch (HttpRequestException exception) when (attempt < ControlAttempts && exception.InnerException is IOException)
            {
            }
            catch (HttpRequestException exception)
            {
                throw new InvalidOperationException($"The control port failed {path} for case {differentialCase.Name} ({tag}).", exception);
            }
        }
    }

    private Uri ControlUrl(string path) => new($"http://127.0.0.1:{controlPort}{path}");
}
