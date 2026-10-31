using System.Text;
using Curl.Extension.OpenTelemetry.Otlp;

namespace Curl.Extension.OpenTelemetry.UnitTests.Otlp;

[TestClass]
public sealed class OtlpJsonWriterTests
{
    [TestMethod]
    public void Write_ASpanWithUnsetStatus_WritesTheJsonProtobufEncoding_OtlpSpecificationJsonProtobufEncoding()
    {
        var span = new OtlpSpan(
            "4bf92f3577b34da6a3ce929d0e0e4736", "00f067aa0ba902b7", "GET", 1000, 2000,
            [OtlpAttribute.String("url.full", "http://x/"), OtlpAttribute.Int("server.port", 80)], false, null);

        var json = Encoding.UTF8.GetString(OtlpJsonWriter.Write(span));

        Assert.AreEqual(
            "{\"resourceSpans\":[{\"resource\":{\"attributes\":[{\"key\":\"service.name\",\"value\":{\"stringValue\":\"curl\"}}]},"
            + "\"scopeSpans\":[{\"scope\":{\"name\":\"curl\"},\"spans\":[{\"traceId\":\"4bf92f3577b34da6a3ce929d0e0e4736\","
            + "\"spanId\":\"00f067aa0ba902b7\",\"name\":\"GET\",\"kind\":3,\"startTimeUnixNano\":\"1000\",\"endTimeUnixNano\":\"2000\","
            + "\"attributes\":[{\"key\":\"url.full\",\"value\":{\"stringValue\":\"http://x/\"}},{\"key\":\"server.port\",\"value\":{\"intValue\":\"80\"}}]}]}]}]}",
            json);
    }

    [TestMethod]
    public void Write_AnErrorSpan_WritesStatusCodeErrorAndItsMessage_OtlpTraceProtoStatus()
    {
        var withMessage = Encoding.UTF8.GetString(OtlpJsonWriter.Write(new OtlpSpan("t", "s", "GET", 0, 0, [], true, "Could not resolve host: x")));
        var withoutMessage = Encoding.UTF8.GetString(OtlpJsonWriter.Write(new OtlpSpan("t", "s", "GET", 0, 0, [], true, null)));

        StringAssert.Contains(withMessage, "\"attributes\":[],\"status\":{\"message\":\"Could not resolve host: x\",\"code\":2}}");
        StringAssert.Contains(withoutMessage, "\"attributes\":[],\"status\":{\"code\":2}}");
    }
}
