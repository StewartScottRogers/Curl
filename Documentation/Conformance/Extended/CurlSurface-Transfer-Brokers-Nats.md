# CurlSurface: transfer options for the broker schemes nats, natss

curl's generic transfer options (`-v`, `-o`, `-O`, `-w`, `--fail`, `--fail-with-body`, `-m`) for `nats` and `natss`,
run through the real curl command line in process against a scripted NATS server on port 4222
(`Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/Brokers/Nats/`: `NatsTransferSurfaceCases` for `nats`,
`NatssTransferSurfaceCases` for `natss` over the harness's TLS route with a self-signed certificate and `-k`, and the
shared checks in `NatsTransferChecks`). The transfer is a subscription to `greet` (URL `<scheme>://localhost:4222/greet`)
that receives one message, `hello`, and ends when the server closes; it writes `hello\n` (ADR-1065). Expected values
come from curl's manual page for curl 8.22.0 (curl.se/docs/manpage.html) and ADR-1065. `--fail` and
`--fail-with-body` concern HTTP response codes, and ADR-1065 maps no NATS server error to exit 22: a `-ERR` after SUB
is exit 8 `NATS server error: <text>` with or without them, and these rows pin that ADR answer. `-m` runs on an
injected clock (`NatsTransferClock`) against a server that sends INFO and never answers curl's handshake. Row `-1` is
`nats`, row `-2` is `natss`.

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| CurlSurface:NatsVerbose-1 | MUST | `nats`: `-v` writes curl 8.22.0's connection line (`* Established connection to localhost (127.0.0.1 port 4222) from ...`) on standard error, and standard output holds the body alone (manual, "-v"). | NatsTransferSurfaceCases.Verbose_Answered_WritesTheConnectionLineOnStandardErrorAndOnlyTheBodyOnStandardOutput |
| CurlSurface:NatsOutput-1 | MUST | `nats`: `-o file` in a temporary directory writes the body to the file and nothing to standard output (manual, "-o"). | NatsTransferSurfaceCases.Output_Answered_WritesTheBodyToTheFileAndNothingToStandardOutput |
| CurlSurface:NatsRemoteName-1 | MUST | `nats`: `-O` (under `--output-dir`) writes the body to a file named after the URL path's last segment, `greet`, and nothing to standard output (manual, "-O"). | NatsTransferSurfaceCases.RemoteName_Answered_WritesTheBodyToTheFileNamedAfterTheSubject |
| CurlSurface:NatsWriteOut-1 | MUST | `nats`: `-w` writes `%{exitcode}` 0, an empty `%{errormsg}`, `%{url}` and `%{url_effective}` as given, `%{scheme}` `nats`, `%{remote_ip}` 127.0.0.1, `%{remote_port}` 4222, `%{size_download}` the bytes written and `%{time_total}` 0.000000 on an injected clock that never moves, after the body (manual, "-w"). | NatsTransferSurfaceCases.WriteOut_Answered_WritesEveryVariableAfterTheBody |
| CurlSurface:NatsFail-1 | MUST | `nats`: `--fail` against a `-ERR` after SUB ends with exit 8, `curl: (8) NATS server error: <text>` on standard error and `%{exitcode}`/`%{errormsg}` agreeing; no NATS error is exit 22 (ADR-1065; manual, "--fail", "EXIT CODES" 8). | NatsTransferSurfaceCases.Fail_ServerErr_EndsWithExit8AndTheServersText |
| CurlSurface:NatsFailWithBody-1 | MUST | `nats`: `--fail-with-body` against that `-ERR` ends the same way, exit 8 (ADR-1065; manual, "--fail-with-body"). | NatsTransferSurfaceCases.FailWithBody_ServerErr_EndsWithExit8AndTheServersText |
| CurlSurface:NatsMaxTime-1 | MUST | `nats`: `-m 100` against a server that never answers curl's handshake ends with exit 28 and `curl: (28) ...` once the injected clock reaches the deadline, with nothing on standard output (manual, "-m", "EXIT CODES" 28). | NatsTransferSurfaceCases.MaxTime_ServerNeverAnswersTheHandshake_EndsWithExit28AtTheDeadlineOnTheInjectedClock |
| CurlSurface:NatsVerbose-2 | MUST | `natss`: `-v` writes curl 8.22.0's connection line (`* Established connection to localhost (127.0.0.1 port 4222) from ...`) and the TLS backend's lines (`* schannel: ` on Windows, `* SSL connection using ` elsewhere) on standard error, and standard output holds the body alone (manual, "-v"). | NatssTransferSurfaceCases.Verbose_Answered_WritesConnectionAndTlsLinesOnStandardErrorAndOnlyTheBodyOnStandardOutput |
| CurlSurface:NatsOutput-2 | MUST | `natss`: `-o file` in a temporary directory writes the body to the file and nothing to standard output (manual, "-o"). | NatssTransferSurfaceCases.Output_Answered_WritesTheBodyToTheFileAndNothingToStandardOutput |
| CurlSurface:NatsRemoteName-2 | MUST | `natss`: `-O` (under `--output-dir`) writes the body to a file named after the URL path's last segment, `greet`, and nothing to standard output (manual, "-O"). | NatssTransferSurfaceCases.RemoteName_Answered_WritesTheBodyToTheFileNamedAfterTheSubject |
| CurlSurface:NatsWriteOut-2 | MUST | `natss`: `-w` writes `%{exitcode}` 0, an empty `%{errormsg}`, `%{url}` and `%{url_effective}` as given, `%{scheme}` `natss`, `%{remote_ip}` 127.0.0.1, `%{remote_port}` 4222, `%{size_download}` the bytes written and `%{time_total}` 0.000000 on an injected clock that never moves, after the body (manual, "-w"). | NatssTransferSurfaceCases.WriteOut_Answered_WritesEveryVariableAfterTheBody |
| CurlSurface:NatsFail-2 | MUST | `natss`: `--fail` against a `-ERR` after SUB ends with exit 8, `curl: (8) NATS server error: <text>` on standard error and `%{exitcode}`/`%{errormsg}` agreeing; no NATS error is exit 22 (ADR-1065; manual, "--fail", "EXIT CODES" 8). | NatssTransferSurfaceCases.Fail_ServerErr_EndsWithExit8AndTheServersText |
| CurlSurface:NatsFailWithBody-2 | MUST | `natss`: `--fail-with-body` against that `-ERR` ends the same way, exit 8 (ADR-1065; manual, "--fail-with-body"). | NatssTransferSurfaceCases.FailWithBody_ServerErr_EndsWithExit8AndTheServersText |
| CurlSurface:NatsMaxTime-2 | MUST | `natss`: `-m 100` against a server that never answers curl's handshake ends with exit 28 and `curl: (28) ...` once the injected clock reaches the deadline, with nothing on standard output (manual, "-m", "EXIT CODES" 28). | NatssTransferSurfaceCases.MaxTime_ServerNeverAnswersTheHandshake_EndsWithExit28AtTheDeadlineOnTheInjectedClock |
