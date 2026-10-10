---
id: BL-1988
title: Close GF-0058: -F to an smtp:// or imap:// URL is not sent as a MIME message: SMTP sends VRFY and IMAP sends LIST
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-1988 — Close GF-0058: -F to an smtp:// or imap:// URL is not sent as a MIME message: SMTP sends VRFY and IMAP sends LIST

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0058 (-F to an smtp:// or imap:// URL is not sent as a MIME message: SMTP sends VRFY and IMAP sends LIST), so a later gap analysis measures each of `behaviour:test1187`, `behaviour:test646`, `behaviour:test648`, `behaviour:test649`, `behaviour:test647` as `match`.

## Context

- Finding: GF-0058, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test1187`, `behaviour:test646`, `behaviour:test648`, `behaviour:test649`, `behaviour:test647`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test646 (smtp with -F parts and --mail-from/--mail-rcpt): '<verify><protocol> differs at byte 10 (line 2): expected "MAIL FROM:<sender@example.com>\r\n", got "VRFY recipient@example.com\r\n"'; 648 and 1187 are the same. test649 (-F ...;encoder=7bit with an 8-bit file): expected 'EHLO 649', got the end. test647 (imap APPEND from -F): expected 'A003 APPEND 647 (\\Seen) {940}', got 'A003 LIST "647" *'. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 646,649,647

Suggestion, copied from the finding:

In Curl.Console's mail mapping (MailRequestOptionsMapping), treat -F parts on an smtp:// or imap:// URL as the upload, as curl 8.21.0 does. Build the MIME message (multipart/mixed with nested multipart/alternative, ;headers=, ;encoder=) with the same multipart writer HTTP uses, but with mail headers and no Content-Length. Send it through SmtpMailTransaction or ImapAppend. A part whose encoder cannot carry its bytes (7bit with 8-bit data) fails before EHLO, with curl's exit code.

## Acceptance criteria

- [ ] `behaviour:test1187`: Curl answers what curl 8.21.0 answers, `upstream test1187 passes`, so the item measures `match`.
- [ ] `behaviour:test646`: Curl answers what curl 8.21.0 answers, `upstream test646 passes`, so the item measures `match`.
- [ ] `behaviour:test648`: Curl answers what curl 8.21.0 answers, `upstream test648 passes`, so the item measures `match`.
- [ ] `behaviour:test649`: Curl answers what curl 8.21.0 answers, `upstream test649 passes`, so the item measures `match`.
- [ ] `behaviour:test647`: Curl answers what curl 8.21.0 answers, `upstream test647 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-10: Created.
