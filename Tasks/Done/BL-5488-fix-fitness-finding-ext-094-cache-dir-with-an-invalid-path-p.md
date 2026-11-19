---
id: BL-5488
title: Fix fitness finding EXT-094: --cache-dir with an invalid path prints .NET exception text and the full local path
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.HttpCache.UnitLibrary/CacheDirectory.cs, Curl.Extension.HttpCache.UnitTests/CacheDirectoryTests.cs, Curl.Extension.Sigstore.UnitLibrary/SigstoreExtension.cs, Curl.Extension.Sigstore.UnitTests/SigstoreExtensionTests.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5488 — Fix fitness finding EXT-094: --cache-dir with an invalid path prints .NET exception text and the full local path

## Goal

EXT-094 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: --cache-dir with an invalid path prints .NET exception text and the full local path. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (2 runs)
- **Protocol/area:** extension option errors, Windows
- **Repro:** any HTTP server on 127.0.0.1:47060. `curl -sS -o NUL --cache-dir "<>:|bad" http://127.0.0.1:47060/x`. Also `curl -sS --sigstore-bundle x.json http://127.0.0.1:47060/x` and, for comparison, `curl -sS --sigstore-root x http://127.0.0.1:47060/x`.
- **Expected:** ADR-1289 and ADR-1295: an extension option error carries no .NET exception text or directory and names the option, e.g. `curl: --cache-dir cannot use '<>:|bad'` plus the try-help line, exit 2. Companion checks are made at activation with the same style (`curl: --sigstore-root needs --sigstore-bundle` plus try-help).
- **Actual (PORT):** `curl: (2) The filename, directory name, or volume label syntax is incorrect. : 'C:\Users\<user>\…\<>:|bad\7d690d98f2…5af2.entry'.`, exit 2 (user name and working directory exposed). `--sigstore-bundle x.json` alone prints `curl: (2) --sigstore-bundle needs --sigstore-identity and --sigstore-root` with no try-help line.
- **Likely location:** `Curl.Extension.HttpCache.UnitLibrary/CacheDirectory.cs` (catches only `FileNotFoundException`/`DirectoryNotFoundException`); the activation check in `Curl.Extension.Sigstore.UnitLibrary`.
- **Acceptance check:** the first command prints one message naming `--cache-dir` and `'<>:|bad'`, with no `C:\Users` text and no OS sentence; `--sigstore-bundle x.json` alone prints `curl: --sigstore-bundle needs …` plus the try-help line, exit 2.

## Acceptance criteria

- [x] the first command prints one message naming `--cache-dir` and `'<>:|bad'`, with no `C:\Users` text and no OS sentence; `--sigstore-bundle x.json` alone prints `curl: --sigstore-bundle needs …` plus the try-help line, exit 2.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Widened touches to the Sigstore activation check named by the finding (no other Doing task holds it). Decision in ADR-1302. Measured: Curl now prints `curl: --cache-dir cannot use '<>:|bad'` plus try-help, exit 2; Windows-only colon case pinned with OSCondition, platform-neutral test uses a NUL character. Quality run clean for both libraries; fast tests of both test projects and Composition pass.

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding EXT-094 (Low).
- 2026-11-18: Backlog -> Doing. Criteria at claim: e32926b1 69116afe 3d836d5e.
- 2026-11-18: Doing -> Done. --cache-dir invalid path and --sigstore-bundle without companions refused at activation (ADR-1302)
