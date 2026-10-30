using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.StructuredFields.UnitTests;

[TestClass]
public sealed class StructuredFieldsTransferHookTests
{
    private readonly ITransferHook _hook = new StructuredFieldsTransferHook();

    [TestMethod]
    public async Task ExecuteAsync_KnownFields_ReportsEachReadablyInVerboseOutput()
    {
        InfoRecordingTransferEvents events = new();

        await _hook.ExecuteAsync(Context(events), Origin(("Cache-Status", "ExampleCache; hit"), ("Content-Type", "text/plain"), ("priority", "u=3"), ("cache-status", "CDN; fwd=uri-miss")));

        CollectionAssert.AreEqual(
            new[]
            {
                "Structured field Cache-Status: List of 2: Token ExampleCache [hit=Boolean true]; Token CDN [fwd=Token uri-miss]",
                "Structured field priority: Dictionary of 1: u=Integer 3",
            },
            events.InfoLines);
    }

    [TestMethod]
    public async Task ExecuteAsync_KnownFields_GivesWriteOutVariablesBesideAnotherExtensionsValues()
    {
        var result = await _hook.ExecuteAsync(Context(new InfoRecordingTransferEvents()), Origin(("Origin-Agent-Cluster", " ?1 "), ("Priority", "u=")));

        var values = result.Report!.ExtensionWriteOutValues;
        Assert.AreEqual("Origin-Agent-Cluster: Boolean true\nPriority: not a valid Dictionary (an item is missing), ignored", values[StructuredFieldsTransferHook.AllFieldsVariable]);
        Assert.AreEqual("Boolean true", values["structured_field.origin-agent-cluster"]);
        Assert.AreEqual("not a valid Dictionary (an item is missing), ignored", values["structured_field.priority"]);
        Assert.AreEqual("kept", values["other"]);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoKnownField_GivesAnEmptyStructuredFieldsVariableAndReportsNothing()
    {
        InfoRecordingTransferEvents events = new();

        var result = await _hook.ExecuteAsync(Context(events), Origin(("Content-Type", "text/plain")));

        Assert.AreEqual(string.Empty, result.Report!.ExtensionWriteOutValues[StructuredFieldsTransferHook.AllFieldsVariable]);
        Assert.IsEmpty(events.InfoLines);
        Assert.AreEqual(4, result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_AResultWithNoReport_IsReturnedUnchanged()
    {
        var failure = TransferResult.Failure(CurlExitCode.RecvError, "no", 0);

        Assert.AreSame(failure, await _hook.ExecuteAsync(Context(new InfoRecordingTransferEvents()), _ => ValueTask.FromResult(failure)));
    }

    private static TransferContext Context(ITransferEvents events) => new()
    {
        Url = CurlUrl.Parse("http://example.com/r"),
        Output = new MemoryStream(),
        Http = new HttpRequestOptions(),
        Events = events,
    };

    /// <summary>Stands in for curl's handler: reports one recorded response's fields, beside another extension's -w value.</summary>
    private static Func<ITransferContext, ValueTask<TransferResult>> Origin(params (string Name, string Value)[] fields) =>
        _ => ValueTask.FromResult(TransferResult.Success(4) with
        {
            Report = new TransferReport
            {
                ResponseCode = 200,
                ResponseHeaders = [.. fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value))],
                ExtensionWriteOutValues = new Dictionary<string, string> { ["other"] = "kept" },
            },
        });
}
