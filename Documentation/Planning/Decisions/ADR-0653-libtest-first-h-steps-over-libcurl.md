# ADR-0653: libtest's first.h steps over Libcurl

- Status: Accepted
- Date: 2026-10-16
- Decided by Claude under Stewart's delegation
- Task: BL-2554

## Context

The libtest ports in `Curl.Conformance.LibTest.UnitLibrary` (ADR-0506, phase 5 of
`Documentation/Planning/Curl-Api-And-libcurl.md`) need what curl 8.22.0's
`tests/libtest/first.h` gives every C program: the `TEST_ERR_*` codes and the step macros
(`global_init`, `easy_init`, `easy_setopt`, the `res_*` forms) that set the program's `result`
and jump to `test_cleanup` on failure. Read from the `curl-8_22_0` tag on 2026-10-16:

- 8.22.0 has no `test.h` and no `test_setopt`; `first.h` holds the macros and `easy_setopt` is the
  setopt step.
- The `TEST_ERR_*` codes are no longer 115 to 126: each is a `CURLE_OBSOLETE*` code -
  `MAJOR_BAD` 20, `RUNS_FOREVER` 24, `EASY_INIT` 29, `MULTI` 32, `NUM_HANDLES` 34, `SELECT` 40,
  `SUCCESS` 41, `FAILURE` 44, `USAGE` 46, `FOPEN` 50, `FSTAT` 51, `BAD_TIMEOUT` 57.
- `global_init`, `easy_init` and `easy_setopt` write `<file>:<line> <function>() failed...` to
  stderr; `curl_easy_perform` and `curl_easy_getinfo` have no macro and write nothing.

Libcurl's calls do not fail the way C's do: `CurlGlobal.Init` throws only for a null engine,
`EasyInit` throws only on a cleaned-up library, and `CurlSlist.Append` throws only for a null string.

## Decision

- `LibTestErrorCodes` holds the twelve codes as `CurlCode` constants with upstream's 8.22.0 values.
- `LibTestSteps` carries `result` as `Result` and has one method per step, answering whether the
  program goes on; `false` is upstream's jump to `test_cleanup` (or `global_init`'s return), and
  ignoring the answer is the `res_*` form. The goto stays in the port, as `if (!steps.X(...))`.
- Each C failure is mapped to the one way the Libcurl call can fail: a null engine is a failed
  `curl_global_init` (`CURLE_FAILED_INIT`, as libcurl answers one), a cleaned-up library is a
  failed `curl_easy_init` (`TEST_ERR_EASY_INIT`), and a null string is a failed
  `curl_slist_append` (`TEST_ERR_MAJOR_BAD`, as the programs set it). Answering a code beats
  throwing because the case's expected return code is what the runner compares.
- Messages are upstream's text, with `[CallerFilePath]`'s file name and `[CallerLineNumber]` for
  `__FILE__` and `__LINE__`, and end in `\n` on every platform, as `curl_mfprintf` writes.
  `EasyPerformAsync` and `EasyGetInfo` write nothing, as upstream writes nothing.

## Consequences

Each port reads like its C source, step for step, and its exit code is the number upstream's
`main()` returns. The multi steps (`multi_init`, `multi_perform` and the rest) and the hang timeout
(`start_test_timing`, `abort_on_test_timeout`) are not here yet; the first port that needs them
adds them beside these.
