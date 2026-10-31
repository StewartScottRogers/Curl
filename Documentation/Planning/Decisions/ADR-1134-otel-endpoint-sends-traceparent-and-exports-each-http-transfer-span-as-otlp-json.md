# ADR-1134 — --otel-endpoint sends a traceparent and exports each HTTP transfer's span as OTLP/HTTP JSON

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4156.

## Context

BL-4156 adds OpenTelemetry tracing as an extension (ADR-0999): `Curl.Extension.OpenTelemetry.UnitLibrary`. It must
send W3C Trace Context's `traceparent` and export spans over OTLP/HTTP with the JSON encoding, using the base class
library only and never changing what curl does without its option. Upstream curl has no such option.

## Decision

- **One option, `--otel-endpoint <url>`.** Its value is the OTLP/HTTP traces endpoint, used as given (the way OTLP's
  `OTEL_EXPORTER_OTLP_TRACES_ENDPOINT` is used as given): `http://localhost:4318/v1/traces` for a local collector.
  Giving it is what turns tracing on; there is no separate flag. The extension is listed as `otel`.
- **Specifications followed.** W3C Trace Context Level 1 (Recommendation, §3.2 `traceparent`, version `00`), the OTLP
  specification 1.5.0 ("OTLP/HTTP", "JSON Protobuf Encoding") with opentelemetry-proto's `trace/v1` messages, and the
  HTTP client span semantic conventions 1.26.0.
- **What is sent.** Each HTTP transfer gets a fresh random trace-id and span-id (neither all zeros, §3.2.2.3 and
  §3.2.2.4), sent as `traceparent: 00-<trace-id>-<span-id>-01`: sampled, since Curl exports every span. A
  `traceparent` `-H` already gave is left to it; when it is valid (§3.2.2, §3.2.4) the span takes its trace-id and
  parent-id, so the exported span is the one the server sees as its parent, and when it is not the span gets fresh ids.
- **The span.** Kind client, named by the method, timed on the transfer's `TimeProvider` around the whole transfer
  (redirects and retries the handler follows included), with `http.request.method`, `url.full` (a user and password
  replaced by `REDACTED:REDACTED`), `server.address`, `server.port` and any `http.response.status_code`. Its status is
  error, with `error.type`, when the transfer failed (the `CurlExitCode` name, and curl's error message as the status
  message) or the status is 400 or more (the status code); otherwise it is unset. The resource's `service.name` and the
  scope's name are `curl`.
- **The export.** After the transfer, one `POST` with `Content-Type: application/json` and `Connection: close`, over
  curl's own connector (TLS for `https`, through the proxy curl's options give). In the JSON, trace-id and span-id are
  hex, enums integers and 64-bit integers decimal strings. A 2xx answer is success; any other answer, or none, is
  logged as a warning (`otel` component) and never changes the transfer's result or exit code.
- **Not HTTP.** A transfer that is not HTTP is left alone: no header can carry the context.

## Left out, and why

- **`tracestate`**, and propagating a `-H` `tracestate`: Curl takes part in no vendor's tracing system, so it has
  nothing to add (§3.3 lets a participant leave it out).
- **Retries and partial success** (OTLP "Retryable Response Codes", "Partial Success"): one span per transfer, sent
  once; a lost span is reported, not retried, so tracing can never delay or repeat a transfer.
- **OTLP/gRPC and the binary Protobuf encoding**: the task asks for OTLP/HTTP JSON, and JSON is the encoding a
  collector reads with no schema compiled in.
- **Batching across transfers, `OTEL_*` environment variables, export headers and the service name option**: each would
  add a configuration surface without a need yet; one task can add them later behind their own options.

## Consequences

`curl --otel-endpoint http://localhost:4318/v1/traces https://example.com/` traces the transfer into any OTLP
collector. With `CURL_EXTENSIONS=0` the option is refused as curl refuses an unknown one, and without the option no
transfer changes.
