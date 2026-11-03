# STOMP requirement matrix: STOMP 1.2

Curl's `stomp://` and `stomps://` client (`Curl.Extended.Protocol.Stomp.UnitLibrary`, ADR-1059) measured against the
STOMP Protocol Specification, Version 1.2 (stomp.github.io/stomp-specification-1.2.html) through the real curl command
line (BL-4493). The cases in `Curl.Conformance.Extended.UnitTests/Stomp/` run curl against a `ScriptedReferenceServer`
whose frames are written from the specification: STOMP is text frames, so canned server frames are its reference
server, and no STOMP code is shared with the client. The specification has no numbered sections, so each ID is
`Stomp:STOMP12-<section>-<n>` with the section's name. Every MUST, MUST NOT, SHOULD and SHOULD NOT (and each MAY the
client relies on) that binds a client is a row; statements that bind only a server, and features curl does not use, are
`n/a`.

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| Stomp:STOMP12-Frames-1 | MUST | A frame is a command, an EOL, `name:value` header lines each ended by an EOL, a blank line, the body and a NUL octet. | StompConnectCases.Publish_NoCredentials_SendsConnectSendAndDisconnectAndEndsOnTheReceipt |
| Stomp:STOMP12-Frames-2 | MAY | An EOL is LF or CR LF; a client reads frames whose EOLs are CR LF. | StompSubscribeCases.Subscribe_CrLfFrames_AreRead |
| Stomp:STOMP12-Frames-3 | MUST NOT | Only SEND, MESSAGE and ERROR frames have a body; CONNECT, SUBSCRIBE and DISCONNECT carry none. | StompConnectCases.Publish_NoCredentials_SendsConnectSendAndDisconnectAndEndsOnTheReceipt |
| Stomp:STOMP12-Frames-4 | MAY | The NUL ending a frame may be followed by any number of EOLs (heart-beats); the reader skips them. | StompSubscribeCases.Subscribe_HeartBeatEolsBetweenFrames_AreSkipped |
| Stomp:STOMP12-Frames-5 | MUST | A frame is read however the stream splits it across reads. | StompSubscribeCases.Subscribe_FramesSplitAcrossReads_AreReassembled |
| Stomp:STOMP12-Encoding-1 | MUST | Commands and headers are encoded in UTF-8. | StompSendCases.Publish_NonAsciiDestination_IsSentAsUtf8 |
| Stomp:STOMP12-Encoding-2 | MUST | In frames other than CONNECT and CONNECTED, CR, LF, colon and backslash in a header are sent as `\r`, `\n`, `\c` and `\\`. | StompSendCases.Publish_DestinationWithColonAndBackslash_IsEscaped |
| Stomp:STOMP12-Encoding-3 | MUST | A received header outside CONNECTED is unescaped. | StompSubscribeCases.Subscribe_ErrorWithEscapedMessage_IsUnescaped |
| Stomp:STOMP12-Encoding-4 | MUST NOT | CONNECT and CONNECTED headers are not escaped or unescaped. | StompConnectCases.Publish_CredentialsWithBackslashAndColon_AreSentUnescapedAsLoginAndPasscode |
| Stomp:STOMP12-Encoding-5 | MUST | An undefined escape sequence such as `\t` is a fatal protocol error. | StompSubscribeCases.Subscribe_UndefinedEscapeSequence_IsAFatalProtocolError |
| Stomp:STOMP12-Repeated-1 | SHOULD | Of repeated header entries only the first is used. | StompSubscribeCases.Subscribe_RepeatedContentLength_UsesTheFirst |
| Stomp:STOMP12-Size-1 | MAY | A server may limit the frame, header count, header line and body sizes. | n/a: limits a server sets; curl sends one small frame of each kind and sets no limit of its own |
| Stomp:STOMP12-Lingering-1 | SHOULD | A server lingers before closing after an ERROR so the client can read it. | n/a: the server's; the client reads the ERROR before the close (Error-2) |
| Stomp:STOMP12-Connect-1 | MUST | A 1.2 client's CONNECT carries `accept-version`. | StompConnectCases.Publish_NoCredentials_SendsConnectSendAndDisconnectAndEndsOnTheReceipt |
| Stomp:STOMP12-Connect-2 | MUST | A 1.2 client's CONNECT carries `host`, the virtual host it connects to. | StompConnectCases.Publish_CredentialsWithBackslashAndColon_AreSentUnescapedAsLoginAndPasscode |
| Stomp:STOMP12-Connect-3 | MAY | CONNECT carries `login` and `passcode` for a secured server. | StompConnectCases.Publish_CredentialsWithBackslashAndColon_AreSentUnescapedAsLoginAndPasscode |
| Stomp:STOMP12-Connect-4 | MAY | CONNECT carries `heart-beat`; `0,0` offers and asks for no heart-beating. | StompConnectCases.Publish_NoCredentials_SendsConnectSendAndDisconnectAndEndsOnTheReceipt |
| Stomp:STOMP12-Connect-5 | MUST | The client sends no further frame until the server answers CONNECT with CONNECTED. | StompConnectCases.Publish_ErrorAtConnect_FailsWithItsMessageAndSendsNothingMore |
| Stomp:STOMP12-Connect-6 | MAY | A client may send STOMP in place of CONNECT. | n/a: curl sends CONNECT, which every 1.2 server accepts |
| Stomp:STOMP12-Negotiation-1 | MUST | CONNECTED's `version` is the version the session speaks, 1.0 when absent; curl speaks only 1.2 and fails otherwise. | StompConnectCases.Subscribe_ServerChoosesVersion11_FailsBeforeSubscribing |
| Stomp:STOMP12-Negotiation-2 | MUST | A server sharing no version with the client answers ERROR and closes; the client fails. | StompConnectCases.Publish_ErrorAtConnect_FailsWithItsMessageAndSendsNothingMore |
| Stomp:STOMP12-HeartBeat-1 | MUST | A side that agreed to send heart-beats sends an EOL when it has no frame to send. | n/a: curl offers `0,0`, so no heart-beating is agreed |
| Stomp:STOMP12-HeartBeat-2 | MAY | A side that agreed to receive heart-beats may treat a silent connection as dead. | n/a: curl offers `0,0`; `-m` bounds the transfer instead |
| Stomp:STOMP12-Send-1 | REQUIRED | SEND carries `destination`. | StompConnectCases.Publish_NoCredentials_SendsConnectSendAndDisconnectAndEndsOnTheReceipt |
| Stomp:STOMP12-Send-2 | MAY | SEND carries `transaction` inside a transaction. | n/a: curl starts no transaction |
| Stomp:STOMP12-ContentLength-1 | SHOULD | A frame with a body carries `content-length`, its octet count. | StompConnectCases.Publish_NoCredentials_SendsConnectSendAndDisconnectAndEndsOnTheReceipt |
| Stomp:STOMP12-ContentLength-2 | MUST | With `content-length`, that many octets are the body, NUL octets among them. | StompSendCases.Publish_BodyHoldingNul_SendsItsWholeOctetCountAsContentLength |
| Stomp:STOMP12-ContentLength-3 | MUST | Without `content-length`, the body ends at the first NUL. | StompSubscribeCases.Subscribe_MessagesWithAndWithoutContentLength_AreWrittenUntilTheServerCloses |
| Stomp:STOMP12-ContentType-1 | SHOULD | A frame with a body carries `content-type`. | StompSendCases.Publish_Body_SendsAContentType |
| Stomp:STOMP12-Subscribe-1 | REQUIRED | SUBSCRIBE carries `destination`. | StompSubscribeCases.Subscribe_MessagesWithAndWithoutContentLength_AreWrittenUntilTheServerCloses |
| Stomp:STOMP12-Subscribe-2 | MUST | SUBSCRIBE carries an `id` unique on the connection. | StompSubscribeCases.Subscribe_MessagesWithAndWithoutContentLength_AreWrittenUntilTheServerCloses |
| Stomp:STOMP12-Subscribe-3 | MAY | SUBSCRIBE's `ack` is `auto`, `client` or `client-individual`, `auto` when absent. | StompSubscribeCases.Subscribe_MessagesWithAndWithoutContentLength_AreWrittenUntilTheServerCloses |
| Stomp:STOMP12-Unsubscribe-1 | REQUIRED | UNSUBSCRIBE carries the subscription's `id`. | n/a: curl reads until the server closes and never unsubscribes |
| Stomp:STOMP12-Ack-1 | MUST | Under `client` or `client-individual` the client ACKs or NACKs each MESSAGE by its `ack` header. | n/a: curl subscribes with `ack:auto`, so it never acknowledges |
| Stomp:STOMP12-Transaction-1 | REQUIRED | BEGIN, COMMIT and ABORT carry `transaction`. | n/a: curl starts no transaction |
| Stomp:STOMP12-Message-1 | MUST | A MESSAGE carries `destination`, `message-id` and `subscription`; the client delivers its body. | StompSubscribeCases.Subscribe_MessagesWithAndWithoutContentLength_AreWrittenUntilTheServerCloses |
| Stomp:STOMP12-Disconnect-1 | SHOULD | For a graceful close the client sends DISCONNECT with `receipt` and waits for the RECEIPT before closing. | StompConnectCases.Publish_NoCredentials_SendsConnectSendAndDisconnectAndEndsOnTheReceipt |
| Stomp:STOMP12-Disconnect-2 | MUST NOT | The client sends no frame after DISCONNECT. | StompConnectCases.Publish_NoCredentials_SendsConnectSendAndDisconnectAndEndsOnTheReceipt |
| Stomp:STOMP12-Receipt-1 | MUST | A RECEIPT's `receipt-id` is the `receipt` of the frame it answers. | StompSendCases.Publish_ReceiptForAnotherId_IsAnUnexpectedFrame |
| Stomp:STOMP12-Receipt-2 | MUST NOT | Without the RECEIPT the client does not assume the server processed its frames. | StompSendCases.Publish_ServerClosesWithoutReceipt_FailsAsDisconnected |
| Stomp:STOMP12-Error-1 | SHOULD | An ERROR carries a short `message`; the client reports it. | StompConnectCases.Publish_ErrorAtConnect_FailsWithItsMessageAndSendsNothingMore |
| Stomp:STOMP12-Error-2 | MUST | A server closes the connection after an ERROR; the client treats ERROR as the end of the session. | StompConnectCases.Publish_ErrorAtConnect_FailsWithItsMessageAndSendsNothingMore |
| Stomp:STOMP12-Transport-1 | MAY | STOMP runs over any reliable two-way stream; `stomps://` is TLS from the first byte on port 61614. | StompConnectCases.Publish_Stomps_SpeaksTlsFromTheFirstByteOnPort61614 |
