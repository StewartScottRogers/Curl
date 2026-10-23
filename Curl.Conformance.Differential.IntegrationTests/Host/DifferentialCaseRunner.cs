namespace Curl.Conformance.Differential.IntegrationTests.Host;

/// <summary>
/// Runs cases through Curl (tag <c>A</c>) and the reference curl (tag <c>B</c>) one at a time,
/// since the loopback server's state and wire log are shared: resets the server, runs the
/// binary, fetches the wire log from the control port, then normalises and compares.
/// </summary>
internal sealed class DifferentialCaseRunner(
    DifferentialProcessRunner processRunner, int controlPort, IReadOnlyList<string> ours, IReadOnlyList<string> reference)
{
    /// <summary>Runs every case the reference build can run and returns each one's outcome.</summary>
    public async Task<IReadOnlyList<DifferentialCaseOutcome>> RunAsync(IEnumerable<DifferentialCase> cases)
    {
        using var control = new HttpClient(new SocketsHttpHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(5) };
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
        await control.GetByteArrayAsync(ControlUrl("/reset"));
        var result = await processRunner.RunAsync(command, differentialCase, tag);
        var wire = await control.GetByteArrayAsync(ControlUrl("/log"));
        return DifferentialOutputNormaliser.Normalise(result with { Wire = wire }, processRunner.WorkingDirectoryOf(differentialCase, tag));
    }

    private Uri ControlUrl(string path) => new($"http://127.0.0.1:{controlPort}{path}");
}
