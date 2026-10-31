---
id: BL-4131
title: Extension: List and sync WebDAV collections
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094, BL-4303, BL-4304, BL-4305]
touches: [Curl.Extension.WebDavTools.UnitLibrary, Curl.Extension.WebDavTools.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed:
---
# BL-4131 — Extension: List and sync WebDAV collections

## Goal

Curl provides `--webdav-list` (parsed PROPFIND) and a directory sync option, as the extension `Curl.Extension.WebDavTools.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 30 of 63. Specification: RFC 4918. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.WebDavTools.UnitLibrary` and `Curl.Extension.WebDavTools.UnitTests` exist in `/Extensions/WebDavTools/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides `--webdav-list` (parsed PROPFIND) and a directory sync option.
- [x] Unit tests check its wire behaviour against named sections of RFC 4918, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 3): Split for size before any code, following BL-4125 (GCS). An option extension registers an `ExtensionOption` and an `ITransferHook` (as `SegmentedDownloadExtension` does), but a hook cannot yet change the URL, method or body of the request it passes on (`HookedTransferContext` forwards `Url` and `PostData`; nothing sets `Http.CustomMethod`), so filed BL-4303 (`WithRequest` in Extension.Abstractions/Transfer). Planned default: send PROPFIND and the sync GETs through the engine (TLS, proxy, `--user`) via that hook, not raw HTTP over IConnection. Filed BL-4304 (project scaffold, PROPFIND body and Depth header, 207 Multistatus reader) and BL-4305 (sync planner from a listing and a local folder). This task keeps the options (`--webdav-list`, a sync option taking a local directory), the hook, catalogue, option, transfer-hook and description registration (shared list files, added to touches), the ADR and the CURL_EXTENSIONS=0 test.
- 2026-10-30 (lane 13): Delivered directly rather than through the multi-agent `/feature` stages, to fit the run's cost cap: it is one small library over the existing transfer hook seam (as in BL-4128). Design in ADR-1149. The options are `--webdav-list` and `--webdav-sync <dir>`, the extension name is `webdav-tools`, and RFC 4918 is followed. Like `--segments` (ADR-1087), the extension has no scheme, so it registers in the option, description and transfer-hook catalogues and `ExtensionCatalogue.cs` gets no line. Criterion 1 is ticked on that precedent. Defaults I chose: listing lines are tab-separated (`collection`/`file`, length, RFC 1123 date, href); sync writes `refused`/`current`/`downloaded` lines; a non-207 PROPFIND or non-2xx GET fails with exit 22, a refused body with exit 8, an unwritable folder with exit 23, a non-HTTP URL with exit 1; both options together or an empty folder give exit 2. Downloads go to `.webdav-part` files, which are renamed over the local copy only on a 2xx.
- 2026-10-30 (lane 13): Checked end to end with the built curl.exe against a loopback PowerShell HttpListener. `--webdav-list` printed the listing (exit 0). `--webdav-sync` downloaded `a.txt` with its last-write time set to `getlastmodified`. `--extensions` and `--ai-help extensions` list `webdav-tools`. `CURL_EXTENSIONS=0 curl --webdav-list` gives "option --webdav-list: is unknown", exit 2. The same check found that the PROPFIND went out without its body: BL-4303's `WithRequest` sets `PostData`, but the HTTP handler sends `Http.Body`. I filed BL-4350, which depends on BL-4344 because that task holds the whole `Curl.Extension.Abstractions.UnitTests` project. The listing is still correct meanwhile, because RFC 4918 section 9.1 treats an empty PROPFIND body as `allprop`. ADR-1149 records this under "Known gap".
- 2026-10-30 (lane 13): Measure-CodeQuality for WebDavTools and Composition took 64 minutes, because Composition reaches every extension test project. Results: Composition 100/100 with 0 failing members. WebDavTools 100/100, but two members failed complexity: `WebDavSyncTransferHook.DownloadAsync` (Cx 18) and `WebDavCollectionListing.ListAsync` (Cx 12). I split both (`PropfindAsync` and `Interpret`; `FetchAsync` and `Keep`); the 99 WebDavTools tests pass afterwards. The shift's 08:18 deadline came before the fast tests and a second quality run, so I requeued with the code uncommitted for the shift to stash. What is left for the next claim: apply the stash, run `Measure-CodeQuality.ps1 -Library Curl.Extension.WebDavTools.UnitLibrary` (Composition already passed and its catalogue lines are unchanged), `dotnet build`, the fast tests, then commit and Done. Criteria 1 to 6 were checked this run.
- 2026-10-30 (lane 13): CI on Linux and macOS: the sync tests write only into a temporary folder they create and delete, and they build every expected path with `Path.Combine`, so the output does not depend on the platform.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 9634e382 6e6e8883 ceea66be e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Split for size: waits on BL-4303 (hook WithRequest), BL-4304 (scaffold, PROPFIND and Multistatus) and BL-4305 (sync planner)
- 2026-10-30: Backlog -> Doing. Criteria at claim: 9634e382 6e6e8883 ceea66be e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-30: Doing -> Backlog. Shift deadline: code done and 99 tests pass, but the fast tests and a WebDavTools quality re-run after the complexity split are still to do (Notes)
- 2026-10-30: Backlog -> Deferred. Split for size (over its cost cap) into BL-4373 and BL-4374
