# Libcurl and Curl.Api: two public .NET APIs over one engine

**Status: approved by Stewart 2026-10-15; phases 1 and 2 filed as tasks, phase 3 filed by the
last phase 2 task, phase 4 by the last phase 3 task, phase 5 filed by the last
phase 4 task (BL-2514), except its gap office part.** Nothing below exists in the code yet except where it
says "today". The decision record is ADR-0506; where this plan and the ADR disagree, the ADR wins
and this plan is corrected.

## The goal

- **`Libcurl`** (named by Stewart; renamed from `Curl.libcurl` on 2026-10-15 to match upstream's
  library name; tests `Libcurl.UnitTests`, root namespace `Libcurl`) - a libcurl-faithful .NET API: `CurlGlobal.Init()` returns a library object
  that creates handles; `CurlEasy` with `SetOption(CurlOption.X, value)`, `Perform()` returning
  `CurlCode`, `PerformAsync`, `GetInfo(CurlInfo.X)`; every `CURLOPT_*` and `CURLINFO_*` by name;
  callbacks as delegates; multi (with the socket-action API and timers), share, URL, MIME,
  formadd, WebSocket, header API, version info, escape, getdate, slist, pause, connect-only
  send/recv and the strerror family. The 362 upstream `libcurl-api` cases are measured against it
  once their `tests/libtest` programs are ported to C#.
- **`Curl.Api`** - a .NET-normal API: `CurlClient` (constructed, `IAsyncDisposable`), async with
  `CancellationToken` everywhere, `Stream` bodies, option records, `CurlException` carrying the
  `CurlCode`, `IAsyncEnumerable` WebSocket messages, and `CurlHttpMessageHandler : HttpMessageHandler`
  so `HttpClient` code can use Curl as its transport. BCL only; no `Microsoft.Extensions.*`.
- **`Curl.Console`** re-based to consume `Libcurl`, as upstream's curl tool consumes libcurl.

## Layering

```
 Curl.Console (the curl tool)     .NET programs            C programs (future)
        |                         |          |                    |
        v                         v          v                    v
   Libcurl  <---------  (either)   Curl.Api        libcurl.dll/.so exports (future)
        |                                    |
        +------------------+-----------------+
                           v
              Curl.Api.UnitLibrary  (the seam)
                EngineContracts  ICurlEngine, IEasyTransfer, IMultiTransfer, IShareHandle,
                                 ICurlUrlHandle, IMimeBuilder, IEasyWebSocket, ITransferHeaders
                Codes            CurlCode, CurlMultiCode, CurlShareCode, CurlUrlCode,
                                 CurlHeaderCode, CurlFormAddCode
                Options          Easy<Group>Options records, EasyOptions aggregate
                Info             TransferInfo (every CURLINFO_*)
                Callbacks        write/read/header/progress/debug/... delegates
                           ^
                           | implements
              Curl.Engine.UnitLibrary  (the managed engine, moved out of Curl.Console)
                           |
     Curl.Core  Curl.Networking  Curl.Tls  Curl.Authentication  Curl.Cookies  Curl.Output
     Curl.Protocol.Abstractions  Curl.Protocol.<17>  Curl.Http2  Curl.Http3  Curl.Quic ...
```

Replacing the managed engine (with native libcurl over P/Invoke, say) means one new project that
implements `Curl.Api.EngineContracts` - nothing else changes. Why the engine is its own library
rather than part of `Curl.Api.UnitLibrary`: ADR-0506, "Why a dedicated engine library".

### New projects and where they sort

| Project | Sorts | Quality gates |
| --- | --- | --- |
| `Curl.Api` | first, before `Curl.Authentication.*` | the four gates (ADR-0506) |
| `Curl.Api.UnitLibrary` | after `Curl.Api` | the four gates |
| `Curl.Api.UnitTests` | after `Curl.Api.UnitLibrary`; tests both | - |
| `Curl.Engine.UnitLibrary` | between `Curl.Cryptography.*` and `Curl.Http2.*` | the four gates |
| `Curl.Engine.UnitTests` | after it | - |
| `Libcurl` | after every `Curl.*` project | the four gates |
| `Libcurl.UnitTests` | after it | - |

## Today: what is engine and what is tool

Upstream's dividing line is the guide: libcurl does what `curl_easy_setopt` can ask for; the tool
does what only a command line has.

