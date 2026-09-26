# Curl.Conformance.UnitLibrary

The harness that runs curl's own upstream test cases (`tests/data/test*`) against
Curl, in process, as data-driven MSTest cases. It follows ADR-0013
(`Documentation/Planning/Decisions/ADR-0013-upstream-test-cases-run-as-data-driven-mstest.md`).

It is empty today (BL-148 scaffolded it). What it is to hold, per ADR-0013 decision 2:

- the test-file parser, variable substitution (`%HOSTIP`, `%TESTNUMBER`, `%LOGDIR`, ...)
  and `%if` evaluation;
- the in-memory test servers, reached through `IConnector` and `IDatagramConnector`
  from `Curl.Protocol.Abstractions.UnitLibrary`, which record the bytes they receive;
- the comparison of a case's results against its `<verify>` section.

This library references `Curl.Protocol.Abstractions.UnitLibrary` and no protocol
library. It never opens a socket: every connection goes to an in-memory server. It is
held to the same quality gates as every other `.UnitLibrary` (100% line and branch
coverage, complexity at most 10, CRAP at most 30) and is AOT-compatible like every
production project.
