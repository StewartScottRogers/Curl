---
id: BL-5876
title: Make -w %header{name} print response headers over oci://, as -D already shows them
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.OciRegistry.UnitLibrary/Http/OciHttpHeadReporter.cs, Curl.Extended.Protocol.OciRegistry.UnitLibrary/Client/TransferEventsConnector.cs, Curl.Extended.Protocol.OciRegistry.UnitTests/Http/OciHttpHeadReporter]
requirement: none
created: 2026-11-24
completed:
---
# BL-5876 — Make -w %header{name} print response headers over oci://, as -D already shows them

## Goal

`-w "%header{name}"` (and `%{header_json}`) over `oci://` prints the named header of the final response, e.g. `Docker-Content-Digest`, as it does over `https://` and as `-D -` already shows for `oci://`.

## Context

From Surly cookbook bug report `2026-11-24T1330-Curl-bug-report.md` (batch 14), finding 5 (Low). Build tested: Curl `2145a49eae`, Surl v0.1.5 `d64faa4bb`, Windows 11. Re-run and confirmed the same day.

Repro: `surl --directory store --cert server.pem --key server.key --user build:b1ld --allow-uploads oci://127.0.0.1:18500/`; push a config blob, a layer and a manifest tagged `demo/app:1.0` with `-T`. `curl -sS --cacert server.pem -u build:b1ld -o /dev/null -w "%header{docker-content-digest}" oci://127.0.0.1:18500/demo/app:1.0` prints nothing (exit 0), while `-D -` shows `Docker-Content-Digest: sha256:5367...0e66`. Start in `Curl.Extended.Protocol.OciRegistry.UnitLibrary/Http/OciHttpHeadReporter.cs` (it writes `-D` output and builds the `TransferReport`) and `Client/TransferEventsConnector.cs`: the report probably carries no response header list for the write-out. Compare with how the HTTP path and the S3 client (`S3HttpHeadReporter`) hand headers to `%header{}`; if S3 has the same gap, file a follow-up. Headers of the last response only, as curl does after a redirect or token round trip.

## Acceptance criteria

- [ ] `-w "%header{docker-content-digest}"` over `oci://` prints the digest from the final response, pinned by a unit test with a test-double registry.
- [ ] `%{header_json}` over `oci://` lists the final response's headers, pinned by a unit test.
- [ ] After a bearer-token round trip, the headers are the final response's, not the 401's, pinned by a unit test.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created from Surly bug report 2026-11-24T1330, finding 5.
- 2026-11-24: Backlog -> Doing. Criteria at claim: e04e10b4 1a7baa4e f19d9e46 3d836d5e.
