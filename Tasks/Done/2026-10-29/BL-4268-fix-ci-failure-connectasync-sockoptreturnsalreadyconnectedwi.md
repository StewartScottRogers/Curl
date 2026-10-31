---
id: BL-4268
title: Fix CI failure ConnectAsync_SockoptReturnsAlreadyConnectedWithoutOpenSocket_ConnectsNothingSoTheFirstWriteFails on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests/Cgr145Tests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4268 — Fix CI failure ConnectAsync_SockoptReturnsAlreadyConnectedWithoutOpenSocket_ConnectsNothingSoTheFirstWriteFails on Linux

## Goal

`ConnectAsync_SockoptReturnsAlreadyConnectedWithoutOpenSocket_ConnectsNothingSoTheFirstWriteFails` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `ConnectAsync_SockoptReturnsAlreadyConnectedWithoutOpenSocket_ConnectsNothingSoTheFirstWriteFails` failed on Linux in CI run 40957132484 (https://github.com/StewartScottRogers/Curl/actions/runs/40957132484). First failing commit: b03266d4.

    Assertion failed. Expected condition to be false.

Lanes test only on Windows, so reproduce with `gh run view 40957132484 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `ConnectAsync_SockoptReturnsAlreadyConnectedWithoutOpenSocket_ConnectsNothingSoTheFirstWriteFails` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: not a product defect. In run 40957132484 the write threw, so the socket the callback saw was never connected (a connected socket gets a NetworkStream and the write succeeds), and the listener Poll returned at 79 ms, inside its 100 ms wait: a connection from elsewhere was already pending on the listener. On Linux, parallel tests that connect to a port just freed (e.g. a refused-probe test) can land on a fresh bind(0) listener that reuses that port.
- Fix: the test now asserts the socket the SockoptFunction saw is not connected (getpeername through a non-owning Socket view, as the first test in the class does) instead of polling the shared listener. Platform-neutral; Curl.Engine.UnitTests 2932 passed on Windows. The CI part of the criterion is met by keeping the change platform-neutral (lane rule 11); the shift CI watch files any failure.
- Touches narrowed from the whole Curl.Engine.UnitTests and Curl.Engine.UnitLibrary to the one test file the fix changes; no production code changed, so no quality run.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 395bec4e.
- 2026-10-29: Doing -> Done. Cgr145 AlreadyConnected test checks its own socket, not a listener other tests can reach on Linux
