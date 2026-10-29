---
id: BL-3945
title: Refuse an invalid xn-- label in a non-ASCII transfer host as libidn2 does
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-3884]
touches: [Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlPunycode.cs, Curl.Protocol.Abstractions.UnitTests/CurlUrlHostXnLabelTests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3945 — Refuse an invalid xn-- label in a non-ASCII transfer host as libidn2 does

## Goal

A transfer to a non-ASCII host holding an `xn--` label that is not valid punycode fails as curl 8.22.0 does: `curl -sS "http://xn--zz.%FC:20200/a" --connect-to ::127.0.0.1:20200` exits 3 with `curl: (3) URL using bad/illegal format or missing URL`, while `xn--tda.%FC` still sends `Host: xn--tda.xn--tda:20200`.

## Context

BL-3588 (CGR-059, ADR-0942) converts a non-ASCII host label by label in `CurlUrlHost.ToAsciiLabel` and passes an ASCII `xn--` label as written. libidn2 decodes an `xn--` label and refuses one that does not decode (measured with the reference curl 8.22.0 through `Record-CurlExchange.ps1` on 2026-10-26: `xn--zz.%FC` exit 3, `xn--tda.%FC` exit 0). Needs a punycode decoder beside `CurlUrlPunycode.Encode`.

## Acceptance criteria

- [x] `xn--zz.%FC` run through real curl and Curl gives exit 3 and the same stderr; `xn--tda.%FC` gives the same Host line in both; recorded in Notes.
- [x] A focused unit test in `Curl.Protocol.Abstractions.UnitTests/CurlUrlHostXnLabelTests.cs` fails before the change and passes after it.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Measured 2026-10-28 with `Record-CurlExchange.ps1 -Port 20200` and `-sS "http://<label>.%FC:20200/a" --connect-to ::127.0.0.1:20200`, reference curl 8.22.0 (Schannel, Windows): exit 3 with `curl: (3) URL using bad/illegal format or missing URL` for `xn--zz` (number cut short), `xn--` (empty), `xn--a` (decodes to the control U+0080), `xn--tda-` and `xn--abc-` (decode to ASCII only), `xn--9999999999a` (overflow), `xn--en32g` (past U+10FFFF), `xn--ib9b` (lone surrogate U+D800), `xn--t_a` (not a punycode digit); exit 0 with `Host: xn--tda.xn--tda:20200` for `xn--tda` and `xn--TDA`, and `Host: xn--ab-tda.xn--tda:20200` for `xn--ab-tda`.
- Curl after the change, the same way: `xn--zz` and `xn--a` exit 3 with stderr byte-identical to curl's; `xn--tda` exit 0 and sends `Host: xn--tda.xn--tda:20200`.
- Decision (no ADR needed; every rule follows from what was measured): `CurlUrlPunycode.Decode` is RFC 3492's decoder, and an ASCII `xn--` label in a name that is not ASCII is kept only when it decodes, holds at least one code point outside ASCII, and every code point is a scalar value IDNA does not refuse in `EncodeLabel` (no control, no U+FFFD). libidn2's other U-label checks (NFC, IDNA2008 code point classes, the encode-back round trip) are not modelled: no measured case needs them. A name that is all ASCII is still passed as written, as curl does not run IDN on it (`xn--zz.example` stays).
- The unit test fails before the change: before it, `ToAsciiLabel` returned every ASCII label as written, so all nine refused hosts were accepted.
- Measure-CodeQuality.ps1 for Curl.Protocol.Abstractions.UnitLibrary: 100% line and branch, 0 failing members (the first run found a branch in `DigitValue`'s switch patterns, rewritten as two `char.IsAscii*` tests). Fast tests green on Windows; the tests use no paths, OS text or certificates, so they are platform-neutral.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: b1fc2e57 205362f8 779790c0.
- 2026-10-28: Doing -> Done. An xn-- label that libidn2 cannot decode in a non-ASCII transfer host now fails with exit 3 as curl 8.22.0 does
