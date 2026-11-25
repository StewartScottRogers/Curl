---
id: BL-2523
title: Accept --proto lists naming an unknown scheme beside a known one, as curl 8.21.0 does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-16
completed:
---
# BL-2523 — Accept --proto lists naming an unknown scheme beside a known one, as curl 8.21.0 does

## Goal

`curl --proto =ipfs,http http://127.0.0.1:1/` connects (exit 7 here) as curl 8.21.0 does, instead of Curl refusing the option with `unrecognized protocol 'ipfs'` and exit 2.

## Context

Measured 2026-10-16 during BL-2491 on Windows' curl 8.21.0 (Schannel): `-s -S --proto =ipfs,http http://127.0.0.1:1/` prints `curl: (7) Failed to connect to 127.0.0.1:1 after N ms: Could not connect to server` and exits 7, while Curl prints `curl: unrecognized protocol 'ipfs'`, `curl: option --proto: is badly used here` and the try-help line, exit 2. `--proto =ipfs` alone is refused by both with exit 2 and `option --proto: is badly used here` (curl prints no `unrecognized protocol` line there either). Start in `Curl.Cli.UnitLibrary`'s `--proto` parsing (`AllowedProtocols`). Measure the warning line real curl prints (or not) under `-v` before pinning it.

## Acceptance criteria

- [ ] A `Curl.Cli.UnitTests` test pins that `--proto =ipfs,http` is accepted with `AllowedProtocols` holding `http` and the same standard-error bytes curl 8.21.0 prints for it.
- [ ] A test pins `--proto =ipfs` still refused with exit 2 and curl 8.21.0's exact lines.
- [ ] `--ai-help` stays right for `--proto`.

## Notes

- 2026-11-02 (lane 4): Re-measured on this machine's curl 8.22.0 (Schannel, the target per ADR-0479). `-s -S --proto =ipfs,http http://127.0.0.1:1/`, `--proto =ipfs` and `-v --proto =ipfs,http` all print `curl: unrecognized protocol 'ipfs'`, `curl: option --proto: is badly used here` and the try-help line, then exit 2. That is the same as Curl today, and `CommandLineProtocolSetOptionTests` already pins it (the "ipfs unknown" row). Making Curl accept the list, as the Goal and criterion 1 ask, would break drop-in parity with 8.22.0. Criterion 2's "no unrecognized protocol line" was only true of 8.21.0. The criteria cannot be met as written without going against the target curl, and a lane may not reword them (BL-2488). No code was changed.
- 2026-11-18 (lane 8): Re-measured after the Deferred clear: this machine's curl is still 8.22.0 (Schannel) and `-s -S --proto =ipfs,http http://127.0.0.1:1/` still prints `unrecognized protocol 'ipfs'`, `option --proto: is badly used here`, the try-help line and exits 2, as Curl does. The criteria (written for 8.21.0) still cannot be met without breaking parity, and a lane may not reword them (BL-2488), so this goes to Blocked for Stewart to close it or reword it. No code was changed.
- 2026-11-24 (lane 5): Re-measured on this machine's curl 8.23.0 (Schannel): `-s -S --proto =ipfs,http http://127.0.0.1:1/` prints `curl: unrecognized protocol 'ipfs'`, `curl: option --proto: is badly used here`, the try-help line and exits 2, exactly as Curl does and `CommandLineProtocolSetOptionTests` pins. Stewart's 2026-11-18 closing decision (matches the target curl; do not reopen unless the target moves) still holds: the newer curl still refuses, so this goes back to Deferred. No code was changed.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 9cafc694 46d4e715 a0b0533f.
- 2026-10-16: Doing -> Deferred. Lane 3 measured curl 8.22.0 (the target, ADR-0479) refusing --proto =ipfs,http and http,bogus with exit 2, exactly as Curl does; only 8.21.0 accepted them, so there is nothing to change. The lane's Deferred move was lost (STALL path), applied here.
- 2026-11-02: Deferred -> Backlog. Stewart 2026-11-02: complete all existing tasks before the third-party handoff.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 9cafc694 46d4e715 a0b0533f.
- 2026-11-02: Doing -> Blocked. Stewart: curl 8.22.0 (the target) refuses --proto =ipfs,http with exit 2 exactly as Curl already does, so the 8.21.0 criteria would break parity; may this task be closed as already met by 8.22.0 parity, or Deferred?
- 2026-11-02: Blocked -> Doing. Interactive session closes it under the delegated decision rule (which curl build to match). Criteria at claim: 9cafc694 46d4e715 a0b0533f.
- 2026-11-02: Doing -> Deferred. Closed as obsolete, decided by Claude under Stewart's delegation: the target curl 8.22.0 (Gap/Baselines/target.json) refuses --proto =ipfs,http with exit 2 exactly as Curl does, so meeting these 8.21.0 criteria would break parity. Not to be reopened unless the target moves back.
- 2026-11-18: Deferred -> Backlog. Stewart 2026-11-18: clear Deferred; returned to Backlog
- 2026-11-18: Backlog -> Doing. Criteria at claim: 9cafc694 46d4e715 a0b0533f.
- 2026-11-18: Doing -> Blocked. Stewart: curl 8.22.0 (the target) still refuses --proto =ipfs,http with exit 2 exactly as Curl does; close BL-2523 as met by 8.22.0 parity, or reword its 8.21.0 criteria?
- 2026-11-18: Blocked -> Deferred. Stewart 2026-11-18: closed, matches curl 8.22.0 (the target refuses --proto =ipfs,http with exit 2, as Curl does); do not reopen unless the target version moves
- 2026-11-24: Deferred -> Backlog. Undeferred by Stewart 2026-11-24 to give the idle lanes work.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 9cafc694 46d4e715 a0b0533f.
- 2026-11-24: Doing -> Deferred. Still closed under Stewart's 2026-11-18 decision: curl 8.23.0 refuses --proto =ipfs,http with exit 2 exactly as Curl does; reopen only if the target curl accepts it
