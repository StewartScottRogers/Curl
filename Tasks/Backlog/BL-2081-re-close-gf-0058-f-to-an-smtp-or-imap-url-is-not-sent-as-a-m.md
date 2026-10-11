---
id: BL-2081
title: Re-close GF-0058: -F to an smtp:// or imap:// URL is not sent as a MIME message: SMTP sends VRFY and IMAP sends LIST
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests]
lane: no
requirement: none
created: 2026-10-11
completed:
---
# BL-2081 — Re-close GF-0058: -F to an smtp:// or imap:// URL is not sent as a MIME message: SMTP sends VRFY and IMAP sends LIST

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0058 (-F to an smtp:// or imap:// URL is not sent as a MIME message: SMTP sends VRFY and IMAP sends LIST), so a later gap analysis measures each of `behaviour:test1187`, `behaviour:test646`, `behaviour:test648`, `behaviour:test649`, `behaviour:test647` as `match`.

## Context

This is a Re-close task: every earlier task for GF-0058 ([BL-1988]) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.

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

- 2026-10-11, lane 3 (interactive only): the gap could not be reproduced from a lane.
  BL-1988's fix (192ec6f80) reached `master` and `gap` before gap run 2026-10-11_0936.
  Measured with `Record-CurlExchange.ps1 -Smtp` and this tree's curl
  (`Curl.Console/bin/Debug/net10.0/curl.exe`) on test646's command line (empty-name `-F`
  parts, `=(;type=multipart/alternative`, `;headers=@file`, `;headers=<file`, three `-H`):
  EHLO 646, MAIL FROM:<sender@example.com> SIZE=1033, RCPT TO, DATA with the
  multipart/mixed message, exit 0 - no VRFY. So the measured VRFY (and IMAP LIST for
  test647) depends on how the gap harness invokes Curl or which binary it runs, as for
  GF-0057/BL-2080 and GF-0051/BL-2079. The gap office's reproduction tool
  (Measure-UpstreamCases.cs) and its upstream data are refused to lanes by the audit guard.
- Remove `lane: no` once an interactive session has run Measure-UpstreamCases.cs for
  cases 646,647,649 on the current tree, captured test646's command line as the harness
  passes it with its protocol transcript, and written under Notes why Curl drops the `-F`
  parts there (argument quoting, the binary used, or a difference in the case), so a lane
  can fix it without the gap office's paths. The harness investigation BL-2079 and
  BL-2080 wait on likely answers this one too.

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
- 2026-10-11: Doing -> Backlog. requeue as interactive only; this tree sends test646's MIME message (MAIL FROM, DATA, no VRFY), so GF-0058 differs only under the gap harness, whose paths lanes cannot read
