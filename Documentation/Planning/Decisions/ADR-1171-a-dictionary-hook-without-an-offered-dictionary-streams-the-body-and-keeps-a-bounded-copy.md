# ADR-1171: A dictionary hook without an offered dictionary streams the body and keeps a bounded copy

Status: Accepted. Decided by Claude under Stewart's delegation (BL-4393).

## Context

`--compression-dictionary` buffered every response in memory and wrote it after the transfer ended, so `-N` showed nothing until the server closed and a large download was held whole. The response headers (`Use-As-Dictionary`, `Content-Encoding`) reach the hook only in the report after the transfer, so it cannot know at the first byte whether the body is a dictionary.

## Decision

- When no dictionary is advertised, the body cannot be `dcb` or `dcz` and needs no decoding: it streams to the output as it arrives, and a copy of up to 16 MiB (`StoredBodyLimit`) is kept. After the transfer, a response with `Use-As-Dictionary` is stored from the copy; a body past the limit streams but is not stored.
- When a dictionary is advertised the body may need decoding, so it is still held until the transfer ends.
- A dictionary is stored under `Report.EffectiveUrl` (the URL after `-L`), else the requested URL (RFC 9842 section 2.1).

## Consequences

Memory for a non-dictionary download is bounded by the limit instead of the body size. A transfer that advertised a dictionary is unchanged.
