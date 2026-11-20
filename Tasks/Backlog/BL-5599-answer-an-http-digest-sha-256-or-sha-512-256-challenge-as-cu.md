---
id: BL-5599
title: Answer an HTTP Digest SHA-256 or SHA-512-256 challenge as curl 8.22.0 does
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary/DigestAuthenticator.cs, Curl.Authentication.UnitLibrary/DigestAlgorithm.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5599 — Answer an HTTP Digest SHA-256 or SHA-512-256 challenge as curl 8.22.0 does

## Goal

With `--digest`, a server challenge with `algorithm=SHA-256`, `SHA-256-sess`, `SHA-512-256` or `SHA-512-256-sess` (and `userhash=true`) gets the same response from the port as from curl 8.22.0.

## Context

From Surly cookbook bug report `2026-11-19T1942-Curl-bug-report.md` (batch 1 of the 1,000-recipe plan), finding #5. Build tested by the reports: the Curl port at `b9ca725077` (`curl`, `curl-mcp 0.1.0`, `Curl.Api 0.2.0`, `Libcurl 0.2.0`), Windows 11 (Linux and macOS through the cookbook's CI where stated); server Surl `763754785`. Check that it still reproduces on the current branch first.

The cookbook asked for "a way to choose Digest SHA-256". In curl there is no option for it: the server chooses the algorithm in its challenge, and curl answers whatever it supports (RFC 7616). Check that the port does the same for every algorithm curl 8.22.0 supports, against a server offering only that algorithm, and two algorithms in two challenges (curl picks the first it supports). `Curl.Authentication.UnitLibrary/DigestAlgorithm.cs` already lists algorithms; measure curl with `Record-CurlExchange.ps1` and pin the bytes.

## Acceptance criteria

- [ ] For each of SHA-256, SHA-256-sess, SHA-512-256 and SHA-512-256-sess, the port's `Authorization: Digest` header against a fixed challenge, nonce and cnonce matches curl 8.22.0's recorded header byte for byte.
- [ ] A `userhash=true` challenge is answered as curl answers it.
- [ ] Focused unit tests in `Curl.Authentication.UnitTests` pin each case; `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-20: Created from Surly bug reports (1942 #5).
