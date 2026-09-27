---
id: BL-389
title: Resend a custom Expect request after a 417 mid-upload the way curl loops to its redirect limit
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-319]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-389 — Resend a custom Expect request after a 417 mid-upload the way curl loops to its redirect limit

## Goal

A request with an `-H "Expect: 100-continue"` line that draws a 417 while its body is being sent is resent the way curl 8.21.0 resends it: the custom line again, a new wait, and another resend for each 417, until the redirect limit ends it with exit 47 `Maximum (50) redirects followed`.

## Context

- Measured in BL-319 (Notes): `curl -T big.bin -H "Expect: 100-continue"` against a server that answers every `Expect` request with 417 after 65536 body bytes made 51 connections, waited for `100 Continue` on each, and ended with exit 47, `%{num_connects}` 51, all 51 417 heads in `-D`.
- BL-319 resends once without the wait (`HttpRequestFraming.WithoutExpect`), so the second 417 arrives after the whole body and is the result, exit 0.
- Decide with a measurement whether `--max-redirs` bounds the loop and whether `-L` matters.

## Acceptance criteria

- [ ] The loop is measured on curl 8.21.0, with and without `--max-redirs 3`, and the bytes are in Notes.
- [ ] A handler test replays it through the fakes and matches the measured connection count, exit code, message and `-D` bytes.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http*` reports no failing member.

## Notes

## Log

- 2026-09-27: Created.
