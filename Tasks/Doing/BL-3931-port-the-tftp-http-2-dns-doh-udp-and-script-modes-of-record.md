---
id: BL-3931
title: Port the TFTP, HTTP/2, DNS, DoH, UDP and -Script modes of Record-CurlExchange to Record-CurlExchange.sh
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3930, BL-3938, BL-3939, BL-3940, BL-3941]
touches: [Record-CurlExchange.sh]
requirement: none
created: 2026-10-27
completed:
---
# BL-3931 — Port the TFTP, HTTP/2, DNS, DoH, UDP and -Script modes of Record-CurlExchange to Record-CurlExchange.sh

## Goal

`Record-CurlExchange.sh` handles every remaining mode of `Record-CurlExchange.ps1` (`-Tftp`,
`-Http2`, `-Http2Origin`, `-UdpSink`, `-DnsPort`, `-DohPort`, `-Script`, and `-HalfCloseAfterResponse`
with `-Tls`), so no mode prints "not ported yet".

## Context

Part of BL-3934; follows BL-3930 in the same file. See BL-3929's Context for the approach and
testing. Known constraints from the first pass: socat 1.8 has no ALPN option, so HTTP/2 over TLS
needs `openssl s_server -alpn h2`; TFTP, DNS and the UDP sink need `socat UDP-RECVFROM` or similar;
`-Script`'s extra-connection handling existed for Windows' LDAP client and may have no Unix
counterpart (say so if it has none rather than faking it). HTTP/2 framing replies may be easier to
keep as pre-built byte files than to build in bash.

## Acceptance criteria

- [ ] The "not ported" block at the top of `Record-CurlExchange.sh` is gone or lists only modes with a stated reason why they cannot exist off Windows, and no mode exits 2 with "not ported yet".
- [ ] Each newly ported mode has at least one case recording byte-identical `request.bin`, `stdout.bin`, `stderr.txt` and `exitcode.txt` to the `.ps1`; cases and results are in the Log.
- [ ] `bash -n Record-CurlExchange.sh` passes and the HTTP, HTTPS, FTP and mail cases still match.

## Notes

Too big for one run (six modes, HTTP/2 framing alone is large): split into BL-3938 (TFTP), BL-3939 (UDP sink, DNS, DoH), BL-3940 (HTTP/2), BL-3941 (-Script, HalfClose with -Tls), chained. This task then only confirms the criteria once all four are Done. No code edited in this run.

## Log

- 2026-10-27: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 30a358d0 6a7980b6 541414b4.
- 2026-10-28: Doing -> Backlog. Split for size into BL-3938, BL-3939, BL-3940, BL-3941; waits for them
- 2026-10-28: Backlog -> Doing. Criteria at claim: 30a358d0 6a7980b6 541414b4.
