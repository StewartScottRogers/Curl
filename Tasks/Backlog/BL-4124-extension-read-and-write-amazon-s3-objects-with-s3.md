---
id: BL-4124
title: Extension: Read and write Amazon S3 objects with s3://
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4092, BL-4197, BL-4198, BL-4199]
touches: [Curl.Extension.S3.UnitLibrary, Curl.Extension.S3.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4124 — Extension: Read and write Amazon S3 objects with s3://

## Goal

Curl provides `s3://bucket/key` URLs with SigV4 signing, multipart upload and bucket listing (works with MinIO and R2), as the extension `Curl.Extension.S3.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 23 of 63. Specification: Amazon S3 REST API reference. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [ ] `Curl.Extension.S3.UnitLibrary` and `Curl.Extension.S3.UnitTests` exist in `/Extensions/S3/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [ ] It provides `s3://bucket/key` URLs with SigV4 signing, multipart upload and bucket listing (works with MinIO and R2).
- [ ] Unit tests check its wire behaviour against named sections of Amazon S3 REST API reference, with no network (fake connections or recorded bytes).
- [ ] With `CURL_EXTENSIONS=0` a URL with its scheme fails exactly as curl fails an unsupported scheme (exit 1, same stderr bytes); unit test.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [ ] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 1): Split for size before any code, following BL-4110 and BL-4175. Filed BL-4197 (project scaffold and SigV4 signer, written here because an extension may not reference Curl.Protocol.Http), BL-4198 (S3 XML bodies: ListObjectsV2, multipart initiate/complete, Error) and BL-4199 (HTTP/1.1 exchange over IConnection; TLS comes from ConnectTarget.UseTls through the connector). This task keeps the handler (s3://bucket/key to endpoint, path-style or virtual-host, GET, -T PUT with multipart above a part size, listing for an empty key or a trailing slash, credentials and region), registration, description, ADR and the CURL_EXTENSIONS=0 test. Added ExtensionDescriptionCatalogue.cs to touches for the `--extensions` description (no task in Doing names it; BL-4110 in Backlog also adds a line, which is a union-merged list file).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 5141452e 986a113e d035ed3b bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Split for size: waits on BL-4197 (project and SigV4 signer), BL-4198 (S3 XML bodies) and BL-4199 (HTTP/1.1 exchange)
