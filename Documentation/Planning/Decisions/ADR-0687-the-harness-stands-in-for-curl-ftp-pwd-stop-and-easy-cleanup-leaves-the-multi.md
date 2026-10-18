# ADR-0687 — The harness stands in for CURL_FTP_PWD_STOP, and curl_easy_cleanup leaves the multi handle

- **Status:** Accepted
- **Date:** 2026-10-18

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2636.

## Context

Upstream test753 (`lib753.c`, ported by BL-2536) needs `<features>Debug ftp</features>` and sets
`CURL_FTP_PWD_STOP=1`. In upstream's debug build `ftp_state_pwd` reads it:

```c
#ifdef DEBUGBUILD
  if(!data->id && getenv("CURL_FTP_PWD_STOP"))
    return CURLE_OK;
#endif
```

so the process's first transfer sends no `PWD` and its state machine waits after the login. The program then
cleans `EASY1` up without `curl_multi_remove_handle`, adds `EASY2` for the same URL, and the case checks that
`EASY2` opens its own connection (`USER`/`PASS` twice, then `PWD`, `EPSV`, `TYPE I`, `SIZE 753`, `QUIT`). A
release build has no such switch, and the ratchet skipped the case for lacking Debug.

Curl's engine also kept a disposed handle in its multi handle, so the program's loop never saw the running
count fall and the case hung.

## Decision

- **The FTP handler can stop before `PWD`.** `FtpProtocolHandler.StopsBeforeEntryPath` (default `false`) makes
  a new control connection's connect phase wait after the login, sending nothing, until the transfer is
  cancelled or the connect phase times out (exit 28), as upstream leaves its state machine in place.
- **Only the conformance harness turns it on, and only for the engine's first perform.** `ManagedCurlEngine`'s
  internal constructor takes `stopsFirstFtpTransferBeforePwd`; when set, the shared state holds a
  `FirstTransferFtpPwdStop` that the first `CreateDispatcher` call claims, standing in for `data->id` 0.
  `UpstreamConformanceTests.CreateEngine` sets it when the case's environment has `CURL_FTP_PWD_STOP`, and
  `PlatformFor` reports Debug to a case that sets it. The public constructor, `Curl.Console` and `Libcurl`
  never set it, so a release-like Curl ignores the variable as release curl does.
- **`curl_easy_cleanup` removes the handle from the multi handle it is in.** `ManagedEasyTransfer.DisposeAsync`
  first calls `Remove` on its multi owner, cancelling its perform, as libcurl's `curl_easy_cleanup` calls
  `curl_multi_remove_handle` for a handle still added.

## Consequences

test753 runs and passes, and is on `PassingUpstreamCases.txt`. Any other Debug case stays skipped unless its
own stand-in is added.
