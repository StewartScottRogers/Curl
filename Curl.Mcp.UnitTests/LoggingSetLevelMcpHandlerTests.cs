using System.Text.Json;

namespace Curl.Mcp;

[TestClass]
public sealed class LoggingSetLevelMcpHandlerTests
{
    [TestMethod]
    public void MethodAndCapability_AreLoggingSetLevelAndAnEmptyLoggingObject()
    {
        LoggingSetLevelMcpHandler handler = new();

        Assert.AreEqual("logging/setLevel", handler.Method);
        Assert.AreEqual("logging", handler.CapabilityName);
        Assert.AreEqual("{}", handler.Capability.GetRawText());
    }

    [TestMethod]
    public async Task HandleAsync_AnyLevel_AnswersAnEmptyObject()
    {
        LoggingSetLevelMcpHandler handler = new();
        using JsonDocument parameters = JsonDocument.Parse("""{"level":"debug"}""");

        JsonElement result = await handler.HandleAsync(parameters.RootElement, CancellationToken.None);

        Assert.AreEqual("{}", result.GetRawText());
    }
}
