---
id: BL-5286
title: Turn the smb and smbs protocols on
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/EngineComposition.cs, Curl.Engine.UnitLibrary/DisabledProtocolHandler.cs, Curl.Cli.UnitLibrary/CommandLineProtocolSet.cs, Curl.Cli.UnitLibrary/CurlVersionText.cs, Curl.Engine.UnitTests/SmbEnabledTests.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5286 — Turn the smb and smbs protocols on

## Goal

The port answers `smb://` and `smbs://` URLs through its own `Curl.Original.Protocol.Smb.UnitLibrary`, as upstream curl 8.21.0 built with SMB does, instead of exiting 1 with `Protocol "smb" is disabled`, and `curl -V` lists `smb` and `smbs`.

## Context

Filed from Surl's Phase 7 measurement of the port (Stewart approved filing on the port's board, 2026-11-16): Surl `Documentation/Conformance/CurlPort/Port-Board-Handoff.md`, entry 9 (github.com/StewartScottRogers/Surl). The port was measured at commit `b42c319ba940f66452b2b6f3e9910d46825b08fe` (port `curl.exe` SHA-256 `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7`), on Windows x64, 2026-11-05, beside upstream curl 8.21.0, the oracle: where the two differ against the same Surl server, the port is the one in the wrong.

- **Finding:** Surl `Documentation/Conformance/CurlPort/Smb.md` (one cause, 24 rows).
- **Surl tests (24):** every `CurlPortBesideUpstreamCurlOverSmbTests` case, e.g. `Download_ServedFile_WritesItsBytes[files/hello.txt]`, `Upload_UploadsOn_StoresTheBytes[up.txt, files/up.txt]`, `Smbs_SelfSignedCertificateNotTrusted_Exits60` (each row and its expected exit code in `Smb.md`); Surl commit `4e626fa87f39c6c5d141a3bac7b5f9353ab7338c`.
- **Command line, e.g.:** `curl -sS -m 20 -u alice:secret smb://127.0.0.1:<port>/files/hello.txt`.
- **Upstream curl 8.21.0 (correct):** static OpenSSL build, SHA-256 `589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648` (built with SMB; the Schannel reference build has none): logs in with NTLMv1, downloads and uploads: exit 0 writing `hello smb`; 67 for a wrong login; 78 for a missing share or file; 9, 25 and 3 where the rows say; 60 for an untrusted `smbs` certificate.
- **Port (defect):** exit 1, `curl: (1) Protocol "smb" is disabled` (`"smbs"`) in every row, without connecting. At `b42c319`, `Curl.Engine.UnitLibrary/EngineComposition.cs` registers `smb` and `smbs` with a `DisabledProtocolHandler`.
- **What decides:** curl 8.21.0 `docs/libcurl/opts/CURLOPT_PROTOCOLS_STR.md` (`SMB`, `SMBS`), `docs/URL-SYNTAX.md` ("SMB"); the port's own rule that nothing is left out (CLAUDE.md, "Decisions").
- **Where to start:** `Curl.Engine.UnitLibrary/EngineComposition.cs`, `DisabledProtocolHandler.cs`, `Curl.Cli.UnitLibrary/CommandLineProtocolSet.cs`, `CurlVersionText.cs`.

## Acceptance criteria

- [ ] `curl -V` lists `smb` and `smbs` in `Protocols:`.
- [ ] `curl -u alice:secret smb://<host>/files/hello.txt` against an SMB server downloads the file (exit 0), and a wrong password exits 67 (`CURLE_LOGIN_DENIED`), shown by port tests.
- [ ] A missing share or file exits 78 (`CURLE_REMOTE_FILE_NOT_FOUND`) and `-T` uploads a file, shown by port tests.

## Notes

The port disabled SMB on purpose to match the reference builds, which lack it: BL-3887 and BL-3888 (CGR-043) and ADR-0835 (`smb-and-smbs-are-disabled-as-in-the-reference-builds`). Doing this task means an ADR superseding ADR-0835, decided under Stewart's delegation; the `--proto =smb` refusal BL-3888 pinned changes with it, and the upstream SMB cases and `Cgr043*` tests that pin the disabled answer move to the enabled one.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 720ff3dd 0a85c8cf b598bc23.