| Stays in `Curl.Console` (tool) | Moves to `Curl.Engine.UnitLibrary` (libcurl) |
| --- | --- |
| `Program`, `CurlCommandRunner`'s parse, glob, output-file, retry, parallel and reporting code | `CurlCommandRunner`'s per-transfer perform path (URL parse and refusals, proxy and credential selection, SSH options, cookie load and save, HSTS upgrade, alt-svc, setup failures, redirects, watchdogs), as `TransferPerformer`, with `EngineTransferState`, the part of a running transfer it keeps (BL-2291) |
| `CurlComposition.CreateRunner` and the runner wiring | `CurlComposition`'s handler, transport, resolver, TLS and security-context factories |
| `DeferredOutputFileStream`, `OutputFile*`, `PhysicalOutputPaths`, `IOutputPaths`, `RemoteFileName`, `ContentDispositionFileName`, `RedirectLocationFileName`, `DirectoryCreationFailure`, `OutputFileDirectories`, `NativeExtendedAttributeWriter`, `IExtendedAttributeWriter`, `NativeRegularFileTest`, `WindowsOutputFileNameSanitizer`, `RemoteHeaderNameStream`, `RemoteTimeFailureWarning` | `TransferContextFactory`, `TransferDispatch`, `HeaderLineTeeStream` |
| `ParallelHostQueue`, `ParallelProgressMeter`, `ParallelRun`, `ParallelTransferProgress`, `ParallelTransferQueue`, `UrlTransfer`, `RetryPolicyMapping`, `RunningTransferState` (the tool's part of a running transfer, deriving from the engine's `EngineTransferState`, BL-2291), `TlsSessionFileLines` (it uses `ToolNotes`, BL-2287) | `CookieEngine`, `AltSvcTransferCache`, `HstsCacheFile`, `ClockOverridingHstsStore`, `FixedWallClock`, `SshKnownHostsFileSearch`, `CRuntimeErrorNumbers`, `UrlEffective` (`CURLINFO_EFFECTIVE_URL`; the performer's HSTS line uses it, BL-2291) |
| `ProgressBarRecorder`, `ProgressMeterFields`, `ProgressMeterLines`, `TransferProgressRecorder`, `TerminalColumns`, `WarningLineWrapper`, `ToolNotes` | `HttpRequestOptionsMapping`, `HttpVersionMapping`, `MailRequestOptionsMapping`, `MultipartFormPartMapping`, `RedirectPolicyMapping`, `SshOptionsMapping`, `TlsClientOptionsMapping`, `OptionGroupConnectionSettings`, `TlsBuildInformation`, `TransferProxySelection`, `TransferTimers`, `UnusableDohUrlResolver`, `CurlTransports` |
| `StandardOutput*`, `StandardError*`, `StandardInput*`, `ClosedStandardOutputStream`, `WriteGate`, `WriteGateStream`, `DumpHeaderOutputStream`, `EtagSaveStream`, and the runner's stream wrappers `LineFeedToCrLfStream`, `TextModeUntilBinaryStream`, `FlushEachWriteStream`, `HoldableStream` | `AwsSigV4HttpAuthenticator`, `HttpSignatureHttpAuthenticator`, `HttpSignatureSetup`, `HandBuiltKerberosSources`, `HandBuiltNtlmSecurityContextFactory`, `KerberosDisk*`, `LateBoundSecurityContextFactory`, `NegotiateOptionsMapping`, `Socks5AuthenticationMapping`, `TransferCredentialLookup`, `NoCryptoEngines` |
| `InProcessCurl` (public; `Gap/Tools/Measure-UpstreamCases.cs` calls it), `RunDiagnosticLog`, `RecordingDataFileReader`, `TransferDiagnosticLines`, `TransferEventOutput`, `DiskWriteOutFileOpener`, `RefusingWriteOutFileOpener`, and until `RunningTransferState`, `TransferTimers` and `WriteGate` move: `ConnectionIdRecordingTransferEvents`, `TlsResultRecordingTransferEvents`, `MultiStateTraceEvents`, `GatedDiagnosticLog` (BL-2285) | the trace-event decorators (`ClientReaderResetTraceEvents`, `ClientWriterTraceEvents`, `ConnectReplyHeadWritingEvents`, `ResponseWaitTimerTraceEvents`), `ConnectionEndPointRecorder`, `EndPointRecording*Connector`, `EndPointReportingProtocolHandler`, `LazyProtocolHandler`, `RoutingFtpProtocolHandler`, `LateBoundDiagnosticLog` |
| `CurlEasyErrorText` (moved to `Libcurl/Misc/ErrorText` by BL-2450; `Curl.Console` passes it in) | `CurlErrorBuffer` (it is `CURLOPT_ERRORBUFFER`'s 256-byte cut) |

A move task that finds a file in its list referencing a tool-side type leaves it, says why in its
Notes, and the classification here is corrected. `LineFeedToCrLfStream`, `TextModeUntilBinaryStream`,
`FlushEachWriteStream` and `HoldableStream` are decided where their callers land.

## The API inventory

From libcurl's public headers at the targeted version (curl 8.22.0, `Gap/Baselines/target.json`):
`curl/curl.h`, `easy.h`, `multi.h`, `urlapi.h`, `websockets.h`, `header.h`, `options.h`.

### Easy (`curl.h`, `easy.h`) - `CurlEasy`

`curl_easy_init`, `_setopt`, `_perform`, `_getinfo`, `_cleanup` (`Dispose`), `_duphandle`,
`_reset`, `_pause` (`CURLPAUSE_*`), `_recv`, `_send` (with `CURLOPT_CONNECT_ONLY`), `_upkeep`,
`_escape`, `_unescape`, `_strerror`, `_ssls_import`, `_ssls_export`. `Perform()` is synchronous
for fidelity (it blocks on `PerformAsync`, the one sanctioned bridge, documented as such);
`PerformAsync(CancellationToken)` is the native shape.

### CURLOPT groups (`curl_easy_setopt`) - `Easy<Group>Options` records

| Group record | CURLOPT members (representative, not exhaustive) |
| --- | --- |
| `EasyBehaviourOptions` | `VERBOSE`, `HEADER`, `NOPROGRESS`, `NOSIGNAL`, `WILDCARDMATCH`, `ERRORBUFFER`, `STDERR`, `FAILONERROR`, `KEEP_SENDING_ON_ERROR`, `PRIVATE`, `SHARE`, `QUICK_EXIT`, `NEW_FILE_PERMS`, `NEW_DIRECTORY_PERMS` |
| `EasyTransferOptions` | `RANGE`, `RESUME_FROM(_LARGE)`, `CUSTOMREQUEST`, `FILETIME`, `DIRLISTONLY`, `NOBODY`, `UPLOAD`, `INFILESIZE(_LARGE)`, `UPLOAD_BUFFERSIZE`, `BUFFERSIZE`, `MAXFILESIZE(_LARGE)`, `TIMECONDITION`, `TIMEVALUE(_LARGE)`, `TRANSFERTEXT`, `CRLF`, `PROXY_TRANSFER_MODE`, `LOW_SPEED_LIMIT`, `LOW_SPEED_TIME`, `MAX_SEND_SPEED_LARGE`, `MAX_RECV_SPEED_LARGE`, `TIMEOUT(_MS)` |
| `EasyNetworkOptions` | `URL`, `CURLU`, `PATH_AS_IS`, `PROTOCOLS_STR`, `REDIR_PROTOCOLS_STR`, `DEFAULT_PROTOCOL`, `PORT`, `INTERFACE`, `LOCALPORT(RANGE)`, `CONNECT_TO`, `RESOLVE`, `DNS_CACHE_TIMEOUT`, `DNS_SERVERS`, `DNS_INTERFACE`, `DNS_LOCAL_IP4/6`, `DNS_SHUFFLE_ADDRESSES`, `DOH_URL`, `IPRESOLVE`, `TCP_NODELAY`, `TCP_FASTOPEN`, `TCP_KEEPALIVE/IDLE/INTVL/CNT`, `ADDRESS_SCOPE`, `UNIX_SOCKET_PATH`, `ABSTRACT_UNIX_SOCKET`, `CONNECTTIMEOUT(_MS)`, `HAPPY_EYEBALLS_TIMEOUT_MS`, `FRESH_CONNECT`, `FORBID_REUSE`, `MAXCONNECTS`, `MAXAGE_CONN`, `MAXLIFETIME_CONN`, `CONNECT_ONLY`, `ACCEPTTIMEOUT_MS`, `UPKEEP_INTERVAL_MS`, `SERVER_RESPONSE_TIMEOUT(_MS)`, `USE_SSL` |
| `EasyTlsOptions` | `SSLCERT(_BLOB)`, `SSLCERTTYPE`, `SSLKEY(_BLOB)`, `SSLKEYTYPE`, `KEYPASSWD`, `SSLENGINE(_DEFAULT)`, `SSLVERSION`, `SSL_VERIFYPEER`, `SSL_VERIFYHOST`, `SSL_VERIFYSTATUS`, `CAINFO(_BLOB)`, `CAPATH`, `CA_CACHE_TIMEOUT`, `CRLFILE`, `ISSUERCERT(_BLOB)`, `CERTINFO`, `PINNEDPUBLICKEY`, `SSL_CIPHER_LIST`, `TLS13_CIPHERS`, `SSL_EC_CURVES`, `SSL_SIGNATURE_ALGORITHMS`, `SSL_SESSIONID_CACHE`, `SSL_OPTIONS`, `SSL_FALSESTART`, `ECH`, `DOH_SSL_VERIFY*`, and the `PROXY_SSL*`/`PROXY_CA*`/`PROXY_PINNEDPUBLICKEY` twins |
| `EasyAuthenticationOptions` | `NETRC`, `NETRC_FILE`, `USERPWD`, `USERNAME`, `PASSWORD`, `LOGIN_OPTIONS`, `HTTPAUTH`, `PROXYAUTH`, `PROXYUSERPWD`, `PROXYUSERNAME`, `PROXYPASSWORD`, `SASL_AUTHZID`, `SASL_IR`, `XOAUTH2_BEARER`, `DISALLOW_USERNAME_IN_URL`, `UNRESTRICTED_AUTH`, `SERVICE_NAME`, `PROXY_SERVICE_NAME`, `GSSAPI_DELEGATION`, `KRBLEVEL`, `AWS_SIGV4`, `TLSAUTH_TYPE/USERNAME/PASSWORD` (and proxy twins) |
| `EasyProxyOptions` | `PROXY`, `PROXYPORT`, `PROXYTYPE`, `PRE_PROXY`, `NOPROXY`, `HTTPPROXYTUNNEL`, `SUPPRESS_CONNECT_HEADERS`, `PROXYHEADER`, `HEADEROPT`, `SOCKS5_AUTH`, `SOCKS5_GSSAPI_NEC`, `SOCKS5_GSSAPI_SERVICE`, `HAPROXYPROTOCOL`, `HAPROXY_CLIENT_IP` |
| `EasyHttpOptions` | `HTTPHEADER`, `USERAGENT`, `REFERER`, `AUTOREFERER`, `ACCEPT_ENCODING`, `TRANSFER_ENCODING`, `HTTP_CONTENT_DECODING`, `HTTP_TRANSFER_DECODING`, `FOLLOWLOCATION`, `MAXREDIRS`, `POSTREDIR`, `POST`, `POSTFIELDS`, `COPYPOSTFIELDS`, `POSTFIELDSIZE(_LARGE)`, `HTTPGET`, `MIMEPOST`, `MIME_OPTIONS`, `HTTPPOST` (formadd), `HTTP_VERSION`, `HTTP09_ALLOWED`, `IGNORE_CONTENT_LENGTH`, `EXPECT_100_TIMEOUT_MS`, `HTTP200ALIASES`, `REQUEST_TARGET`, `COOKIE`, `COOKIEFILE`, `COOKIEJAR`, `COOKIESESSION`, `COOKIELIST`, `ALTSVC`, `ALTSVC_CTRL`, `HSTS`, `HSTS_CTRL`, `PIPEWAIT`, `STREAM_DEPENDS(_E)`, `STREAM_WEIGHT` |
| `EasyProtocolOptions` (one nested record per protocol) | FTP: `FTPPORT`, `QUOTE`, `PREQUOTE`, `POSTQUOTE`, `APPEND`, `FTP_USE_EPRT/EPSV/PRET`, `FTP_CREATE_MISSING_DIRS`, `FTP_ALTERNATIVE_TO_USER`, `FTP_SKIP_PASV_IP`, `FTPSSLAUTH`, `FTP_SSL_CCC`, `FTP_ACCOUNT`, `FTP_FILEMETHOD`; SSH: `SSH_AUTH_TYPES`, `SSH_COMPRESSION`, `SSH_HOST_PUBLIC_KEY_MD5/SHA256`, `SSH_PUBLIC_KEYFILE`, `SSH_PRIVATE_KEYFILE`, `SSH_KNOWNHOSTS`; SMTP: `MAIL_FROM`, `MAIL_RCPT`, `MAIL_AUTH`, `MAIL_RCPT_ALLOWFAILS`; TFTP: `TFTP_BLKSIZE`, `TFTP_NO_OPTIONS`; TELNET: `TELNETOPTIONS`; RTSP: `RTSP_REQUEST`, `RTSP_SESSION_ID`, `RTSP_STREAM_URI`, `RTSP_TRANSPORT`, `RTSP_CLIENT_CSEQ`, `RTSP_SERVER_CSEQ`; WebSocket: `WS_OPTIONS` |
| `EasyCallbackOptions` | `WRITEFUNCTION`, `READFUNCTION`, `HEADERFUNCTION`, `PROGRESSFUNCTION`, `XFERINFOFUNCTION`, `DEBUGFUNCTION`, `SEEKFUNCTION`, `SOCKOPTFUNCTION`, `OPENSOCKETFUNCTION`, `CLOSESOCKETFUNCTION`, `PREREQFUNCTION`, `RESOLVER_START_FUNCTION`, `TRAILERFUNCTION`, `CHUNK_BGN_FUNCTION`, `CHUNK_END_FUNCTION`, `FNMATCH_FUNCTION`, `INTERLEAVEFUNCTION`, `HSTSREADFUNCTION`, `HSTSWRITEFUNCTION`, `SSH_KEYFUNCTION`, `SSH_HOSTKEYFUNCTION`, `SSL_CTX_FUNCTION` (answers `CURLE_NOT_BUILT_IN` as the Schannel build does); each `*DATA` user pointer is the delegate's closure |

### CURLINFO (`curl_easy_getinfo`) - `TransferInfo`

Strings: `EFFECTIVE_URL`, `EFFECTIVE_METHOD`, `REDIRECT_URL`, `CONTENT_TYPE`, `PRIMARY_IP`,
`LOCAL_IP`, `FTP_ENTRY_PATH`, `RTSP_SESSION_ID`, `SCHEME`, `REFERER`, `CAINFO`, `CAPATH`,
`PRIVATE`. Longs: `RESPONSE_CODE`, `HTTP_CONNECTCODE`, `HTTP_VERSION`, `FILETIME(_T)`,
`REDIRECT_COUNT`, `HEADER_SIZE`, `REQUEST_SIZE`, `SSL_VERIFYRESULT`, `PROXY_SSL_VERIFYRESULT`,
`HTTPAUTH_AVAIL`, `PROXYAUTH_AVAIL`, `HTTPAUTH_USED`, `PROXYAUTH_USED`, `OS_ERRNO`,
`NUM_CONNECTS`, `PRIMARY_PORT`, `LOCAL_PORT`, `CONDITION_UNMET`, `RTSP_CLIENT_CSEQ`,
`RTSP_SERVER_CSEQ`, `RTSP_CSEQ_RECV`, `PROTOCOL`, `PROXY_ERROR`, `USED_PROXY`, `XFER_ID`,
`CONN_ID`, `LASTSOCKET`, `ACTIVESOCKET`. Times (double and `_T` microseconds): `TOTAL`,
`NAMELOOKUP`, `CONNECT`, `APPCONNECT`, `PRETRANSFER`, `POSTTRANSFER_T`, `STARTTRANSFER`,
`REDIRECT`, `QUEUE_TIME_T`, `EARLYDATA_SENT_T`, `RETRY_AFTER`. Sizes and speeds (`_T`):
`SIZE_UPLOAD`, `SIZE_DOWNLOAD`, `SPEED_UPLOAD`, `SPEED_DOWNLOAD`, `CONTENT_LENGTH_DOWNLOAD`,
`CONTENT_LENGTH_UPLOAD`. Lists: `SSL_ENGINES`, `COOKIELIST`, `CERTINFO`. `TLS_SSL_PTR` answers
the Schannel build's shape (no native pointer to give; documented).

### Multi (`multi.h`) - `CurlMulti`

`curl_multi_init`, `_add_handle`, `_remove_handle`, `_perform`, `_cleanup`, `_info_read`
(`CURLMSG_DONE`), `_wait`, `_poll`, `_wakeup`, `_fdset`, `_timeout`, `_setopt` (`CURLMOPT_*`:
`SOCKETFUNCTION`, `TIMERFUNCTION`, `PIPELINING`, `MAXCONNECTS`, `MAX_HOST_CONNECTIONS`,
`MAX_TOTAL_CONNECTIONS`, `MAX_CONCURRENT_STREAMS`, `PUSHFUNCTION`, `NETWORK_CHANGED`), the
socket-action API (`_socket_action`, `_assign`, `CURL_POLL_*`, `CURL_CSELECT_*`,
`CURL_SOCKET_TIMEOUT`), `_get_handles`, `_waitfds`, `_get_offt` (`CURLMINFO_*`), the
notification API (`_notify_enable`, `_notify_disable`), `_strerror`. Sockets are exposed as
opaque handles (`CurlSocket`) over `IConnection`, never as raw `Socket`s from protocol code.

### Share (`curl.h`) - `CurlShare`

`curl_share_init`, `_setopt` (`CURLSHOPT_SHARE`, `UNSHARE`, `LOCKFUNC`, `UNLOCKFUNC`,
`USERDATA`), `_cleanup`, `_strerror`; data kinds `COOKIE`, `DNS`, `SSL_SESSION`, `CONNECT`,
`PSL`, `HSTS`.

### URL (`urlapi.h`) - `CurlUrlHandle`

`curl_url`, `_cleanup`, `_dup`, `_get`, `_set`, `_strerror`; parts `URL`, `SCHEME`, `USER`,
`PASSWORD`, `OPTIONS`, `HOST`, `PORT`, `PATH`, `QUERY`, `FRAGMENT`, `ZONEID`; every `CURLU_*`
flag. Built on Curl's existing `CurlUrl` parser.

### MIME and formadd - `CurlMime`, `CurlMimePart`, `CurlForm`

`curl_mime_init`, `_free`, `_addpart`, `_name`, `_filename`, `_type`, `_encoder`, `_data`,
`_filedata`, `_data_cb`, `_subparts`, `_headers`; deprecated `curl_formadd`, `curl_formget`,
`curl_formfree` (`CURLFORM_*`). Built on `Curl.Core`'s `Multipart`.

### WebSocket (`websockets.h`)

`curl_ws_recv`, `curl_ws_send`, `curl_ws_meta`, `curl_ws_start_frame`; `CURLWS_TEXT`, `BINARY`,
`CONT`, `CLOSE`, `PING`, `PONG`, `OFFSET`, `RAW_MODE`, `NOAUTOPONG`. Built on
`Curl.Original.Protocol.Ws`.

### Header API (`header.h`)

`curl_easy_header`, `curl_easy_nextheader`, `struct curl_header`, `CURLH_HEADER`, `TRAILER`,
`CONNECT`, `1XX`, `PSEUDO`; `CURLHE_*` codes.

### Options metadata (`options.h`)

`curl_easy_option_by_name`, `_by_id`, `_next`; `struct curl_easyoption`, `CURLOT_*` types,
`CURLOT_FLAG_ALIAS`. This is the one table `SetOption` dispatches through.

### Everything else

`curl_global_init` (`CURL_GLOBAL_*`), `_init_mem` (accepted, allocation callbacks ignored and
documented), `_cleanup`, `_sslset`, `_trace`; `curl_version`, `curl_version_info`
(`CURLVERSION_*`, feature bits matching `-V`); `curl_getdate`; `curl_free`; `curl_slist_append`,
`_free_all` (`CurlSlist`); `curl_escape`, `curl_unescape` (deprecated); `curl_getenv`;
`curl_strequal`, `curl_strnequal`; the `curl_mprintf` family only as far as ported libtest
programs need it (C varargs have no .NET equal; it becomes a formatter, decided when a port needs
it); `curl_easy_strerror`, `curl_multi_strerror`, `curl_share_strerror`, `curl_url_strerror`.

## Phases

Phases are logical; the task dependencies govern order. Phase 1's later half (the option-group
retargets) waits on phase 2's option groups, so the two phases interleave.

### Phase 1 - extract the engine, re-base the console onto it (20 tasks, filed)

1. Scaffold `Curl.Engine.UnitLibrary` and `Curl.Engine.UnitTests`.
2. Inside `Curl.Console`, split `CurlCommandRunner`'s per-transfer perform path into
   `TransferPerformer` (three tasks: the redirect-following attempt; the per-transfer setup; the
   output decoupling so the performer writes only through injected streams and events).
3. Move the library-side files to the engine in clusters, each with its tests and its paragraphs
   of `Curl.Console/CLAUDE.md`: trace-event decorators and end-point recorders; authentication
   and security sources; per-transfer stores; option mappings; the composition factories;
   `TransferContextFactory` and `TransferDispatch`; `TransferPerformer` (seven tasks). The engine
   still reads `CommandLineOptions`.
4. Retarget the engine group by group from `CommandLineOptions` to the seam's `Easy<Group>Options`,
   growing `Curl.Console`'s `EasyOptionsFromCommandLine` mapping (seven tasks), then remove the
   engine's `Curl.Cli` reference.
5. Align the documents with the split.

Every phase 1 task keeps every test, the ratchet (`PassingUpstreamCases.txt`) and the gap office's
scores unchanged, leaves `CurlEasyErrorText` where it is (it has since moved to `Libcurl/Misc/ErrorText`, BL-2450) and keeps
`InProcessCurl.RunAsync`'s public signature (ADR-0506, "What must not change").

### Phase 2 - scaffold the API projects and define the seam (21 tasks, filed)

1. Scaffold `Curl.Api.UnitLibrary`, `Curl.Api`, `Curl.Api.UnitTests`, `Libcurl`,
   `Libcurl.UnitTests`; root `CLAUDE.md`'s naming section and `Measure-CodeQuality.ps1`'s
   production list learn them.
2. In parallel, each in its own seam folder: the result codes; seven option groups; the callback
   delegates; the `TransferInfo` read model.
3. The engine contracts: `ICurlEngine` and `IEasyTransfer` with the `EasyOptions` aggregate;
   multi; share, URL and MIME; WebSocket, headers and connect-only.
4. The managed engine implements `ICurlEngine` and `IEasyTransfer` and fills `TransferInfo`.
5. `Curl.Console` runs its serial transfers through `IEasyTransfer` and reads `-w` variables from
   `TransferInfo`.
6. Documents and glossary; then **file phase 3's tasks** from this plan.

### Phase 3 - `Libcurl`, area by area (61 tasks, filed; the table is the original estimate, corrected under "Filed tasks")

| Area | Tasks |
| --- | --- |
| `CurlGlobal`, library object, `CurlCode` strerror table, `curl_version_info` | 3 |
| Options metadata table (`options.h`) and `SetOption` for each group (one task per option group) | 9 |
| `GetInfo` for every `CURLINFO_*`, by group | 3 |
| `CurlEasy`: perform, `PerformAsync`, reset, duphandle, pause, upkeep, error buffer | 4 |
| Callbacks wired through the engine (write, read, header, progress/xferinfo, debug, seek, sockopt/opensocket/closesocket, prereq/resolver-start/trailer, chunk/fnmatch, interleave, HSTS read/write, SSH key callbacks) | 8 |
| Connect-only `Recv`/`Send` | 1 |
| Engine multi implementation (event loop over connections, timers) and `CurlMulti` (perform/wait/poll/wakeup/info_read, socket action and timers, get_handles/waitfds/get_offt/notify, CURLMOPT) | 9 |
| `CurlShare` (cookie, DNS, SSL session, connection, PSL, HSTS sharing; lock callbacks) | 3 |
| `CurlUrlHandle` (get, set, flags, dup, strerror) | 3 |
| `CurlMime`, `CurlMimePart` and deprecated formadd/formget | 4 |
| WebSocket (`recv`, `send`, `meta`, `start_frame`, raw mode) | 3 |
| Header API | 1 |
| Escape, unescape, getdate, slist, getenv, strequal | 2 |
| Move `CurlEasyErrorText` beside `curl_easy_strerror` - paired with an **interactive** `Gap/Tools/Measure-ExitCodeGap.ps1` change on the `gap` branch first (`lane: no`) | 2 |
| Re-base `Curl.Console` onto `Libcurl`: serial transfers, `-w` and info, `--parallel` through `CurlMulti`, removing its direct engine reference | 4 |

### Phase 4 - `Curl.Api` (19 tasks, filed 2026-10-16 by BL-2455; the estimate was 14-16, corrected under "Filed tasks")

`CurlClient` lifecycle and engine choice; request/response records and `Stream` bodies; option
records mapped to the easy option groups; `CurlException` and code mapping; cancellation through
`CancellationToken` (pause/abort semantics); progress through `IProgress<T>`; MIME bodies;
cookies/HSTS/alt-svc stores; WebSocket client with `IAsyncEnumerable` messages; concurrent
transfers over the multi contract; `CurlHttpMessageHandler` (request/response mapping, streaming,
HTTP/2 and HTTP/3 version policy, decompression, redirects and auth, cancellation, proxy); a
usage README; `--ai-help` is untouched (no option changes).

### Phase 5 - port `tests/libtest` and measure `libcurl-api` (30 tasks filed, 2 interactive ones still to file; the estimate was 40-45)

1. A libtest harness in the conformance projects: a C# `ILibTest` program shape, the `<tool>lib…`
   case runner beside the existing upstream-case runner, `libtest`'s `first.c` helpers
   (`test_setopt`, `easy_init`, the `TEST_ERR_*` codes) as C# helpers over `Libcurl`
   (3 tasks).
2. Port the 362 programs in batches of about ten by shared helper and area, each batch adding its
   passing cases to the ratchet (about 36 tasks).
3. **The gap office's part is interactive only** (`Gap/` changes only on the `gap` branch from an
   interactive session, ADR-0433): stop excluding `libcurl-api` cases, measure them through the
   harness, and have `gap-behaviour` group their failures (2-3 tasks, `lane: no`).

