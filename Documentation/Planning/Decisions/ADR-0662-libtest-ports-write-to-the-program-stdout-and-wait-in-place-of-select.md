# ADR-0662: libtest ports write to the program's stdout and wait in place of select()

- Status: Accepted
- Date: 2026-10-17
- Decided by Claude under Stewart's delegation (BL-2527)

## Context

BL-2527 ported libtest programs 500 to 509 to `Curl.Conformance.LibTest.UnitLibrary`. Three gaps
between a C program and its port had to be closed the same way in every port.

## Decision

1. libcurl's default write function writes to the process's `stdout`; Libcurl's discards the body.
   A port's program `stdout` is `LibTestContext.StandardOutput`, so every port that transfers sets
   `CURLOPT_WRITEDATA` to that stream first, which installs Libcurl's default `FILE *` writer over it.
2. `select_test` over `curl_multi_fdset`'s sets cannot wait on the managed multi handle's sockets,
   which are no descriptors. `LibTestSteps.SelectTestAsync` calls `multi_fdset` for its checks and then
   waits with `curl_multi_wait` for the same timeout, which ends as soon as a transfer has work, as the
   `select()` would.
3. A port keeps its body in a public `TestAsync` taking the `CurlLibrary`, run by `LibTestRun`
   after `curl_global_init`, so a test can hand it a library already cleaned up to reach the failing
   `curl_easy_init` and `curl_multi_init` checks. A run of `easy_setopt` steps goes through
   `LibTestSteps.EasySetOptions` (or `SetOptionsAndPerformAsync`, `SetOptionsAndAddHandle`), which stops
   at the first refusal; where a program's setopt values cannot be refused by Libcurl and the program
   interleaves output with them (`lib506.c`, `lib509.c`), the port does not check the answer.

## Consequences

Ten upstream cases now hold the ratchet (500, 502, 504, 505, 507, 538, 581, 585, 586, 595). The seven
that still fail are filed: BL-2583 (501, 509), BL-2584 (503), BL-2585 (506), BL-2586 (508),
BL-2587 (580) and BL-2588 (596).
