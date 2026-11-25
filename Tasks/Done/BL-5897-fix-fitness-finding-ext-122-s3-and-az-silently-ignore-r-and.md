---
id: BL-5897
title: Fix fitness finding EXT-122: s3:// and az:// silently ignore -r and -C; -C - appends the whole object to the partial file
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.S3.UnitLibrary/Client/S3RequestMethod.cs, Curl.Extended.Protocol.AzureStorage.UnitLibrary/Client, Curl.Extended.Protocol.AzureStorage.UnitLibrary/CLAUDE.md, Curl.Extended.Protocol.AzureStorage.UnitTests/Client, Documentation/Conformance/Extended/AzureStorage.md]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5897 — Fix fitness finding EXT-122: s3:// and az:// silently ignore -r and -C; -C - appends the whole object to the partial file

## Goal

EXT-122 from the outside fitness review of 2026-11-24 13:58 no longer reproduces: s3:// and az:// silently ignore -r and -C; -C - appends the whole object to the partial file. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T1358` (commit `1e5d591`, the `preview` release binaries). That review was a deeper second pass of the same commit as `Curl-Fitness-Review-2026-11-24T0708`, covering areas the earlier pass did not test.

This is an extension (Part B): it is judged against Curl's own documentation (`curl --extended-help`, `Documentation/Protocols.md`, `Documentation/Conformance/Extended/*.md`, the ADRs), never against C curl.

The reviewer's mock servers were Python, and some findings used real servers in Docker (Dovecot, GreenMail, Samba, Mosquitto, Kafka, Prosody, SeaweedFS, Azurite); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Medium (data corruption with exit 0) · **Confidence:** Confirmed (s3 2 runs, az 2 runs) · **Area:** s3, az storage schemes. Windows build.
- **Setup:**
  - SeaweedFS: `docker run -d --name sw -p 37000:8333 -v <dir>:/cfg chrislusf/seaweedfs server -s3 -s3.config=/cfg/s3.json` with an identity `AKADMIN` / `SECRETadmin123` (Admin). Create the bucket: `curl -X PUT --aws-sigv4 aws:amz:us-east-1:s3 -u AKADMIN:SECRETadmin123 http://127.0.0.1:37000/bkt`.
  - Azurite: `docker run -d --name azurite -p 37010:10000 -v <dir>/pki:/pki mcr.microsoft.com/azure-storage/azurite azurite-blob --blobHost 0.0.0.0 --loose --cert /pki/srv.pem --key /pki/srv.key`, with container `ctr`.
  - `small.bin` is 1000 bytes (byte i = (i*7+3) % 256), uploaded to both with `-T`.
- **Repro (s3):** with `AWS_ENDPOINT_URL=http://127.0.0.1:37000`, `AWS_ACCESS_KEY_ID=AKADMIN`, `AWS_SECRET_ACCESS_KEY=SECRETadmin123`:
  - `curl -sS -r 10-19 s3://bkt/small.bin -o r.bin` → `r.bin` is 1000 bytes, exit 0.
  - `head -c 300 small.bin > part.bin && curl -sS -C - s3://bkt/small.bin -o part.bin` → `part.bin` is 1300 bytes (the 300-byte prefix plus the whole object), exit 0. `-C 300` does the same.
- **Repro (az):** `curl -sS --cacert pki/ca.pem --ssl-no-revoke -u "devstoreaccount1:Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==" -C - az://127.0.0.1:37010/devstoreaccount1/ctr/small.bin -o part.bin` → 1300 bytes, exit 0. With `-r 10-19` it writes 1000 bytes.
- **Expected:** ADR-1249 says a storage scheme must not send a request other than the one it reports; ADR-1105 lists ranges as not implemented. So `-r` / `-C` must either be honoured (a `Range` header on GetObject / Get Blob) or refused with exit 4 before any request, as `-I` and `-X` already are for these schemes.
- **Actual (.NET):** both options are silently ignored; with `-C` the output file is corrupted and the exit code is 0.
- **Likely location:** `Curl.Extended.Protocol.S3.UnitLibrary/Client/S3RequestMethod.cs` and `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Client/AzureStorageRequestMethod.cs` (`RefusalOf`). gs:// and oci:// probably share the gap (not tested).
- **Acceptance check:** after the `head -c 300` setup, `curl -sS -C - s3://bkt/small.bin -o part.bin` either leaves `part.bin` byte-identical to `small.bin` (1000 bytes) or exits 4 naming the unsupported option; `curl -sS -r 10-19 s3://bkt/small.bin` writes exactly 10 bytes or exits 4. The same for az.

## Acceptance criteria

- [x] after the `head -c 300` setup, `curl -sS -C - s3://bkt/small.bin -o part.bin` either leaves `part.bin` byte-identical to `small.bin` (1000 bytes) or exits 4 naming the unsupported option; `curl -sS -r 10-19 s3://bkt/small.bin` writes exactly 10 bytes or exits 4. The same for az.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- The `s3://` half no longer reproduced: BL-5873 (after the reviewed commit) sends a signed `Range` header for `-r`
  and `-C` on downloads. Checked with the built binary against a recorded loopback server: `-C -` onto 300 bytes sent
  `range: bytes=300-` and left 1000 bytes; `-r 10-19` sent `range: bytes=10-19` and wrote 10 bytes.
  `S3RangeDownloadTests` already pins it, so no S3 code changed.
- `az://`/`abfs[s]://` now refuse `-r` and `-C` (other than offset 0) with exit 4 (ADR-1347, Claude's decision).
  Built binary: `-C -`, `-C 300` and `-r 10-19` exit 4 and leave the 300-byte partial file untouched.
  The refusal is its own class (`AzureStorageByteRangeRefusal`) because a range is not a request method, so
  `touches` was widened to the Azure `Client` folders, its `CLAUDE.md` and its conformance matrix (no other task in
  Doing named them).
- Fast tests: 115 assemblies passed. `Measure-CodeQuality.ps1` on the Azure Storage library: 0 failing members.
- Follow-ups filed: BL-5912 (gs://), BL-5913 (oci://), BL-5914 (s3 uploads, lists, deletes).

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
- 2026-11-24: Backlog -> Doing. Criteria at claim: ac9571ab 69116afe 3d836d5e.
- 2026-11-24: Doing -> Done. az:// and abfs[s]:// refuse -r and -C with exit 4 and leave the partial file alone; s3:// already honours them (BL-5873)
