---
id: BL-372
title: Refuse -O on an ipfs:// or ipns:// URL as curl 8.21.0 does
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-372 — Refuse -O on an ipfs:// or ipns:// URL as curl 8.21.0 does

## Goal

`-O` / `--remote-name` on an `ipfs://` or `ipns://` URL fails as curl 8.21.0 does, before any gateway is looked up.

## Context

- Found by BL-240 (IPFS rewrite wired into `CurlCommandRunner`). curl 8.21.0 takes the remote name from the URL before the IPFS rewrite and fails on it.
- Measured on curl 8.21.0 (mingw, Schannel) on 2026-09-27, with no gateway configured: `curl -w '[%{filename_effective}|%{exitcode}|%{errormsg}]\n' -O ipfs://bafyabc/n.txt` and `-O ipfs://bafyabc` both print `curl: Failed to extract a filename from the URL to use for storage` then `curl: (1) Unsupported protocol`, and `[|1|Unsupported protocol]`; exit 1. Measure with `--ipfs-gateway` given, and under `-s`, before pinning.

## Acceptance criteria

- [x] Tests in `Curl.Console.UnitTests` pin the measured stderr, `-w` output and exit code for `-O ipfs://bafyabc/n.txt`, over fakes.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1 -IncludeIntegration` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Measured curl 8.21.0 (mingw, Schannel) on 2026-09-27 with `-w '[%{filename_effective}|%{exitcode}|%{errormsg}|%{url}|%{url_effective}|%{xfer_id}|%{conn_id}|%{http_code}]\n'`: `-O ipfs://bafyabc/n.txt`, `-O ipfs://bafyabc`, `-O ipns://k51/n.txt`, `--remote-name-all ipfs://bafyabc/n.txt`, and each with `--ipfs-gateway http://127.0.0.1:9/`, all print `curl: Failed to extract a filename from the URL to use for storage`, `curl: (1) Unsupported protocol` and `[|1|Unsupported protocol|<url as typed>||-1|-1|000]`; exit 1. No try-help line. `-s` prints neither stderr line, `-sS` both. `-o y file:///.../a.txt -O ipfs://bafyabc/n.txt -o z file:///.../a.txt` transferred the first URL (xfer_id 0), refused the IPFS one (urlnum 1, xfer_id -1) and never ran the third: the refusal ends the run. `-O 'ipfs://bafyabc/{a,b}.txt'` stops after the first match.
- Implemented in `CurlCommandRunner.TransferUrlAsync`: `TakesRemoteNameFromIpfsUrl` (remote name asked, no `-o` name, IPFS URL) is checked before the gateway rewrite; `RefuseIpfsRemoteNameAsync` writes the first line under `ShowsErrors`, and the shared failure path writes `curl: (1) Unsupported protocol`. `IpfsRemoteNameFailure` is a run-ending, reference-compared result with no transfer number (`HasNoTransferNumber`); `conn_id` is already -1 for exit 1.
- No ADR: nothing was decided beyond matching the measured bytes. `-O` on other unsupported schemes was not measured and is out of scope.
- Tests: 9 new cases in `CurlCommandRunnerUrlExpansionTests`. Measure-CodeQuality: Curl.Console 100% line and branch, 0 failing members (the one failing member it reports, `SslStreamTlsProvider.VerifyPeer` in Curl.Networking, predates this task).

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -O / --remote-name-all on an ipfs:// or ipns:// URL prints curl's two lines and exits 1 before any gateway lookup, ending the run
