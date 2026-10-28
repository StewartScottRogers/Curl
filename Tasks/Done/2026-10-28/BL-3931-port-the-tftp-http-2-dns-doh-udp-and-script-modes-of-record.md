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
completed: 2026-10-28
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

- [x] The "not ported" block at the top of `Record-CurlExchange.sh` is gone or lists only modes with a stated reason why they cannot exist off Windows, and no mode exits 2 with "not ported yet".
- [x] Each newly ported mode has at least one case recording byte-identical `request.bin`, `stdout.bin`, `stderr.txt` and `exitcode.txt` to the `.ps1`; cases and results are in the Log.
- [x] `bash -n Record-CurlExchange.sh` passes and the HTTP, HTTPS, FTP and mail cases still match.

## Notes

Too big for one run (six modes, HTTP/2 framing alone is large): split into BL-3938 (TFTP), BL-3939 (UDP sink, DNS, DoH), BL-3940 (HTTP/2), BL-3941 (-Script, HalfClose with -Tls), chained. This task then only confirms the criteria once all four are Done. No code edited in this run.
- Second claim: BL-3938 to BL-3941 were Done, but the header still listed -Http2ResetCode with -Tls as not ported (exit 2), because s_server serves one connection - not a reason it cannot exist off Windows. Ported it here: h2.pl listens on the port itself for a reset run over TLS and gives each connection its own `openssl s_server -naccept 1 -unix` with a perl relay in front and its plaintext on two pipes, offering ALPN h2,http/1.1 as the .ps1 does for a reset run (so curl's HTTP/1.1 retry gets the first Response). The "NOT PORTED YET" block and rce_not_ported are gone; the header section is now "PARAMETERS OF OTHER MODES".
- Measured: .ps1 under pwsh on Windows (-ListenAddress 0.0.0.0, WSL curl 8.18.0 via `-Curl wsl` and --resolve to the Windows host's address) against the .sh in WSL (--resolve to 127.0.0.1), `-Http2Origin alpn -Tls -Http2ResetCode 13`, `-sS -k -A x https://example.test:18443/x`: request.bin, stdout.bin, stderr.txt (empty), exitcode.txt (0) and transcript.txt byte-identical (MD5 1ba81919..., 19aaab34..., 2084e592...). The other modes' cases are in BL-3938 to BL-3941's Logs.
- Regression in WSL: -Http2Origin alpn -Tls (no reset), prior-knowledge with Http2ResetCode 13, HTTP, HTTPS (-Tls), FTP and SMTP all record with exit 0; bash -n passes.
- Script-only change: no C# or test project changed, so the build and fast tests are unaffected; platform-neutral bash and perl, nothing for CI on Linux and macOS to differ on.

## Log

- 2026-10-27: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 30a358d0 6a7980b6 541414b4.
- 2026-10-28: Doing -> Backlog. Split for size into BL-3938, BL-3939, BL-3940, BL-3941; waits for them
- 2026-10-28: Backlog -> Doing. Criteria at claim: 30a358d0 6a7980b6 541414b4.
- 2026-10-28: Ported -Http2ResetCode with -Tls; reset case byte-identical to the .ps1 (see Notes).
- 2026-10-28: Doing -> Done. Record-CurlExchange.sh ports every mode of the .ps1, including -Http2ResetCode with -Tls; nothing exits 2 'not ported yet'
