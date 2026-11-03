# CurlSurface: curl's transfer options for the redis and rediss schemes

curl's generic transfer options - `-v`, `-o`, `-O`, `-w`, `--fail`, `--fail-with-body` and `-m` - checked for each
messaging and stream scheme through the real command line, in process, against the scheme's canned server
(`TransferSurfaceCaseRun` in `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/MessagingStreams/`, one case
file per scheme family). Expected values come from curl 8.22.0's manual page (curl.se/docs/manpage.html: "-v", "-o",
"-O", "-w", "--fail", "-m", "EXIT CODES") and, where an extension deliberately differs, from its ADR, named in the row.
Every run is on a clock the check drives, so `%{time_total}` is `0.000000` and `-m` ends at the deadline with no real
wait. TLS schemes run with `-k` against a fresh self-signed certificate. IDs are `CurlSurface:<option>-<Scheme>-<n>`.

`redis` and `rediss` send `GET k` (`redis://localhost/GET/k`) to a server that answers `HELLO 3` as RESP3 does and
`GET` with the bulk string `v` (written `v` and a newline) or with the error `ERR wrong kind of value`. Redis maps no
server error to exit 22: ADR-1079 makes an error reply exit 8 whatever `--fail` says, as `--fail` is defined for HTTP.

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| CurlSurface:Verbose-Redis-1 | MUST | `-v` on a `redis` transfer writes `* Connected to localhost (127.0.0.1) port 6379` to standard error, and only the reply to standard output; exit 0. | RedisTransferSurfaceCases.Verbose_SuccessfulGet_WritesConnectionLinesToStandardErrorAndOnlyTheReplyToStandardOutput |
| CurlSurface:Output-Redis-1 | MUST | `-o file` on a `redis` transfer writes the reply to the file in a temporary directory and nothing to standard output or standard error; exit 0. | RedisTransferSurfaceCases.Output_File_WritesTheReplyToTheFileAndNothingToStandardOutput |
| CurlSurface:RemoteName-Redis-1 | MUST | `--output-dir dir -O` on a `redis` transfer writes the reply to `dir/k`, the URL's last path segment, and nothing to standard output; exit 0. | RedisTransferSurfaceCases.RemoteName_WritesTheReplyToAFileNamedForTheUrlsLastSegment |
| CurlSurface:WriteOut-Redis-1 | MUST | `-w` after a successful `redis` transfer writes `%{exitcode}` 0, an empty `%{errormsg}`, `%{url}` and `%{url_effective}` as given, `%{scheme}` `redis`, `%{remote_ip}` 127.0.0.1, `%{remote_port}` 6379, `%{size_download}` 2 and `%{time_total}` 0.000000 on the injected clock, after the reply. | RedisTransferSurfaceCases.WriteOut_SuccessfulGet_WritesEveryVariableAfterTheReply |
| CurlSurface:WriteOut-Redis-2 | MUST | `-w` after a `redis` error reply writes `%{exitcode}` 8 and `%{errormsg}` `Redis server answered GET with error: ...` (ADR-1079), `%{size_download}` 0, and standard error the same message; exit 8. | RedisTransferSurfaceCases.WriteOut_ErrorReply_WritesExitCode8AndTheErrorMessage |
| CurlSurface:Fail-Redis-1 | MUST | `--fail` on a `redis` error reply exits 8, not 22: Redis maps no server error to 22 (ADR-1079); nothing on standard output. | RedisTransferSurfaceCases.Fail_ErrorReply_StaysExit8AsRedisMapsNoServerErrorTo22 |
| CurlSurface:FailWithBody-Redis-1 | MUST | `--fail-with-body` on a `redis` error reply exits 8, not 22 (ADR-1079); nothing on standard output. | RedisTransferSurfaceCases.Fail_ErrorReply_StaysExit8AsRedisMapsNoServerErrorTo22 |
| CurlSurface:Fail-Redis-2 | MUST | `--fail` on a successful `redis` transfer changes nothing: the reply on standard output, exit 0. | RedisTransferSurfaceCases.Fail_SuccessfulGet_WritesTheReplyAndExits0 |
| CurlSurface:MaxTime-Redis-1 | MUST | `-m 5` on a `redis` transfer whose server never answers exits 28 at the deadline on the injected clock, standard error `curl: (28) Operation timed out after 5000 milliseconds with 0 bytes received`, nothing on standard output. | RedisTransferSurfaceCases.MaxTime_ServerNeverAnswers_Exits28AtTheDeadline |
| CurlSurface:Verbose-Rediss-1 | MUST | `-v` on a `rediss` transfer writes `* Connected to localhost (127.0.0.1) port 6379` to standard error, then the platform curl's TLS line (`* schannel: ` on Windows, `* SSL connection using TLSv1.` elsewhere), and only the reply to standard output; exit 0. | RedisTransferSurfaceCases.Verbose_SuccessfulGet_WritesConnectionLinesToStandardErrorAndOnlyTheReplyToStandardOutput |
| CurlSurface:Output-Rediss-1 | MUST | `-o file` on a `rediss` transfer writes the reply to the file in a temporary directory and nothing to standard output or standard error; exit 0. | RedisTransferSurfaceCases.Output_File_WritesTheReplyToTheFileAndNothingToStandardOutput |
| CurlSurface:RemoteName-Rediss-1 | MUST | `--output-dir dir -O` on a `rediss` transfer writes the reply to `dir/k`, the URL's last path segment, and nothing to standard output; exit 0. | RedisTransferSurfaceCases.RemoteName_WritesTheReplyToAFileNamedForTheUrlsLastSegment |
| CurlSurface:WriteOut-Rediss-1 | MUST | `-w` after a successful `rediss` transfer writes `%{exitcode}` 0, an empty `%{errormsg}`, `%{url}` and `%{url_effective}` as given, `%{scheme}` `rediss`, `%{remote_ip}` 127.0.0.1, `%{remote_port}` 6379, `%{size_download}` 2 and `%{time_total}` 0.000000 on the injected clock, after the reply. | RedisTransferSurfaceCases.WriteOut_SuccessfulGet_WritesEveryVariableAfterTheReply |
| CurlSurface:WriteOut-Rediss-2 | MUST | `-w` after a `rediss` error reply writes `%{exitcode}` 8 and `%{errormsg}` `Redis server answered GET with error: ...` (ADR-1079), `%{size_download}` 0, and standard error the same message; exit 8. | RedisTransferSurfaceCases.WriteOut_ErrorReply_WritesExitCode8AndTheErrorMessage |
| CurlSurface:Fail-Rediss-1 | MUST | `--fail` on a `rediss` error reply exits 8, not 22: Redis maps no server error to 22 (ADR-1079); nothing on standard output. | RedisTransferSurfaceCases.Fail_ErrorReply_StaysExit8AsRedisMapsNoServerErrorTo22 |
| CurlSurface:FailWithBody-Rediss-1 | MUST | `--fail-with-body` on a `rediss` error reply exits 8, not 22 (ADR-1079); nothing on standard output. | RedisTransferSurfaceCases.Fail_ErrorReply_StaysExit8AsRedisMapsNoServerErrorTo22 |
| CurlSurface:Fail-Rediss-2 | MUST | `--fail` on a successful `rediss` transfer changes nothing: the reply on standard output, exit 0. | RedisTransferSurfaceCases.Fail_SuccessfulGet_WritesTheReplyAndExits0 |
| CurlSurface:MaxTime-Rediss-1 | MUST | `-m 5` on a `rediss` transfer whose server never answers exits 28 at the deadline on the injected clock, standard error `curl: (28) Operation timed out after 5000 milliseconds with 0 bytes received`, nothing on standard output. | RedisTransferSurfaceCases.MaxTime_ServerNeverAnswers_Exits28AtTheDeadline |
