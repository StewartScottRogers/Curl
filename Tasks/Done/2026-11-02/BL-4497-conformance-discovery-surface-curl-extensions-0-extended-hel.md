---
id: BL-4497
title: Conformance: discovery surface (CURL_EXTENSIONS=0, --extended-help, --ai-help, Curl.Mcp URL and version tools) for every extended protocol
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503]
touches: [Curl.Conformance.Extended.UnitTests/CurlSurface/Discovery, Documentation/Conformance/Extended/CurlSurface-Discovery.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4497 — Conformance: discovery surface (CURL_EXTENSIONS=0, --extended-help, --ai-help, Curl.Mcp URL and version tools) for every extended protocol

## Goal

Every extended protocol is switched off by `CURL_EXTENSIONS=0` exactly as an unknown scheme is refused, is listed by
`--extended-help` and `--ai-help extensions` and never by `curl -V`, and is treated by `Curl.Mcp`'s `parse_url`,
`build_url` and `version` tools as ADR-1181 says, all checked through the real hosts in process, with every gap fixed
or listed with its fix task.

## Context

The rules come from the root `CLAUDE.md` ("Extensions": an extension never changes what curl does, leaves `curl -V`
and `Libcurl` alone, and `CURL_EXTENSIONS=0` turns every extension off), ADR-0999, ADR-1006 (extension options),
ADR-1142 (helper schemes refused as curl refuses an unknown scheme: exit 1, `curl: (1) Protocol "demo" not
supported`), ADR-1180 (MCP serves `--ai-help extensions`) and ADR-1181 (MCP: `parse_url` and `build_url` accept a
listed extended scheme without `NonSupportScheme`; `version` gains `extendedProtocols` after `protocols`, which stays
curl's list; under `CURL_EXTENSIONS=0` extended schemes are unsupported and `extendedProtocols` is empty). Code:
`Curl.Extension.Composition.UnitLibrary/ExtensionListing.cs` (`OffLine`, `Schemes`), `ExtensionDescriptionCatalogue.cs`,
`Curl.Mcp.UnitLibrary/McpUrlTool.cs`, `McpVersionTool.cs`, `Curl.Mcp/CurlMcpHost.cs`; BL-4462 made the internals of
`Curl.Console`, `Curl.Mcp` and `Curl.Mcp.UnitLibrary` visible to the test project.

Checks, for every scheme of every extension (take the list from each library's `*Extension.cs`): with
`CURL_EXTENSIONS=0` a transfer exits 1 with curl's unsupported-protocol message, byte for byte the same as for a
scheme nobody serves (compare with `curl foo://x`), and an extension's long option is refused as curl refuses an
unknown option (exit 2); with extensions on, `--extended-help` lists each extension's line and schemes, `--ai-help
extensions` has each one's section, and `curl -V`'s `Protocols:` line and `Features:` stay curl's; with extensions
off both listings say they are off. In `Curl.Mcp`, `parse_url` and `build_url` accept each scheme (any letter case)
with extensions on and refuse it with `CURLUE_UNSUPPORTED_SCHEME` with them off, and `version`'s `extendedProtocols`
lists each scheme once in `ExtensionDescriptionCatalogue` order, empty with them off. The Plugin extension has no fixed
scheme: check its description line here and leave its helper schemes to BL-4489.

One file per check family in `Curl.Conformance.Extended.UnitTests/CurlSurface/Discovery/`; requirement IDs
`CurlSurface:<rule>-<n>` in a matrix `Documentation/Conformance/Extended/CurlSurface-Discovery.md`. Part of the
extended-protocol conformance plan Stewart approved on 2026-11-02; follow `Documentation/Conformance/Extended/README.md`.
A defect found in a host or an extension is listed in `KnownFailures.txt` with a filed High task
`Fix <Name> conformance: <requirement>` (pipeline feature; touches the owning project, its tests and
`KnownFailures.txt`; criteria: the case passes, a focused unit test pins the fix (ADR-0482), the line is removed).

## Acceptance criteria

- [x] `Documentation/Conformance/Extended/CurlSurface-Discovery.md` lists each rule above with a `CurlSurface:` ID
      mapped to a test, and the harness's matrix check passes.
- [x] Cases in `Curl.Conformance.Extended.UnitTests/CurlSurface/Discovery/` cover every scheme for: `CURL_EXTENSIONS=0`
      refusal byte-identical to an unknown scheme's (exit 1), an extension option refused under it (exit 2),
      `--extended-help` and `--ai-help extensions` on and off, `curl -V` unchanged, and the MCP `parse_url`,
      `build_url` and `version` answers on and off.
- [x] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix <Name> conformance: ...` task; the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [x] `Curl.Conformance.Extended.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP
      at most 30 per method (`Measure-CodeQuality.ps1`).
- [x] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

- Every case passes: no host or extension defect was found, so nothing is added to `KnownFailures.txt` and no fix task is filed. "Every scheme" is every scheme `--extended-help` lists (taken from the running host, so a new extension is covered without editing the cases); the extended protocol libraries' own schemes are pinned in `DiscoveryCaseRun.ExtendedProtocols` (from each `*Extension.cs`) so a scheme dropping out of the listing fails Off-2 and Listing-1.
- The MCP checks call `CurlMcpHost.CreateRegistry` with the real `ExtensionProtocolHandlers.Create`, as the host builds it. The unsupported-scheme refusal is matched on `UnsupportedScheme` in the tool's error text, the code name the URL tool reports for `CURLUE_UNSUPPORTED_SCHEME`.
- The unknown-scheme and unknown-option comparisons take Curl's own answer for `foo://x/` and `--no-such-option-at-all` as the reference and substitute the name, byte for byte, so they need no pinned curl text.
- Only test code and the matrix changed, so `Curl.Conformance.Extended.UnitLibrary` is untouched and its coverage and complexity cannot move; no Measure-CodeQuality run was needed. Every test is platform-neutral: no paths, no OS text. CI on Linux and macOS: met by keeping the change platform-neutral and the fast tests green here.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: fcbc98ea 1574b18b fe617ef5 d8e3fb0e 46eaa437 ef6c9026.
- 2026-11-02: Doing -> Done. 13 discovery-surface cases pass with extensions on and off
