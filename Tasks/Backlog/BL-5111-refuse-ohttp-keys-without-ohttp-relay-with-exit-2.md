---
id: BL-5111
title: Refuse --ohttp-keys without --ohttp-relay with exit 2
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.ObliviousHttp.UnitLibrary/ObliviousHttpExtension.cs, Curl.Extension.ObliviousHttp.UnitTests/ObliviousHttpExtensionTests.cs]
requirement: none
created: 2026-11-13
completed:
---
# BL-5111 — Refuse --ohttp-keys without --ohttp-relay with exit 2

## Goal

`--ohttp-keys` given without `--ohttp-relay` exits 2 with `--ohttp-keys needs --ohttp-relay` and sends nothing, instead of silently sending the request directly to the origin.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding EXT-060 (Low, Confirmed; listed with the security blockers). Part B: judged against the port's own docs and specs, not curl. Silent privacy downgrade on user error.

**Repro.**
1. `curl -sS -m 5 --ohttp-keys keys.bin http://127.0.0.1:28000/plain` (whether or not `keys.bin` exists)
2. Control: `curl -sS -m 5 --odoh-target http://127.0.0.1:28000/t http://127.0.0.1:28000/plain`

**Expected.** Consistency with the port's other companion options: `--odoh-target` alone gives `curl: --odoh-target needs --odoh-proxy`, exit 2 (`Curl.Extension.ObliviousDoh.UnitLibrary/ObliviousDohExtension.cs:41`); `--sigstore-bundle` alone gives `--sigstore-bundle needs --sigstore-identity and --sigstore-root`, exit 2. A user who gives `--ohttp-keys` intends an oblivious request, so it should be refused alone rather than sent directly from the user's address.

**Actual.** Step 1 sends `GET /plain` directly to the origin, prints `hello`, exits 0, no warning (even when `keys.bin` does not exist).

**Where to start.** `Curl.Extension.ObliviousHttp.UnitLibrary/ObliviousHttpExtension.cs`: hook activation (`HookFor`) is keyed only on `--ohttp-relay`. Follow the `ArgumentException` pattern of `ObliviousDohExtension`. Check whether `--ohttp-relay` without `--ohttp-keys` is already refused and keep that behaviour.

## Acceptance criteria

- [ ] Tests added to `Curl.Extension.ObliviousHttp.UnitTests/ObliviousHttpExtensionTests.cs` prove `--ohttp-keys <x>` without `--ohttp-relay` throws the `--ohttp-keys needs --ohttp-relay` failure that surfaces as `CurlExitCode` 2 with the exact message, with no hook returned and no connection made.
- [ ] Regression tests prove both options together still produce the hook, no companion options still returns no hook, and the existing `--ohttp-relay` without `--ohttp-keys` behaviour is unchanged.
- [ ] `--extended-help` for the extension says `--ohttp-keys` needs `--ohttp-relay`; `--ai-help` is kept right if it mentions it.
- [ ] Tests are platform-neutral and use no network.
- [ ] `dotnet build Curl.Extension.ObliviousHttp.UnitLibrary -warnaserror` is clean, and `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app (`dotnet run tool.cs`) or `Record-CurlExchange.ps1`, never Python. The unit tests need no server.
- Extension isolation holds: the extension references only `Curl.Extension.Abstractions` and shared layers.
- Review acceptance check, as a hand check: step 1 exits 2 with `curl: --ohttp-keys needs --ohttp-relay`, and the server sees no request.

## Log

- 2026-11-13: Created.
