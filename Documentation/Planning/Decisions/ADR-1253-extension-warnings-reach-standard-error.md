# ADR-1253: Extension warnings reach standard error as `Warning:` lines

- Status: Accepted
- Date: 2026-11-11
- Decided by Claude under Stewart's delegation (BL-4945)

## Context

ADR-1138 (`--csharp`, `--powershell`) and ADR-1134 (`--otel-endpoint`) say a failed file write or OTLP export is a
warning, as `--libcurl` only warns. The hooks wrote to the transfer's diagnostic log, which prints nothing unless
`--log-level` asks for it, so the user saw nothing.

## Decision

The runner wraps its diagnostic log in `ExtensionWarningDiagnosticLog` unless `-s` is given. A `Warning` or `Error`
entry of the `code-generation` or `otel` component, which the inner log would not print, is written to standard error as
`Warning: <message>`; every other entry goes to the inner log unchanged. `--otel-endpoint` text that is not an absolute
`http` or `https` URL is warned about once per HTTP transfer (not refused), so a collector reached through a scheme the
exporter may later learn is never made an error.

## Consequences

The message texts are Curl's own: curl has no such options, so there is nothing to measure.
