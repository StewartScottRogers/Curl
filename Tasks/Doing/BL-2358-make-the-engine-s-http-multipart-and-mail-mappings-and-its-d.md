---
id: BL-2358
title: Make the engine's HTTP, multipart and mail mappings and its data-file lookups stop using Curl.Cli types
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Api.UnitLibrary, Curl.Api.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2358 — Make the engine's HTTP, multipart and mail mappings and its data-file lookups stop using Curl.Cli types

## Goal

`HttpRequestOptionsMapping`, `MultipartFormPartMapping` and `MailRequestOptionsMapping` no longer take `CommandLineOptions` or use `Curl.Cli.ImapUploadFlags`, and `SshKnownHostsFileSearch` and `TransferCredentialLookup` no longer use `Curl.Cli.IDataFileReader`.

## Context

ADR-0506; one of the four pieces BL-2310 needs before the engine can drop its `Curl.Cli` reference. Measured 2026-10-15 by BL-2310: HttpRequestOptionsMapping (line ~94 and HappyEyeballsTimeoutOf) reads Headers, PostData, UserAgent, PathAsIs, HappyEyeballsTimeout; MultipartFormPartMapping (NameEscapingOf, RequestContentTypeOf) reads FormEscape and Headers; MailRequestOptionsMapping.FromCommandLine reads UploadFlags (ImapUploadFlags) and ListOnly. Read each from the `Curl.Api` easy groups (EasyHttpOptions, EasyNetworkOptions, MimeOptions, EasySmtpOptions, EasyProtocolOptions), adding a field where libcurl has the matching `CURLOPT_*` (CURLOPT_UPLOAD_FLAGS for the IMAP flags). For `IDataFileReader`, define an engine-owned file-reader interface (or take the file text as a value) and have `Curl.Console` adapt its reader.

## Acceptance criteria

- [ ] The five files named in Goal do not name `CommandLineOptions`, `ImapUploadFlags` (from `Curl.Cli`) or `IDataFileReader` (from `Curl.Cli`).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1` passes all four gates for every library changed, measured in one call.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