### Future work, not planned as tasks

- The NativeAOT C ABI: `libcurl.dll` / `libcurl.so` exporting the `curl_*` functions over
  `Libcurl` with `[UnmanagedCallersOnly]`, so C programs (and upstream's own libtest
  programs, unported) link Curl. A separate request (ADR-0506).
- A native-libcurl engine over P/Invoke implementing `Curl.Api.EngineContracts`.

## Estimate

| Phase | Tasks |
| --- | --- |
| 1 - engine extraction and console re-base | 20 (filed) |
| 2 - scaffolding and seam | 21 (filed) |
| 3 - `Libcurl` | 61 (filed) |
| 4 - `Curl.Api` | 19 (filed) |
| 5 - libtest ports and gap measurement | 30 (filed) + 2 interactive |
| **All** | **153** |

## Filed tasks

Filed 2026-10-15; the board holds their state. Tasks with no dependency on another task here
start at once: BL-2281, BL-2282 and BL-2292 (BL-2281 and BL-2292 share `Curl.slnx`, so one
waits for the other); after BL-2292, the seam tasks BL-2293 to BL-2302 run in parallel as their own dependencies allow
(their `touches` are disjoint seam folders).

Phase 1 - engine extraction and console re-base:

| Task | Title |
| --- | --- |
| BL-2281 | Scaffold `Curl.Engine.UnitLibrary` and `Curl.Engine.UnitTests` |
| BL-2282 | Split the redirect-following transfer attempt out of `CurlCommandRunner` into `TransferPerformer` |
| BL-2283 | Move the per-transfer setup from `CurlCommandRunner` into `TransferPerformer` |
| BL-2284 | Make `TransferPerformer` write only through injected streams and transfer events |
| BL-2285 | Move the transfer-event decorators and end-point recorders to the engine |
| BL-2286 | Move the authentication and security-context sources to the engine |
| BL-2287 | Move the per-transfer stores to the engine |
| BL-2288 | Move the option mappings, `CurlTransports` and `TransferTimers` to the engine |
| BL-2289 | Move `CurlComposition`'s factories to `EngineComposition` |
| BL-2290 | Move `TransferContextFactory` and `TransferDispatch` to the engine |
| BL-2291 | Move `TransferPerformer` to the engine |
| BL-2303 | Retarget connection, DNS and timeout settings to `EasyNetworkOptions` |
| BL-2304 | Retarget TLS settings to `EasyTlsOptions` |
| BL-2305 | Retarget proxy settings to `EasyProxyOptions` |
| BL-2306 | Retarget authentication settings to `EasyAuthenticationOptions` |
| BL-2307 | Retarget HTTP settings to `EasyHttpOptions` |
| BL-2308 | Retarget behaviour and transfer settings to `EasyBehaviourOptions` and `EasyTransferOptions` |
| BL-2309 | Retarget the protocol settings to `EasyProtocolOptions` |
| BL-2310 | Remove the engine's reference to `Curl.Cli.UnitLibrary` |
| BL-2311 | Align `Curl.Console` and `Curl.Engine` documents with the split |

