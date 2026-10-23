# ADR-0802 — Upstream case 1211 stays off the ratchet: curl 8.22.0 sends `QUIT` and exits 10, as Curl does

- Status: Accepted
- Date: 2026-10-23
- Task: BL-3400
- Amends: ADR-0737 (its row for 1211), and carries ADR-0474's measurement forward to curl 8.22.0
- Decided by Claude under Stewart's delegation.

## Context

Upstream case 1211 (`ftp://%HOSTIP:%FTPPORT/1211 -P -` against the `NODATACONN425` server, which
answers `RETR` with `150` and then `425` and never dials the `EPRT` address) expects the
control-connection commands `USER`, `PASS`, `PWD`, `EPRT |1|`, `TYPE I`, `SIZE 1211`, `RETR 1211`
with no `QUIT`, and exit 28. Upstream lists it in `tests/data/DISABLED` as unreliable, so that
expectation is never checked against curl. ADR-0737 left it off the ratchet with "differs: Curl
sends `QUIT` where the case expects none"; ADR-0474 measured curl 8.21.0 sending `QUIT` and
exiting 10.

BL-3400 measured the reference curl 8.22.0 (`/ucrt64/bin/curl`, Schannel, `x86_64-w64-mingw32`,
ADR-0018) and Curl on the same exchange with
`Record-CurlExchange.ps1 -Ftp -FtpReply "RETR=150 Opening data connection\r\n425 Can't open data connection"`
and `ftp://127.0.0.1:<port>/1211 -P - -v`:

| Client | Control-connection commands | Exit |
| --- | --- | ---: |
| curl 8.22.0 | `USER anonymous`, `PASS ftp@example.com`, `PWD`, `EPRT \|1\|127.0.0.1\|<port>\|`, `TYPE I`, `SIZE 1211`, `RETR 1211`, `QUIT` | 10 |
| Curl | `USER anonymous`, `PASS ftp@example.com`, `PWD`, `EPRT \|1\|127.0.0.1\|<port>\|`, `TYPE I`, `SIZE 1211`, `RETR 1211`, `QUIT` | 10 |

Both print `curl: (10) FTP: The server failed to connect to data port`. Run through the case
runner (`$env:CURL_UPSTREAM_CASE='1211'`), Curl fails only at the `QUIT` on line 8 of
`<protocol>`.

## Decision

1. Curl keeps sending `QUIT` and exiting 10 for a negative reply read behind the `150`, as curl
   8.22.0 does; `FtpProtocolHandlerActiveModeTests.ExecuteAsync_NegativeReplyBehindThe150_QuitsWithExit10WithoutWaiting`
   keeps pinning it.
2. Case 1211 stays off `PassingUpstreamCases.txt`: its expected protocol (no `QUIT`) and exit 28
   are stale against curl 8.22.0, which is why upstream disables it. ADR-0737's reason for 1211
   now reads "stale against curl 8.22.0: curl and Curl both send `QUIT` and exit 10".

## Consequences

Making 1211 pass would make Curl differ from the platform's curl on the same exchange, which a
drop-in replacement cannot do. If a later reference curl stops sending `QUIT` here, this is
measured again.
