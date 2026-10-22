using System.Net;
using Curl.Testing;

namespace Curl.Conformance;

[TestClass]
public sealed class StalledLookupDnsResolverTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ResolveAsync_StalledLookupHostAfterADay_NeverAnswersUntilCancelled()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ManualTimeProvider clock = new();
        StalledLookupDnsResolver resolver = new(new LoopbackOnlyDnsResolver(), clock);
        using CancellationTokenSource cancellation = new();
        diagnostics.Arrange("host", UpstreamCaseScreening.StalledLookupHost);

        Task<IReadOnlyList<IPAddress>> lookup = resolver.ResolveAsync(UpstreamCaseScreening.StalledLookupHost, cancellation.Token).AsTask();
        clock.Advance(TimeSpan.FromDays(1));
        bool answeredBeforeCancel = lookup.IsCompleted;
        await cancellation.CancelAsync();

        diagnostics.Act("answered after a day", answeredBeforeCancel);
        diagnostics.Assert("answered after a day", false, answeredBeforeCancel);
        Assert.IsFalse(answeredBeforeCancel);
        await Assert.ThrowsAsync<OperationCanceledException>(() => lookup);
    }

    [TestMethod]
    public async Task ResolveAsync_AnyOtherName_AnswersFromTheWrappedResolver()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        StalledLookupDnsResolver resolver = new(new LoopbackOnlyDnsResolver(), new ManualTimeProvider());
        diagnostics.Arrange("host", "localhost");

        IReadOnlyList<IPAddress> addresses = await resolver.ResolveAsync("localhost", CancellationToken.None);

        diagnostics.Act("addresses", string.Join(", ", addresses));
        diagnostics.Assert("addresses", "::1, 127.0.0.1", string.Join(", ", addresses));
        CollectionAssert.AreEqual(new[] { IPAddress.IPv6Loopback, IPAddress.Loopback }, addresses.ToArray());
    }
}
