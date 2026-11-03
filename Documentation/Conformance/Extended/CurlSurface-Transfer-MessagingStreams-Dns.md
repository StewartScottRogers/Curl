# CurlSurface: curl's transfer options for the dns scheme

curl's generic transfer options - `-v`, `-o`, `-O`, `-w`, `--fail`, `--fail-with-body` and `-m` - checked for each
messaging and stream scheme through the real command line, in process, against the scheme's canned server
(`TransferSurfaceCaseRun` in `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/MessagingStreams/`, one case
file per scheme family). Expected values come from curl 8.22.0's manual page (curl.se/docs/manpage.html: "-v", "-o",
"-O", "-w", "--fail", "-m", "EXIT CODES") and, where an extension deliberately differs, from its ADR, named in the row.
Every run is on a clock the check drives, so `%{time_total}` is `0.000000` and `-m` ends at the deadline with no real

`dns` (UDP port 53, the default `--dns-transport`) asks `dns://localhost/example.com` for its A record of a datagram
server in the case file, which copies the query's message ID and question and answers `example.com. 300 IN A
192.0.2.1` (NOERROR) or no record (NXDOMAIN). curl 8.22.0 has no `dns` scheme, so the answer is the extension's
dig-like text (ADR-1140), and the expected text names the query's random message ID the server received. Any response
code, NXDOMAIN included, is written and ends with exit 0, as dig does (ADR-1140): DNS maps no server answer to exit 22,
so `--fail` and `--fail-with-body` change nothing. `-m 2` is shorter than the UDP transport's 5-second wait for an
answer, so curl's deadline, not the transport's retries, ends the run.

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| CurlSurface:Verbose-Dns-1 | MUST | `-v` on a `dns` transfer writes `* Established connection to localhost (127.0.0.1 port 53) from ...` to standard error, and only the answer to standard output; exit 0. | DnsTransferSurfaceCases.Verbose_SuccessfulQuery_WritesTheConnectionLineToStandardErrorAndOnlyTheAnswerToStandardOutput |
| CurlSurface:Output-Dns-1 | MUST | `-o file` on a `dns` transfer writes the answer to the file in a temporary directory and nothing to standard output or standard error; exit 0. | DnsTransferSurfaceCases.Output_File_WritesTheAnswerToTheFileAndNothingToStandardOutput |
| CurlSurface:RemoteName-Dns-1 | MUST | `--output-dir dir -O` on a `dns` transfer writes the answer to `dir/example.com`, the URL's last path segment, and nothing to standard output; exit 0. | DnsTransferSurfaceCases.RemoteName_WritesTheAnswerToAFileNamedForTheUrlsLastSegment |
| CurlSurface:WriteOut-Dns-1 | MUST | `-w` after a successful `dns` transfer writes `%{exitcode}` 0, an empty `%{errormsg}`, `%{url}` and `%{url_effective}` as given, `%{scheme}` `dns`, `%{remote_ip}` 127.0.0.1, `%{remote_port}` 53, `%{size_download}` the answer text's length and `%{time_total}` 0.000000 on the injected clock, after the answer. | DnsTransferSurfaceCases.WriteOut_SuccessfulQuery_WritesEveryVariableAfterTheAnswer |
| CurlSurface:WriteOut-Dns-2 | MUST | `--fail -w` after a `dns` NXDOMAIN answer writes `%{exitcode}` 0 and an empty `%{errormsg}` after the answer, nothing on standard error: DNS maps no response code to 22 (ADR-1140). | DnsTransferSurfaceCases.WriteOut_NameErrorUnderFail_WritesExitCode0AsDnsMapsNoResponseCodeTo22 |
| CurlSurface:Fail-Dns-1 | MUST | `--fail` on a `dns` NXDOMAIN answer exits 0, not 22, and writes the answer: DNS maps no response code to 22 (ADR-1140). | DnsTransferSurfaceCases.Fail_NameError_StaysExit0AndWritesTheAnswerAsDnsMapsNoResponseCodeTo22 |
| CurlSurface:FailWithBody-Dns-1 | MUST | `--fail-with-body` on a `dns` NXDOMAIN answer exits 0, not 22, and writes the answer (ADR-1140). | DnsTransferSurfaceCases.Fail_NameError_StaysExit0AndWritesTheAnswerAsDnsMapsNoResponseCodeTo22 |
| CurlSurface:Fail-Dns-2 | MUST | `--fail` on a successful `dns` transfer changes nothing: the answer on standard output, exit 0. | DnsTransferSurfaceCases.Fail_SuccessfulQuery_WritesTheAnswerAndExits0 |
| CurlSurface:MaxTime-Dns-1 | MUST | `-m 2` on a `dns` transfer whose server never answers exits 28 at the deadline on the injected clock, standard error `curl: (28) Operation timed out after 2000 milliseconds with 0 bytes received`, nothing on standard output. | DnsTransferSurfaceCases.MaxTime_ServerNeverAnswers_Exits28AtTheDeadline |
