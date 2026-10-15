---
id: BL-2344
title: Map -F and --form-string onto EasyHttpOptions.MimePost and make MultipartFormPartMapping read it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2307]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Api.UnitLibrary/Options/Http, Curl.Api.UnitTests/Options/Http]
requirement: none
created: 2026-10-15
completed:
---
# BL-2344 — Map -F and --form-string onto EasyHttpOptions.MimePost and make MultipartFormPartMapping read it

## Goal

`EasyOptionsFromCommandLine.HttpOf` maps the `-F` / `--form-string` parts onto `EasyHttpOptions.MimePost` as curl's tool builds its `curl_mime` tree, and the engine's `MultipartFormPartMapping` (and `TransferContextFactory`'s form body) build the multipart body from that record instead of `CommandLineOptions.FormParts`; behaviour is byte for byte unchanged.

## Context

Split out of BL-2307 (ADR-0506 plan phase 1 step 4, ADR-0527), which built `HttpOf` without the form. The record cannot yet say everything `FormPartSpecification` says: a `name=<file` part is `curl_mime_filedata` with its file name cleared (`curl_mime_filename(part, NULL)`), which `EasyMimePart.FileName = null` cannot tell apart from `name=@file`'s base name, and `@-` / `<-` read standard input, which the tool reads into memory (`EasyMimeBytes`) before setting it. Decide the record gap (ADR, extend `EasyMimePart` in `Curl.Api.UnitLibrary/Options/Http` with a test) and agree with `--libcurl`'s `MimeLines` in `Curl.Cli.UnitLibrary/LibcurlSourceCode.cs`. `--form-escape` already maps to `MimeOptions.FormEscape` (BL-2307). The mail schemes' escaping (`MultipartFormPartMapping.NameEscapingOf`) and the `-H Content-Type` lookup stay as they are. Follow BL-2303's rules for retarget tasks.

## Acceptance criteria

- [ ] `EasyOptionsFromCommandLine.HttpOf` sets `MimePost` for `-F`, `--form-string`, `name=@file`, `name=<file`, `;type=`, `;filename=`, `;headers=`, `;encoder=` and `name=(` ... `=)`, each with an `EasyOptionsFromCommandLineTests` case, and leaves it `null` without them.
- [ ] `MultipartFormPartMapping` reads `EasyMimeBody`, not `FormPartSpecification`.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes, no existing test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console,Curl.Api.UnitLibrary` passes.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case still passes.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
