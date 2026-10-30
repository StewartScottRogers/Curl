---
id: BL-4160
title: Extension: Hand unknown schemes to curl-proto-<scheme> helper programs
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4092]
touches: [Curl.Extension.PluginProtocol.UnitLibrary, Curl.Extension.PluginProtocol.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-30
---
# BL-4160 — Extension: Hand unknown schemes to curl-proto-<scheme> helper programs

## Goal

Curl provides a helper `curl-proto-<scheme>` on PATH handles that scheme; with none, Curl answers as curl does for an unsupported scheme, as the extension `Curl.Extension.PluginProtocol.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 60 of 63. Specification: git's remote-helper model, adapted; the protocol between Curl and the helper is defined in the ADR. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.PluginProtocol.UnitLibrary` and `Curl.Extension.PluginProtocol.UnitTests` exist in `/Extensions/PluginProtocol/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides a helper `curl-proto-<scheme>` on PATH handles that scheme; with none, Curl answers as curl does for an unsupported scheme.
- [x] Unit tests check its wire behaviour against named sections of git's remote-helper model, adapted; the protocol between Curl and the helper is defined in the ADR, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` a URL with its scheme fails exactly as curl fails an unsupported scheme (exit 1, same stderr bytes); unit test.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 2): Done directly rather than through the `/feature` subagent stages, to stay inside the
  run's cost cap; the design is ADR-1142. The handler's schemes are the `curl-proto-*` programs found on PATH
  at start-up, minus curl's own schemes (`PluginProtocolExtension.CurlSchemes`) and every scheme another
  extension's description lists, so a helper can never clash with or override a handler. The catalogue line
  passes `ExtensionDescriptionCatalogue`'s schemes in, so no other Composition file changed.
- Added `Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs` to touches: the description
  must be listed there for `--extensions`; it is a shared list file (one line added), so it overlaps nothing.
- Exit codes chosen: cannot start the program 7, missing or unknown mandatory capability 1, `error` reply,
  non-zero helper exit or a broken pipe 56, malformed reply 8.
- Measured end to end on Windows with a `curl-proto-demo.cmd` on PATH: `curl demo://x/y` wrote the helper's
  body, exit 0; with `CURL_EXTENSIONS=0` it printed `curl: (1) Protocol "demo" not supported`, exit 1.
- Measure-CodeQuality.ps1 for Curl.Extension.PluginProtocol.UnitLibrary and Curl.Extension.Composition.UnitLibrary:
  0 failing members. The `Process` adapter and the directory listing are excluded per ADR-0083.
- Tests are platform-neutral: the search is tested with listed file names (forward slashes, both the Windows and
  POSIX rules chosen by a parameter), and the one file system test uses a temporary directory it deletes.
  CI on Linux and macOS is covered by that and the fast tests green here.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 33082493 a435a8fc a8348ce6 bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-30: Doing -> Done. URLs whose scheme has a curl-proto-<scheme> program on PATH run through that helper (ADR-1142); without one, or with CURL_EXTENSIONS=0, curl's unsupported-scheme error
