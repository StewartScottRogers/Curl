---
id: BL-1186
title: Write the [TIMER] trace lines for --trace-config timer, network and -vvvv
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1159]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1186 — Write the [TIMER] trace lines for --trace-config timer, network and -vvvv

## Goal

Curl writes curl 8.21.0's `[TIMER]` lines under `--trace-config timer`, `network`, `all` and `-vvvv`.

## Context

- Split from BL-1159 (ADR-0357 amendment). Measured 2026-10-02 (BL-1159 Notes): `[TIMER] [HAPPY_EYEBALLS] cleared` after `Trying` on a direct connect that succeeds; for `localhost` with both families, `[TIMER] [HAPPY_EYEBALLS] set for 200000ns` and `[TIMER] [HAPPY_EYEBALLS] gives multi timeout in 200ms` after the first `Trying`. Written from the happy eyeballs loop in `Curl.Networking`'s `TcpConnector`, beside `SetupFilterTraceEvents`.

## Acceptance criteria

- [x] Tests pin the `[TIMER]` lines of a plain connect, a `localhost` connect where both families answer, and a refused connect, and that none appears without `timer`, `network` or `all`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

- Measured 2026-10-02, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1`, fixtures in `%TEMP%\bl1186`:
  - `-v --trace-config network http://127.0.0.1:P/`: `[TIMER] [HAPPY_EYEBALLS] cleared` after `[HAPPY-EYEBALLS] connect attempt #0 successful`, before `[HAPPY-EYEBALLS] Connected to ...`.
  - `-v --trace-config timer http://localhost:P/` (IPv4 only listening): `Trying [::1]`, `set for 200000ns`, `gives multi timeout in 200ms`, `Trying 127.0.0.1`, `cleared`, `Established`.
  - Same under `network`: `set for 200000ns` right after `[HAPPY-EYEBALLS] next HAPPY_EYEBALLS timeout in 200ms`; then, after the first poll round, `[TIMER] [HAPPY_EYEBALLS] expires in 199982ns` and `gives multi timeout in 200ms`; `cleared` after the losers' `[TCP] cf_socket_close` lines.
  - Refused (`127.0.0.1:1`, `localhost:1`): no `cleared`; `localhost:1` writes only `set` and `gives`.
  - `expires in` appears only when `multi` is traced (`timer,multi`, `network`, `-vvvv`), not under `timer`, `timer,tcp` or `-vv --trace-config timer`. With `--connect-timeout` there are also `[TIMER] [CONNECTTIMEOUT]` lines.
- Delivered: `ConnectAttemptTraceEvents` takes `tracesTimer` and writes `set for <microseconds>ns` (curl's unit label) and `gives multi timeout in <ms>ms` right after the second family's delay is named, and `cleared` before `Connected to`. `TcpConnector.TracesHappyEyeballsTimer` turns it on (and builds the trace events when it is the only one on); `CurlComposition.TracesTimer` is `timer`, `network` or `all` (`-vvvv` adds `all`).
- Choices (ADR-0357's volatile-value rule): the volatile `set for 199999ns` / `gives ... 201ms` are written from the configured delay exactly. Curl writes no initial poll round after the first attempt, so under `network` `gives` follows `set` directly instead of after the `adjust_pollset` lines.
- Split: the multi-gated `expires in` lines and `[TIMER] [CONNECTTIMEOUT]` filed as BL-1210.
- Tests: `ConnectAttemptTraceEventsTests.TwoFamiliesRacingUnderNetwork_*`, `TimerAloneOnAPlainConnect_WritesOnlyCleared`; `TcpConnectorTests.ConnectAsync_TracingTheHappyEyeballsTimerAcrossTwoFamilies_*`, `..._WhenTheDialIsRefused_WritesNoTimerLine`; `CurlCompositionDnsTraceTests.Connect_WithTheTimerComponent_*` (`timer`, `network`, `all`, `-vvvv`), `Connect_UnderTraceConfigTimer_*`, `Connect_WithoutTheTimerComponent_WritesNoTimerLine`. `Measure-CodeQuality.ps1`: Curl.Networking.UnitLibrary and Curl.Console 100% line and branch; the one Curl.Console failing member, `CurlCommandRunner.TransferUrlAsync` (complexity 12), is in a file this task did not change.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. --trace-config timer, network, all and -vvvv write curl's [TIMER] [HAPPY_EYEBALLS] set, gives and cleared lines; CONNECTTIMEOUT and expires-in split to BL-1210
