---
id: BL-4124
title: Extension: Read and write Amazon S3 objects with s3://
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4092, BL-4197, BL-4198, BL-4199]
touches: [Curl.Extension.S3.UnitLibrary, Curl.Extension.S3.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Console.UnitTests/CurlCompositionTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4124 — Extension: Read and write Amazon S3 objects with s3://

## Goal

Curl provides `s3://bucket/key` URLs with SigV4 signing, multipart upload and bucket listing (works with MinIO and R2), as the extension `Curl.Extension.S3.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 23 of 63. Specification: Amazon S3 REST API reference. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.S3.UnitLibrary` and `Curl.Extension.S3.UnitTests` exist in `/Extensions/S3/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides `s3://bucket/key` URLs with SigV4 signing, multipart upload and bucket listing (works with MinIO and R2).
- [x] Unit tests check its wire behaviour against named sections of Amazon S3 REST API reference, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` a URL with its scheme fails exactly as curl fails an unsupported scheme (exit 1, same stderr bytes); unit test.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 1): Split for size before any code, following BL-4110 and BL-4175. Filed BL-4197 (project scaffold and SigV4 signer, written here because an extension may not reference Curl.Protocol.Http), BL-4198 (S3 XML bodies: ListObjectsV2, multipart initiate/complete, Error) and BL-4199 (HTTP/1.1 exchange over IConnection; TLS comes from ConnectTarget.UseTls through the connector). This task keeps the handler (s3://bucket/key to endpoint, path-style or virtual-host, GET, -T PUT with multipart above a part size, listing for an empty key or a trailing slash, credentials and region), registration, description, ADR and the CURL_EXTENSIONS=0 test. Added ExtensionDescriptionCatalogue.cs to touches for the `--extensions` description (no task in Doing names it; BL-4110 in Backlog also adds a line, which is a union-merged list file).

- 2026-10-29 (lane 12): Built the handler (ADR-1090): `Client/S3ProtocolHandler`, `S3Configuration` (--user, else AWS_ACCESS_KEY_ID/AWS_SECRET_ACCESS_KEY/AWS_SESSION_TOKEN; AWS_REGION, AWS_DEFAULT_REGION, else us-east-1), `S3Endpoint` (virtual-hosted on AWS, path-style for a dotted bucket and for AWS_ENDPOINT_URL_S3/AWS_ENDPOINT_URL, i.e. MinIO and R2), `S3Client` (one signed request per connection, status 300+ is exit 22 naming the S3 Error code) and `S3Operations` (GetObject, PutObject, 8 MiB multipart from 8 MiB, ListObjectsV2 with delimiter / over every page). Defaults taken: the AWS CLI's variable names and 8 MiB part size, so existing environments work unchanged; an upload to an empty key or prefix fails with exit 3 rather than become CreateBucket.
- Added `Curl.Console.UnitTests/CurlCompositionTests.cs` to touches: it pins the list of connecting handlers, which now includes S3ProtocolHandler (no task in Doing names it).
- Measure-CodeQuality.ps1: Curl.Extension.S3.UnitLibrary and Curl.Extension.Composition.UnitLibrary 100% line and branch, complexity at most 10, no member outside a gate. Build clean, fast tests green. CI on Linux and macOS: the tests use no paths, OS errors or certificates, so they are platform-neutral.

- 2026-10-29 (lane 11): Integrated lane 12's branch after the rebase conflict; renumbered its ADR from 1075 (taken by the OAuth2 ADR) to ADR-1090. Build clean, fast tests green, Measure-CodeQuality.ps1 0 failing members for S3 and Composition.
- 2026-10-29 (lane 11): Rebased onto work/dark-factory again; ADR-1080 and ADR-1085 were both taken (Kafka leader, WS subprotocol), so the S3 ADR is now ADR-1090, and the composition test lists both the AMQP 1.0 and S3 handlers (25).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 5141452e 986a113e d035ed3b bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Split for size: waits on BL-4197 (project and SigV4 signer), BL-4198 (S3 XML bodies) and BL-4199 (HTTP/1.1 exchange)
- 2026-10-29: Backlog -> Doing. Criteria at claim: 5141452e 986a113e d035ed3b bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Lane 12 could not integrate: rebase conflict the resolver could not settle. The work is on branch factory/BL-4124-lane-12-20261029-140509; start with git cherry-pick --no-commit factory/BL-4124-lane-12-20261029-140509 and fix it.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 5141452e 986a113e d035ed3b bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. s3:// GetObject, PutObject with multipart and ListObjectsV2, SigV4-signed, integrated with ADR-1090
