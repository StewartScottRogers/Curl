using System.Text;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.GraphQl;

[TestClass]
public sealed class GraphQlTransferHookTests
{
    [TestMethod]
    public void ActivatedBy_WithoutAGraphQlOption_GivesNoHook()
    {
        Assert.IsNull(GraphQlTransferHook.ActivatedBy(ExtensionOptionValues.None));
        Assert.IsNull(GraphQlTransferHook.ActivatedBy(Values(("graphql-variables", "{}")), TextWriter.Null));
        Assert.ThrowsExactly<ArgumentNullException>(() => GraphQlTransferHook.ActivatedBy(null!));
    }

    [TestMethod]
    public async Task ExecuteAsync_Post_SendsTheDocumentAsJsonWithAcceptAndPassesTheBodyOn()
    {
        using TemporaryFile document = new("query A($id: ID) { a(id: $id) }");
        StringWriter errors = new();
        ITransferHook hook = Hook(errors, ("graphql", document.Path), ("graphql-variables", "{\"id\":1}"), ("graphql-operation", "A"));
        TransferContext context = Context(["X-Own: 1"]);
        ITransferContext? sent = null;

        TransferResult result = await hook.ExecuteAsync(context, given => Answer(given, "{\"data\":{\"a\":1}}", answered => sent = answered));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        BytesBody body = (BytesBody)sent!.Http!.Body!;
        Assert.AreEqual("application/json", body.ContentType);
        Assert.AreEqual("{\"query\":\"query A($id: ID) { a(id: $id) }\",\"operationName\":\"A\",\"variables\":{\"id\":1}}", Encoding.UTF8.GetString(body.Content.Span));
        CollectionAssert.AreEqual(new[] { "X-Own: 1", "Accept: application/graphql-response+json, application/json;q=0.9" }, sent.Http.Headers.ToArray());
        Assert.AreEqual("{\"data\":{\"a\":1}}", Encoding.UTF8.GetString(((MemoryStream)context.Output).ToArray()));
        Assert.AreEqual(string.Empty, errors.ToString());
    }

    [TestMethod]
    public async Task ExecuteAsync_Accept_KeepsTheUsersOwnAccept()
    {
        ITransferHook hook = Hook(TextWriter.Null, ("graphql-introspect", string.Empty));
        ITransferContext? sent = null;

        await hook.ExecuteAsync(Context(["accept: application/json"]), given => Answer(given, "{}", answered => sent = answered));

        CollectionAssert.AreEqual(new[] { "accept: application/json" }, sent!.Http!.Headers.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_Introspection_SendsTheIntrospectionQueryWithoutOperationName()
    {
        using TemporaryFile variables = new("{\"v\":true}");
        ITransferHook hook = Hook(TextWriter.Null, ("graphql-introspect", string.Empty), ("graphql-operation", "Ignored"), ("graphql-variables", "@" + variables.Path));
        ITransferContext? sent = null;

        await hook.ExecuteAsync(Context([]), given => Answer(given, "{}", answered => sent = answered));

        string body = Encoding.UTF8.GetString(((BytesBody)sent!.Http!.Body!).Content.Span);
        Assert.StartsWith("{\"query\":\"query IntrospectionQuery { __schema {", body);
        Assert.EndsWith("\",\"variables\":{\"v\":true}}", body);
        Assert.DoesNotContain("operationName", body);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResponseErrors_ListsThemOnStderr()
    {
        StringWriter errors = new();
        ITransferHook hook = Hook(errors, ("graphql-introspect", string.Empty));

        await hook.ExecuteAsync(Context([]), given => Answer(given, "{\"errors\":[{\"message\":\"Denied\",\"path\":[\"me\"]}]}", _ => { }));

        Assert.AreEqual("GraphQL error: Denied at path me" + Environment.NewLine, errors.ToString());
    }

    [TestMethod]
    public async Task ExecuteAsync_UnreadableDocumentOrVariables_FailsWithReadError()
    {
        string missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.graphql");
        bool ran = false;

        TransferResult noDocument = await Hook(TextWriter.Null, ("graphql", missing)).ExecuteAsync(Context([]), _ => { ran = true; return ValueTask.FromResult(TransferResult.Success(0)); });
        TransferResult emptyPath = await Hook(TextWriter.Null, ("graphql", string.Empty)).ExecuteAsync(Context([]), _ => { ran = true; return ValueTask.FromResult(TransferResult.Success(0)); });
        TransferResult noVariables = await Hook(TextWriter.Null, ("graphql-introspect", string.Empty), ("graphql-variables", "@" + missing)).ExecuteAsync(Context([]), _ => { ran = true; return ValueTask.FromResult(TransferResult.Success(0)); });
        TransferResult badVariables = await Hook(TextWriter.Null, ("graphql-introspect", string.Empty), ("graphql-variables", "[1]")).ExecuteAsync(Context([]), _ => { ran = true; return ValueTask.FromResult(TransferResult.Success(0)); });

        Assert.IsFalse(ran);
        Assert.AreEqual(CurlExitCode.ReadError, noDocument.ExitCode);
        Assert.AreEqual("Failed to read the GraphQL document " + missing, noDocument.ErrorMessage);
        Assert.AreEqual(CurlExitCode.ReadError, emptyPath.ExitCode);
        Assert.AreEqual(CurlExitCode.ReadError, noVariables.ExitCode);
        Assert.AreEqual("GraphQL variables [1] are not a JSON object", badVariables.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_NotHttp_RunsTheTransferUnchanged()
    {
        ITransferHook hook = Hook(TextWriter.Null, ("graphql-introspect", string.Empty));
        TransferContext context = new() { Url = CurlUrl.Parse("ftp://example.com/f"), Output = new MemoryStream() };
        ITransferContext? sent = null;

        await hook.ExecuteAsync(context, given => { sent = given; return ValueTask.FromResult(TransferResult.Success(0)); });

        Assert.AreSame(context, sent);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await hook.ExecuteAsync(null!, _ => ValueTask.FromResult(TransferResult.Success(0))));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await hook.ExecuteAsync(context, null!));
    }

    private static ITransferHook Hook(TextWriter errors, params (string Name, string Value)[] given) =>
        GraphQlTransferHook.ActivatedBy(Values(given), errors)!;

    private static ExtensionOptionValues Values(params (string Name, string Value)[] given) =>
        new(given.ToDictionary(option => option.Name, option => option.Value));

    private static TransferContext Context(IReadOnlyList<string> headers) => new()
    {
        Url = CurlUrl.Parse("http://example.com/graphql"),
        Output = new MemoryStream(),
        Http = new HttpRequestOptions { Headers = headers },
    };

    private static async ValueTask<TransferResult> Answer(ITransferContext context, string body, Action<ITransferContext> record)
    {
        record(context);
        byte[] bytes = Encoding.UTF8.GetBytes(body);
        await context.Output.WriteAsync(bytes);
        await context.Output.FlushAsync();
        return TransferResult.Success(bytes.Length);
    }

    private sealed class TemporaryFile : IDisposable
    {
        public TemporaryFile(string content)
        {
            Path = System.IO.Path.GetTempFileName();
            File.WriteAllText(Path, content);
        }

        public string Path { get; }

        public void Dispose() => File.Delete(Path);
    }
}
