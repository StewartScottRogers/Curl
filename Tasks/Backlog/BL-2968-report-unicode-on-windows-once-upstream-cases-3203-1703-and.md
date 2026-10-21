---
id: BL-2968
title: Report Unicode on Windows once upstream cases 3203, 1703 and 472 pass under it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary/UpstreamWindowsFeatures.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests, Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-10-21
completed:
---
# BL-2968 — Report Unicode on Windows once upstream cases 3203, 1703 and 472 pass under it

## Goal

The conformance harness reports `Unicode` on Windows, as Windows' `curl.exe` does, and upstream cases 3203, 1703 and 472 pass there under it.

## Context

ADR-0737 (BL-2858) keeps `Unicode` off. Adding `"Unicode"` to `UpstreamWindowsFeatures.Names` on 2026-10-21 and running each case with `$env:CURL_UPSTREAM_CASE` gave:

- test3203 (listed; `%if Unicode` names its file `漢字-ä.txt`): the `file://` directory listing came back as `\xC3\xA6\xC2\xBC...` - the name's UTF-8 bytes encoded as UTF-8 a second time. Find whether the harness writes the `<file name>` from Latin-1 or the `file://` listing encodes it twice.
- test1703 (a UTF-8 file name percent-encoded in a `file://` URL): `--output` file empty ("expected `foo\n`, got the end"): the decoded path's bytes are probably read as Latin-1, not UTF-8.
- test472 (`--aws-sigv4` with query `a=%e3%81%82`, `Debug` stand-in): `Signature=ea8d5ce0...` where the case expects `b8783c83...`; compare the canonical query string with curl 8.22.0's `http_aws_sigv4.c`. This may fail without `Unicode` too.

Where the fix lands outside `touches`, add the folder in the same commit. Each product fix also gets a focused unit test in its library's `.UnitTests` (ADR-0482). Update ADR-0737's decision 2 or supersede it.

## Acceptance criteria

- [ ] `UpstreamWindowsFeatures.Names` contains `Unicode`, and test3203 still passes on Windows.
- [ ] test1703 and test472 are on `PassingUpstreamCases.txt` and pass on Windows; test472 also on Linux and macOS in CI.
- [ ] Each product fix has a focused unit test in its library's `.UnitTests` project that fails without it.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
