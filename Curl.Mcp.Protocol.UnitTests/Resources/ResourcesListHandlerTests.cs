using System.Text.Json;
using Curl.Mcp.Protocol.Model;

namespace Curl.Mcp.Protocol.Resources;

[TestClass]
public sealed class ResourcesListHandlerTests
{
    // The resources/list result the SDK 2.2.0 server answered with in Curl.Mcp.IntegrationTests/Golden/resources-list.json (BL-3374).
    private const string GoldenResult = """{"resources":[{"name":"ai-help","uri":"curl://ai-help","description":"Index of curl's options for AI agents: the categories and the most-used options (curl --ai-help).","mimeType":"text/markdown"},{"name":"ai-help-all","uri":"curl://ai-help/all","description":"Every curl option by category, then the exit codes and the --write-out variables (curl --ai-help all).","mimeType":"text/markdown"},{"name":"ai-help-auth","uri":"curl://ai-help/auth","description":"Authentication methods: every option in curl's auth category (curl --ai-help auth).","mimeType":"text/markdown"},{"name":"ai-help-connection","uri":"curl://ai-help/connection","description":"Manage connections: every option in curl's connection category (curl --ai-help connection).","mimeType":"text/markdown"},{"name":"ai-help-curl","uri":"curl://ai-help/curl","description":"The command line tool itself: every option in curl's curl category (curl --ai-help curl).","mimeType":"text/markdown"},{"name":"ai-help-deprecated","uri":"curl://ai-help/deprecated","description":"Legacy: every option in curl's deprecated category (curl --ai-help deprecated).","mimeType":"text/markdown"},{"name":"ai-help-dns","uri":"curl://ai-help/dns","description":"Names and resolving: every option in curl's dns category (curl --ai-help dns).","mimeType":"text/markdown"},{"name":"ai-help-file","uri":"curl://ai-help/file","description":"FILE protocol: every option in curl's file category (curl --ai-help file).","mimeType":"text/markdown"},{"name":"ai-help-ftp","uri":"curl://ai-help/ftp","description":"FTP protocol: every option in curl's ftp category (curl --ai-help ftp).","mimeType":"text/markdown"},{"name":"ai-help-global","uri":"curl://ai-help/global","description":"Global options: every option in curl's global category (curl --ai-help global).","mimeType":"text/markdown"},{"name":"ai-help-http","uri":"curl://ai-help/http","description":"HTTP and HTTPS protocol: every option in curl's http category (curl --ai-help http).","mimeType":"text/markdown"},{"name":"ai-help-imap","uri":"curl://ai-help/imap","description":"IMAP protocol: every option in curl's imap category (curl --ai-help imap).","mimeType":"text/markdown"},{"name":"ai-help-ldap","uri":"curl://ai-help/ldap","description":"LDAP protocol: every option in curl's ldap category (curl --ai-help ldap).","mimeType":"text/markdown"},{"name":"ai-help-mqtt","uri":"curl://ai-help/mqtt","description":"MQTT protocol: every option in curl's mqtt category (curl --ai-help mqtt).","mimeType":"text/markdown"},{"name":"ai-help-output","uri":"curl://ai-help/output","description":"File system output: every option in curl's output category (curl --ai-help output).","mimeType":"text/markdown"},{"name":"ai-help-pop3","uri":"curl://ai-help/pop3","description":"POP3 protocol: every option in curl's pop3 category (curl --ai-help pop3).","mimeType":"text/markdown"},{"name":"ai-help-post","uri":"curl://ai-help/post","description":"HTTP POST specific: every option in curl's post category (curl --ai-help post).","mimeType":"text/markdown"},{"name":"ai-help-proxy","uri":"curl://ai-help/proxy","description":"Options for proxies: every option in curl's proxy category (curl --ai-help proxy).","mimeType":"text/markdown"},{"name":"ai-help-scp","uri":"curl://ai-help/scp","description":"SCP protocol: every option in curl's scp category (curl --ai-help scp).","mimeType":"text/markdown"},{"name":"ai-help-sftp","uri":"curl://ai-help/sftp","description":"SFTP protocol: every option in curl's sftp category (curl --ai-help sftp).","mimeType":"text/markdown"},{"name":"ai-help-smtp","uri":"curl://ai-help/smtp","description":"SMTP protocol: every option in curl's smtp category (curl --ai-help smtp).","mimeType":"text/markdown"},{"name":"ai-help-ssh","uri":"curl://ai-help/ssh","description":"SSH protocol: every option in curl's ssh category (curl --ai-help ssh).","mimeType":"text/markdown"},{"name":"ai-help-telnet","uri":"curl://ai-help/telnet","description":"TELNET protocol: every option in curl's telnet category (curl --ai-help telnet).","mimeType":"text/markdown"},{"name":"ai-help-tftp","uri":"curl://ai-help/tftp","description":"TFTP protocol: every option in curl's tftp category (curl --ai-help tftp).","mimeType":"text/markdown"},{"name":"ai-help-timeout","uri":"curl://ai-help/timeout","description":"Timeouts and delays: every option in curl's timeout category (curl --ai-help timeout).","mimeType":"text/markdown"},{"name":"ai-help-tls","uri":"curl://ai-help/tls","description":"TLS/SSL related: every option in curl's tls category (curl --ai-help tls).","mimeType":"text/markdown"},{"name":"ai-help-upload","uri":"curl://ai-help/upload","description":"Upload, sending data: every option in curl's upload category (curl --ai-help upload).","mimeType":"text/markdown"},{"name":"ai-help-verbose","uri":"curl://ai-help/verbose","description":"Tracing, logging etc: every option in curl's verbose category (curl --ai-help verbose).","mimeType":"text/markdown"}]}""";

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

