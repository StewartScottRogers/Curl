# ADR-1456: A refused gRPC server reflection request reports its status and headers, so `--dpop` retries it

- Status: Accepted. Decided by Claude under Stewart's delegation.
- Date: 2026-11-26
- Task: BL-6068

## Context

The `--dpop` hook retries a 400 or 401 answer that brings a new `DPoP-Nonce` once (RFC 9449 §8, §9), reading the
status and the header from the result's `TransferReport`. A refused server reflection request (ADR-1454) failed with no
report, and a gRPC response's report held no response headers at all, so neither the reflection request nor the call
could be retried.

## Decision

- A gRPC response's report carries its Response-Headers' fields, without the pseudo-headers, as `ResponseHeaders`. This
  is the call's report as well as the reflection stream's, so a refused call is retried too, and `-w %{header_json}`,
  `%header{...}` and `%{num_headers}` show a gRPC response's headers, as they do for curl's own HTTP/2 responses. Trailers
  that follow the Response-Headers are not added.
- A reflection request the server answers with a status instead of a reply fails with the report of that response, so
  its status, headers and `-w` values reach the hooks and the output. The exit code and message are unchanged.
- The `--dpop` hook needs no change: its retry runs the whole transfer again on a new connection, and both the
  reflection proof and the call's proof then carry the nonce.

## Consequences

- With `--dpop`, a reflection request or a call refused with a new `DPoP-Nonce` is sent once more.
- `--retry` and `-w` now see the HTTP status of a refused reflection request.
