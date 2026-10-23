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
completed:
---
# BL-3164 — Measure refused loopback connects on Linux and macOS and fail fast there if Curl is slower than curl

## Goal

Off Windows, a refused loopback connection fails at once with curl's text and timing, and `--retry-connrefused` retries as curl does; Windows behaviour stays exactly as it is.

## Context

Stewart reported on 2026-10-22 (Linux and macOS): `curl -sS http://127.0.0.1:9/` takes about 2.1 s against upstream's about 0.2 s ("after 0 ms"), and `--retry 5 --retry-max-time 1 --retry-delay 0 --retry-connrefused` makes two attempts upstream but one for Curl. Measured on Windows on 2026-10-22 with the preview build and System32 curl 8.21.0 Schannel: real curl ALSO takes 2023 ms ("after 2023 ms") and makes one attempt, identical to Curl, because Windows' TCP stack retries a refused SYN. So Windows behaviour is correct and must not change. This task is for Linux and macOS only.

Step one is to measure, not to code: compare Curl with the OpenSSL reference curl 8.22.0 on Linux and macOS (CI, or the gap office's `gap-platforms.yml` run; record the numbers in the task Notes). If Curl is not slower there, change no product code, add the platform-pinned tests and finish. If it is slower, find the cause in `Curl.Networking.UnitLibrary/TcpConnector.cs` (the connect loop near line 996 calls `ConnectFailureReason.Describe(exception, OperatingSystem.IsWindows())`; look for a retry or delay on `SocketError.ConnectionRefused`) and fail fast on the first refused result off Windows. If the retry count differs for a reason other than the delay, the retry decision lives in `Curl.Core.UnitLibrary/TransferRetrier.cs`.

## Acceptance criteria

- [ ] The Notes record the Linux and macOS timings of curl 8.22.0 and of Curl for the single request and for the retry command, taken from CI or a gap-platforms run.
- [ ] `Curl.Networking.UnitTests/TcpConnectorRefusedConnectTests.cs` has a test marked `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]` showing that a refused connect (fake dialler returning `SocketError.ConnectionRefused`) fails on the first result, with a fake `TimeProvider` showing no wait, and exit 7; off Windows a real refused loopback connect fails in under 500 ms.
- [ ] A test marked `[OSCondition(OperatingSystems.Windows)]` pins the unchanged Windows behaviour.
- [ ] A `TransferRetrier` test shows that with `--retry-connrefused --retry-max-time 1 --retry-delay 0` a refused connect gives two attempts and two error lines off Windows, if the retry decision needed a change; otherwise the Notes say it did not.
- [ ] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean and the fast Networking and Core tests pass on Windows.

## Notes

## Log

- 2026-10-22: Created.
