# ADR-0803 — Upstream case 594 stays off the ratchet: libcurl 8.22.0 takes 68 seconds and exits 12, as Curl does

- Status: Accepted
- Date: 2026-10-23
- Task: BL-3401
- Amends: ADR-0737 (its row for 594)
- Decided by Claude under Stewart's delegation.

## Context

Upstream case 594 runs `lib591`: a verbose active-mode FTP upload (`CURLOPT_FTPPORT` `-`) through a
multi handle with `CURLOPT_ACCEPTTIMEOUT_MS` set to `%FTPTIME2` seconds (the case runner gives `8`).
Its server refuses `EPRT` with `500`, accepts `PORT`, and under `NODATACONN` never answers `STOR`
and never dials the `PORT` address. The case expects `QUIT` after `STOR` and exit 28. Upstream lists
it in `tests/data/DISABLED` as failing too often to be reliable, so that expectation is never checked
against curl. ADR-0737 left it off the ratchet because Curl did not finish within the harness's 20
seconds.

BL-3401 measured the reference libcurl 8.22.0 (`C:\Program Files\Git\ucrt64\bin\libcurl-4.dll`,
Schannel, the library `/ucrt64/bin/curl` uses, ADR-0018). No C compiler is installed, so `lib591.c`
could not be built. A throwaway C# program called the DLL directly instead, setting lib591's options
(`CURLOPT_VERBOSE`, `CURLOPT_UPLOAD`, a read callback over the case's upload file, `CURLOPT_FTPPORT`
`-`, `CURLOPT_ACCEPTTIMEOUT_MS` 8000) and driving a multi handle with `curl_multi_perform` and
`curl_multi_wait` (100 ms) until done. It ran against
`Record-CurlExchange.ps1 -Ftp -FtpReply "EPRT=500 we don't like EPRT now",'STOR=STALL'`:

| Client | Control-connection commands after login | Wall clock | Exit |
| --- | --- | ---: | ---: |
| libcurl 8.22.0 | `PWD`, `CWD path`, `EPRT \|1\|…`, `PORT …`, `TYPE I`, `STOR 594`, no `QUIT` | 68078 ms (68472 ms process) | 12 |
| Curl, through the case runner | the same up to `STOR 594` | stopped by the harness at 20 s | — |

libcurl writes `Accept timeout occurred while waiting server connect` after 8 seconds. Then its
`ftp_done` waits a minute for the `STOR` reply that never comes, writes `FTP response timeout` and
`control connection looks dead`, and closes without `QUIT`. Curl does the same, as BL-2795 measured
for curl 8.22.0 with the default accept timeout:
`FtpProtocolHandlerTimeLimitTests.ExecuteAsync_ActiveStorUnansweredAndNeverConnected_EndsWithExit12AfterTheAcceptTimeoutAndAMinuteMoreWithNoQuit`
pins it with a manual `TimeProvider`. No harness run of 594 can finish in 20 seconds while the
platform's libcurl needs 68.

## Decision

1. Curl keeps the accept timeout, then a minute's wait for the pending reply, then exit 12 with no
   `QUIT`, as libcurl 8.22.0 does. Its FTP code does not change.
2. Case 594 stays off `PassingUpstreamCases.txt`. Its expected `QUIT` and exit 28 are stale against
   libcurl 8.22.0, and passing it within 20 seconds would make Curl differ from the platform's
   libcurl. ADR-0737's row for 594 is to be read as "stale against libcurl 8.22.0: libcurl and Curl both
   wait 8 + 60 seconds and exit 12 with no `QUIT`".

## Consequences

The case stays reported as not finishing within 20 seconds. If a later reference libcurl drops the
minute's wait after an accept timeout, or sends `QUIT` and exits 28 here, this is measured again.
