using Curl.Api.Messages;
using Curl.Api.Options;
using Curl.Api.RequestBodies;

namespace Curl.Api.Extensions;

/// <summary>Pins the Surly cookbook's <c>signalrs-log-in-to-a-hub-in-csharp</c> request shape (BL-5762): a credentialed <c>POST</c> of a hub invocation to <c>signalr://</c> or <c>signalrs://</c> reaches the handler as the <c>-T</c> upload the SignalR handler reads whole as the invocation.</summary>
[TestClass]
public sealed class SignalRApiPostTests
{
    private const string Invocation = "{\"type\":1,\"target\":\"Login\",\"arguments\":[\"alice\",\"s3cret\"]}";

    [TestMethod]
    [DataRow("signalr")]
    [DataRow("signalrs")]
    public async Task SendAsync_PostedInvocationWithCredentials_ReachesTheHandlerAsTheUploadItReadsAsTheInvocation(string scheme)
    {
        UploadingEasyTransfer easy = new();
        await using CurlClient client = new(new UploadingCurlEngine(easy));

        _ = await client.SendAsync(
            new CurlRequest(HttpMethod.Post, $"{scheme}://127.0.0.1:5001/")
            {
                Body = CurlContent.FromBytes(System.Text.Encoding.UTF8.GetBytes(Invocation)),
                Authentication = new CurlAuthenticationOptions { UserPassword = "alice:s3cret" },
            },
            CancellationToken.None);

        EasyOptions options = easy.PerformedOptions!;
        Assert.IsTrue(options.Transfer.Upload);
        Assert.AreEqual(Invocation, System.Text.Encoding.UTF8.GetString(easy.Uploads.Single()));
        Assert.AreEqual("alice:s3cret", options.Authentication.UserPassword);
    }
}