Phase 2 - scaffolding and seam:

| Task | Title |
| --- | --- |
| BL-2292 | Scaffold `Curl.Api`, `Curl.Api.UnitLibrary`, `Curl.Api.UnitTests`, `Libcurl`, `Libcurl.UnitTests` |
| BL-2293 | Define the result-code enums |
| BL-2294 | Define `EasyBehaviourOptions` and `EasyTransferOptions` |
| BL-2295 | Define `EasyNetworkOptions` |
| BL-2296 | Define `EasyTlsOptions` |
| BL-2297 | Define `EasyAuthenticationOptions` |
| BL-2298 | Define `EasyProxyOptions` |
| BL-2299 | Define `EasyHttpOptions` |
| BL-2300 | Define `EasyProtocolOptions` |
| BL-2301 | Define the callback delegates and `EasyCallbackOptions` |
| BL-2302 | Define `TransferInfo` |
| BL-2312 | Define `ICurlEngine`, `IEasyTransfer` and `EasyOptions` |
| BL-2313 | Define `IMultiTransfer` |
| BL-2314 | Define `IShareHandle`, `ICurlUrlHandle` and `IMimeBuilder` |
| BL-2315 | Define `IEasyWebSocket`, `ITransferHeaders` and connect-only |
| BL-2316 | Implement `ICurlEngine` and `IEasyTransfer.PerformAsync` in the engine |
| BL-2317 | Fill `TransferInfo` from the engine's transfer report |
| BL-2318 | Run `Curl.Console`'s serial transfers through `IEasyTransfer` |
| BL-2319 | Read the `-w` variables from `TransferInfo` |
| BL-2320 | Glossary and product overview |
| BL-2321 | File phase 3 tasks for `Libcurl` |

