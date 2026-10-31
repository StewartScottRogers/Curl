---
id: GF-0100
title: The gap tool's in-process curl refuses --ech, so the ECH cases send no request (test4000) and exit 2 instead of 101 (test4001)
area: behaviour
platform: windows
key: behaviour:in-process-runner-refuses-ech-option
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-30_2103
closed:
regression: false
items: [behaviour:test4000, behaviour:test4001]
touches: [Curl.Console, Curl.Console.UnitTests]
task:
tasks: []
---
# GF-0100 - The gap tool's in-process curl refuses --ech, so the ECH cases send no request (test4000) and exit 2 instead of 101 (test4001)

## Summary

Curl differs from upstream curl in behaviour: The gap tool's in-process curl refuses --ech, so the ECH cases send no request (test4000) and exit 2 instead of 101 (test4001).

## Evidence

behaviour:test4000 expected 'upstream test4000 passes', actual '<verify><protocol> differs at byte 0 (line 1): expected "GET /4000 HTTP/1.1\r\n", got the end' (command '--ech grease -k https://%HOSTIP:%HTTPSPORT/4000'; no request reached the server). behaviour:test4001 expected 'upstream test4001 passes', actual '<verify><errorcode>: expected exit code 101, got 2' (command '--ech ecl:... -k https://...'). Exit 2 is the parser's refusal of --ech, which CommandLineOption refuses unless CommandLineOptions.ActsAsEchBuild is set (BL-3571). Both cases list <features>ECH</features>, and the harness counts ECH as present (Curl.Conformance.UnitLibrary/UpstreamCurlVersionLineFeatures.cs, ADR-0520), so it runs them. The test project's ratchet passes both (Curl.Conformance.UnitTests/PassingUpstreamCases.txt lines 1995-1996) because UpstreamConformanceTests.RunCurlAsync composes with parsesAsEchBuild: true. Curl.Console/InProcessCurl.cs CreateDialingRunner, which Gap/Tools/Measure-UpstreamCases.cs calls (lines 238-239), does not pass that flag. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.22.0/tests/data" $env:TEMP/raw.json 4000 4001

## Suggestion

In Curl.Console/InProcessCurl.cs, make CreateDialingRunner (and the IConnector overload of RunAsync) pass parsesAsEchBuild: true to CurlComposition.CreateRunner, as UpstreamConformanceTests.RunCurlAsync does. This keeps the in-process runner in step with the harness's feature list, which names ECH (ADR-0520). Also check the other parse flags RunCurlAsync sets (parsesAsQuicBuild, the honours*Variable flags), so the gap tool and the ratchet run curl the same way. Add a Curl.Console.UnitTests test in InProcessCurlTests.cs: '--ech grease' through InProcessCurl.RunAsync is accepted and makes its request. test4001 also needs the gap tool's HTTPS server to speak TLS 1.3 (UpstreamConformanceTests.NeedsTls13ServerCases, BL-1949); Windows SslStream serves TLS 1.3. Explained by ADR-0918: the product curl refuses --ech as every reference build does. Only the conformance runners read the command line as an ECH build, and the gap tool's runner was never switched on.

## Measurements

- 2026-10-30_2103: 2 of 2 items are gaps.

## Log

- 2026-10-30_2103: Opened by gap-behaviour.
