---
id: BL-5325
title: Refuse --sigstore-identity, --sigstore-root and --sigstore-issuer without --sigstore-bundle
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Sigstore.UnitLibrary/SigstoreTransferHook.cs, Curl.Extension.Sigstore.UnitLibrary/SigstoreExtension.cs, Curl.Extension.Sigstore.UnitTests]
requirement: none
created: 2026-11-16
completed:
---
# BL-5325 — Refuse --sigstore-identity, --sigstore-root and --sigstore-issuer without --sigstore-bundle

## Goal

`--sigstore-identity`, `--sigstore-root` or `--sigstore-issuer` given without `--sigstore-bundle` exits 2 naming `--sigstore-bundle` before any request is sent, instead of downloading with no verification.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding EXT-069 (Low, Confirmed, 2 runs; silent security downgrade on a user error). Area: sigstore; Windows. This is an extension, so it is judged against the port's own docs and ADRs, not curl.

**Repro.** `curl -sS -m5 --sigstore-identity me@example.com --sigstore-root root.pem --sigstore-issuer https://accounts.example.com http://127.0.0.1:38000/plain -o sg.out`

**Expected.** Consistency with the port's companion checks: `--sigstore-bundle` alone exits 2 with `--sigstore-bundle needs --sigstore-identity and --sigstore-root`; `--ohttp-keys` alone and `--odoh-target` alone exit 2. A user who names a signer means to verify, so this should exit 2 with, for example, `curl: --sigstore-identity needs --sigstore-bundle` and send no request.

**Actual.** The body is written to `sg.out`, exit 0, no warning. (The same silent-ignore applies with lower impact to `--zsync-seed` without `--zsync`, `--graphql-variables`/`--graphql-operation` without `--graphql`, and `--http-env` without `--http-file`; those are outside this task, and the report lists them for Stewart to file separately.)

**Where to start.** `Curl.Extension.Sigstore.UnitLibrary`, hook activation (`SigstoreTransferHook.cs`, keyed only on `--sigstore-bundle`). Follow how the existing companion check for `--sigstore-bundle` alone returns exit 2, and how `--ohttp-keys` alone does. If the exit-2 message list is documented (`--extended-help`, the Sigstore doc), keep it right.

## Acceptance criteria

- [ ] New tests in `Curl.Extension.Sigstore.UnitTests` prove each of `--sigstore-identity`, `--sigstore-root` and `--sigstore-issuer` given alone, and all three together, without `--sigstore-bundle`, return exit 2 with a message naming `--sigstore-bundle` and the option given, and that the transfer never starts (the stand-in transport sees no request).
- [ ] A test proves the existing cases are unchanged: `--sigstore-bundle` with identity and root still verifies, `--sigstore-bundle` alone still exits 2 with its existing message, and no sigstore option at all is a no-op.
- [ ] Tests are platform-neutral.
- [ ] Review acceptance check recorded in the Log as a hand check: the repro command exits 2 naming `--sigstore-bundle`, `sg.out` is not created, and the server logs no request.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: the command exits 2 naming `--sigstore-bundle`, `sg.out` is not created, and the server logs no request.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 3721fc72 605337b5 c1cf21f9 116dc05a a0b49a29.