Phase 3 - `Libcurl`, area by area (61 tasks, filed 2026-10-15 by BL-2321; the table under "Phase 3" above is the estimate, corrected to the filed counts: global 4, options 9, easy 4, info 3, callbacks 8, connect-only 1, multi 9, share 3, URL 3, MIME 4, WebSocket 3, header API 1, misc 2, error-text move 2, console re-base 4, phase 4 filing 1). Libcurl-side tasks use fakes of the seam and touch `Libcurl/<Area>`; the engine-side tasks (multi, share, URL, MIME, WebSocket, callbacks) touch `Curl.Engine.UnitLibrary` and run one at a time. BL-2449 is interactive only (`lane: no`, `Gap/`).

| Task | Title |
| --- | --- |
| BL-2456 | Libcurl: CurlGlobal.Init(ICurlEngine) and the CurlLibrary object that creates handles |
| BL-2457 | Libcurl: CurlGlobal.Init() builds the managed engine |
| BL-2458 | Libcurl: curl_version, curl_version_info and the feature bits |
| BL-2459 | Libcurl: curl_multi_strerror, curl_share_strerror, curl_url_strerror and the header-API error text |
| BL-2399 | Libcurl: the options table (options.h) and the SetOption dispatch |
| BL-2400 | Libcurl: SetOption for the behaviour options (EasyBehaviourOptions) |
| BL-2401 | Libcurl: SetOption for the transfer options (EasyTransferOptions) |
| BL-2402 | Libcurl: SetOption for the network options (EasyNetworkOptions) |
| BL-2403 | Libcurl: SetOption for the TLS options (EasyTlsOptions) |
| BL-2404 | Libcurl: SetOption for the authentication options (EasyAuthenticationOptions) |
| BL-2405 | Libcurl: SetOption for the proxy options (EasyProxyOptions) |
| BL-2406 | Libcurl: SetOption for the HTTP options (EasyHttpOptions) |
| BL-2407 | Libcurl: SetOption for the protocol options (EasyProtocolOptions) |
| BL-2408 | Libcurl: CurlEasy with Perform, PerformAsync and Dispose |
| BL-2409 | Libcurl: CurlEasy.Reset and DupHandle |
| BL-2410 | Libcurl: CurlEasy.Pause and Upkeep |
| BL-2411 | Libcurl: CURLOPT_ERRORBUFFER and CurlEasy.ErrorText |
| BL-2412 | Libcurl: GetInfo for string and list CURLINFO values, and the dispatch |
| BL-2413 | Libcurl: GetInfo for long CURLINFO values |
| BL-2414 | Libcurl: GetInfo for time, size and speed CURLINFO values |
| BL-2415 | Libcurl: write, read and header callbacks wired through the engine |
| BL-2416 | Libcurl: progress, xferinfo and debug callbacks wired through the engine |
| BL-2417 | Libcurl: seek callback wired through the engine |
| BL-2418 | Libcurl: sockopt, opensocket and closesocket callbacks wired through the engine |
| BL-2419 | Libcurl: prerequest, resolver-start and trailer callbacks wired through the engine |
| BL-2420 | Libcurl: chunk begin/end, fnmatch and interleave callbacks wired through the engine |
| BL-2421 | Libcurl: HSTS read and write callbacks wired through the engine |
| BL-2422 | Libcurl: SSH key callbacks and SSL_CTX_FUNCTION wired through the engine |
| BL-2423 | Libcurl: CurlEasy.Recv and Send for CURLOPT_CONNECT_ONLY |
| BL-2424 | Engine: the managed multi event loop (add, remove, perform, info_read) |
| BL-2425 | Engine: multi timers, wait, poll and wakeup |
| BL-2426 | Engine: multi socket-action API and CURLMOPT_SOCKETFUNCTION/TIMERFUNCTION |
| BL-2427 | Engine: multi get_handles, waitfds, get_offt, notifications and network change |
| BL-2428 | Engine: CURLMOPT settings (pipelining, connection limits, concurrent streams, push) |
| BL-2429 | Libcurl: CurlMulti with add, remove, perform, info_read and cleanup |
| BL-2430 | Libcurl: CurlMulti wait, poll, wakeup, fdset and timeout |
| BL-2431 | Libcurl: CurlMulti socket action, assign and timer callbacks |
| BL-2432 | Libcurl: CurlMulti get_handles, waitfds, get_offt, notify and CURLMOPT_* SetOption |
| BL-2433 | Libcurl: CurlShare with SetOption and lock callbacks |
| BL-2434 | Engine: sharing of cookies, DNS, TLS sessions and connections |
| BL-2435 | Engine: sharing of the public-suffix list and HSTS |
| BL-2436 | Engine: ICurlUrlHandle get over CurlUrl for every part and flag |
| BL-2437 | Engine: ICurlUrlHandle set, append-query, dup and clear |
| BL-2438 | Libcurl: CurlUrlHandle (get, set, dup, strerror) |
| BL-2439 | Engine: IMimeBuilder over Curl.Core Multipart |
| BL-2440 | Engine: ICurlFormBuilder for formadd, formget and formfree |
| BL-2441 | Libcurl: CurlMime and CurlMimePart |
| BL-2442 | Libcurl: CurlForm (formadd, formget, formfree) and CURLOPT_HTTPPOST |
| BL-2443 | Engine: IEasyWebSocket receive, send and metadata |
| BL-2444 | Engine: WebSocket start_frame, raw mode and no-autopong |
| BL-2445 | Libcurl: CurlEasy WebSocket API (recv, send, meta, start_frame) |
| BL-2446 | Libcurl: the header API (curl_easy_header, curl_easy_nextheader) |
| BL-2447 | Libcurl: escape, unescape, getenv, strequal and strnequal |
| BL-2448 | Libcurl: curl_getdate and CurlSlist |
| BL-2449 | Make the gap office's exit-code measurement find CurlEasyErrorText in its new home (interactive, gap branch) |
| BL-2450 | Move CurlEasyErrorText beside curl_easy_strerror in Libcurl |
| BL-2451 | Re-base Curl.Console: serial transfers run through Libcurl CurlEasy |
| BL-2452 | Re-base Curl.Console: -w variables read through Libcurl GetInfo |
| BL-2453 | Re-base Curl.Console: --parallel through CurlMulti |
| BL-2454 | Re-base Curl.Console: remove its direct reference to Curl.Engine.UnitLibrary |
| BL-2455 | File phase 4 tasks for Curl.Api from Documentation/Planning/Curl-Api-And-libcurl.md |

