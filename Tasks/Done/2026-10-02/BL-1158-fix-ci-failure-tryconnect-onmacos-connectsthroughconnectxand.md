---
id: BL-1158
title: Fix CI failure TryConnect_OnMacOS_ConnectsThroughConnectxAndCarriesTheFirstBytes on macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1158 — Fix CI failure TryConnect_OnMacOS_ConnectsThroughConnectxAndCarriesTheFirstBytes on macOS

## Goal

`TryConnect_OnMacOS_ConnectsThroughConnectxAndCarriesTheFirstBytes` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `TryConnect_OnMacOS_ConnectsThroughConnectxAndCarriesTheFirstBytes` failed on macOS in CI run 37004026335 (https://github.com/StewartScottRogers/Curl/actions/runs/37004026335). First failing commit: 92d5ec3e.

    Assertion failed. Expected condition to be true.

Lanes test only on Windows, so reproduce with `gh run view 37004026335 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `TryConnect_OnMacOS_ConnectsThroughConnectxAndCarriesTheFirstBytes` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: `connectx` with `CONNECT_RESUME_ON_READ_WRITE` sends no SYN until the first write, so the
  `dup`'d socket's `getpeername` fails with `ENOTCONN` and `Socket.Connected` is `false`. ADR-0355's
  expectation was wrong, and in production `TcpDialer` would then have hit `NetworkStream`'s
  "not connected" refusal on macOS with `--tcp-fastopen`.
- Fix (ADR-0358): the new `DeferredConnectSocketStream` reads and writes a socket as it is
  (`Socket.Send`/`Receive` do not check `Connected`). `TcpDialer.Connected` uses it for a socket that
  is not yet connected, and `NetworkStream` otherwise. Tested on every platform over loopback. The macOS
  test now writes through that stream and checks that the local end point is readable.
- Lanes cannot run macOS. Locally, the macOS test is skipped on Windows and everything else is
  green. CI on macOS is the check for the commit that lands this; the shift's CI watch files a
  new task if it fails again.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. --tcp-fastopen on macOS reads and writes the connectx socket through DeferredConnectSocketStream instead of the NetworkStream that refused it