    private static ResourcesListHandler HandlerOver(params IMcpResourceProvider[] providers)
    {
        McpServerRegistry registry = new();
        foreach (IMcpResourceProvider provider in providers)
        {
            registry.Add(provider);
        }

        return new ResourcesListHandler(registry);
    }

    [TestMethod]
    public void Constructor_NullRegistry_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new ResourcesListHandler(null!));
    }

    [TestMethod]
    public void MethodAndCapability_Always_AreResourcesListAndAnEmptyResourcesCapability()
    {
        ResourcesListHandler handler = HandlerOver();

        Assert.AreEqual("resources/list", handler.Method);
        Assert.AreEqual("resources", handler.CapabilityName);
        Assert.AreEqual("{}", handler.Capability.GetRawText());
    }

    [TestMethod]
    public async Task HandleAsync_OneProvider_ListsUriNameDescriptionAndMimeType()
    {
        ResourcesListHandler handler = HandlerOver(new FakeResourceProvider(new McpResource("doc", "test://doc", "A document.", "text/plain")));

        JsonElement result = await handler.HandleAsync(Json("{}"), CancellationToken.None);

        Assert.AreEqual("""{"resources":[{"name":"doc","uri":"test://doc","description":"A document.","mimeType":"text/plain"}]}""", result.GetRawText());
    }

    [TestMethod]
    public async Task HandleAsync_NoProviders_ListsNoResources()
    {
        ResourcesListHandler handler = HandlerOver();

        JsonElement result = await handler.HandleAsync(null, CancellationToken.None);

        Assert.AreEqual("""{"resources":[]}""", result.GetRawText());
    }

    [TestMethod]
    public async Task HandleAsync_SeveralProviders_ListsEveryResourceInTheOrderAdded()
    {
        ResourcesListHandler handler = HandlerOver(
            new FakeResourceProvider(new McpResource("a", "test://a", null, null), new McpResource("b", "test://b", null, "text/markdown")),
            new FakeResourceProvider(new McpResource("c", "test://c", "C.", null)));

        JsonElement result = await handler.HandleAsync(null, CancellationToken.None);

        Assert.AreEqual("""{"resources":[{"name":"a","uri":"test://a"},{"name":"b","uri":"test://b","mimeType":"text/markdown"},{"name":"c","uri":"test://c","description":"C."}]}""", result.GetRawText());
    }

    [TestMethod]
    public async Task HandleAsync_WithCursor_IgnoresTheCursorAndListsEveryResource()
    {
        ResourcesListHandler handler = HandlerOver(new FakeResourceProvider(new McpResource("a", "test://a", null, null)));

        JsonElement withCursor = await handler.HandleAsync(Json("""{"cursor":"page-2"}"""), CancellationToken.None);
        JsonElement withoutCursor = await handler.HandleAsync(Json("{}"), CancellationToken.None);

        Assert.AreEqual(withoutCursor.GetRawText(), withCursor.GetRawText());
    }

    [TestMethod]
    public async Task HandleAsync_ProviderEquivalentToTheGoldenCatalogue_AnswersTheGoldenResult()
    {
        JsonElement golden = Json(GoldenResult);
        McpResource[] resources = [.. golden.GetProperty("resources").EnumerateArray().Select(resource => new McpResource(
            resource.GetProperty("name").GetString()!,
            resource.GetProperty("uri").GetString()!,
            resource.GetProperty("description").GetString(),
            resource.GetProperty("mimeType").GetString()))];
        ResourcesListHandler handler = HandlerOver(new FakeResourceProvider(resources));

        JsonElement result = await handler.HandleAsync(Json("{}"), CancellationToken.None);

        Assert.IsTrue(JsonElement.DeepEquals(golden, result), result.GetRawText());
    }
}
