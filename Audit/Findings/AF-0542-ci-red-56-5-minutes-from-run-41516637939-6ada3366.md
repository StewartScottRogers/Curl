---
id: AF-0542
title: CI red 56.5 minutes from run 41516637939 (6ada3366): MQTT 5 CONNECT and Frame_Malformed conformance tests
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:41516637939:ci-red
reproduction: none
task: BL-4648
tasks: BL-4648
found: 2026-11-02
found-at: fcdba3d03e8b5378439deb78f2055b6f5eccf2bb
scorecard: 2026-11-02_1724.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0542 - CI red 56.5 minutes from run 41516637939 (6ada3366): MQTT 5 CONNECT and Frame_Malformed conformance tests

## Summary

Low finding from the process auditor at `logs/ci-runs.json`: CI red 56.5 minutes from run 41516637939 (6ada3366): MQTT 5 CONNECT and Frame_Malformed conformance tests.

## Evidence

Location: `logs/ci-runs.json`

ciRedSpells: start 2026-11-02T20:34:49Z (13:34 Arizona), end 21:31:19Z (14:31), 56.5 min, runId 41516637939 on 6ada3366a. Failing test: Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst, from the MQTT 5 reference broker added in 6007992eb (12:41). The coordinator filed BL-4543 at 13:28:52 from the failed jobs of the run while it was still going (DarkFactory-20261102-095114.log:110). L6 fixed it and pushed at 13:51:53. Duplicate fix tasks were also filed for the same test: BL-4544 (renumbered from a second BL-4543, L5, done 13:53:27) and BL-4547 (L3 claimed it at 13:47:08 and requeued it at 13:53:58 to wait on BL-4543; L1 reclaimed it and closed it at 14:10:19 as 'fixes confirmed'), so three fix runs covered one test. A second test, Frame_Malformed_IsAnsweredWithConnectionClose501 (run 41519828305), was filed as BL-4548 at 13:47:51 and closed at 14:11:17 as 'Already fixed by e6e55c175'. Run 41521714156 (e6e55c17) failed with an unreadable log. Green came with run 41526034389 at 21:31Z, 40 minutes after the first fix landed.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-02 -LogRoot Z:\repos\Curl.audit\20261102-172448\logs -CiRunsJson Z:\repos\Curl.audit\20261102-172448\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261102-172448\logs\DarkFactory-20261102-095114*.log -Pattern 'BL-454[3478] (ci|claim|REQUEUE|DONE)'
```

- Expected: No red spell over 30 minutes; one fix task per failing test
- Actual: runId 41516637939, 56.5 minutes; BL-4543, BL-4544 and BL-4547 all claimed for the same MQTT 5 CONNECT test

## Re-audits

- 2026-11-03 | 2026-11-03_0025.md | reproduces: yes | Run over this window, the CI red rule is crossed again by an MQTT 5 conformance test failing on Linux and macOS. Spell 41560119555 (bcc7d1f5) lasted 34.63 minutes from 2026-11-03T01:43:57Z, caused by Mqtt5s_UntrustedCertificate_IsRefusedBeforeAnyMqttWithExit60; BL-4596 was filed at 18:46:45 Arizona and done at 19:02:41. The specific CONNECT and Frame_Malformed tests and the duplicate CI-fix tasks (BL-4547 requeue) did not recur, so a narrow reading would be false.

## Log

- 2026-11-02: filed proposed.
- 2026-11-02: proposed -> accepted.
