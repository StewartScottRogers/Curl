# ADR-0976: An SMTP response-read failure always reports `errno: 0`

- Status: Accepted
- Date: 2026-10-28
- Decision: Decided by Claude under Stewart's delegation (BL-3764, CGR-235)

## Context

When the server closes the connection or answers `MAIL` with garbage, curl exits 56 with
`response reading failed (errno: N)`. The third-party gap report (CGR-235) saw `N` = 0 on curl 8.22.0
(Windows, Schannel) and `N` = 115 on curl 8.18.0 on Linux. 115 is `EINPROGRESS`, left over in `errno` from
the earlier non-blocking connect: curl formats whatever `errno` holds, it is not a property of the failed read.

## Decision

Curl keeps printing `errno: 0` on every platform. The Linux value depends on the C library's stale `errno`,
the report marks it only Probable and only seen on 8.18, and the 8.22.0 reference prints 0. No code changes.
Existing tests in `Curl.Original.Protocol.Smtp.UnitTests` pin the text; `Cgr235Tests` pins the shared constant.
