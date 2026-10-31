using System.Globalization;
using System.Text.Json;

namespace Curl.Extension.OpenTelemetry.Otlp;

/// <summary>
/// Writes an <c>ExportTraceServiceRequest</c> in OTLP's JSON Protobuf encoding (OTLP specification 1.5.0, "JSON Protobuf
/// Encoding"): lowerCamelCase field names, trace-id and span-id as hex strings rather than base64, enums as integers, and
/// 64-bit integers as decimal strings (the Protocol Buffers JSON mapping).
/// </summary>
internal static class OtlpJsonWriter
{
    /// <summary>The <c>Content-Type</c> of the body (OTLP specification, "OTLP/HTTP Request").</summary>
    internal const string ContentType = "application/json";

    /// <summary>The <c>service.name</c> resource attribute and the instrumentation scope's name.</summary>
    internal const string ServiceName = "curl";

    /// <summary><c>SPAN_KIND_CLIENT</c>.</summary>
    private const int SpanKindClient = 3;

    /// <summary><c>STATUS_CODE_ERROR</c>.</summary>
    private const int StatusCodeError = 2;

    /// <summary>Writes one request holding one resource, one scope and <paramref name="span" />.</summary>
    /// <param name="span">The span to export.</param>
    /// <returns>The UTF-8 JSON body.</returns>
    internal static byte[] Write(OtlpSpan span)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteStartArray("resourceSpans");
            json.WriteStartObject();
            json.WriteStartObject("resource");
            WriteAttributes(json, [OtlpAttribute.String("service.name", ServiceName)]);
            json.WriteEndObject();
            json.WriteStartArray("scopeSpans");
            json.WriteStartObject();
            json.WriteStartObject("scope");
            json.WriteString("name", ServiceName);
            json.WriteEndObject();
            json.WriteStartArray("spans");
            WriteSpan(json, span);
            json.WriteEndArray();
            json.WriteEndObject();
            json.WriteEndArray();
            json.WriteEndObject();
            json.WriteEndArray();
            json.WriteEndObject();
        }

        return buffer.ToArray();
    }

    private static void WriteSpan(Utf8JsonWriter json, OtlpSpan span)
    {
        json.WriteStartObject();
        json.WriteString("traceId", span.TraceId);
        json.WriteString("spanId", span.SpanId);
        json.WriteString("name", span.Name);
        json.WriteNumber("kind", SpanKindClient);
        json.WriteString("startTimeUnixNano", span.StartUnixNano.ToString(CultureInfo.InvariantCulture));
        json.WriteString("endTimeUnixNano", span.EndUnixNano.ToString(CultureInfo.InvariantCulture));
        WriteAttributes(json, span.Attributes);
        if (span.IsError)
        {
            WriteErrorStatus(json, span.StatusMessage);
        }

        json.WriteEndObject();
    }

    private static void WriteErrorStatus(Utf8JsonWriter json, string? message)
    {
        json.WriteStartObject("status");
        if (message is not null)
        {
            json.WriteString("message", message);
        }

        json.WriteNumber("code", StatusCodeError);
        json.WriteEndObject();
    }

    private static void WriteAttributes(Utf8JsonWriter json, IEnumerable<OtlpAttribute> attributes)
    {
        json.WriteStartArray("attributes");
        foreach (var attribute in attributes)
        {
            json.WriteStartObject();
            json.WriteString("key", attribute.Key);
            json.WriteStartObject("value");
            if (attribute.StringValue is not null)
            {
                json.WriteString("stringValue", attribute.StringValue);
            }
            else
            {
                json.WriteString("intValue", attribute.IntValue.ToString(CultureInfo.InvariantCulture));
            }

            json.WriteEndObject();
            json.WriteEndObject();
        }

        json.WriteEndArray();
    }
}
