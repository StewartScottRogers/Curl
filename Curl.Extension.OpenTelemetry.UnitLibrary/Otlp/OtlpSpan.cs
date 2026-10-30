namespace Curl.Extension.OpenTelemetry.Otlp;

/// <summary>One span as OTLP's <c>Span</c> message carries it (opentelemetry-proto <c>trace/v1/trace.proto</c>).</summary>
/// <param name="TraceId">The trace-id as 32 lowercase hex characters.</param>
/// <param name="SpanId">The span-id as 16 lowercase hex characters.</param>
/// <param name="Name">The span name.</param>
/// <param name="StartUnixNano">When the span started, in nanoseconds since the Unix epoch.</param>
/// <param name="EndUnixNano">When the span ended, in nanoseconds since the Unix epoch.</param>
/// <param name="Attributes">The span's attributes, in the order they are written.</param>
/// <param name="IsError">Whether the span's status is <c>STATUS_CODE_ERROR</c>; otherwise it is left unset.</param>
/// <param name="StatusMessage">The error status's description, or <see langword="null" /> for none.</param>
internal sealed record OtlpSpan(
    string TraceId,
    string SpanId,
    string Name,
    long StartUnixNano,
    long EndUnixNano,
    IReadOnlyList<OtlpAttribute> Attributes,
    bool IsError,
    string? StatusMessage);
