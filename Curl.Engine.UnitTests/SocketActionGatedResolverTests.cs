using System.Net;
using System.Net.Sockets;
using Curl.Api.EngineContracts.Multi;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

[TestClass]
public sealed class SocketActionGatedResolverTests
{
    private static readonly IPAddress Address = IPAddress.Parse("192.0.2.9");

    [TestMethod]
    public void Over_NoWait_GivesTheResolverItself()
    {
        HeldResolver resolver = new();

        Assert.AreSame(resolver, SocketActionGatedResolver.Over(resolver, wait: null));
        Assert.IsInstanceOfType<SocketActionGatedResolver>(SocketActionGatedResolver.Over(resolver, new TransferSocketWait(TimeProvider.System)));
    }

    [TestMethod]
    public async Task ResolveAsync_WhileTheLookupRuns_WaitsReadableOnAWakeupTheLookupsEndSignals()
    {
        TransferSocketWait wait = new(TimeProvider.System);
        HeldResolver inner = new();
        SocketActionGatedResolver resolver = new(inner, wait);

        Task<IReadOnlyList<IPAddress>> resolving = resolver.ResolveAsync("h", CancellationToken.None).AsTask();
        await inner.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        nint wakeup = wait.Socket!.Value;
        CurlPoll waitingDuring = wait.Waiting;
        bool readableDuring = IsReadable(wakeup, TimeSpan.Zero);
        inner.Answer.SetResult();
        IReadOnlyList<IPAddress> addresses = await resolving.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.AreEqual(CurlPoll.In, waitingDuring);
        Assert.IsFalse(readableDuring);
        Assert.IsTrue(IsReadable(wakeup, TimeSpan.FromSeconds(5)));
        Assert.AreEqual(CurlPoll.None, wait.Waiting);
        Assert.IsFalse(wait.IsWaitingOnItsSocket);
        CollectionAssert.AreEqual(new[] { Address }, addresses.ToArray());
        wait.StopGating();
    }

    [TestMethod]
    public async Task ResolveAsync_GatingStoppedDuringTheLookup_ReturnsTheLookupsResultWithoutThrowing()
    {
        TransferSocketWait wait = new(TimeProvider.System);
        HeldResolver inner = new();
        SocketActionGatedResolver resolver = new(inner, wait);

        Task<IReadOnlyList<IPAddress>> resolving = resolver.ResolveAsync("h", CancellationToken.None).AsTask();
        await inner.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        wait.StopGating();
        inner.Answer.SetResult();
        IReadOnlyList<IPAddress> addresses = await resolving.WaitAsync(TimeSpan.FromSeconds(10));

        CollectionAssert.AreEqual(new[] { Address }, addresses.ToArray());
    }

    [TestMethod]
    public void BeginResolve_GatingAlreadyStopped_DisposesTheWakeupItIsGiven()
    {
        TransferSocketWait wait = new(TimeProvider.System);
        DisposeCounter wakeup = new();
        wait.StopGating();

        wait.BeginResolve(1, wakeup);

        Assert.AreEqual(1, wakeup.Disposed);
        Assert.IsNull(wait.Socket);
    }

    private sealed class DisposeCounter : IDisposable
    {
        public int Disposed { get; private set; }

        public void Dispose() => Disposed++;
    }

    [TestMethod]
    public async Task ResolveWithFailureReasonAsync_InnerGivesAReason_PassesItThroughAndEndsTheLookup()
    {
        TransferSocketWait wait = new(TimeProvider.System);
        DnsResolution refused = new([], DnsLookupFailure.BadConfiguration);
        SocketActionGatedResolver resolver = new(new ReasonResolver(refused), wait);

        DnsResolution resolution = await resolver.ResolveWithFailureReasonAsync("h", CancellationToken.None);

        Assert.AreSame(refused, resolution);
        Assert.AreEqual(CurlPoll.None, wait.Waiting);
        wait.StopGating();
    }

    [TestMethod]
    public void Signal_Twice_ConnectsOnce()
    {
        using LoopbackLookupWakeup wakeup = LoopbackLookupWakeup.Open();

        wakeup.Signal();
        wakeup.Signal();

        Assert.IsTrue(IsReadable(wakeup.Handle, TimeSpan.FromSeconds(5)));
    }

    [TestMethod]
    public void Dispose_NeverSignalled_ClosesTheListenerAndLaterSignalsDoNothing()
    {
        LoopbackLookupWakeup wakeup = LoopbackLookupWakeup.Open();
        IPEndPoint listening = LocalEndPointOf(wakeup.Handle);

        wakeup.Dispose();
        wakeup.Signal();

        using Socket probe = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        SocketException refused = Assert.ThrowsExactly<SocketException>(() => probe.Connect(listening));
        Assert.AreEqual(SocketError.ConnectionRefused, refused.SocketErrorCode);
    }

    private static IPEndPoint LocalEndPointOf(nint handle)
    {
        using Socket socket = new(new SafeSocketHandle(handle, ownsHandle: false));
        return (IPEndPoint)socket.LocalEndPoint!;
    }

    private static bool IsReadable(nint handle, TimeSpan timeout)
    {
        using Socket socket = new(new SafeSocketHandle(handle, ownsHandle: false));
        return socket.Poll(timeout, SelectMode.SelectRead);
    }

    /// <summary>A resolver whose look-up answers <see cref="Address" /> once the test completes <see cref="Answer" />.</summary>
    private sealed class HeldResolver : IDnsResolver
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource Answer { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            Started.SetResult();
            await Answer.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return [Address];
        }
    }

    /// <summary>A resolver with a failure reason that answers one resolution.</summary>
    private sealed class ReasonResolver(DnsResolution resolution) : IDnsResolverWithFailureReason
    {
        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
            ValueTask.FromResult(resolution.Addresses);

        public ValueTask<DnsResolution> ResolveWithFailureReasonAsync(string host, CancellationToken cancellationToken) =>
            ValueTask.FromResult(resolution);
    }
}
