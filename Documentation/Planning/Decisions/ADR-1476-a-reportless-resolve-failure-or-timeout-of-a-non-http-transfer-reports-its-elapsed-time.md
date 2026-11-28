# ADR-1476: A reportless resolve failure or timeout of a non-HTTP transfer reports its elapsed time

- Status: Accepted. Decided by Claude under Stewart's delegation.
- Date: 2026-11-28
- Task: BL-6041

## Context

A handler that fails before it connects returns a result with no `TransferReport`, and
`EndPointReportingProtocolHandler` left every such result unstamped (BL-5421), because
the library engine reads "no report" for `CURLINFO_RESPONSE_CODE` and
`CURLINFO_NUM_CONNECTS` (BL-3302, BL-3309, BL-3316). So `%{time_total}` was 0.

Measured on Windows with curl 8.22.0 (Schannel), `-w "%{exitcode} %{time_total} %{num_connects}"`:
a connect `-m` or `--connect-timeout` ended (`ftp`, `ws`, `wss`: exit 28), an unresolved
host (exit 6) and an unresolved SOCKS proxy (exit 5) each print the elapsed time and
`num_connects` 0.

## Decision

A failed result with no report gets one, carrying the transfer's timings, when its exit
code is 5, 6 or 28 and its scheme is not `http` or `https`. Every other reportless
failure stays without a report.

## Why

- The exit codes name the failures curl was measured to time and that left no report; a
  refused connect already has a report, from its operating system error number.
- `http` and `https` keep BL-5421's behaviour: their timed-out transfer with no report
  is what makes the library engine count the attempted connection (BL-3302). The HTTP
  handler times its own failures.
- For the other schemes the engine's `CURLINFO_NUM_CONNECTS` after such a timeout is now
  0 where it was 1, which is what curl prints for `ftp` and `wss`.
