---
id: BL-5899
title: Fix fitness finding EXT-124: xmpp / xmpps silently replace invalid UTF-8 in the -T / --data-binary message body with U+FFFD and exit 0
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-5915]
touches: [Curl.Extended.Protocol.Xmpp.UnitLibrary/Client/XmppRequests.cs, Curl.Extended.Protocol.Xmpp.UnitLibrary/Client/XmppClientMessages.cs, Curl.Extended.Protocol.Xmpp.UnitTests/XmppUploadTests.cs, Documentation/Conformance/Extended/Xmpp.md]
requirement: none
created: 2026-11-24
completed:
---
# BL-5899 — Fix fitness finding EXT-124: xmpp / xmpps silently replace invalid UTF-8 in the -T / --data-binary message body with U+FFFD and exit 0

## Goal

EXT-124 from the outside fitness review of 2026-11-24 13:58 no longer reproduces: xmpp / xmpps silently replace invalid UTF-8 in the -T / --data-binary message body with U+FFFD and exit 0. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T1358` (commit `1e5d591`, the `preview` release binaries). That review was a deeper second pass of the same commit as `Curl-Fitness-Review-2026-11-24T0708`, covering areas the earlier pass did not test.

This is an extension (Part B): it is judged against Curl's own documentation (`curl --extended-help`, `Documentation/Protocols.md`, `Documentation/Conformance/Extended/*.md`, the ADRs), never against C curl.

The reviewer's mock servers were Python, and some findings used real servers in Docker (Dovecot, GreenMail, Samba, Mosquitto, Kafka, Prosody, SeaweedFS, Azurite); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Medium (silent data change) · **Confidence:** Confirmed (3 runs) · **Area:** xmpp, xmpps. Windows build.
- **Setup:** Prosody (`prosody/prosody`, domain `localhost`, STARTTLS with a test CA), users `bob` / `bobpw` and `carol` / `carolpw` (`prosodyctl register`), client port mapped to 38222.
- **Repro:**
  - `printf 'caf\xe9 latin1' > l1.bin`
  - `curl -sS --ssl-no-revoke --cacert ca.pem -u bob:bobpw -T l1.bin xmpp://localhost:38222/carol@localhost -w "[%{exitcode}]\n"`
  - `docker exec <prosody> cat /var/lib/prosody/localhost/offline/carol.list`
- **Expected:** `Documentation/Conformance/Extended/Xmpp.md`: "`-T file` publishes like `-d`: the file's bytes become the message `<body/>`". The port already refuses a body it cannot carry exactly (a control character gives `curl: (43) XMPP stanza holds a character XML 1.0 forbids`). Bytes that are not valid UTF-8 cannot be carried unchanged either, so they should be refused the same way (exit 43).
- **Actual (.NET):** `[0]`; the stored body is `caf\239\191\189 latin1` — `\xe9` became U+FFFD. `\xff\xfe` became two U+FFFD. No warning.
- **Likely location:** `Curl.Extended.Protocol.Xmpp.UnitLibrary/Client/XmppRequests.cs` (~line 49): `Encoding.UTF8.GetString(data.Span)` replaces invalid bytes. Use `new UTF8Encoding(false, throwOnInvalidBytes: true)` and map the exception to exit 43.
- **Acceptance check:** the repro exits 43 with a message naming invalid UTF-8 and nothing is stored for carol; `-d 'café'` (valid UTF-8) still delivers `café`.

## Acceptance criteria

- [ ] the repro exits 43 with a message naming invalid UTF-8 and nothing is stored for carol; `-d 'café'` (valid UTF-8) still delivers `café`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Done and committed with this requeue (`fix(xmpp): refuse a message body that is not valid UTF-8`): `XmppRequests`
  decodes the body strictly and fails with exit 43, `XMPP message body is not valid UTF-8`, before it connects
  (ADR-1347). `XmppUploadTests` pins it for `-T` and post data, and that valid UTF-8 `café` is sent unchanged.
  Reproduced first: the five invalid-byte rows failed on the old code. XMPP tests, the whole fast run and the build are
  green; `Measure-CodeQuality.ps1 -Library Curl.Extended.Protocol.Xmpp.UnitLibrary` reports 0 failing members.
- Measured with the built curl.exe on Windows (ACP 1252): the `-T` Latin-1 repro exits 43 with that message; `-d @file`
  holding UTF-8 `café` goes on to connect; typed `-d 'café'` also exits 43, because a typed argument is bytes in the ANSI
  code page (ADR-0838). So the first criterion's second half does not hold on Windows (it did not before either: the body
  arrived with U+FFFD). It needs the command line, context and handler changed: BL-5915. Once BL-5915 is Done, run the
  first criterion's two commands and tick it. No Prosody was used: the refusal happens before any connection, so nothing
  can be stored.
- `touches` widened to `XmppClientMessages.cs`, `XmppUploadTests.cs` and `Xmpp.md` (the message, the test and the
  document of this change); no other task in Doing named them.
- Also filed BL-5916: CR and CR LF in a body look to be changed to LF by the stanza serialiser.

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 3cabc559 69116afe 3d836d5e.
- 2026-11-24: Doing -> Backlog. Waits on BL-5915: typed -d café on Windows reaches xmpp as ANSI bytes; the invalid-UTF-8 refusal is committed
