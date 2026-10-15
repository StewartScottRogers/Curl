---
id: BL-2340
title: Align UpstreamCurlPlatform with the Features: line now that ECH and HTTP3 left it on Linux and macOS
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2340 — Align UpstreamCurlPlatform with the Features: line now that ECH and HTTP3 left it on Linux and macOS

## Goal

`UpstreamCurlPlatform`'s doc comment says truly how it decides `ECH` and `http/3`, now that Curl's `curl -V` `Features:` line lists neither on any platform (ADR-0520, ADR-0521).

## Context

- `Curl.Conformance.UnitLibrary/UpstreamCurlPlatform.cs` says it reports "every feature on Curl's `curl -V` `Features:` line, with ... `HTTP3` as `http/3`", and lists `ECH` in `CommonFeatures` and `http/3` on Unix. BL-2329 and BL-2330 dropped `ECH` and `HTTP3` from `CurlVersionText.FeaturesLine`, so that sentence is no longer true.
- Decide (ADR if behaviour changes) whether the harness keeps reporting `ECH` and `http/3` because Curl still supports `--ech` and `--http3` (keeps the HTTP/3 cases such as test2500 measured on Linux and macOS), or drops them to match upstream's `runtests.pl`, which derives features from `curl -V`. Keeping them is the cheaper default; then only the doc comment and this library's `CLAUDE.md` change.
- Check the ratchet on Linux/macOS stays green either way (AF-0226).

## Acceptance criteria

- [x] `UpstreamCurlPlatform`'s remarks state the actual source of `ECH` and `http/3`, true of the code.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Decided by Claude under Stewart's delegation: keep reporting `ECH` and `http/3` (Curl still serves `--ech` and `--http3`, so the HTTP/3 cases such as test2500 stay measured on Linux and macOS). Behaviour is unchanged, so no ADR; only the remarks changed. The library CLAUDE.md already says only that `http/3` is Linux and macOS only, which stays true.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. UpstreamCurlPlatform remarks now state ECH and http/3 are listed though the Features: line omits them
