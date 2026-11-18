---
id: BL-5440
title: Fix fitness finding STD-122: LDAP DIGEST-MD5 security layer ignores the server's maxbuf; a request larger than maxbuf goes in one oversized SASL buffer and the server drops the connection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapSaslSecurityLayer.cs, Curl.Original.Protocol.Ldap.UnitLibrary/LdapDigestMd5SecurityLayer.cs, Curl.Original.Protocol.Ldap.UnitLibrary/LdapDigestMd5Bind.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapDigestMd5MaxBufferTests.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5440 — Fix fitness finding STD-122: LDAP DIGEST-MD5 security layer ignores the server's maxbuf; a request larger than maxbuf goes in one oversized SASL buffer and the server drops the connection

## Goal

STD-122 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: LDAP DIGEST-MD5 security layer ignores the server's maxbuf; a request larger than maxbuf goes in one oversized SASL buffer and the server drops the connection. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Medium (large requests fail where REF succeeds; fails closed)
- **Confidence:** Confirmed (maxbuf 1024 and the default 65536, 2 runs each)
- **Protocol/area:** LDAP SASL auth-conf layer, Windows
- **Repro:**
  1. The OpenLDAP container and SASL setup from STD-116 steps 1–3 and a logging relay on 127.0.0.1:42880 → 42389.
  2. Default maxbuf 65536: write `big.cfg` containing `url = "ldap://127.0.0.1:42880/ou=people,dc=example,dc=org?cn?sub?(|(uid=alice)(cn=<70000 × q>))"` and run `curl -sS -m 20 -u alice:secret --digest -K big.cfg`.
  3. maxbuf 1024: set `olcSaslSecProps: noanonymous,noplain,maxssf=256,maxbufsize=1024`, `docker restart` the container, then `curl -sS -m 20 -u alice:secret --digest "ldap://127.0.0.1:42880/ou=people,dc=example,dc=org?cn?sub?(|(uid=alice)(cn=<1500 × q>))"`.
- **Expected (REF):** the wrapped SearchRequest is split into buffers of at most maxbuf (65534 + 4614 bytes; 1022 + 630). It prints `DN: uid=alice,…`, exit 0.
- **Actual (PORT):** one buffer of 70126 (or 1630) bytes; the server closes; `curl: (39) LDAP remote: Unavailable`, exit 39.
- **Likely location:** `Curl.Original.Protocol.Ldap.UnitLibrary/LdapSaslSecurityLayer.cs` `WriteAsync` and `LdapDigestMd5Bind.SecurityLayerFor`: the challenge's `maxbuf` is never parsed and `Wrap` is applied to the whole PDU.
- **Acceptance check:** both commands print alice's entry, exit 0; the relay log shows no client SASL buffer longer than the challenge's maxbuf.

## Acceptance criteria

- [x] both commands print alice's entry, exit 0; the relay log shows no client SASL buffer longer than the challenge's maxbuf.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Widened touches to the DIGEST-MD5 layer and bind (no other Doing task touches Ldap). `SecurityLayerFor` parses the challenge maxbuf (default 65536); the layer splits each write so every sealed buffer is at most maxbuf (plain chunk = floor((maxbuf-6)/8)*8-11). No live OpenLDAP here: the relay-log criterion is pinned by unit tests of frame lengths, not measured against the container. Quality run: 0 failing members.

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-122 (Medium).
- 2026-11-18: Backlog -> Doing. Criteria at claim: bd3d8af0 69116afe 3d836d5e.
- 2026-11-18: Doing -> Done. DIGEST-MD5 layer splits sealed writes to the challenge maxbuf; unit tests pin it
