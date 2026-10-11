# ADR-0474: Upstream test1211 is measured `excluded`, because upstream disables it and real curl exits 10

- Status: Accepted
- Date: 2026-10-10
- Task: BL-2021 (gap finding GF-0048, item `behaviour:test1211`)
- Decided by Claude under Stewart's delegation

## Context

Upstream test1211 runs `ftp://%HOSTIP:%FTPPORT/1211 -P -` against the `NODATACONN425` server,
which answers `RETR` with `150 Opening data connection` and at once `425 Can't open data
connection` and never dials the `EPRT` address. It expects exit 28 and no `QUIT`. Tests 1206
and 1207 run the same exchange with `--max-time %FTPTIME2` and expect exit 10 after `QUIT`;
Curl passes both since BL-1978.

BL-2021 measured real curl 8.21.0 (the Schannel build, `x86_64-w64-mingw32`) with
`Record-CurlExchange.ps1 -Ftp -FtpReply "RETR=150 Opening data connection\r\n425 Can't open data connection"`
and `-P -`, which answers `EPRT` 200 and dials nothing:

| Command line | Exit | Last commands | Time |
| --- | ---: | --- | ---: |
| `--max-time 8 ftp://127.0.0.1:<port>/1211 -P - -v` | 10 | `RETR 1211`, `QUIT` | 0.5 s |
| `ftp://127.0.0.1:<port>/1211 -P - -v` | 10 | `RETR 1211`, `QUIT` | 0.1 s |

Both print `Ctrl conn has data while waiting for data conn`, `FTP code: 425` and
`curl: (10) FTP: The server failed to connect to data port`. curl 8.21.0's `lib/ftp.c` agrees:
`ftp_check_ctrl_on_data_wait` returns `CURLE_FTP_ACCEPT_FAILED` for any 4xx or 5xx reply read
while it waits for the data connection, and nothing on that path depends on `--max-time`; the
only `CURLE_OPERATION_TIMEDOUT` near it is `ftp_readresp`'s answer to a 421.

Upstream does not run test1211: curl 8.21.0's `tests/data/DISABLED` lists it (beside 1209, under
the comment for 1184, "causes flakiness in CI builds"), so its expected exit 28 is never checked
against curl.

## Decision

1. Curl keeps exit 10 after `QUIT` for a negative reply read behind the `150`, with or without
   `--max-time`, as real curl 8.21.0 does. `FtpProtocolHandlerActiveModeTests.
   ExecuteAsync_NegativeReplyBehindThe150_QuitsWithExit10WithoutWaiting` pins it, with no
   transfer time set.
2. test1211 is not added to `PassingUpstreamCases.txt`; the in-process ratchet leaves it
   `Inconclusive` with its first difference (`QUIT`).
3. The gap office measures `behaviour:test1211` as `excluded`, with the reason "disabled
   upstream (tests/data/DISABLED); real curl 8.21.0 exits 10 after QUIT, which Curl matches
   (ADR-0474)", so GF-0048's item can close.
4. Any other case upstream's `DISABLED` lists, whose expectation real curl contradicts, is
   handled the same way: measure real curl, match it, and exclude the case with that reason.

## Consequences

Matching test1211's text would make Curl differ from real curl on the same exchange, which a
drop-in replacement cannot do. Applying the exclusion in the gap office's measurement lives
under `Gap/`, which the dark factory never reads or changes, so it is left to an interactive
session's next gap run.
