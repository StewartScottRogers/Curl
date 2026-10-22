---
id: BL-2901
title: Move stripcredentials (OutputFileExtendedAttributes.WithoutCredentials) into a library the unit-program ports reach, for tool1621
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/OutputFileExtendedAttributes.cs, Curl.Console.UnitTests/OutputFileExtendedAttributesTests.cs, Curl.Engine.UnitLibrary/OriginUrlCredentials.cs, Curl.Engine.UnitTests/OriginUrlCredentialsTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2901 — Move stripcredentials (OutputFileExtendedAttributes.WithoutCredentials) into a library the unit-program ports reach, for tool1621

## Goal

curl's `stripcredentials` (Curl's `OutputFileExtendedAttributes.WithoutCredentials`, now in `Curl.Console`) lives in a library, so the port of `tests/tunit/tool1621.c` (BL-2845) can call it.

## Context

The C table: an unsupported scheme (`ninja://`) and a bad host (`http://odd%40host/`, `http://only%40one%40host/`) give NULL; otherwise user and password are removed and the URL normalised (`https://localhost:45` is `https://localhost:45/`, `http://odd%40user@host/` is `http://host/`). Source: https://github.com/curl/curl/blob/curl-8_22_0/tests/tunit/tool1621.c and src/tool_xattr.c. Move it (suggested `Curl.Core.UnitLibrary/OriginUrlCredentials.cs`) with a null result where curl returns NULL, keeping the xattr writer's behaviour.

## Acceptance criteria

- [x] The counterpart is in a library `Curl.Conformance.UnitProgram.UnitLibrary` references and gives every row of tool1621.c's table (all build conditions on), NULL as `null`.
- [x] `Curl.Conformance.UnitProgram.UnitLibrary` can call the counterpart (public, or `InternalsVisibleTo Include="Curl.Conformance.UnitProgram.UnitLibrary"` in its library's csproj), so BL-2845 can port the program without touching this library.
- [x] A focused unit test in the library's own `.UnitTests` project pins every row of the C program's table (ADR-0482).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and the changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

- Placed in `Curl.Engine.UnitLibrary` (public `OriginUrlCredentials.Strip`), not the suggested
  `Curl.Core.UnitLibrary`: curl's `stripcredentials` is `curl_url_set(CURLU_GUESS_SCHEME)`, two part
  clears and `curl_url_get`, and the URL API (`ManagedCurlUrlHandle`) lives in the engine, which Core
  cannot reach. Reusing it gives every tool1621 row (unsupported scheme, `%40` hosts, the trailing `/`)
  with no second URL parser. `Curl.Conformance.UnitProgram.UnitLibrary` already references the engine,
  and the class is public, so no csproj changed. `touches` moved from the Core files to the Engine
  files; no task in Doing on `origin/work/dark-factory` named them.
- `OutputFileExtendedAttributes.WithoutCredentials` now calls it and keeps the URL as given where it
  answers `null` (curl skips the attribute there, but a URL that was transferred always parses, so the
  writer's output for `%{url_effective}` is unchanged). Its test rows now pin the normalised form
  (`http://u:p@host` gives `http://host/`, as curl's URL API writes it).
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console`: 0 failing members.

## Log

- 2026-10-21: Created (split from BL-2845, which waits on it).
- 2026-10-21: Backlog -> Doing. Criteria at claim: eaa8e49a 3d228e1f d3a9ab15 099d1d27.
- 2026-10-21: Doing -> Done. OriginUrlCredentials.Strip in Curl.Engine gives every tool1621 row (null where curl returns NULL); the --xattr writer uses it
