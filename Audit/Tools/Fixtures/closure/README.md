# Closure fixture

Synthetic input for the ADR-0422 checks in `Write-AuditFindings.ps1 -SelfTest`. Nothing here is
real. The quality auditor is flagged unreliable throughout; the self-test stubs the runner's
mechanical rerun: AF-0010 killed, AF-0011 survived.

- `findings/`: AF-0010 and AF-0011 accepted with a mutation reproduction; AF-0012 accepted in the
  format from before ADR-0422 (no reproduction, tasks, duplicate-of or closed-how), whose last
  re-audit said "reproduces: no"; AF-0013 accepted, its last re-audit "yes"; AF-0014 and AF-0015
  accepted with the same key.
- `reports/`: quality re-audits AF-0010 to AF-0013 as fixed, reports the AF-0014 mutant in other
  words and a new mutant in `C.cs`; security re-audits AF-0011 as fixed.
  Quality also re-audits AF-0017 as still reproducing and AF-0014 with `"reproduces": null`;
  security re-audits AF-0018, citing `Cited.cs`.
- `findings/` also: AF-0017 in `Curl.Net.UnitTests`, which references `Curl.Net.UnitLibrary`,
  which references `Curl.Dep.UnitLibrary`; AF-0018 in `Curl.Far.UnitTests`.
- `manifest.json`: PD-103 in `Curl.Dep.UnitLibrary/Reader.cs` and PD-104 in
  `Curl.Other.UnitLibrary/Cited.cs`, both security plants.
- `tree/`: the audited files the mutation sites are in, for member names, and the project files
  whose references make AF-0017 depend on PD-103's project.

Expected: AF-0015 closed as a duplicate of AF-0014, whose tasks become BL-6, BL-7; AF-0010 closed
mechanical; AF-0011 open (the mutant survived), with a security line marked "(re-audited by security)";
AF-0012 closed consecutive, by 2026-10-07_0844.md and this audit, with closed-how inserted;
AF-0013 open (its previous re-audit said "yes"); AF-0014 gains a "still reported" line and a "not re-audited" line; AF-0017 and AF-0018 gain a
"not re-audited | overlaps planted defect" line (PD-103, PD-104) and stay open; AF-0019 new, with
the mechanical key
`quality:Curl.Net.UnitLibrary/C.cs:Check-eq:surviving-mutant`.