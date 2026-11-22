# ADR-1334: Curl.Api hands over the body a failed transfer delivered before its error (BL-5746)

Status: Accepted. Decided by Claude under Stewart's delegation.

## Context

The console streams a body to stdout as it arrives and reports a failure at the end, so a gRPC server stream
that sends three replies and then status 13 (or hits a deadline) prints the replies and exits 22. `CurlClient.SendAsync`
threw the `CurlException` as soon as the transfer had ended in error, even when body bytes had been delivered, so a
`Curl.Api` caller got no replies. The same held for any HTTP transfer that failed after part of the body arrived (18, 56, 28)
when it ended before `SendAsync` returned.

## Decision

`SendAsync` throws only for a transfer that failed before delivering any body byte. When body bytes were delivered
and the transfer then failed, `SendAsync` returns the response; `CurlResponse.Body` yields every delivered byte and its
read that reaches the end throws the `CurlException` (or `OperationCanceledException` after a cancel), as it already did
for a failure arriving after `SendAsync` returned. The behaviour is the same for gRPC and HTTP, whatever the timing.

## Consequences

A caller must read the body to the end to learn of a failure; this was already so for large bodies. Pinned in
`ResponseBodyStreamTests`.
