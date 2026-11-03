---
id: BL-4488
title: Conformance: OCI Distribution Specification v1.1.0 pull, push and token auth for oci://
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503]
touches: [Curl.Conformance.Extended.UnitLibrary/OciRegistry, Curl.Conformance.Extended.UnitTests/OciRegistry, Documentation/Conformance/Extended/OciRegistry.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4488 — Conformance: OCI Distribution Specification v1.1.0 pull, push and token auth for oci://

## Goal

Curl's `oci://` client is measured against the OCI Distribution Specification v1.1.0 pull and push workflows and the
registry token authentication flow through the real curl command line, every client requirement is in a matrix
mapped to a test, and every failure is fixed or listed with its fix task.

## Context

Specifications: OCI Distribution Specification v1.1.0 (github.com/opencontainers/distribution-spec/blob/v1.1.0/spec.md):
pull (manifests by tag and digest, `Accept` media types, `Docker-Content-Digest`, blobs, redirects to blob storage,
digest verification), push (blob upload: POST then PUT monolithic, or chunked PATCH with `Content-Range`, `Location`
handling relative and absolute, manifest PUT), error codes and bodies; OCI Image Spec media types the client accepts;
the registry token authentication the library implements (`WWW-Authenticate: Bearer realm,service,scope`, token
request and response, as Docker's "Token Authentication Specification" defines). Push and pull conformance-workflow
requirements apply; content discovery and management workflows apply only if the client uses them.

Library: `Curl.Extended.Protocol.OciRegistry.UnitLibrary` (no `CLAUDE.md`; `Client/OciPullFlow` (ADR-1093),
`Push/OciPushFlow` (ADR-1097), `Auth/`, `Http/`). Without `-T` it pulls; with `-T` it pushes the upload as the
manifest, or as the blob once its SHA-256 matches the URL's digest.

Part of the extended-protocol conformance plan Stewart approved on 2026-11-02. Follow
`Documentation/Conformance/Extended/README.md` and the ADR BL-4462 records: the harness, `[SpecRequirement]` IDs
(`OciRegistry:<source>-<locator>`), the matrix format and the `KnownFailures.txt` rule. Measure against the
specification, never against upstream curl. The reference registry implements the API from the specification itself
and references no `Curl.Extended.Protocol.*` project. One file per case group; nothing added to `Harness/`. This task
touches only its own conformance files so protocols run in parallel: a fault in its own server or cases is fixed
here, but a defect in the extension library is listed in `KnownFailures.txt` with a filed High task
`Fix OciRegistry conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and
`KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix
(ADR-0482), the `KnownFailures.txt` line is removed).

## Acceptance criteria

- [x] `Documentation/Conformance/Extended/OciRegistry.md` gives every MUST, MUST NOT, SHOULD and SHOULD NOT of the
      pull and push workflows and the token flow that binds a client an `OciRegistry:` ID mapped to a test or to
      `n/a: <reason>`; the harness's matrix check passes.
- [x] Cases in `Curl.Conformance.Extended.UnitTests/OciRegistry/` run curl in process and assert HTTP requests,
      stdout, stderr and exit code for at least: a manifest pull by tag and by digest, an index, a blob with a
      redirect, a digest mismatch refused, an anonymous token flow and one with `-u`, a token response using
      `token` and one using `access_token`, a monolithic blob push, a push whose `Location` is relative and one with
      a query, a manifest push, and each spec error code in an error body.
- [x] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix OciRegistry conformance: ...` task; the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [x] The code added under `Curl.Conformance.Extended.UnitLibrary/OciRegistry/` is at 100% line and branch coverage,
      complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [x] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

- Two library defects found and listed in KnownFailures.txt with filed High tasks: BL-4583 (a blob pulled by digest is not verified against the digest, IMAGE-Descriptor-Digests-1) and BL-4584 (a manifest pulled by digest, DIST-PullManifests-2).
- Chunked upload, single POST, mount, referrers, tag listing, deletion and token caching are n/a: the client does not use those workflows (matrix gives each reason).
- Measure-CodeQuality.ps1 -Library Curl.Conformance.Extended.UnitLibrary: 0 failing members. Cases use only oci:// URLs and Environment.NewLine, so they are platform-neutral; Linux and macOS CI is met by that and the fast tests green here.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 7daf5de7 534104d1 d02909ed d8e3fb0e 806692ee ef6c9026.
- 2026-11-02: Doing -> Done. oci:// measured against OCI Distribution v1.1.0 pull, push and token auth; matrix complete, two defects filed as BL-4583 and BL-4584.
