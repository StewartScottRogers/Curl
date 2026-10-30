namespace Curl.Extension.CodeGeneration.UnitTests;

[TestClass]
public sealed class CodeGenerationTransferHookTests
{
    private string directory = null!;

    [TestInitialize]
    public void CreateDirectory() => directory = Directory.CreateTempSubdirectory("codegen").FullName;

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(directory, true);

    [TestMethod]
    public async Task ExecuteAsync_EachTransfer_WritesEveryRequestSoFarThenRunsTheTransferUnchanged()
    {
        var path = Path.Combine(directory, "out.cs");
        var hook = new CodeGenerationTransferHook([new CodeFile(path, CSharpProgram.For)]);
        var expected = new TransferResult(CurlExitCode.Ok, 4);
        ITransferContext? ran = null;
        var first = Context("http://h/one");

        var result = await hook.ExecuteAsync(first, context =>
        {
            ran = context;
            return ValueTask.FromResult(expected);
        });
        await hook.ExecuteAsync(Context("http://h/two"), _ => ValueTask.FromResult(expected));

        Assert.AreSame(expected, result);
        Assert.AreSame(first, ran);
        var program = await File.ReadAllTextAsync(path);
        Assert.IsLessThan(program.IndexOf("http://h/two", StringComparison.Ordinal), program.IndexOf("http://h/one", StringComparison.Ordinal));
    }

    // curl --libcurl warns "Failed to open <file> to write libcurl code" and still runs the transfer.
    [TestMethod]
    public async Task ExecuteAsync_AFileItCannotWrite_LogsItAndStillRunsTheTransfer()
    {
        var log = new RecordingLog();
        var hook = new CodeGenerationTransferHook([new CodeFile(directory, PowerShellScript.For)]);
        var expected = new TransferResult(CurlExitCode.Ok, 0);

        var result = await hook.ExecuteAsync(
            new TransferContext { Url = CurlUrl.Parse("http://h/"), Output = Stream.Null, DiagnosticLog = log },
            _ => ValueTask.FromResult(expected));

        Assert.AreSame(expected, result);
        Assert.AreEqual("Failed to open " + directory + " to write code", log.Messages.Single());
    }

    private static TransferContext Context(string url) => new() { Url = CurlUrl.Parse(url), Output = Stream.Null };

    private sealed class RecordingLog : IDiagnosticLog
    {
        public List<string> Messages { get; } = [];

        public bool IsEnabled(DiagnosticLogLevel level) => true;

        public void Write(DiagnosticLogLevel level, string component, string message) => Messages.Add(message);
    }
}