Phase 4 - `Curl.Api` (19 tasks, filed 2026-10-16 by BL-2455). `Curl.Api` is built on the seam
(`ICurlEngine` and the rest of `Curl.Api.UnitLibrary`), not on `Libcurl`, so its tasks wait on
each other, not on phase 3: every engine-side dependency they have is already Done. Code lives in
`Curl.Api/<Area>` with tests in `Curl.Api.UnitTests/<Area>`, so areas with disjoint `touches` can
run in parallel lanes. The estimate's bullets map to tasks as: lifecycle and engine choice
(BL-2496, BL-2497); records and `Stream` bodies (BL-2499, BL-2500, BL-2501); option records
(BL-2502, BL-2503); `CurlException` (BL-2498); cancellation (BL-2504); progress (BL-2505); MIME
(BL-2506); stores (BL-2507); WebSocket (BL-2508); concurrency (BL-2509); `CurlHttpMessageHandler`
(BL-2510 to BL-2512); README (BL-2513); phase 5 filing (BL-2514). The estimate's HTTP/2 and
HTTP/3 policy, decompression, redirects, auth and proxy bullets are BL-2511 and BL-2512.

| Task | Title |
| --- | --- |
| BL-2496 | `Curl.Api`: CurlClient lifecycle over an ICurlEngine |
| BL-2497 | `Curl.Api`: CurlClient.Create builds the managed engine |
| BL-2498 | `Curl.Api`: CurlException carrying the CurlCode |
| BL-2499 | `Curl.Api`: CurlRequest and CurlResponse records |
| BL-2500 | `Curl.Api`: request bodies from a Stream or byte content |
| BL-2501 | `Curl.Api`: streamed response bodies |
| BL-2502 | `Curl.Api`: transport option records mapped to the easy option groups |
| BL-2503 | `Curl.Api`: HTTP, authentication and protocol option records |
| BL-2504 | `Curl.Api`: cancellation through CancellationToken |
| BL-2505 | `Curl.Api`: progress through IProgress<T> |
| BL-2506 | `Curl.Api`: MIME multipart bodies |
| BL-2507 | `Curl.Api`: cookie, HSTS and alt-svc stores |
| BL-2508 | `Curl.Api`: WebSocket client with IAsyncEnumerable messages |
| BL-2509 | `Curl.Api`: concurrent transfers over the multi contract |
| BL-2510 | `Curl.Api`: CurlHttpMessageHandler maps HttpRequestMessage and HttpResponseMessage |
| BL-2511 | `Curl.Api`: CurlHttpMessageHandler HTTP version policy and proxy |
| BL-2512 | `Curl.Api`: CurlHttpMessageHandler decompression, redirects and authentication |
| BL-2513 | `Curl.Api`: usage README |
| BL-2514 | File phase 5 tasks: port tests/libtest and measure libcurl-api |

