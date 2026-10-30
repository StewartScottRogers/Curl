# Curl.Extension.OpenTelemetry.UnitLibrary

The OpenTelemetry tracing extension (ADR-0999, ADR-1134), tested against W3C Trace Context Level 1 and the OTLP
specification. Namespace `Curl.Extension.OpenTelemetry`. It references only `Curl.Extension.Abstractions.UnitLibrary`
and the shared layers it brings.

- `OpenTelemetryExtension` gives the `--otel-endpoint <url>` option, its description and its hook activator, which
  `Curl.Extension.Composition` registers.
- `OpenTelemetryTransferHook` sends a `traceparent` with every HTTP transfer, times it, and exports its span.
- `TraceContext/TraceParent` reads and writes the `traceparent` value.
- `Otlp/OtlpHttpClientSpan` builds the HTTP client span, `Otlp/OtlpJsonWriter` writes it as an OTLP JSON
  `ExportTraceServiceRequest`, and `Otlp/OtlpHttpExporter` posts it to the collector over curl's connector.

Tested against the specifications, not against curl. Held to the four quality gates in the root `CLAUDE.md`. Its tests
are in `Curl.Extension.OpenTelemetry.UnitTests`.
