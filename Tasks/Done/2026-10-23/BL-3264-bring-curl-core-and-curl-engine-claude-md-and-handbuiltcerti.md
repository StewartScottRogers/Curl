---
id: BL-3264
title: Bring Curl.Core and Curl.Engine CLAUDE.md and HandBuiltCertificateVerifier's chainPolicy doc in line with the code
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Core.UnitLibrary/CLAUDE.md, Curl.Engine.UnitLibrary/CLAUDE.md, Curl.Networking.UnitLibrary/HandBuiltCertificateVerifier.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3264 — Bring Curl.Core and Curl.Engine CLAUDE.md and HandBuiltCertificateVerifier's chainPolicy doc in line with the code

## Goal

Three statements that the merge `4d8ca7af8...9f0bbb58c` made false say what the code now does, as the root CLAUDE.md rule "say what it does, do what it says" requires.

## Context

The shift-end review (ADR-0710) found these. The code-reviewer reported them, and each one was checked against the code.

1. `Curl.Core.UnitLibrary/CLAUDE.md:114` says "any other body still reads one segment per read".
   - BL-3214 removed `ReadOneSegment`/`ReadOneSegmentAsync`, and `ConcatenatedReadStream.Read` now always fills across segments. That includes the tool's `-F` bodies.
2. `Curl.Engine.UnitLibrary/CLAUDE.md:205` says "`Raw` both decodings off".
   - `HttpRequestOptions.Raw` was removed. `HttpRequestOptionsMapping.cs:128-129` now maps `PassesContentCoding = !http.HttpContentDecoding` and `PassesTransferCoding = !http.HttpTransferDecoding` separately (ADR-0780).
3. `Curl.Networking.UnitLibrary/HandBuiltCertificateVerifier.cs:20` documents `chainPolicy` as "The `--cacert` chain policy, or `null` for the system store."
   - `ServerCertificateVerification.SystemStoreChainPolicy()` (lines 132-146) now returns a non-null online-revocation policy for the system store in the Schannel build without `--ssl-no-revoke` (ADR-0783).
   - `null` now means the system store with no revocation check.

## Acceptance criteria

- [x] `Curl.Core.UnitLibrary/CLAUDE.md` says that every multipart body read fills its buffer across segments. It keeps the read-once rules for callback and file segments, and no longer says any body reads one segment per read.
- [x] `Curl.Engine.UnitLibrary/CLAUDE.md` names `PassesContentCoding` and `PassesTransferCoding`, each set on its own from `HttpContentDecoding` and `HttpTransferDecoding`, and no longer mentions a `Raw` property of `HttpRequestOptions`.
- [x] The `chainPolicy` `<param>` in `HandBuiltCertificateVerifier.cs` names all three cases: the `--cacert` policy, the Schannel build's system-store revocation policy, and `null` for the system store with no revocation check.
- [x] `dotnet build` and the fast tests pass.

## Notes

- Three doc-only edits; build clean, fast tests green (no failures).

## Log

- 2026-10-23: Created by the shift-end review of 9f0bbb58c.
- 2026-10-23: Backlog -> Doing. Criteria at claim: ec6ecc5b 1b8749e3 e1496eac 6b653068.
- 2026-10-23: Doing -> Done. Core and Engine CLAUDE.md and HandBuiltCertificateVerifier chainPolicy doc match the code
