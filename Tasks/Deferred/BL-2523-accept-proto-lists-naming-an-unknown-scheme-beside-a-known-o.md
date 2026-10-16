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

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 9cafc694 46d4e715 a0b0533f.
- 2026-10-16: Doing -> Deferred. Lane 3 measured curl 8.22.0 (the target, ADR-0479) refusing --proto =ipfs,http and http,bogus with exit 2, exactly as Curl does; only 8.21.0 accepted them, so there is nothing to change. The lane's Deferred move was lost (STALL path), applied here.
