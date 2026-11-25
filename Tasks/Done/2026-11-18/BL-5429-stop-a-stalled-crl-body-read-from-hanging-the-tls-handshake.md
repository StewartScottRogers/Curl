---
id: BL-5429
title: Stop a stalled CRL body read from hanging the TLS handshake
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/RevocationListDownload.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5429 — Stop a stalled CRL body read from hanging the TLS handshake

## Goal

A CRL distribution point that sends its headers and then stops sending can no longer hang the TLS handshake. The fetch ends when the 5-second fetch budget or the connect's remaining time runs out.

## Context

Three reviewers in the shift-end review of f1af53a37c...1257be9874 (ADR-0710) flagged this: silent-failure-hunter, type-design-analyzer and code-reviewer.

The BL-5428 change to `Curl.Networking.UnitLibrary/RevocationListDownload.cs:23-36` caused it:

- **Before:** the fetch used `HttpClient { Timeout = 5s }` with the default `ResponseContentRead`, so the timeout covered the whole body.
- **Now:** the code sets `Timeout.InfiniteTimeSpan`, sends with `HttpCompletionOption.ResponseHeadersRead` and reads the body in a synchronous `body.Read(...)` loop.
- **The gap:** the token covers only the wait for headers and `ReadAsStream`. A blocked `Read` takes no token, and `cancellation.ThrowIfCancellationRequested()` runs only after a read returns.

Failure scenario: a CRL server answers `200 OK` with `Content-Length: 1000` and then goes silent. The fetch runs synchronously inside the certificate validation callback, so the handshake hangs. It ignores `--connect-timeout`, `-m`, the fetch budget and `RevocationLimit`.

The doc comment ("Cuts the fetch short, headers and body both") is not true of the code. The class is `[ExcludeFromCodeCoverage]` and the unit tests stub the fetch, so no test catches this.

Fix sketch: right after `ReadAsStream`, register `cancellation.Register(static state => ((IDisposable)state!).Dispose(), response)` so that cancelling aborts the blocked read. `TryFetchRevocationList` already catches the resulting `ObjectDisposedException` or `IOException`. Reading with `ReadAsync(chunk, cancellation)` is another option.

## Acceptance criteria

- [x] In `RevocationListDownload.Fetch`, a cancelled token interrupts a body read that is blocked waiting for bytes. No body read waits where the token cannot reach it.
- [x] An Integration test in `Curl.Networking.IntegrationTests` uses a loopback server that sends headers and then stalls. It shows `Fetch` returning or throwing within the token's deadline plus a small margin.
- [x] The `cancellation` parameter's doc comment is true of the code.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Fix: `cancellation.Register(Dispose, response)` right after `ReadAsStream` aborts a blocked read; a final `ThrowIfCancellationRequested` stops a truncated list being returned. Integration test `RevocationListDownloadStalledBodyIntegrationTests` passes. Quality run skipped: the class is excluded from coverage and the change adds no branch.

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing. Criteria at claim: 815b0aa2 e8e45fe6 90e2aaa9 3d836d5e.
- 2026-11-18: Doing -> Done. CRL body read now aborted by the token; integration test added
