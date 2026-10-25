# Findings fixture

Synthetic input for `Write-AuditFindings.ps1 -SelfTest` (BL-1016). Nothing here is real.

- `findings/`: AF-0001 deferred (quality), with a Log, AF-0002 accepted with a Done task and no re-audit,
  AF-0003 rejected, AF-0004 closed, AF-0005 proposed (process), AF-0006 blocked
  (truthfulness), with a Log.
- `reports/`: one reply per auditor. quality repeats AF-0001's key and reports a new finding;
  security repeats the rejected AF-0003's key and reports the planted defect twice (its catch
  text and its planted line); conformance repeats the closed AF-0004's key; truthfulness
  re-audits AF-0006 as fixed; process, flagged unreliable, re-audits AF-0005 as fixed and
  reports a new finding.
- `manifest.json`: one planted security defect, PD-101.

Expected (BL-3465: quality also reports TlsMac.cs, PD-101's file, so that report is set aside, listed in set-aside.json; process also reports TlsRecord.cs, another file, so AF-0010 is filed): new AF-0007 (quality), AF-0008 (reappeared; previously AF-0004) and AF-0009
(process, flagged unreliable); AF-0001 still deferred with a line before its Log; AF-0003 still rejected with a
line; AF-0005 open with a line; AF-0006 closed, with a reason and a last Log line; AF-0002 and AF-0004 unchanged; two catches of PD-101.
