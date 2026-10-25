---
id: BL-3164
title: Measure refused loopback connects on Linux and macOS and fail fast there if Curl is slower than curl
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Core.UnitLibrary/TransferRetrier.cs, Curl.Networking.UnitTests/TcpConnectorRefusedConnectTests.cs]
requirement: none
created: 2026-10-22
completed: 2026-10-24
---
# BL-3164 — Measure refused loopback connects on Linux and macOS and fail fast there if Curl is slower than curl

## Goal

Off Windows, a refused loopback connection fails at once with curl's text and timing, and `--retry-connrefused` retries as curl does; Windows behaviour stays exactly as it is.

## Context

Stewart reported on 2026-10-22 (Linux and macOS): `curl -sS http://127.0.0.1:9/` takes about 2.1 s against upstream's about 0.2 s ("after 0 ms"), and `--retry 5 --retry-max-time 1 --retry-delay 0 --retry-connrefused` makes two attempts upstream but one for Curl. Measured on Windows on 2026-10-22 with the preview build and System32 curl 8.21.0 Schannel: real curl ALSO takes 2023 ms ("after 2023 ms") and makes one attempt, identical to Curl, because Windows' TCP stack retries a refused SYN. So Windows behaviour is correct and must not change. This task is for Linux and macOS only.

Step one is to measure, not to code: compare Curl with the OpenSSL reference curl 8.22.0 on Linux and macOS (CI, or the gap office's `gap-platforms.yml` run; record the numbers in the task Notes). If Curl is not slower there, change no product code, add the platform-pinned tests and finish. If it is slower, find the cause in `Curl.Networking.UnitLibrary/TcpConnector.cs` (the connect loop near line 996 calls `ConnectFailureReason.Describe(exception, OperatingSystem.IsWindows())`; look for a retry or delay on `SocketError.ConnectionRefused`) and fail fast on the first refused result off Windows. If the retry count differs for a reason other than the delay, the retry decision lives in `Curl.Core.UnitLibrary/TransferRetrier.cs`.

## Acceptance criteria

- [x] The Notes record the Linux and macOS timings of curl 8.22.0 and of Curl for the single request and for the retry command, taken from CI or a gap-platforms run.
- [x] `Curl.Networking.UnitTests/TcpConnectorRefusedConnectTests.cs` has a test marked `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]` showing that a refused connect (fake dialler returning `SocketError.ConnectionRefused`) fails on the first result, with a fake `TimeProvider` showing no wait, and exit 7; off Windows a real refused loopback connect fails in under 500 ms.
- [x] A test marked `[OSCondition(OperatingSystems.Windows)]` pins the unchanged Windows behaviour.
- [x] A `TransferRetrier` test shows that with `--retry-connrefused --retry-max-time 1 --retry-delay 0` a refused connect gives two attempts and two error lines off Windows, if the retry decision needed a change; otherwise the Notes say it did not.
- [x] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean and the fast Networking and Core tests pass on Windows.

## Notes

- 2026-10-22 (lane 5): Interactive only. The first criterion and the decision whether any product code changes both rest on Linux and macOS timings of curl 8.22.0 and Curl for `curl -sS http://127.0.0.1:9/` and the `--retry-connrefused` command; a dark factory lane runs only on Windows, cannot see CI and may not read the gap office's runs. Code read on Windows found no delay on a refused connect: `TcpDialer.ConnectAsync` calls `Socket.ConnectAsync` once (macOS `connectx` only under `--tcp-fastopen`), `AddressFamilyRace` waits only for a second family, and nothing in Curl.Networking or Curl.Core emulates the Windows SYN retry, so the Linux 2.1 s must be measured to be explained. The retry count looks like a consequence of the delay (the first attempt uses up `--retry-max-time 1`), not of `TransferRetrier`. Remove lane: no once an interactive session has recorded those four Linux and four macOS timings (curl 8.22.0 and Curl, single request and retry command) here.
- 2026-10-24 (interactive): Measured in GitHub Actions run 40107893646 (throwaway branch `measure/bl-3164`, a temporary workflow building curl 8.22.0 from the tarball as `gap-platforms.yml` does, OpenSSL 3.0.13 on ubuntu-latest and 3.5.9 on macos-latest arm64), 5 runs each, median wall time in ms. Curl is the native AOT `dotnet publish Curl.Console -c Release` binary that the preview release ships; the JIT `dotnet build` binary adds about 450 ms of .NET startup on both platforms and is not what users run.
  - Linux `curl -sS http://127.0.0.1:9/`: curl 8.22.0 5 ms, Curl 12 ms (JIT 455); both exit 7 `Failed to connect to 127.0.0.1:9 after 0 ms: Could not connect to server`.
  - Linux retry command: curl 8.22.0 1008 ms, Curl 1010 ms (JIT 1459); both make two attempts and print two `curl: (7)` lines (without `-sS`, one `Warning: Problem : connection refused. Retrying in 1 second. 5 retries left.` each).
  - macOS single request: curl 8.22.0 119 ms, Curl 72 ms (JIT 570); both exit 7 with the same text (Curl's first `after N ms` read 0-12 ms, curl's 0 ms).
  - macOS retry command: curl 8.22.0 1224 ms, Curl 1116 ms (JIT 1661); both two attempts and two error lines.
  - So Curl matches curl off Windows and no product code changed: neither the 2.1 s nor the single attempt reproduces with curl 8.22.0 OpenSSL on Linux or macOS. The retry decision in `TransferRetrier` needed no change (two attempts already, as curl). The pinned tests are `TcpConnectorRefusedConnectTests.cs`: two off Windows (fake refused dial: one dial, no timer, exit 7, `after 0 ms`; a real refused loopback port in under 500 ms) and one on Windows (one dial, no wait of Curl's own; the about 2 s there is the stack's SYN retry). `lane: no` removed now the timings are recorded.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 3bdd64aa 0462b33e 3b94aea2 554ae46e a627db4e.
- 2026-10-22: Doing -> Backlog. Interactive only: needs Linux and macOS timings of curl 8.22.0 and Curl (CI or gap-platforms), which a Windows lane cannot take or see
- 2026-10-24: Backlog -> Doing. Interactive session measuring Linux and macOS in GitHub Actions Criteria at claim: 3bdd64aa 0462b33e 3b94aea2 554ae46e a627db4e.
- 2026-10-24: Doing -> Done. Measured Linux and macOS: Curl matches curl 8.22.0 on refused loopback connects and --retry-connrefused; platform-pinned tests added, no product change
