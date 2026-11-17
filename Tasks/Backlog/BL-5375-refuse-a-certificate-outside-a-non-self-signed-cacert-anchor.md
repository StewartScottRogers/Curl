---
id: BL-5375
title: Refuse a certificate outside a non-self-signed --cacert anchor's name constraints on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs, Curl.Networking.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-5375 — Refuse a certificate outside a non-self-signed --cacert anchor's name constraints on Windows

## Goal

On Windows, `https://` verification with a `--cacert` file whose anchor is a non-self-signed (intermediate) CA refuses a server certificate outside that CA's permitted name subtrees or inside its excluded ones, as curl does (exit 60); this closes a High security weakening where a name-constrained subordinate CA could impersonate any host.

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding STD-092 (High, Confirmed: permitted and excluded cases, 3 runs; re-reproduced by the orchestrator). Area: HTTPS server certificate verification, `--cacert` holding an intermediate, Windows build. Shares its code path with STD-055 / BL-5385 (accept an intermediate-only `--cacert` with revocation on): fixing BL-5385 would expose this in the default configuration, so the check must hold on both the `--ssl-no-revoke` path and the revocation-on path.

**Repro.**
1. Root CA `root.pem` (self-signed, CA:TRUE).
2. Intermediate `intnc.pem` signed by root: CA:TRUE, keyUsage keyCertSign+cRLSign, critical `nameConstraints = permitted;DNS:example.com`. Leaf `leafnc.pem` signed by intnc: CN=localhost, SAN `DNS:localhost`, EKU serverAuth.
3. Intermediate `intexcl.pem` signed by root with critical `nameConstraints = excluded;DNS:localhost`. Leaf `leafexcl.pem` (SAN `DNS:localhost`, serverAuth) signed by intexcl.
4. Serve `leafnc`+`intnc` on 127.0.0.1:45107 and `leafexcl`+`intexcl` on 127.0.0.1:45131 over TLS, each answering `HTTP/1.1 200 OK` with body `hello` (a PowerShell or C# file-based TLS server).
5. Run `curl -sS -m 15 https://localhost:45107/ --cacert intnc.pem --ssl-no-revoke` and `curl -sS -m 15 https://localhost:45131/ --cacert intexcl.pem --ssl-no-revoke`.

**Expected (REF).** `curl: (60) schannel: error 0x00004000` (permitted-subtree violation) and `curl: (60) schannel: error 0x00008000` (excluded-subtree violation), exit 60, with and without `--ssl-no-revoke`. REF2 and the port's own Linux build also refuse (exit 60).
**Actual (PORT).** Both print `hello`, exit 0. Without `--ssl-no-revoke` the port exits 60 only because of STD-055 ("the certificate chain is incomplete").
**Where to start.** `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs`: `CaCertificateFileAnchorsChain`, `HasOnlyMissingRootStatus`, `HasOnlyMissingRootOrAnchorRevocationStatus`. The Windows chain engine reports only `PartialChain` when the chain ends at a non-self-signed custom anchor and never evaluates that anchor's NameConstraints; the port accepts `PartialChain` when an element is in the file. Evaluate the anchor's (and every CA element's) permitted/excluded subtrees against the leaf's SAN before accepting a partial chain, on both paths, and for `--proxy-cacert`.

## Acceptance criteria

- [ ] New tests in `Curl.Networking.UnitTests` (a new file) prove a partial chain ending at a file anchor whose permitted subtree does not cover the leaf's SAN is refused with `schannel: error 0x00004000`, and one whose excluded subtree covers it is refused with `schannel: error 0x00008000`, both exit 60.
- [ ] A test proves both refusals hold with and without `--ssl-no-revoke`, and on the proxy trust path (`--proxy-cacert`).
- [ ] A test proves an unconstrained intermediate anchor, and a constrained one whose permitted subtree covers the leaf's SAN, are still accepted.
- [ ] Tests are platform-neutral (or OS-conditioned to Windows where the Schannel error text is pinned).
- [ ] Hand check recorded in the Log: the two commands exit 60 with `schannel: error 0x00004000` and `schannel: error 0x00008000` respectively, with and without `--ssl-no-revoke`, and `curl -sS https://localhost:<port>/ --cacert int1.pem --ssl-no-revoke` for an unconstrained intermediate still exits 0. Measure real curl with `Record-CurlExchange.ps1` where possible.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: the two commands exit 60 with `schannel: error 0x00004000` and `schannel: error 0x00008000` respectively, with and without `--ssl-no-revoke`. `curl -sS https://localhost:<port>/ --cacert int1.pem --ssl-no-revoke` for an unconstrained intermediate still exits 0.
- Coordinate with STD-055 / BL-5385 (same functions); whichever lands second keeps the other's tests green.

## Log

- 2026-11-17: Created.
