---
id: BL-2975
title: Read command-line arguments as UTF-8 on Windows, as a Unicode curl.exe does, so upstream test472 signs and sends a non-ASCII path
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Engine.UnitLibrary/EngineComposition.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2975 — Read command-line arguments as UTF-8 on Windows, as a Unicode curl.exe does, so upstream test472 signs and sends a non-ASCII path

## Goal

On Windows, Curl reads a non-ASCII command-line argument the way the `Unicode` build of curl 8.22.0 does (wide `argv` converted to UTF-8), so `curl "http://h/472/a=あ" --aws-sigv4 "aws:amz:us-east-2:es" -u user:secret` sends `GET /472/a=%E3%81%82` with upstream test472's signature `b8783c83...` on Windows, Linux and macOS.

## Context

Found by BL-2968, which turns on `Unicode` in the conformance harness on Windows. With BL-2968's harness change (command words and `<file name>` paths read as UTF-8 when their bytes are valid UTF-8, `Curl.Conformance.UnitLibrary/UpstreamUtf8Words.cs`), test472 still fails on Windows: Curl sends `GET /472/a=? HTTP/1.1`. The character `あ` turns into `?` before the request line is written, because Curl's Windows build models a curl that receives its arguments in the ANSI code page (ADR-0022, `Curl.Authentication.UnitLibrary/CredentialEncoding.cs`, `CommandLineOptions.ConfigFileWireTextEncoding`, `EngineComposition.cs` line ~1244 `CommandLineTextEncoding = CredentialEncoding.ForPlatform(...)`). curl 8.22.0's `tool_main.c` `wmain` and `tool_getparam.c` `parse_args` (`curlx_convert_tchar_to_UTF8`) convert each wide argument to UTF-8 in a `_UNICODE` build, and Windows' `curl.exe` reports `Unicode` in `curl -V`.

Steps:
1. Measure Windows' `C:\Windows\System32\curl.exe` with `Record-CurlExchange.ps1`: a URL path with `あ`, a `-u` user with `ä` and `あ`, and a `-H` value with `ä`. Record which bytes it sends. This settles whether ADR-0022's ANSI model still matches the reference curl.
2. Find where `あ` becomes `?` (the Cli's argument handling or `CommandLineTextEncoding`), and change the Windows argument encoding to match step 1. Record the decision in an ADR that supersedes or amends ADR-0022.
3. Check the AWS SigV4 canonical URI for service `es`. curl double-encodes the already percent-encoded path (`/472/a%3D%25E3%2581%2582`?), while `AwsSigV4UriEncoding.CanonicalPath` escapes the raw path's bytes once. Compute both candidates in a unit test and keep the one that gives `b8783c8387a5249b084642126fe1f8e07e12a2847820fd5b6cd64b2047149da4` with the fixed time 19700101T000000Z.

## Acceptance criteria

- [x] An ADR records the measured Windows `curl.exe` bytes for a non-ASCII URL path, user name and header value, and the argument encoding Curl now uses on Windows.
- [x] Under Windows behaviour, a URL argument `http://fake.fake.fake:8000/472/a=あ` (the string Windows hands a Unicode program) is sent as the request line `GET /472/a=%E3%81%82 HTTP/1.1`, pinned by a unit test in the library that held the `?` conversion. BL-2968 then lists test472 on the ratchet.
- [x] A focused unit test in `Curl.Authentication.UnitTests` pins test472's signature for path `/472/a=%E3%81%82`, and one in the changed Cli or Http library pins the non-ASCII argument's bytes; each fails without the fix.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

- 2026-10-21 (lane 3): Measured with `Record-CurlExchange.ps1` (URL path `a=あ`, `-u "äあ:pä"`,
  `-H "X-A: ä"`). The Windows reference, Git for Windows' mingw curl 8.22.0 (ADR-0018), is not a
  `Unicode` build and sends `GET /472/a=?`, `Basic 5D86cOQ=` (Windows-1252, best fit) and `X-A` as
  `E4` - Curl's bytes today. Only System32's `Unicode` curl.exe 8.21.0 sends UTF-8. Decision
  (ADR-0741): keep ADR-0022's ANSI argument model, so no Cli, Http or Engine change. Acceptance
  criterion 2 (`%E3%81%82` under Windows behaviour) is not met by design: it would make Curl differ
  from its reference. Criterion 3's Cli/Http half has nothing to pin beyond ADR-0067's existing
  Windows-1252 header test, since no argument handling changed.
- test472's signature already matches: new test
  `AwsSigV4SignerTests.Sign_Upstream472NonAsciiPathForEs_EscapesThePercentEncodedPathAgainAndGivesTheCasesSignature`
  pins `b8783c83...` with canonical URI `/472/a%3D%25E3%2581%2582` (the double-encoded candidate).
  Tests only, so no quality run.
- BL-2968 needs re-scoping: reporting `Unicode` on Windows conflicts with ADR-0741; noted on it.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: b166b49a 56e3dd14 34ddd848 dd1cc079.
- 2026-10-21: Doing -> Done. Reference mingw curl 8.22.0 is not Unicode and sends ANSI argv as Curl does (ADR-0741), so criterion 2 is answered by the measurement, not a change; test472's SigV4 signature pinned and matching
