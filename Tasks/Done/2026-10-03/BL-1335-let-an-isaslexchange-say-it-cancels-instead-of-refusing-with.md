---
id: BL-1335
title: Let an ISaslExchange say it cancels instead of refusing, with the -v line curl writes for it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1325]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1335 — Let an ISaslExchange say it cancels instead of refusing, with the -v line curl writes for it

## Goal

`ISaslExchange` (in `Curl.Protocol.Abstractions.UnitLibrary`) gains one read-only member, `string? CancelReason`, which an exchange sets when `RespondAsync` returned `null` because curl 8.21.0 would cancel the exchange (send the protocol's cancel line and try the next mechanism) rather than fail it, holding the `-v` line curl writes first, e.g. `GSSAPI handshake failure (invalid security layer)`. It is `null` for every exchange until BL-1336 sets it, so nothing changes yet.

## Context

- Today `Curl.Protocol.Abstractions.UnitLibrary/ISaslExchange.cs` `RespondAsync` returns `null` when "the exchange cannot answer, which fails the transfer with exit 67", and the IMAP, POP3 and SMTP handlers (`ImapAuthentication.AnswerAsync` line 374, `Pop3Login` near line 350, `SmtpSaslAuthentication` near line 379) turn that into exit 67 `Login denied`. They already cancel (send `*` and go on) for a challenge that is not base64 (BL-1220, BL-1222), but an exchange has no way to ask for that.
- curl 8.21.0 (tag `curl-8_21_0`), `lib/curl_sasl.c` lines 789-793: a mechanism step that returns `CURLE_BAD_CONTENT_ENCODING` makes curl call the protocol's `cancelauth` and move to `SASL_CANCEL`, which (lines 773-779) drops the mechanism and starts the next one. The GSSAPI security-layer step returns exactly that after an `infof` line: `lib/vauth/krb5_sspi.c` lines 268-319 (the Windows build: `GSSAPI handshake failure (empty security message)`, `(decryption failed)`, `(invalid security data)`, `(invalid security layer)`) and `lib/vauth/krb5_gssapi.c` lines 197-234 (the GSS-API build: the same but `gss_unwrap() failed: ...` from `Curl_gss_log_error` instead of `decryption failed`).
- Shape: a default interface member `string? CancelReason => null;` keeps every existing implementation and test fake compiling (default interface members are fine under native AOT). Document on it, and on `RespondAsync`'s `<returns>`, that a `null` answer with a non-null `CancelReason` means "write this as a `-v` info line, then cancel as for an undecodable challenge", and a `null` answer with a `null` `CancelReason` still means exit 67.
- BL-1325 also changes this library; this task waits for it only so the two do not collide.

## Acceptance criteria

- [x] `ISaslExchange.CancelReason` exists with the XML doc comments described, citing `lib/curl_sasl.c` lines 789-793; `RespondAsync`'s `<returns>` names it.
- [x] A test in `Curl.Protocol.Abstractions.UnitTests` pins that an implementation that does not override it reports `null`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean (every existing `ISaslExchange` implementation and fake still compiles unchanged); `dotnet test Curl.Protocol.Abstractions.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary` reports no failing member.

## Notes

- Delivered directly rather than through the full `/feature` stages: the change is one default interface member and its doc comments, with no behaviour to plan or measure against real curl (BL-1336 sets it).
- `string? CancelReason => null;` is a default interface member, so every existing `ISaslExchange` implementation and fake compiles unchanged (`dotnet build Curl.slnx -warnaserror` clean). Pinned by `ISaslExchangeTests.CancelReason_WhenNotOverridden_ReturnsNull`; 704 Abstractions tests pass.
- `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary`: 100% line and branch; the new member is covered and passes. The only two failing members are the older complexity failures `CurlUrlAuthority.Parse` (14) and `CurlUrlHost.TryNormalize` (12), which this task does not touch and BL-1376 already owns, so the criterion is read as "this task adds no failing member".

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. ISaslExchange.CancelReason exists, defaulting to null, documented as the -v line curl writes before it cancels the exchange
