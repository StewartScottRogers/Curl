---
id: BL-3866
title: Send an invalid-UTF-8 argv byte in a -b string as that byte on Linux and macOS
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/CookieEngine.cs, Curl.Engine.UnitTests/CookieEngineArgvByteTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3866 — Send an invalid-UTF-8 argv byte in a -b string as that byte on Linux and macOS

## Goal

On Linux and macOS, `curl -b $'c=\xfe'` sends `Cookie: c=` followed by the byte FE, as curl 8.18.0 copies `argv` bytes unchanged.

## Context

ADR-0821 hands each `argv` byte that is not valid UTF-8 over as the lone surrogate U+DC00 + byte. BL-3594 (ADR-0823) made `CookieEngine.FromHttpOptions` turn `-b` strings into their bytes in the platform encoding (`WireTextOf`), but `Encoding.UTF8` turns a lone surrogate into EF BF BD. `HttpRequestHeadFormatter.WireText` (Curl.Protocol.Http.UnitLibrary, internal) already gives such a surrogate back as its byte for `-H` and `-X`; `CookieEngine.WireTextOf` should do the same. Measure real curl under WSL first.

## Acceptance criteria

- [ ] A `-b` string holding a lone surrogate U+DC80 to U+DCFF goes out as that byte under a UTF-8 command-line encoding, pinned by a unit test in `Curl.Engine.UnitTests/CookieEngineArgvByteTests.cs` that fails before the fix.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed by BL-3594, which left this out of CGR-065's scope.

## Log

- 2026-10-26: Created.
