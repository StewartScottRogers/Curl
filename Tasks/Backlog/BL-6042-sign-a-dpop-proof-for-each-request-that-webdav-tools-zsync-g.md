---
id: BL-6042
title: Sign a DPoP proof for each request that --webdav tools, --zsync, --grpc-web and later transfer hooks send
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs, Curl.Extension.Composition.UnitTests/DpopWithRequestChangingHooksTests.cs, Curl.Extension.Dpop.UnitLibrary/DpopTransferHook.cs, Curl.Extension.Dpop.UnitTests/DpopTransferHookTests.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6042 — Sign a DPoP proof for each request that --webdav tools, --zsync, --grpc-web and later transfer hooks send

## Goal

With `--dpop`, every request an inner transfer hook sends or rewrites carries a proof for that request's own method and URL.

## Context

Found in BL-6029 (ADR-1426), which moved the `--dpop` hook inside `--resumable-upload`'s. In
`ExtensionTransferHookCatalogue` the hooks listed after the DPoP hook can still send several requests, or change the
method or URL, under the one proof the DPoP hook made: start with `WebDavToolsExtension` (calls `next` with
`WithRequest(url, "GET", null)`), `DeltaDownloadExtension`, `GrpcWebExtension`, `GrpcExtension`, `PrivacyPassOptions`
and `CompressionDictionaryExtension`. `ObliviousHttpExtension` sends to a relay, so decide which request its proof
names. RFC 9449 §4.2: `htm` and `htu` match each request and `jti` is unique. Also check that a `DPoP-Nonce` retry of
a request whose body is `Http.Body` (`-F`, a streamed `-d`) sends the whole body again: the hook rewinds only the
`-T` upload.

## Acceptance criteria

- [ ] Tests in `Curl.Extension.Composition.UnitTests` show that each request sent by each hook listed above, run with `--dpop`, carries a proof whose `htm` and `htu` are that request's and whose `jti` is unique, or an ADR records why that hook's request differs.
- [ ] A test in `Curl.Extension.Dpop.UnitTests` shows a `DPoP-Nonce` retry sends an `Http.Body` request body whole.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
