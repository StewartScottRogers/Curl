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
completed: 2026-10-15
---
# BL-2344 — Map -F and --form-string onto EasyHttpOptions.MimePost and make MultipartFormPartMapping read it

## Goal

`EasyOptionsFromCommandLine.HttpOf` maps the `-F` / `--form-string` parts onto `EasyHttpOptions.MimePost` as curl's tool builds its `curl_mime` tree, and the engine's `MultipartFormPartMapping` (and `TransferContextFactory`'s form body) build the multipart body from that record instead of `CommandLineOptions.FormParts`; behaviour is byte for byte unchanged.

## Context

Split out of BL-2307 (ADR-0506 plan phase 1 step 4, ADR-0527), which built `HttpOf` without the form. The record cannot yet say everything `FormPartSpecification` says: a `name=<file` part is `curl_mime_filedata` with its file name cleared (`curl_mime_filename(part, NULL)`), which `EasyMimePart.FileName = null` cannot tell apart from `name=@file`'s base name, and `@-` / `<-` read standard input, which the tool reads into memory (`EasyMimeBytes`) before setting it. Decide the record gap (ADR, extend `EasyMimePart` in `Curl.Api.UnitLibrary/Options/Http` with a test) and agree with `--libcurl`'s `MimeLines` in `Curl.Cli.UnitLibrary/LibcurlSourceCode.cs`. `--form-escape` already maps to `MimeOptions.FormEscape` (BL-2307). The mail schemes' escaping (`MultipartFormPartMapping.NameEscapingOf`) and the `-H Content-Type` lookup stay as they are. Follow BL-2303's rules for retarget tasks.

## Acceptance criteria

- [x] `EasyOptionsFromCommandLine.HttpOf` sets `MimePost` for `-F`, `--form-string`, `name=@file`, `name=<file`, `;type=`, `;filename=`, `;headers=`, `;encoder=` and `name=(` ... `=)`, each with an `EasyOptionsFromCommandLineTests` case, and leaves it `null` without them.
- [x] `MultipartFormPartMapping` reads `EasyMimeBody`, not `FormPartSpecification`.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes, no existing test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console,Curl.Api.UnitLibrary` passes.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case still passes.

## Notes

- Decided (ADR-0531): `@file` sets `EasyMimePart.FileName` to the `;filename=` value or the path's base name, as `curl_mime_filedata` does, and `<file` leaves it null (the tool's `curl_mime_filename(part, NULL)`), so `EasyMimePart` needed no new member. `@-`/`<-` are a new `EasyMimeStandardInput` data kind (curl's tool's stdin `curl_mime_data_cb`), read when the body is built, as before, rather than read into `EasyMimeBytes` while mapping options. Text is UTF-8 `EasyMimeBytes`; the engine decodes it before the builder encodes it in the platform encoding, so the wire bytes are unchanged.
- `MultipartFormPartMapping.FromCommandLine` became `FromMimeBody`; an `EasyMimeCallback` part is refused with `NotSupportedException` (no command line makes one). `NameEscapingOf` and `RequestContentTypeOf` still read `CommandLineOptions`, as the task says. `CurlCommandRunner` builds `MimePost` with `HttpOf` per transfer; `TransferContextFactory` takes the built body and needed no change.
- The engine tests still parse real command lines, through the linked `EasyOptionsFromCommandLine.HttpOf`; the one changed expectation is `y=@t.txt`'s file name, now `t.txt` (the base name the builder derived before) rather than null.
- Verified: build clean, fast tests all green (Conformance included, `PassingUpstreamCases.txt` untouched), `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console,Curl.Api.UnitLibrary`: 0 failing members. `dotnet format --verify-no-changes` on the touched files reports only issues already there (import order, end-of-file line endings in `CurlCommandRunner.cs`).

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. HttpOf maps -F/--form-string onto EasyHttpOptions.MimePost and the engine builds the form from it (ADR-0531)
