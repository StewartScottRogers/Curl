---
id: BL-5866
title: Fix fitness finding EXT-116: --dpop with --resumable-upload sends the DPoP-bound token as Authorization: Bearer with no DPoP proof
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadRequestHeaders.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5866 — Fix fitness finding EXT-116: --dpop with --resumable-upload sends the DPoP-bound token as Authorization: Bearer with no DPoP proof

## Goal

EXT-116 from the outside fitness review of 2026-11-24 07:08 no longer reproduces: --dpop with --resumable-upload sends the DPoP-bound token as Authorization: Bearer with no DPoP proof. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T0708` (commit `1e5d591`, the `preview` release binaries).

This is an extension (Part B): it is judged against Curl's own documentation (`curl --extended-help`, `Documentation/Protocols.md`, `Documentation/Conformance/Extended/*.md`, the ADRs), never against C curl.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup, find the task titled with that finding ID. Check that it still reproduces on the current branch first.

Shared setup: **Shared mock for EXT-115, EXT-116, EXT-119, EXT-109, EXT-111** (HTTP, port 27020, logs every request line and header to a file): `OPTIONS /tus` → 204 with tus 1.0.0 headers offering `creation`; `POST /tus` → 201 with `Location: /files/1`; `PATCH /files/1` → 204; `/strong` → 300,000 bytes where `byte[i] = (i*13+5) % 256`, with ETag and Range support; `/plain` → a body with `Cache-Control: max-age=60`. `up.txt` is a 6-byte file. The finding, verbatim:


- **Severity:** Medium (the requested sender-constraint is silently dropped; listed as a blocker because it weakens a security option) · **Confidence:** Confirmed (Windows twice, Linux once) · **Area:** `--dpop` with `--resumable-upload`. Neighbour of the EXT-104 fix.
- **Repro:** with `dpop.pem` an EC P-256 private key:
  - `curl -sS --dpop dpop.pem --oauth2-bearer DTOK --resumable-upload -T up.txt -o NUL http://127.0.0.1:27020/tus`, then check `Authorization` and `DPoP` headers on the server.
  - Control: `curl -sS --dpop dpop.pem --oauth2-bearer DTOK -o NUL http://127.0.0.1:27020/x`
- **Expected:** `--extended-help` (dpop): "signs a DPoP proof JWT for every HTTP request, sends --oauth2-bearer as Authorization: DPoP". The control and `--segments` do this. Every upload request should carry `Authorization: DPoP DTOK` and a `DPoP:` proof, or the combination should be refused.
- **Actual (.NET):** exit 0; OPTIONS, POST and PATCH carry `Authorization: Bearer DTOK` and no `DPoP:` header.
- **Likely location:** `ResumableUploadRequestHeaders.cs` `AuthorizationOf()` hard-codes `"Bearer "`; the resumable hook (`ExtensionTransferHookCatalogue.cs` line 29) runs before `DpopExtension.HookFor` (line 49), so DPoP never sees these requests.
- **Acceptance check:** each of OPTIONS, POST and PATCH carries `Authorization: DPoP DTOK` and one `DPoP:` header.

## Acceptance criteria

- [ ] each of OPTIONS, POST and PATCH carries `Authorization: DPoP DTOK` and one `DPoP:` header.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created from fitness review 2026-11-24T0708.