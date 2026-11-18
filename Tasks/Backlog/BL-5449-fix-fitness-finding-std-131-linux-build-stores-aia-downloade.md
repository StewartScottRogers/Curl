---
id: BL-5449
title: Fix fitness finding STD-131: Linux build stores AIA-downloaded certificates in ~/.dotnet/corefx/cryptography/x509stores/ca and uses them on every later run, so --cacert and the default store then accept servers that omit their intermediate
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.CaCertificates.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5449 — Fix fitness finding STD-131: Linux build stores AIA-downloaded certificates in ~/.dotnet/corefx/cryptography/x509stores/ca and uses them on every later run, so --cacert and the default store then accept servers that omit their intermediate

## Goal

STD-131 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: Linux build stores AIA-downloaded certificates in ~/.dotnet/corefx/cryptography/x509stores/ca and uses them on every later run, so --cacert and the default store then accept servers that omit their intermediate. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low (needs a trusted root; state-dependent acceptance where OpenSSL curl always refuses; the PORT writes server-chosen certificates under `$HOME`, which curl never does)
- **Confidence:** Confirmed (2 runs on a mock, 1 against badssl)
- **Protocol/area:** HTTPS chain building, Linux build
- **Repro (Linux/WSL):** PKI and servers from STD-130, plus `leaf1noaia` (signed by int1, no AIA) served alone on 127.0.0.1:41103.
  1. `rm -f ~/.dotnet/corefx/cryptography/x509stores/ca/*.pfx`
  2. `curl -s --cacert root.pem https://localhost:41103/; echo $?` → 60 (correct)
  3. `curl -s -o /dev/null --capath capath https://localhost:41102/` (downloads int1 via AIA; the store directory now holds `<SHA1(int1)>.pfx`)
  4. `curl -sS --cacert root.pem https://localhost:41103/; echo $?`
  Public: clear the directory, run `curl -s -o /dev/null --capath /etc/ssl/certs https://incomplete-chain.badssl.com/`, then `curl -sS https://incomplete-chain.badssl.com/`.
- **Expected (REF2):** step 4 exits 60 with `unable to get local issuer certificate (20)`; REF2 writes no files. The public default-store run exits 60.
- **Actual (PORT):** step 4 prints `hello`, exit 0; the public run returns the page, exit 0.
- **Likely location:** .NET's Unix `X509Chain` saves downloaded intermediates to the CurrentUser\CA store and reads it on every build. `ServerCertificateVerification.cs` `ReadChainAnchors` / `SystemStoreChainPolicy` and `ServerCertificateVerification.CaCertificates.cs` `RebuildAgainstAnchors` must not use that store on the OpenSSL-flavoured build (e.g. reject a chain element that is neither sent by the peer nor an anchor). Fixing STD-130 stops new writes but not certificates already stored or written by other .NET programs.
- **Acceptance check:** copy any PFX of int1 into `~/.dotnet/corefx/cryptography/x509stores/ca/`, then `curl --cacert root.pem https://localhost:41103/` and `curl --capath capath https://localhost:41103/` both exit 60 with `(20)`.

## Acceptance criteria

- [ ] copy any PFX of int1 into `~/.dotnet/corefx/cryptography/x509stores/ca/`, then `curl --cacert root.pem https://localhost:41103/` and `curl --capath capath https://localhost:41103/` both exit 60 with `(20)`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-131 (Low).