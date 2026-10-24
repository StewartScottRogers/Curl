using Curl.Api.Options;
using Curl.Api.Options.Http;

namespace Curl.Engine;

// The tool options a library handle's transports are built with: CURLOPT_PIPEWAIT makes the pool wait for a
// connection being opened to show it multiplexes, and without it the pool opens one at once (BL-3351).
public sealed partial class ManagedCurlEngineTests
{
    [TestMethod]
    [DataRow(true, true)]
    [DataRow(false, false)]
    public void CreateTransports_PoolingConnector_WaitsForMultiplexingOnlyUnderPipeWait(bool pipeWait, bool waits)
    {
        ManagedEngineSharedState shared = new(TimeProvider.System, _ => string.Empty);
        EasyOptions options = new() { Http = new EasyHttpOptions { PipeWait = pipeWait } };

        CurlTransports transports = ManagedCurlEngine.CreateTransports(options, shared);

        Assert.AreEqual(waits, transports.PoolingConnector.WaitsForMultiplexing);
    }
}
