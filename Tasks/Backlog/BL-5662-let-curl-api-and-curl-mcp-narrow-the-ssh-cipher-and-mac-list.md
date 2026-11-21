---
id: BL-5662
title: Let Curl.Api and curl-mcp narrow the SSH cipher and MAC lists, as curl --ciphers does for SSH
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api/Options/Protocol/CurlSshOptions.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5662 — Let Curl.Api and curl-mcp narrow the SSH cipher and MAC lists, as curl --ciphers does for SSH

## Goal

A `Curl.Api` user, and the `curl-mcp` `transfer` tool, can give the SSH cipher and MAC lists the console takes for scp and sftp, and the connection offers only those.

## Context

From Surly cookbook bug report `2026-11-20T2225-Curl-bug-report.md` (batch 7 of the 1,000-recipe plan), finding 5. Build tested: Curl `7d167fa10a`, Windows 11; server Surl `9ad60d80c`. Check it still reproduces on the current branch first.

Neither `Curl.Api` nor `curl-mcp` can narrow its own SSH cipher or MAC list. Check first what curl 8.22.0 itself offers for SSH: with libssh2, `--ciphers` is not applied to SSH (libcurl has no `CURLOPT_SSH_CIPHERS`; libssh2 reads `LIBSSH2_METHOD_CRYPT_CS` only through its own API), so the console may have no such option either. If curl has none, record that (no change to the console, `Curl.Api` follows curl) and close the task as matching upstream. If the port's console already narrows SSH algorithms (`Curl.Ssh.UnitLibrary/Negotiation/SshAlgorithmCatalogue.cs`), expose the same in `Curl.Api`'s `CurlSshOptions` and the `transfer` tool.

## Acceptance criteria

- [ ] The Notes record whether curl 8.22.0 (libssh2 build) narrows SSH ciphers or MACs with any option.
- [ ] If it does, `Curl.Api` and the `transfer` tool can set the same lists and the SSH key exchange offers only those, pinned by a test; if it does not, the docs say so and nothing else changes.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-20: Created from Surly bug report 2026-11-20T2225, finding 5.
