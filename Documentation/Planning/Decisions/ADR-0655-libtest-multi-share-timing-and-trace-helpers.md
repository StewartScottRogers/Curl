# ADR-0655: libtest's multi, share, timing and trace helpers over Libcurl

- Status: Accepted
- Date: 2026-10-16
- Decided by Claude under Stewart's delegation
- Task: BL-2555

## Context

ADR-0653 ported `first.h`'s easy steps and left the multi steps and the hang timeout for later.
The libtest ports also need what curl 8.22.0's `tests/libtest/testutil.c` and `testtrace.c` give
the C programs. Read from the `curl-8_22_0` tag on 2026-10-16:

- `first.h` has `multi_init`, `multi_setopt`, `multi_add_handle`, `multi_remove_handle`,
  `multi_perform`, `multi_fdset`, `multi_timeout`, `multi_poll` and `res_multi_wakeup`; there is
  no `multi_wait` macro, no `share_init` and no `url_init` - programs call `curl_multi_wait`,
  `curl_share_init` and `curl_url` themselves (`lib586.c` writes `curl_share_init() failed` and
  returns `TEST_ERR_MAJOR_BAD`).
- `multi_timeout` sets `TEST_ERR_BAD_TIMEOUT` for both a failed call and a timeout below -1; the
  other multi steps set `TEST_ERR_MULTI` for a failed call and `TEST_ERR_NUM_HANDLES` for an
  impossible count.
- `start_test_timing` and `abort_on_test_timeout[_custom]` time the program with `curlx_now` and
  `curlx_timediff_ms`; the message always names `TEST_HANG_TIMEOUT` (60000 ms), even for a custom
  timeout.
- `testutil.c` holds only `tutil_suburl`, `tutil_rlim2str` and `tutil_throwaway_cb` (8.22.0 has no
  `tutil_tvnow` or `tutil_tvdiff`: they became `curlx_now` and `curlx_timediff_ms`).
- `testtrace.c` holds `debug_dump`, `libtest_debug_cb` (driven by `debug_config`) and
  `cli_debug_cb`, which keeps its line state in `static` variables.

## Decision

- The multi steps join `LibTestSteps` (a `partial` class) with upstream's codes and messages,
  `CurlErrorText.Multi` for `curl_multi_strerror`. `MultiWaitAsync` is held to `multi_poll`'s rules
  so a port of a `curl_multi_wait` loop reads as one step.
- `ShareInit` and `UrlInit` fail, as `EasyInit` does, only on a cleaned-up library, and then write
  the programs' own `curl_share_init() failed` / `curl_url() failed` with no file or line and set
  `TEST_ERR_MAJOR_BAD`, as `lib586.c` does.
- `LibTestClock` wraps an injected `TimeProvider`: `Now` (`curlx_now`), `DifferenceMilliseconds`
  (`curlx_timediff_ms`, truncated), `StartTestTiming`/`TestStart` (`tv_test_start`) and
  `WaitAsync` (`curlx_wait_ms`, on the provider's timer, never a sleep). `AbortOnTestTimeout`
  takes the clock, so each program owns its start time instead of a process-wide global.
- `LibTestUtilities` holds `SubUrl` and `ThrowawayWrite`; `tutil_rlim2str` is left out because no
  managed program reads `getrlimit`.
- `LibTestDebugTrace` is `libtest_debug_cb` with `debug_config` as constructor arguments;
  a `tracetime` prefix is the provider's local time as `HH:mm:ss.ffffff ` (upstream mixes the wall
  clock's seconds with the monotonic clock's microseconds, which no test output depends on).
  `LibTestCliTrace` is `cli_debug_cb` with its `static` state per instance, one per program;
  since Libcurl's `DebugCallback` has no handle argument, `CallbackFor(easy)` binds the handle whose
  `CURLINFO_XFER_ID` and `CURLINFO_CONN_ID` tag the lines.
- Trace bytes reach the `TextWriter` one character per byte (Latin-1), so the writer sees exactly
  the bytes upstream's `stderr` gets.

## Consequences

A port of a multi-driven program reads like its C source, step for step, and its timing runs on
a manual clock in tests. The `select_test` step (`select_wrapper`) is not here: Libcurl's
`FdSet` hands back socket sets, not an `fd_set`, and the first port that selects decides its shape.
