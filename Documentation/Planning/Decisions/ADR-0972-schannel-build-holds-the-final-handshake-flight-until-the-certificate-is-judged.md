# ADR-0972: The Schannel build holds the client's final handshake flight until the server certificate is judged

Status: Accepted. Decided by Claude under Stewart's delegation (BL-3757, CGR-228).

## Context

Against a server whose certificate is refused, curl's Schannel build aborts before it finishes
the handshake: the server sees the transport end (`Received an unexpected EOF or 0 bytes from the
transport stream`). `SslStream` judges the certificate only after it wrote the client's final
flight (the TLS 1.3 Finished), so the server's handshake completed and then met a reset.

## Decision

`ConnectionStream.HoldsWritesAfterFirstRead` (set by `SslStreamTlsProvider` in the Schannel build
only) keeps every write made after the first read in memory. A read sends what is held first, so
TLS 1.2 and a retried ClientHello cannot deadlock; `ReleaseHeldWritesAsync` sends it once the
handshake succeeded. A refused certificate ends the handshake with the held flight, and the
alert `SslStream` writes, never sent: the connection is closed, as Schannel's is. The OpenSSL build
sends the alert in curl and is unchanged.

## Consequences

A successful TLS 1.3 handshake sends its Finished a moment later, after `SslStream` returns.