Phase 5 - libtest ports (30 tasks filed 2026-10-16 by BL-2514; the estimate was 40-45, corrected). Measured
against the vendored case files: 364 upstream cases run 259 distinct `lib` programs (112 name `libNNN`,
252 name `lib%TESTNUMBER`), not 362 programs; the 362 counted source files in `tests/libtest`, and a
source file no case runs needs no port. So ten programs per batch gives 26 batches, not about 36. The
harness is a new pair, `Curl.Conformance.LibTest.UnitLibrary` and `.UnitTests` (ports are C# `ILibTest`
classes over `Libcurl`, held to the quality gates), kept apart from the case runner in
`Curl.Conformance.UnitLibrary`. The batches share `PassingUpstreamCases.txt`, so they run one at a
time as a `depends-on` chain in test-number order.

| Task | Title |
| --- | --- |
| BL-2553 | Scaffold Curl.Conformance.LibTest: the ILibTest program shape and result codes |
| BL-2554 | Curl.Conformance.LibTest: first.c helpers over Libcurl (setopt, init and the TEST_ERR codes) |
| BL-2555 | Curl.Conformance.LibTest: multi, share and timing helpers (testutil, testtrace, wait helpers) |
| BL-2556 | Curl.Conformance: run a lib tool case through its ported ILibTest program |
| BL-2527 to BL-2552 | Port libtest programs in 26 batches of up to ten (lib500 to lib5004), each adding its passing cases to the ratchet |

Not filed from a lane: the gap office's two tasks (stop excluding `libcurl-api` cases and measure
them through the harness; have `gap-behaviour` group their failures). A lane's shell may not name
the gap office's folder (ADR-0433), so an interactive session files both with `-NoLane` and
`depends-on: [BL-2552]` (the second also on the first), and runs them after the last batch.
