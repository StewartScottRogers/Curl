---
id: BL-5803
title: Fix fitness finding STD-160: Windows: an excluded directoryName name constraint is bypassed when the leaf's subject uses a different string type or letter case
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/CertificateNameConstraints.cs]
requirement: none
created: 2026-11-23
completed:
---
# BL-5803 — Fix fitness finding STD-160: Windows: an excluded directoryName name constraint is bypassed when the leaf's subject uses a different string type or letter case

## Goal

STD-160 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: Windows: an excluded directoryName name constraint is bypassed when the leaf's subject uses a different string type or letter case. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first.

Shared setup: Common PKI used by STD-158 to STD-165 (Windows): `root.pem` is a self-signed CA. `intA.pem` is a CA signed by root (CA:TRUE, keyUsage keyCertSign+cRLSign). Leaves have SAN `DNS:localhost, IP:127.0.0.1, IP:::1` and EKU serverAuth unless stated. Each test server is a Python `ssl` TLS server on 127.0.0.1 and ::1 that sends leaf + issuing intermediate and answers any request with `HTTP/1.1 200 OK` and body `hello <leafname>`. CRLs are served over plain http at `http://127.0.0.1:21080/...`. `<p>` is the port of the leaf's server. In the proxy variants the same TLS server acts as an HTTPS proxy and answers with the body directly. The finding, verbatim:

- **Severity:** High (accepts a chain REF refuses: a constrained CA can issue for an excluded subject)
- **Confidence:** Confirmed (reviewer 2 runs each, 2 PKI generations; orchestrator re-reproduced). Residue of STD-118.
- **Protocol/area:** name constraints with an intermediate `--cacert` anchor, Windows build
- **Repro:**
  1. `ncExclDir.pem`: CA signed by root with critical `nameConstraints = excluded; dirName: O=Evil` (O encoded as PrintableString).
  2. Leaf `xdir_evil_utf8`: subject `O=Evil` (UTF8String), `CN=localhost`. Leaf `xdir_evil_case`: subject `O=EVIL` (PrintableString), `CN=localhost`. Both signed by ncExclDir.
  3. `curl -sS https://localhost:<p>/ --cacert ncExclDir.pem --ssl-no-revoke` against each leaf.
  4. Reverse case: `ncPermDir.pem` with `permitted; dirName: O=Good`, leaves with `O=Good` as UTF8String and `O=GOOD`.
- **Expected (REF):** step 3: `curl: (60) schannel: error 0x00008000`, exit 60, for both leaves. Step 4: exit 0 for both leaves.
- **Actual (.NET):** step 3 prints `hello xdir_evil_utf8` / `hello xdir_evil_case`, exit 0. Step 4 (fails closed) refuses both with `schannel: error 0x00004000`, exit 60. A byte-identical `O=Evil` leaf is correctly refused.
- **Likely location:** `Curl.Networking.UnitLibrary/CertificateNameConstraints.cs` `DirectoryNameMatches` compares RDN encodings byte for byte. Compare attribute type plus a normalised value (string type ignored, case-insensitive, whitespace folded) per RFC 5280 §7.1, as CryptoAPI does.
- **Acceptance check:** step 3 exits 60 with `0x00008000` for both leaves; step 4 exits 0 for both leaves; the byte-identical `O=Evil` leaf still exits 60.

## Acceptance criteria

- [ ] step 3 exits 60 with `0x00008000` for both leaves; step 4 exits 0 for both leaves; the byte-identical `O=Evil` leaf still exits 60.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.