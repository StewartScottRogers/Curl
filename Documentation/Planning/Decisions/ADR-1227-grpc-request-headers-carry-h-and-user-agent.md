# ADR-1227: A gRPC call's Request-Headers carry `-H` as Custom-Metadata, `-A` or curl's own `user-agent`, and `grpc-timeout` first

Status: Accepted. Date: 2026-11-03. Decided by Claude under Stewart's delegation (BL-4674).

## Context

PROTOCOL-HTTP2.md "Requests" orders the Call-Definition headers before Custom-Metadata, asks that `grpc-timeout`
follow the reserved (`:`) headers immediately, and recommends a structured `user-agent`. Curl's gRPC call sent no
`-H` value and no `user-agent`, and put `te` before `grpc-timeout` (`Grpc:Http2-Request-8` to `Request-12`). curl
has no gRPC, so there is no curl output to measure; the rules follow how curl's HTTP handler reads `-H` and `-A`.

## Decision

- The Request-Headers go `:method`, `:scheme`, `:path`, `:authority`, `grpc-timeout` (with `-m`), `te`,
  `content-type`, `user-agent`, then each `-H` value in command-line order as Custom-Metadata, name lower case
  (HTTP/2 requires it), value trimmed.
- `-H` is read as curl reads it for HTTP: `Name: value` sends the value, `Name:` sends nothing, `Name;` sends an
  empty value, anything else nothing. One naming a Call-Definition header Curl builds (`te`, `grpc-timeout`,
  `content-type`, `user-agent`) replaces it in place, or removes it when it sends nothing, as `-H` overrides curl's own
  HTTP headers. A pseudo-header cannot be given (`:path: x` has an empty name and is ignored).
- `user-agent` is `-A` when given, none for `-A ""`, else `curl/8.22.0`, the value curl's HTTP handler sends.
- A `-bin` value (ADR-1226) that is not base64 ends the transfer with exit 2, curl's usage error, before anything is
  sent.
- The reflection stream (ADR-1149) carries the same headers, as grpcurl sends `-H` to reflection too.

## Consequences

`GrpcRequestHeaders.Build` takes a `GrpcRequestHeaderValues` (timeout, user-agent, `-H` values) the handler fills from
the transfer's `MaxTime` and `HttpRequestOptions`. `GrpcRequestHeaderCases` pins it.
