# CurlSurface: curl's transfer options for the xmpp and xmpps schemes

curl's generic transfer options - `-v`, `-o`, `-O`, `-w`, `--fail`, `--fail-with-body` and `-m` - checked for each
messaging and stream scheme through the real command line, in process, against the scheme's canned server
(`TransferSurfaceCaseRun` in `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/MessagingStreams/`, one case
file per scheme family). Expected values come from curl 8.22.0's manual page (curl.se/docs/manpage.html: "-v", "-o",
"-O", "-w", "--fail", "-m", "EXIT CODES") and, where an extension deliberately differs, from its ADR, named in the row.
Every run is on a clock the check drives, so `%{time_total}` is `0.000000` and `-m` ends at the deadline with no real
wait. TLS schemes run with `-k` against a fresh self-signed certificate. IDs are `CurlSurface:<option>-<Scheme>-<n>`.

`xmpp` and `xmpps` (TLS from the first byte on port 5223) log in anonymously to an `XmppReferenceServer` and send the
disco#info query a URL with no data sends to its path JID (`xmpp://localhost/bob@localhost`, ADR-1068); the server
answers with an empty disco#info result, written `<query xmlns="http://jabber.org/protocol/disco#info" />` and a
newline (56 bytes), or with an `item-not-found` stanza error. Xmpp maps no server error to exit 22: ADR-1068 makes a
stanza error exit 8 whatever `--fail` says. curl 8.22.0's `-v` connection line is `* Established connection to
localhost (127.0.0.1 port N) from ...` (measured with `Record-CurlExchange.ps1`).

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| CurlSurface:Verbose-Xmpp-1 | MUST | `-v` on an `xmpp` transfer writes `* Established connection to localhost (127.0.0.1 port 5222) from ...` (curl 8.22.0, measured) to standard error, and only the reply to standard output; exit 0. | XmppTransferSurfaceCases.Verbose_SuccessfulQuery_WritesConnectionLinesToStandardErrorAndOnlyTheReplyToStandardOutput |
| CurlSurface:Output-Xmpp-1 | MUST | `-o file` on an `xmpp` transfer writes the reply to the file in a temporary directory and nothing to standard output or standard error; exit 0. | XmppTransferSurfaceCases.Output_File_WritesTheReplyToTheFileAndNothingToStandardOutput |
| CurlSurface:RemoteName-Xmpp-1 | MUST | `--output-dir dir -O` on an `xmpp` transfer writes the reply to `dir/bob@localhost`, the URL's last path segment, and nothing to standard output; exit 0. | XmppTransferSurfaceCases.RemoteName_WritesTheReplyToAFileNamedForTheUrlsLastSegment |
| CurlSurface:WriteOut-Xmpp-1 | MUST | `-w` after a successful `xmpp` transfer writes `%{exitcode}` 0, an empty `%{errormsg}`, `%{url}` and `%{url_effective}` as given, `%{scheme}` `xmpp`, `%{remote_ip}` 127.0.0.1, `%{remote_port}` 5222, `%{size_download}` the reply's 56 bytes and `%{time_total}` 0.000000 on the injected clock, after the reply. | XmppTransferSurfaceCases.WriteOut_SuccessfulQuery_WritesEveryVariableAfterTheReply |
| CurlSurface:WriteOut-Xmpp-2 | MUST | `-w` after an `xmpp` stanza error writes `%{exitcode}` 8 and `%{errormsg}` `XMPP stanza error: item-not-found` (ADR-1068), `%{size_download}` 0, and standard error the same message; exit 8. | XmppTransferSurfaceCases.WriteOut_StanzaError_WritesExitCode8AndTheErrorMessage |
| CurlSurface:Fail-Xmpp-1 | MUST | `--fail` on an `xmpp` stanza error exits 8, not 22: Xmpp maps no server error to 22 (ADR-1068); nothing on standard output. | XmppTransferSurfaceCases.Fail_StanzaError_StaysExit8AsXmppMapsNoServerErrorTo22 |
| CurlSurface:FailWithBody-Xmpp-1 | MUST | `--fail-with-body` on an `xmpp` stanza error exits 8, not 22 (ADR-1068); nothing on standard output. | XmppTransferSurfaceCases.Fail_StanzaError_StaysExit8AsXmppMapsNoServerErrorTo22 |
| CurlSurface:Fail-Xmpp-2 | MUST | `--fail` on a successful `xmpp` transfer changes nothing: the reply on standard output, exit 0. | XmppTransferSurfaceCases.Fail_SuccessfulQuery_WritesTheReplyAndExits0 |
| CurlSurface:MaxTime-Xmpp-1 | MUST | `-m 5` on an `xmpp` transfer whose server never answers exits 28 at the deadline on the injected clock, standard error `curl: (28) Operation timed out after 5000 milliseconds with 0 bytes received`, nothing on standard output. | XmppTransferSurfaceCases.MaxTime_ServerNeverAnswers_Exits28AtTheDeadline |
| CurlSurface:Verbose-Xmpps-1 | MUST | `-v` on an `xmpps` transfer writes `* Established connection to localhost (127.0.0.1 port 5223) from ...` (curl 8.22.0, measured) to standard error, then the platform curl's TLS line (`* schannel: ` on Windows, `* SSL connection using TLSv1.` elsewhere), and only the reply to standard output; exit 0. | XmppTransferSurfaceCases.Verbose_SuccessfulQuery_WritesConnectionLinesToStandardErrorAndOnlyTheReplyToStandardOutput |
| CurlSurface:Output-Xmpps-1 | MUST | `-o file` on an `xmpps` transfer writes the reply to the file in a temporary directory and nothing to standard output or standard error; exit 0. | XmppTransferSurfaceCases.Output_File_WritesTheReplyToTheFileAndNothingToStandardOutput |
| CurlSurface:RemoteName-Xmpps-1 | MUST | `--output-dir dir -O` on an `xmpps` transfer writes the reply to `dir/bob@localhost`, the URL's last path segment, and nothing to standard output; exit 0. | XmppTransferSurfaceCases.RemoteName_WritesTheReplyToAFileNamedForTheUrlsLastSegment |
| CurlSurface:WriteOut-Xmpps-1 | MUST | `-w` after a successful `xmpps` transfer writes `%{exitcode}` 0, an empty `%{errormsg}`, `%{url}` and `%{url_effective}` as given, `%{scheme}` `xmpps`, `%{remote_ip}` 127.0.0.1, `%{remote_port}` 5223, `%{size_download}` the reply's 56 bytes and `%{time_total}` 0.000000 on the injected clock, after the reply. | XmppTransferSurfaceCases.WriteOut_SuccessfulQuery_WritesEveryVariableAfterTheReply |
| CurlSurface:WriteOut-Xmpps-2 | MUST | `-w` after an `xmpps` stanza error writes `%{exitcode}` 8 and `%{errormsg}` `XMPP stanza error: item-not-found` (ADR-1068), `%{size_download}` 0, and standard error the same message; exit 8. | XmppTransferSurfaceCases.WriteOut_StanzaError_WritesExitCode8AndTheErrorMessage |
| CurlSurface:Fail-Xmpps-1 | MUST | `--fail` on an `xmpps` stanza error exits 8, not 22: Xmpp maps no server error to 22 (ADR-1068); nothing on standard output. | XmppTransferSurfaceCases.Fail_StanzaError_StaysExit8AsXmppMapsNoServerErrorTo22 |
| CurlSurface:FailWithBody-Xmpps-1 | MUST | `--fail-with-body` on an `xmpps` stanza error exits 8, not 22 (ADR-1068); nothing on standard output. | XmppTransferSurfaceCases.Fail_StanzaError_StaysExit8AsXmppMapsNoServerErrorTo22 |
| CurlSurface:Fail-Xmpps-2 | MUST | `--fail` on a successful `xmpps` transfer changes nothing: the reply on standard output, exit 0. | XmppTransferSurfaceCases.Fail_SuccessfulQuery_WritesTheReplyAndExits0 |
| CurlSurface:MaxTime-Xmpps-1 | MUST | `-m 5` on an `xmpps` transfer whose server never answers exits 28 at the deadline on the injected clock, standard error `curl: (28) Operation timed out after 5000 milliseconds with 0 bytes received`, nothing on standard output. | XmppTransferSurfaceCases.MaxTime_ServerNeverAnswers_Exits28AtTheDeadline |
