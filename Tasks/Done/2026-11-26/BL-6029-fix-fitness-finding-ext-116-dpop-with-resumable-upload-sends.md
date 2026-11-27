---
id: BL-6029
title: Fix fitness finding EXT-116: --dpop with --resumable-upload sends the same DPoP proof, made for PUT to the original URL, on every request (PARTLY FIXED)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Dpop.UnitLibrary/DpopTransferHook.cs, Curl.Extension.Dpop.UnitLibrary/CLAUDE.md, Curl.Extension.Dpop.UnitTests/DpopTransferHookTests.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs, Curl.Extension.Composition.UnitTests/DpopWithResumableUploadOrderTests.cs]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6029 — Fix fitness finding EXT-116: --dpop with --resumable-upload sends the same DPoP proof, made for PUT to the original URL, on every request (PARTLY FIXED)

## Goal

EXT-116 from the outside fitness review of 2026-11-26 07:04 no longer reproduces: --dpop with --resumable-upload sends the same DPoP proof, made for PUT to the original URL, on every request (PARTLY FIXED). Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-26T0704` (commit `23b62c2`, the `preview` release binaries).

This is an extension (Part B: extended protocols and extension options): it is judged against Curl's own documentation (`curl --extended-help`, `Documentation/Protocols.md`, `Documentation/Conformance/Extended/*.md`, the ADRs), never against C curl.

The reviewer used Python mocks and real servers in Docker (pinned images listed in the review workspace's TestServers.md); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Medium (the upload's requests carry invalid, replayed proofs; a server that checks DPoP rejects the upload; it weakens a security option) · **Confidence:** Confirmed (Windows 2 runs, Linux 1) · **Area:** `--dpop` with `--resumable-upload`, both platforms.
- **What is fixed:** each request now carries `Authorization: DPoP` and a `DPoP` header (previously plain `Bearer` with no proof).
- **Mock server:** a tus server on `127.0.0.1:27020` that logs every request head (`OPTIONS /tus`, `POST /tus` → `Location: /files/1`, `PATCH /files/1`); a decoder prints each logged DPoP JWT payload. `dpop.pem` is an EC P-256 key (`openssl ecparam -name prime256v1 -genkey -noout -out dpop.pem`).
- **Repro:** `curl -sS --dpop dpop.pem --oauth2-bearer DTOK --resumable-upload -T up.txt -o /dev/null http://127.0.0.1:27020/tus`, then decode the three logged `DPoP` headers. The same happens with a flow token (`--oauth2-client-credentials … --dpop dpop.pem`).
- **Expected:** `--extended-help` (dpop): "signs a DPoP proof JWT for every HTTP request"; RFC 9449 §4.2: `htm` and `htu` match each request and `jti` is unique. So OPTIONS, POST and PATCH each get their own proof: `htm` OPTIONS / POST / PATCH, `htu` `…/tus` / `…/tus` / `…/files/1`, three different `jti`.
- **Actual (.NET):** exit 0; all three requests carry the same proof `{"jti":"xltv4jBMnRItnQlbKkZj7g","htm":"PUT","htu":"http://127.0.0.1:27020/tus","iat":…,"ath":"…"}` — wrong method on all three, wrong URL for PATCH, one `jti` replayed. `--segments 3` and `-L` redirects each get correct per-request proofs.
- **Likely location:** `Curl.Extension.Dpop.UnitLibrary/DpopTransferHook.cs`, `AttemptAsync()`: it builds one proof from `MethodOf(context)` / `TargetUriOf(context.Url)` and passes it to `next(...)` as a fixed header; the resumable-upload hook then issues several requests under that header set.
- **Acceptance check:** the three decoded proofs have `htm` OPTIONS, POST, PATCH; `htu` `…/tus`, `…/tus`, `…/files/1`; and three different `jti` values.

## Acceptance criteria

- [x] the three decoded proofs have `htm` OPTIONS, POST, PATCH; `htu` `…/tus`, `…/tus`, `…/files/1`; and three different `jti` values.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Cause: BL-5866 put the `--dpop` hook outside `--resumable-upload`'s; since ADR-1379 the upload sends each request
  through `next`, so one outer proof reached all of them. Fix (ADR-1426): the catalogue lists ResumableUpload before
  DPoP. A `DPoP-Nonce` retry now repeats one upload request, so `DpopTransferHook` rewinds its upload first.
- `touches` widened to the catalogue, its order test, the Dpop tests and the Dpop `CLAUDE.md`; no task in Doing on
  `origin/work/dark-factory` named them.
- Verified with `curl.exe` against `Record-CurlExchange.ps1` (OPTIONS, POST, PATCH 401 with a nonce, PATCH): `htm`
  and `htu` matched each request, every `jti` differed, and the retried PATCH sent the whole body.
- `dotnet build`: 0 warnings. Fast tests: 58413 passed, 0 failed.
- Quality: `Measure-CodeQuality.ps1 -Library` for both libraries reaches too many test projects to finish before the
  shift deadline under load, and was stopped after 43 projects. Read from its Cobertura output instead:
  `Curl.Extension.Dpop.UnitLibrary` 100% line and branch from its own tests, no method over complexity 10;
  `ExtensionTransferHookCatalogue` (one line moved, no code added) fully covered by `Curl.Extension.Composition.UnitTests`.
- Follow-up: BL-6042 (other hooks that still run inside the DPoP hook's one proof).

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 58530a5d 69116afe 3d836d5e.
- 2026-11-26: Doing -> Done. --dpop with --resumable-upload now signs each upload request with its own DPoP proof
