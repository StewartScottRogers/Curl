# ADR-1202: The `grpc://` client reports a call the HTTP/2 transport ends as a gRPC status

- Status: Accepted
- Date: 2026-11-02
- Decided by Claude under Stewart's delegation (BL-4585)

## Context

PROTOCOL-HTTP2.md says an RST_STREAM "should propagate an error up to the calling application layer" with the status
its Errors table maps the HTTP/2 error code to, and that a stream initiated after a GOAWAY's last accepted stream is
UNAVAILABLE. Upstream curl has no gRPC client, so there is no curl output to match. Curl's `grpc://` client today fails
such a call with exit 16 (CURLE_HTTP2) and the HTTP/2 exception's text.

## Decision

A call the transport ends is reported exactly like a call that ends with a non-OK `grpc-status`: exit 22 and
`curl: (22) grpc-status <code> (<name>): <message>`, with the code the specification maps it to (the RST_STREAM table,
UNAVAILABLE for a GOAWAY that did not accept the call). The conformance cases in
`Curl.Conformance.Extended.UnitTests/Grpc/GrpcTransportCases.cs` pin the exit code and the `grpc-status <code> (<name>): `
prefix, not the message after it, which the fix chooses.

## Consequences

A script sees one failure shape for every failed gRPC call and can read the status name from it. Exit 16 stays for a
connection-level HTTP/2 failure the specification does not map to a status.
