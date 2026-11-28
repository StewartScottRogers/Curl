# ADR-1476: A followed hop with no response keeps the followed response's header count

- Status: Accepted
- Date: 2026-11-28
- Decided by Claude under Stewart's delegation (BL-6096).

## Context

Under `-L`, curl 8.21.0 prints the followed response's `%{num_headers}` when the next hop fails
before any response arrives (a 302 with four headers prints `4`, whether `-m` ends the hop or its
connection is refused), while `%header{...}` stays empty and `%{header_json}` `{}` (measured with
`Record-CurlExchange.ps1`, ADR-1473). Curl printed `0`: `TransferWriteOutVariables` counted the
last hop's header lists, and no `TransferReport` property counted headers without naming their
source.

## Decision

`TransferReport.HeaderCount` is the one place `%{num_headers}` is counted: the trailer count when
above zero, otherwise the report's own header lines, otherwise `FollowedHeaderCount`.
`RedirectFollower` sets `FollowedHeaderCount` to the followed response's `HeaderCount` on a failed
hop with no HTTP version.

A hop that counts header lines of its own (a proxy's CONNECT reply, an FTP server's replies)
prints those, not the followed count: curl starts its count again when new header lines arrive.
This case was not measured; it keeps what Curl printed before.

## Consequences

The followed response's headers are not copied into the merged report, so `%header{...}` and
`%{header_json}` stay empty, as curl's are.
