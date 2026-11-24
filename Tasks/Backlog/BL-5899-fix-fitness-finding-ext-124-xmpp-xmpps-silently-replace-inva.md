---
id: BL-5899
title: Fix fitness finding EXT-124: xmpp / xmpps silently replace invalid UTF-8 in the -T / --data-binary message body with U+FFFD and exit 0
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Xmpp.UnitLibrary/Client/XmppRequests.cs]
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
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
