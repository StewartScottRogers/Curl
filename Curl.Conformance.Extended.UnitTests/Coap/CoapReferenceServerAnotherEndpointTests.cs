using Curl.Conformance.Extended.Coap;

namespace Curl.Conformance.Extended.UnitTests.Coap;

/// <summary>CoapReferenceServer.IsFromAnotherEndpoint answers false for an answer no array backs and for an array nobody marked.</summary>
[TestClass]
public sealed class CoapReferenceServerAnotherEndpointTests
{
    [TestMethod]
    public void IsFromAnotherEndpoint_AnswerWithNoBackingArray_IsFalse()
    {
        CoapReferenceServer server = new((_, _) => [], TimeProvider.System);
        using NativeMemoryOwner owner = new();

        Assert.IsFalse(server.IsFromAnotherEndpoint(owner.Memory));
    }

    [TestMethod]
    public void IsFromAnotherEndpoint_UnmarkedArray_IsFalse()
    {
        CoapReferenceServer server = new((_, _) => [], TimeProvider.System);

        Assert.IsFalse(server.IsFromAnotherEndpoint(new byte[] { 1, 2, 3 }));
    }

    [TestMethod]
    public void IsFromAnotherEndpoint_MarkedArray_IsTrue()
    {
        CoapReferenceServer server = new((_, _) => [], TimeProvider.System);

        Assert.IsTrue(server.IsFromAnotherEndpoint(CoapResponses.FromAnotherEndpoint([1, 2, 3])));
    }

    private sealed class NativeMemoryOwner : System.Buffers.MemoryManager<byte>
    {
        private readonly byte[] bytes = new byte[4];

        public override Span<byte> GetSpan() => bytes;

        public override System.Buffers.MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();

        public override void Unpin()
        {
        }

        protected override void Dispose(bool disposing)
        {
        }

        protected override bool TryGetArray(out ArraySegment<byte> segment)
        {
            segment = default;
            return false;
        }
    }
}
