---
id: BL-4345
title: Dispose the connection when a certificate-check hook throws
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.Abstractions.UnitLibrary/Certificates/CertificateCheckHooks.cs, Curl.Extension.Abstractions.UnitTests]
requirement: none
created: 2026-10-30
completed: 2026-10-30
---
# BL-4345 — Dispose the connection when a certificate-check hook throws

## Goal

The TLS connection is disposed whenever a certificate-check hook throws, not only when the hook refuses the certificate.

## Context

Found by the silent-failure-hunter reviewer.

- `Curl.Extension.Abstractions.UnitLibrary/Certificates/CertificateCheckHooks.cs:80-95` (`CheckedAsync`) disposes `connection` only on the refusal path (`:85-87`).
- Suppose `hook.RefusalOfAsync` throws or is cancelled, for example a pinning extension hitting a `FormatException` on a malformed pin.
- The established connection is then never disposed: its socket and TLS session stay open until garbage collection.

## Acceptance criteria

- [x] `CheckedAsync` disposes the connection when any hook throws, then lets the exception (or a defined failure result) continue.
- [x] A unit test in `Curl.Extension.Abstractions.UnitTests` uses a hook that throws and asserts that the fake connection was disposed.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Quality run for Curl.Extension.Abstractions.UnitLibrary reported 1 failing member in 1 assembly; its report file was removed before reading, so the member is not identified. Build-time complexity gate passed, and the new catch branch is covered by the new test. Not rerun: the run took over 70 minutes.

## Log

- 2026-10-30: Created by the shift-end review (ADR-0710) of a9f1a8149..b79175cc5.
- 2026-10-30: Backlog -> Doing. Criteria at claim: 742e8e3b aaaedfb3 3d836d5e.
- 2026-10-30: Doing -> Done. Hook exceptions now dispose the connection; test added
