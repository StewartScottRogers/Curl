---
id: GF-0065
title: VRFY and EXPN replies are written to the output with LF where curl writes the server's CR LF
area: behaviour
key: behaviour:smtp-command-reply-written-without-cr
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-10_0657
closed:
regression: false
items: [behaviour:test924, behaviour:test925, behaviour:test927, behaviour:test950]
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
task:
tasks: []
---
# GF-0065 - VRFY and EXPN replies are written to the output with LF where curl writes the server's CR LF

## Summary

Curl differs from upstream curl in behaviour: VRFY and EXPN replies are written to the output with LF where curl writes the server's CR LF.

## Evidence

Every item expects 'upstream test<N> passes'. test924 (--mail-rcpt smith, <data crlf="yes">): 'the --output file against <reply><data> differs at byte 33 (line 1): expected "553-Ambiguous; Possibilities are:\r\n", got "553-Ambiguous; Possibilities are:\n"'. test925 (252 reply), 927 (-X EXPN) and 950 (--request VRFY) are the same. ADR-0135 point 4 says each line goes out 'with its line end as it arrives'. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 924,925,927,950

## Suggestion

Find which side drops the CR. If Curl.Protocol.Smtp.UnitLibrary's SmtpCommandTransfer writes the reply lines with LF, write each with the CR LF it arrived with. If Curl.Conformance.UnitLibrary's SmtpResponder sends a crlf="yes" <data> part with bare LF, send it with CR LF, as upstream's ftpserver.pl does. Pin it in the matching .UnitTests. Explained by ADR-0135: line ends are passed through as they arrive, so the gap stands until the bytes match.

## Measurements

- 2026-10-10_0657: 4 of 4 items are gaps.

## Log

- 2026-10-10_0657: Opened by gap-behaviour.
