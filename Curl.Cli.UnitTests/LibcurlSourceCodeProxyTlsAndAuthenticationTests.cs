namespace Curl.Cli;

/// <summary>
/// Pins the <c>curl_easy_setopt</c> lines <see cref="LibcurlSourceCode" /> writes for the proxy, TLS and
/// authentication options against curl 8.21.0 (mingw, Schannel), measured on 2026-10-01 with
/// <c>Record-CurlExchange.ps1 -NoServer</c> and <c>--libcurl - -s</c> (BL-654 Notes). Each row is one
/// measured command line; the transfer's lines are compared byte for byte.
/// </summary>
[TestClass]
public sealed class LibcurlSourceCodeProxyTlsAndAuthenticationTests
{
    private const string Url = "https://127.0.0.1:1/";
    private const string HttpUrl = "http://127.0.0.1:1/";

    private const string Start =
        "  curl_easy_setopt(curl, CURLOPT_BUFFERSIZE, 102400L);\n"
        + "  curl_easy_setopt(curl, CURLOPT_URL, \"https://127.0.0.1:1/\");\n"
        + "  curl_easy_setopt(curl, CURLOPT_NOPROGRESS, 1L);\n";

    private const string HttpStart =
        "  curl_easy_setopt(curl, CURLOPT_BUFFERSIZE, 102400L);\n"
        + "  curl_easy_setopt(curl, CURLOPT_URL, \"http://127.0.0.1:1/\");\n"
        + "  curl_easy_setopt(curl, CURLOPT_NOPROGRESS, 1L);\n";

    private const string Agent = "  curl_easy_setopt(curl, CURLOPT_USERAGENT, \"curl/8.21.0\");\n";
    private const string MaxRedirs = "  curl_easy_setopt(curl, CURLOPT_MAXREDIRS, 50L);\n";
    private const string Tls = "  curl_easy_setopt(curl, CURLOPT_SSLVERSION, (long)CURL_SSLVERSION_TLSv1_2);\n";
    private const string KeepAlive = "  curl_easy_setopt(curl, CURLOPT_TCP_KEEPALIVE, 1L);\n";
    private const string HeaderOpt = "  curl_easy_setopt(curl, CURLOPT_HEADEROPT, 1L);\n";
    private const string Plain = Agent + MaxRedirs + Tls + KeepAlive;
    private const string Proxy = "  curl_easy_setopt(curl, CURLOPT_PROXY, \"http://p:3128\");\n";
    private const string HttpsProxy = "  curl_easy_setopt(curl, CURLOPT_PROXY, \"https://p:3128\");\n";
    private const string Socks = "  curl_easy_setopt(curl, CURLOPT_PROXY, \"p:1080\");\n  curl_easy_setopt(curl, CURLOPT_PROXYTYPE, (long)CURLPROXY_SOCKS5);\n";
    private const string Credentials = "  curl_easy_setopt(curl, CURLOPT_USERPWD, \"a:b\");\n";
    private const string Indent = "                                           ";

    [TestMethod]
    [DataRow("-k", Start + Agent + MaxRedirs + "  curl_easy_setopt(curl, CURLOPT_SSL_VERIFYPEER, 0L);\n  curl_easy_setopt(curl, CURLOPT_SSL_VERIFYHOST, 0L);\n" + Tls + KeepAlive)]
    [DataRow("--cacert|ca.pem", Start + Agent + MaxRedirs + "  curl_easy_setopt(curl, CURLOPT_CAINFO, \"ca.pem\");\n" + Tls + KeepAlive)]
    [DataRow("--cert|c.pem", Start + Agent + MaxRedirs + "  curl_easy_setopt(curl, CURLOPT_SSLCERT, \"c.pem\");\n" + Tls + KeepAlive)]
    [DataRow("--cert|c.pem:pw", Start + Agent + MaxRedirs + "  curl_easy_setopt(curl, CURLOPT_KEYPASSWD, \"pw\");\n  curl_easy_setopt(curl, CURLOPT_SSLCERT, \"c.pem\");\n" + Tls + KeepAlive)]
    [DataRow("--cert|c.pem:pw|--pass|qq", Start + Agent + MaxRedirs + "  curl_easy_setopt(curl, CURLOPT_KEYPASSWD, \"qq\");\n  curl_easy_setopt(curl, CURLOPT_SSLCERT, \"c.pem\");\n" + Tls + KeepAlive)]
    [DataRow("--cert|C:/x/c.pem:pw", Start + Agent + MaxRedirs + "  curl_easy_setopt(curl, CURLOPT_KEYPASSWD, \"pw\");\n  curl_easy_setopt(curl, CURLOPT_SSLCERT, \"C:/x/c.pem\");\n" + Tls + KeepAlive)]
    [DataRow("--cert|a\\:b:pw", Start + Agent + MaxRedirs + "  curl_easy_setopt(curl, CURLOPT_KEYPASSWD, \"pw\");\n  curl_easy_setopt(curl, CURLOPT_SSLCERT, \"a:b\");\n" + Tls + KeepAlive)]
    [DataRow("--cert|a\\\\b:pw", Start + Agent + MaxRedirs + "  curl_easy_setopt(curl, CURLOPT_KEYPASSWD, \"pw\");\n  curl_easy_setopt(curl, CURLOPT_SSLCERT, \"a\\\\b\");\n" + Tls + KeepAlive)]
    [DataRow("--cert|pkcs11:a:b", Start + Agent + MaxRedirs + "  curl_easy_setopt(curl, CURLOPT_SSLCERT, \"pkcs11:a:b\");\n  curl_easy_setopt(curl, CURLOPT_SSLCERTTYPE, \"ENG\");\n" + Tls + KeepAlive)]
    [DataRow("--cert|c.pem:", Start + Agent + MaxRedirs + "  curl_easy_setopt(curl, CURLOPT_SSLCERT, \"c.pem\");\n" + Tls + KeepAlive)]
    [DataRow("--cert|a:b:c", Start + Agent + MaxRedirs + "  curl_easy_setopt(curl, CURLOPT_KEYPASSWD, \"b:c\");\n  curl_easy_setopt(curl, CURLOPT_SSLCERT, \"a\");\n" + Tls + KeepAlive)]
    [DataRow("--cert|C:", Start + Agent + MaxRedirs + "  curl_easy_setopt(curl, CURLOPT_SSLCERT, \"C\");\n" + Tls + KeepAlive)]
    [DataRow("--cert|1:/x", Start + Agent + MaxRedirs + "  curl_easy_setopt(curl, CURLOPT_KEYPASSWD, \"/x\");\n  curl_easy_setopt(curl, CURLOPT_SSLCERT, \"1\");\n" + Tls + KeepAlive)]
    [DataRow("--cert|a\\b", Start + Agent + MaxRedirs + "  curl_easy_setopt(curl, CURLOPT_SSLCERT, \"a\\\\b\");\n" + Tls + KeepAlive)]
    [DataRow("--cert|a\\", Start + Agent + MaxRedirs + "  curl_easy_setopt(curl, CURLOPT_SSLCERT, \"a\\\\\");\n" + Tls + KeepAlive)]
    [DataRow("--key|k.pem", Start + Agent + MaxRedirs + "  curl_easy_setopt(curl, CURLOPT_SSLKEY, \"k.pem\");\n" + Tls + KeepAlive)]
    [DataRow("--cert-type|DER", Start + Agent + MaxRedirs + "  curl_easy_setopt(curl, CURLOPT_SSLCERTTYPE, \"DER\");\n" + Tls + KeepAlive)]
    [DataRow("--key-type|DER", Start + Agent + MaxRedirs + "  curl_easy_setopt(curl, CURLOPT_SSLKEYTYPE, \"DER\");\n" + Tls + KeepAlive)]
    [DataRow("--pass|pw", Start + Agent + MaxRedirs + "  curl_easy_setopt(curl, CURLOPT_KEYPASSWD, \"pw\");\n" + Tls + KeepAlive)]
    [DataRow("--ciphers|ECDHE", Start + Agent + MaxRedirs + Tls + "  curl_easy_setopt(curl, CURLOPT_SSL_CIPHER_LIST, \"ECDHE\");\n" + KeepAlive)]
    [DataRow("--pinnedpubkey|sha256//abc=", Start + Agent + MaxRedirs + "  curl_easy_setopt(curl, CURLOPT_PINNEDPUBLICKEY, \"sha256//abc=\");\n" + Tls + KeepAlive)]
    [DataRow("--ssl-no-revoke", Start + Agent + MaxRedirs + Tls + "  curl_easy_setopt(curl, CURLOPT_SSL_OPTIONS, (long)CURLSSLOPT_NO_REVOKE);\n" + KeepAlive)]
    [DataRow("--ssl-revoke-best-effort", Start + Agent + MaxRedirs + Tls + "  curl_easy_setopt(curl, CURLOPT_SSL_OPTIONS, (long)CURLSSLOPT_REVOKE_BEST_EFFORT);\n" + KeepAlive)]
    [DataRow("--ssl-allow-beast", Start + Agent + MaxRedirs + Tls + "  curl_easy_setopt(curl, CURLOPT_SSL_OPTIONS, (long)CURLSSLOPT_ALLOW_BEAST);\n" + KeepAlive)]
    [DataRow("--ca-native", Start + Agent + MaxRedirs + Tls + "  curl_easy_setopt(curl, CURLOPT_SSL_OPTIONS, (long)CURLSSLOPT_NATIVE_CA);\n" + KeepAlive)]
    [DataRow("--ssl-auto-client-cert", Start + Agent + MaxRedirs + Tls + "  curl_easy_setopt(curl, CURLOPT_SSL_OPTIONS, (long)CURLSSLOPT_AUTO_CLIENT_CERT);\n" + KeepAlive)]
    [DataRow("--tls-earlydata", Start + Agent + MaxRedirs + Tls + "  curl_easy_setopt(curl, CURLOPT_SSL_OPTIONS, 64UL);\n" + KeepAlive)]
    [DataRow("--capath|cadir", Start + Plain)]
    [DataRow("--crlfile|crl.pem", Start + Plain)]
    [DataRow("--tls13-ciphers|TLS_AES_128_GCM_SHA256", Start + Plain)]
    [DataRow("--curves|X25519", Start + Plain)]
    [DataRow("--sigalgs|rsa_pss_rsae_sha256", Start + Plain)]
    [DataRow("--cert-status", Start + Plain)]
    [DataRow("--engine|foo", Start + Plain)]
    [DataRow("--no-sessionid", Start + Plain)]
    [DataRow("--tlsv1", Start + Agent + MaxRedirs + "  curl_easy_setopt(curl, CURLOPT_SSLVERSION, (long)CURL_SSLVERSION_TLSv1_0);\n" + KeepAlive)]
    [DataRow("--tlsv1.0", Start + Agent + MaxRedirs + "  curl_easy_setopt(curl, CURLOPT_SSLVERSION, (long)CURL_SSLVERSION_TLSv1_0);\n" + KeepAlive)]
    [DataRow("--tlsv1.1", Start + Agent + MaxRedirs + "  curl_easy_setopt(curl, CURLOPT_SSLVERSION, (long)CURL_SSLVERSION_TLSv1_1);\n" + KeepAlive)]
    [DataRow("--tlsv1.2", Start + Plain)]
    [DataRow("--tlsv1.3", Start + Agent + MaxRedirs + "  curl_easy_setopt(curl, CURLOPT_SSLVERSION, (long)CURL_SSLVERSION_TLSv1_3);\n" + KeepAlive)]
    [DataRow("--tls-max|1.2", Start + Agent + MaxRedirs + "  curl_easy_setopt(curl, CURLOPT_SSLVERSION, (long)(CURL_SSLVERSION_TLSv1_2 | CURL_SSLVERSION_MAX_TLSv1_2));\n" + KeepAlive)]
    [DataRow("--tls-max|1.3", Start + Agent + MaxRedirs + "  curl_easy_setopt(curl, CURLOPT_SSLVERSION, (long)(CURL_SSLVERSION_TLSv1_2 | CURL_SSLVERSION_MAX_TLSv1_3));\n" + KeepAlive)]
    [DataRow("--tlsv1.1|--tls-max|1.2", Start + Agent + MaxRedirs + "  curl_easy_setopt(curl, CURLOPT_SSLVERSION, (long)(CURL_SSLVERSION_TLSv1_1 | CURL_SSLVERSION_MAX_TLSv1_2));\n" + KeepAlive)]
    [DataRow("--tls-max|default", Start + Plain)]
    [DataRow("--no-alpn", Start + Agent + MaxRedirs + Tls + "  curl_easy_setopt(curl, CURLOPT_SSL_ENABLE_ALPN, 0L);\n" + KeepAlive)]
    public void Generate_TlsOption_WritesCurlsLines(string arguments, string transfer)
    {
        Assert.AreEqual(transfer, TransferLinesFor(arguments + "|" + Url));
    }

    [TestMethod]
    [DataRow("-x|http://p:3128", Start + Proxy + Agent + MaxRedirs + HeaderOpt + Tls + KeepAlive)]
    [DataRow("-x|http://p:3128|-U|u:p", Start + Proxy + "  curl_easy_setopt(curl, CURLOPT_PROXYUSERPWD, \"u:p\");\n" + Agent + MaxRedirs + HeaderOpt + Tls + KeepAlive)]
    [DataRow("--proxy-basic|-x|http://p:3128", Start + Proxy + "  curl_easy_setopt(curl, CURLOPT_PROXYAUTH, (long)CURLAUTH_BASIC);\n" + Agent + MaxRedirs + HeaderOpt + Tls + KeepAlive)]
    [DataRow("--proxy-digest|-x|http://p:3128", Start + Proxy + "  curl_easy_setopt(curl, CURLOPT_PROXYAUTH, (long)CURLAUTH_DIGEST);\n" + Agent + MaxRedirs + HeaderOpt + Tls + KeepAlive)]
    [DataRow("--proxy-ntlm|-x|http://p:3128", Start + Proxy + "  curl_easy_setopt(curl, CURLOPT_PROXYAUTH, (long)CURLAUTH_NTLM);\n" + Agent + MaxRedirs + HeaderOpt + Tls + KeepAlive)]
    [DataRow("--proxy-negotiate|-x|http://p:3128", Start + Proxy + "  curl_easy_setopt(curl, CURLOPT_PROXYAUTH, (long)CURLAUTH_GSSNEGOTIATE);\n" + Agent + MaxRedirs + HeaderOpt + Tls + KeepAlive)]
    [DataRow("--proxy-anyauth|-x|http://p:3128", Start + Proxy + "  curl_easy_setopt(curl, CURLOPT_PROXYAUTH, (long)CURLAUTH_ANY);\n" + Agent + MaxRedirs + HeaderOpt + Tls + KeepAlive)]
    [DataRow("--proxy-service-name|svc|-x|http://p:3128", Start + Proxy + "  curl_easy_setopt(curl, CURLOPT_PROXY_SERVICE_NAME, \"svc\");\n" + Agent + MaxRedirs + HeaderOpt + Tls + KeepAlive)]
    [DataRow("--noproxy|a.com,b.com", Start + "  curl_easy_setopt(curl, CURLOPT_NOPROXY, \"a.com,b.com\");\n" + Plain)]
    [DataRow("-p|-x|http://p:3128", Start + Proxy + "  curl_easy_setopt(curl, CURLOPT_HTTPPROXYTUNNEL, 1L);\n" + Agent + MaxRedirs + HeaderOpt + Tls + KeepAlive)]
    [DataRow("--socks4|p:1080", Start + "  curl_easy_setopt(curl, CURLOPT_PROXY, \"p:1080\");\n  curl_easy_setopt(curl, CURLOPT_PROXYTYPE, (long)CURLPROXY_SOCKS4);\n" + Agent + MaxRedirs + HeaderOpt + Tls + KeepAlive)]
    [DataRow("--socks4a|p:1080", Start + "  curl_easy_setopt(curl, CURLOPT_PROXY, \"p:1080\");\n  curl_easy_setopt(curl, CURLOPT_PROXYTYPE, (long)CURLPROXY_SOCKS4A);\n" + Agent + MaxRedirs + HeaderOpt + Tls + KeepAlive)]
    [DataRow("--socks5|p:1080", Start + Socks + Agent + MaxRedirs + HeaderOpt + Tls + KeepAlive)]
    [DataRow("--socks5-hostname|p:1080", Start + "  curl_easy_setopt(curl, CURLOPT_PROXY, \"p:1080\");\n  curl_easy_setopt(curl, CURLOPT_PROXYTYPE, (long)CURLPROXY_SOCKS5_HOSTNAME);\n" + Agent + MaxRedirs + HeaderOpt + Tls + KeepAlive)]
    [DataRow("--proxy1.0|p:3128", Start + "  curl_easy_setopt(curl, CURLOPT_PROXY, \"p:3128\");\n  curl_easy_setopt(curl, CURLOPT_PROXYTYPE, (long)CURLPROXY_HTTP_1_0);\n" + Agent + MaxRedirs + HeaderOpt + Tls + KeepAlive)]
    [DataRow("--proxy1.0|p:1|-x|q:2", Start + "  curl_easy_setopt(curl, CURLOPT_PROXY, \"q:2\");\n" + Agent + MaxRedirs + HeaderOpt + Tls + KeepAlive)]
    [DataRow("--preproxy|socks5://p:1080|-x|http://q:3128", Start + "  curl_easy_setopt(curl, CURLOPT_PROXY, \"http://q:3128\");\n  curl_easy_setopt(curl, CURLOPT_PRE_PROXY, \"socks5://p:1080\");\n" + Agent + MaxRedirs + HeaderOpt + Tls + KeepAlive)]
    [DataRow("--socks5-basic|--socks5|p:1080", Start + Socks + Agent + MaxRedirs + HeaderOpt + Tls + "  curl_easy_setopt(curl, CURLOPT_SOCKS5_AUTH, (long)CURLAUTH_BASIC);\n" + KeepAlive)]
    [DataRow("--socks5-gssapi|--socks5|p:1080", Start + Socks + Agent + MaxRedirs + HeaderOpt + Tls + "  curl_easy_setopt(curl, CURLOPT_SOCKS5_AUTH, (long)CURLAUTH_GSSNEGOTIATE);\n" + KeepAlive)]
    [DataRow("--socks5-gssapi-service|svc|--socks5|p:1080", Start + Socks + Agent + MaxRedirs + HeaderOpt + Tls + KeepAlive)]
    [DataRow("--socks5-gssapi-nec|--socks5|p:1080", Start + Socks + Agent + MaxRedirs + HeaderOpt + Tls + "  curl_easy_setopt(curl, CURLOPT_SOCKS5_GSSAPI_NEC, 1L);\n" + KeepAlive)]
    [DataRow("--haproxy-protocol", Start + "  curl_easy_setopt(curl, CURLOPT_HAPROXYPROTOCOL, 1L);\n" + Plain)]
    [DataRow("--haproxy-clientip|1.2.3.4", Start + "  curl_easy_setopt(curl, CURLOPT_HAPROXY_CLIENT_IP, \"1.2.3.4\");\n" + Plain)]
    [DataRow("--suppress-connect-headers|-x|http://p:3128", Start + Proxy + "  curl_easy_setopt(curl, CURLOPT_SUPPRESS_CONNECT_HEADERS, 1L);\n" + Agent + MaxRedirs + HeaderOpt + Tls + KeepAlive)]
    [DataRow("-x||--noproxy||--login-options||-p", Start + "  curl_easy_setopt(curl, CURLOPT_PROXY, \"\");\n  curl_easy_setopt(curl, CURLOPT_HTTPPROXYTUNNEL, 1L);\n  curl_easy_setopt(curl, CURLOPT_NOPROXY, \"\");\n  curl_easy_setopt(curl, CURLOPT_LOGIN_OPTIONS, \"\");\n" + Agent + MaxRedirs + HeaderOpt + Tls + KeepAlive)]
    [DataRow("--proxy-insecure|-x|https://p:3128", Start + HttpsProxy + Agent + MaxRedirs + HeaderOpt + "  curl_easy_setopt(curl, CURLOPT_PROXY_SSL_VERIFYPEER, 0L);\n  curl_easy_setopt(curl, CURLOPT_PROXY_SSL_VERIFYHOST, 0L);\n" + Tls + KeepAlive)]
    [DataRow("--proxy-cacert|ca.pem|-x|https://p:3128", Start + HttpsProxy + Agent + MaxRedirs + HeaderOpt + "  curl_easy_setopt(curl, CURLOPT_PROXY_CAINFO, \"ca.pem\");\n" + Tls + KeepAlive)]
    [DataRow("--proxy-capath|cad|-x|https://p:3128", Start + HttpsProxy + Agent + MaxRedirs + HeaderOpt + Tls + KeepAlive)]
    [DataRow("--proxy-crlfile|crl.pem|-x|https://p:3128", Start + HttpsProxy + Agent + MaxRedirs + HeaderOpt + Tls + KeepAlive)]
    [DataRow("--proxy-cert|c.pem|-x|https://p:3128", Start + HttpsProxy + Agent + MaxRedirs + HeaderOpt + "  curl_easy_setopt(curl, CURLOPT_PROXY_SSLCERT, \"c.pem\");\n" + Tls + KeepAlive)]
    [DataRow("--proxy-key|k.pem|-x|https://p:3128", Start + HttpsProxy + Agent + MaxRedirs + HeaderOpt + "  curl_easy_setopt(curl, CURLOPT_PROXY_SSLKEY, \"k.pem\");\n" + Tls + KeepAlive)]
    [DataRow("--proxy-cert-type|DER|-x|https://p:3128", Start + HttpsProxy + Agent + MaxRedirs + HeaderOpt + "  curl_easy_setopt(curl, CURLOPT_PROXY_SSLCERTTYPE, \"DER\");\n" + Tls + KeepAlive)]
    [DataRow("--proxy-key-type|DER|-x|https://p:3128", Start + HttpsProxy + Agent + MaxRedirs + HeaderOpt + "  curl_easy_setopt(curl, CURLOPT_PROXY_SSLKEYTYPE, \"DER\");\n" + Tls + KeepAlive)]
    [DataRow("--proxy-pass|pw|-x|https://p:3128", Start + HttpsProxy + Agent + MaxRedirs + HeaderOpt + "  curl_easy_setopt(curl, CURLOPT_PROXY_KEYPASSWD, \"pw\");\n" + Tls + KeepAlive)]
    [DataRow("--proxy-ciphers|ECDHE|-x|https://p:3128", Start + HttpsProxy + Agent + MaxRedirs + HeaderOpt + Tls + "  curl_easy_setopt(curl, CURLOPT_PROXY_SSL_CIPHER_LIST, \"ECDHE\");\n" + KeepAlive)]
    [DataRow("--proxy-tls13-ciphers|TLS_AES_128_GCM_SHA256|-x|https://p:3128", Start + HttpsProxy + Agent + MaxRedirs + HeaderOpt + Tls + KeepAlive)]
    [DataRow("--proxy-pinnedpubkey|sha256//abc=|-x|https://p:3128", Start + HttpsProxy + Agent + MaxRedirs + HeaderOpt + "  curl_easy_setopt(curl, CURLOPT_PROXY_PINNEDPUBLICKEY, \"sha256//abc=\");\n" + Tls + KeepAlive)]
    [DataRow("--proxy-ca-native|-x|https://p:3128", Start + HttpsProxy + Agent + MaxRedirs + HeaderOpt + Tls + "  curl_easy_setopt(curl, CURLOPT_PROXY_SSL_OPTIONS, (long)CURLSSLOPT_NATIVE_CA);\n" + KeepAlive)]
    [DataRow("--proxy-ssl-auto-client-cert|-x|https://p:3128", Start + HttpsProxy + Agent + MaxRedirs + HeaderOpt + Tls + "  curl_easy_setopt(curl, CURLOPT_PROXY_SSL_OPTIONS, (long)CURLSSLOPT_AUTO_CLIENT_CERT);\n" + KeepAlive)]
    [DataRow("--proxy-ssl-allow-beast|-x|https://p:3128", Start + HttpsProxy + Agent + MaxRedirs + HeaderOpt + Tls + "  curl_easy_setopt(curl, CURLOPT_PROXY_SSL_OPTIONS, (long)CURLSSLOPT_ALLOW_BEAST);\n" + KeepAlive)]
    [DataRow("--proxy-tlsv1|-x|https://p:3128", Start + HttpsProxy + Agent + MaxRedirs + HeaderOpt + Tls + "  curl_easy_setopt(curl, CURLOPT_PROXY_SSLVERSION, (long)CURL_SSLVERSION_TLSv1);\n" + KeepAlive)]
    [DataRow("--proxy-tlsv1", Start + Plain)]
    [DataRow("--proxy-cert|pc:pp|--proxy-key|pkcs11:x", Start + Agent + MaxRedirs + "  curl_easy_setopt(curl, CURLOPT_PROXY_KEYPASSWD, \"pp\");\n  curl_easy_setopt(curl, CURLOPT_PROXY_SSLCERT, \"pc\");\n  curl_easy_setopt(curl, CURLOPT_PROXY_SSLKEY, \"pkcs11:x\");\n  curl_easy_setopt(curl, CURLOPT_PROXY_SSLKEYTYPE, \"ENG\");\n" + Tls + KeepAlive)]
    public void Generate_ProxyOption_WritesCurlsLines(string arguments, string transfer)
    {
        Assert.AreEqual(transfer, TransferLinesFor(arguments + "|" + Url));
    }

    [TestMethod]
    [DataRow("--basic|-u|a:b", Start + Credentials + "  curl_easy_setopt(curl, CURLOPT_HTTPAUTH, (long)CURLAUTH_BASIC);\n" + Plain)]
    [DataRow("--digest|-u|a:b", Start + Credentials + "  curl_easy_setopt(curl, CURLOPT_HTTPAUTH, (long)CURLAUTH_DIGEST);\n" + Plain)]
    [DataRow("--ntlm|-u|a:b", Start + Credentials + "  curl_easy_setopt(curl, CURLOPT_HTTPAUTH, (long)CURLAUTH_NTLM);\n" + Plain)]
    [DataRow("--negotiate|-u|a:b", Start + Credentials + "  curl_easy_setopt(curl, CURLOPT_HTTPAUTH, (long)CURLAUTH_GSSNEGOTIATE);\n" + Plain)]
    [DataRow("--anyauth|-u|a:b", Start + Credentials + "  curl_easy_setopt(curl, CURLOPT_HTTPAUTH, (long)CURLAUTH_ANY);\n" + Plain)]
    [DataRow("--anyauth|--no-basic", Start + "  curl_easy_setopt(curl, CURLOPT_HTTPAUTH, (long)CURLAUTH_ANYSAFE);\n" + Plain)]
    [DataRow("--basic|--no-basic", Start + Plain)]
    [DataRow("--basic|--digest|--ntlm|--negotiate", Start + "  curl_easy_setopt(curl, CURLOPT_HTTPAUTH, (long)CURLAUTH_BASIC |\n" + Indent + "(long)CURLAUTH_DIGEST |\n" + Indent + "(long)CURLAUTH_GSSNEGOTIATE |\n" + Indent + "(long)CURLAUTH_NTLM);\n" + Plain)]
    [DataRow("--oauth2-bearer|tok", Start + "  curl_easy_setopt(curl, CURLOPT_XOAUTH2_BEARER, \"tok\");\n  curl_easy_setopt(curl, CURLOPT_HTTPAUTH, (long)CURLAUTH_NONE |\n" + Indent + "64UL);\n" + Plain)]
    [DataRow("--oauth2-bearer|t|--basic", Start + "  curl_easy_setopt(curl, CURLOPT_XOAUTH2_BEARER, \"t\");\n  curl_easy_setopt(curl, CURLOPT_HTTPAUTH, (long)CURLAUTH_BASIC |\n" + Indent + "(long)CURLAUTH_NONE |\n" + Indent + "64UL);\n" + Plain)]
    [DataRow("-u|a:b|--oauth2-bearer|t|--anyauth", Start + "  curl_easy_setopt(curl, CURLOPT_XOAUTH2_BEARER, \"t\");\n" + Credentials + "  curl_easy_setopt(curl, CURLOPT_HTTPAUTH, (long)CURLAUTH_ANY);\n" + Plain)]
    [DataRow("--aws-sigv4|aws:amz:us-east-1:s3|-u|a:b", Start + Credentials + "  curl_easy_setopt(curl, CURLOPT_HTTPAUTH, (long)CURLAUTH_NONE |\n" + Indent + "128UL);\n" + Agent + "  curl_easy_setopt(curl, CURLOPT_AWS_SIGV4, \"aws:amz:us-east-1:s3\");\n" + MaxRedirs + Tls + KeepAlive)]
    [DataRow("--delegation|always", Start + Plain + "  curl_easy_setopt(curl, CURLOPT_GSSAPI_DELEGATION, 2L);\n")]
    [DataRow("--delegation|policy", Start + Plain + "  curl_easy_setopt(curl, CURLOPT_GSSAPI_DELEGATION, 1L);\n")]
    [DataRow("--delegation|none", Start + Plain)]
    [DataRow("--service-name|svc", Start + Agent + MaxRedirs + Tls + "  curl_easy_setopt(curl, CURLOPT_SERVICE_NAME, \"svc\");\n" + KeepAlive)]
    [DataRow("-n", Start + "  curl_easy_setopt(curl, CURLOPT_NETRC, (long)CURL_NETRC_REQUIRED);\n" + Plain)]
    [DataRow("--netrc-optional", Start + "  curl_easy_setopt(curl, CURLOPT_NETRC, (long)CURL_NETRC_OPTIONAL);\n" + Plain)]
    [DataRow("--netrc-file|n.txt", Start + "  curl_easy_setopt(curl, CURLOPT_NETRC, (long)CURL_NETRC_REQUIRED);\n  curl_easy_setopt(curl, CURLOPT_NETRC_FILE, \"n.txt\");\n" + Plain)]
    [DataRow("--sasl-authzid|z", Start + Plain + "  curl_easy_setopt(curl, CURLOPT_SASL_AUTHZID, \"z\");\n")]
    [DataRow("--sasl-ir", Start + Plain + "  curl_easy_setopt(curl, CURLOPT_SASL_IR, 1L);\n")]
    [DataRow("--login-options|AUTH=*", Start + "  curl_easy_setopt(curl, CURLOPT_LOGIN_OPTIONS, \"AUTH=*\");\n" + Plain)]
    [DataRow("--location-trusted|--no-location|-u|a:b", Start + Credentials + Agent + "  curl_easy_setopt(curl, CURLOPT_UNRESTRICTED_AUTH, 1L);\n" + MaxRedirs + Tls + KeepAlive)]
    public void Generate_AuthenticationOption_WritesCurlsLines(string arguments, string transfer)
    {
        Assert.AreEqual(transfer, TransferLinesFor(arguments + "|" + Url));
    }

    [TestMethod]
    public void Generate_CombinedTlsProxyAndAuthenticationOptions_WritesEveryLineInCurlsOrder()
    {
        string arguments = "-k|--cacert|ca.pem|--capath|cad|--cert|c.pem:pw|--key|k.pem|--cert-type|DER|--key-type|DER|--ciphers|E|--pinnedpubkey|sha256//a|--ssl-no-revoke|--ca-native|--ssl-allow-beast|--ssl-auto-client-cert|--tls-earlydata|--tlsv1.3|--tls-max|1.3|--no-alpn"
            + "|-x|https://p:1|-U|u:p|--proxy-anyauth|--proxy-service-name|s|--noproxy|a|-p|--preproxy|socks5://q:2|--suppress-connect-headers|--proxy-header|X: 1|--proxy-insecure|--proxy-cacert|ca.pem|--proxy-cert|c.pem|--proxy-key|k.pem|--proxy-cert-type|DER|--proxy-key-type|DER|--proxy-pass|pw|--proxy-ciphers|E|--proxy-pinnedpubkey|sha256//b|--proxy-ca-native|--proxy-ssl-allow-beast|--proxy-ssl-auto-client-cert|--proxy-tlsv1"
            + "|-u|a:b|--digest|--delegation|policy|--service-name|s|--sasl-authzid|z|--sasl-ir|--login-options|L|--netrc-optional|--haproxy-protocol|--haproxy-clientip|1.2.3.4|--socks5-gssapi-nec|--socks5-basic|--socks5-gssapi|" + Url;
        string sslIndent = "                                              ";
        string proxySslIndent = "                                                    ";
        string expected = Start
            + "  curl_easy_setopt(curl, CURLOPT_PROXY, \"https://p:1\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_PROXYUSERPWD, \"u:p\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_HTTPPROXYTUNNEL, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_PRE_PROXY, \"socks5://q:2\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_PROXYAUTH, (long)CURLAUTH_ANY);\n"
            + "  curl_easy_setopt(curl, CURLOPT_NOPROXY, \"a\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_SUPPRESS_CONNECT_HEADERS, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_PROXY_SERVICE_NAME, \"s\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_HAPROXYPROTOCOL, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_HAPROXY_CLIENT_IP, \"1.2.3.4\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_NETRC, (long)CURL_NETRC_OPTIONAL);\n"
            + "  curl_easy_setopt(curl, CURLOPT_LOGIN_OPTIONS, \"L\");\n"
            + Credentials
            + "  curl_easy_setopt(curl, CURLOPT_HTTPAUTH, (long)CURLAUTH_DIGEST);\n"
            + Agent
            + "  curl_easy_setopt(curl, CURLOPT_PROXYHEADER, slist1);\n"
            + MaxRedirs
            + HeaderOpt
            + "  curl_easy_setopt(curl, CURLOPT_KEYPASSWD, \"pw\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_PROXY_KEYPASSWD, \"pw\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_CAINFO, \"ca.pem\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_PROXY_CAINFO, \"ca.pem\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_PINNEDPUBLICKEY, \"sha256//a\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_PROXY_PINNEDPUBLICKEY, \"sha256//b\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_SSLCERT, \"c.pem\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_PROXY_SSLCERT, \"c.pem\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_SSLCERTTYPE, \"DER\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_PROXY_SSLCERTTYPE, \"DER\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_SSLKEY, \"k.pem\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_PROXY_SSLKEY, \"k.pem\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_SSLKEYTYPE, \"DER\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_PROXY_SSLKEYTYPE, \"DER\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_SSL_VERIFYPEER, 0L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_SSL_VERIFYHOST, 0L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_PROXY_SSL_VERIFYPEER, 0L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_PROXY_SSL_VERIFYHOST, 0L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_SSLVERSION, (long)(CURL_SSLVERSION_TLSv1_3 | CURL_SSLVERSION_MAX_TLSv1_3));\n"
            + "  curl_easy_setopt(curl, CURLOPT_PROXY_SSLVERSION, (long)CURL_SSLVERSION_TLSv1);\n"
            + "  curl_easy_setopt(curl, CURLOPT_SSL_OPTIONS, (long)CURLSSLOPT_ALLOW_BEAST |\n"
            + sslIndent + "(long)CURLSSLOPT_NO_REVOKE |\n"
            + sslIndent + "(long)CURLSSLOPT_NATIVE_CA |\n"
            + sslIndent + "(long)CURLSSLOPT_AUTO_CLIENT_CERT |\n"
            + sslIndent + "64UL);\n"
            + "  curl_easy_setopt(curl, CURLOPT_PROXY_SSL_OPTIONS, (long)CURLSSLOPT_ALLOW_BEAST |\n"
            + proxySslIndent + "(long)CURLSSLOPT_NATIVE_CA |\n"
            + proxySslIndent + "(long)CURLSSLOPT_AUTO_CLIENT_CERT);\n"
            + "  curl_easy_setopt(curl, CURLOPT_SSL_CIPHER_LIST, \"E\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_PROXY_SSL_CIPHER_LIST, \"E\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_SSL_ENABLE_ALPN, 0L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_SOCKS5_GSSAPI_NEC, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_SOCKS5_AUTH, (long)CURLAUTH_BASIC |\n"
            + sslIndent + "(long)CURLAUTH_GSSNEGOTIATE);\n"
            + "  curl_easy_setopt(curl, CURLOPT_SERVICE_NAME, \"s\");\n"
            + KeepAlive
            + "  curl_easy_setopt(curl, CURLOPT_GSSAPI_DELEGATION, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_SASL_AUTHZID, \"z\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_SASL_IR, 1L);\n";

        Assert.AreEqual(expected, TransferLinesFor(arguments));
    }

    [TestMethod]
    public void Generate_ProxyAndAuthenticationAmongTheHttpOptions_PlacesEachWhereCurlDoes()
    {
        string arguments = "-f|-I|--oauth2-bearer|t|-x|http://p:1|-U|u:p|--haproxy-protocol|-n|--login-options|L|-u|a:b|-H|A: 1|-e|r|--aws-sigv4|aws|--proxy-header|X: 1|-L|--compressed|-b|c=1|-c|j|--negotiate|-X|PUT|--connect-timeout|2|-4|--resolve|h:1:1.1.1.1|" + Url;
        string expected = Start
            + "  curl_easy_setopt(curl, CURLOPT_NOBODY, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_XOAUTH2_BEARER, \"t\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_PROXY, \"http://p:1\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_PROXYUSERPWD, \"u:p\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_HAPROXYPROTOCOL, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_FAILONERROR, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_NETRC, (long)CURL_NETRC_REQUIRED);\n"
            + "  curl_easy_setopt(curl, CURLOPT_LOGIN_OPTIONS, \"L\");\n"
            + Credentials
            + "  curl_easy_setopt(curl, CURLOPT_HTTPAUTH, (long)CURLAUTH_GSSNEGOTIATE |\n" + Indent + "(long)CURLAUTH_NONE |\n" + Indent + "192UL);\n"
            + "  curl_easy_setopt(curl, CURLOPT_HTTPHEADER, slist1);\n"
            + "  curl_easy_setopt(curl, CURLOPT_REFERER, \"r\");\n"
            + Agent
            + "  curl_easy_setopt(curl, CURLOPT_FOLLOWLOCATION, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_AWS_SIGV4, \"aws\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_PROXYHEADER, slist2);\n"
            + MaxRedirs
            + "  curl_easy_setopt(curl, CURLOPT_ACCEPT_ENCODING, \"\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_COOKIE, \"c=1\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_COOKIEJAR, \"j\");\n"
            + HeaderOpt
            + Tls
            + "  curl_easy_setopt(curl, CURLOPT_FILETIME, 1L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_CUSTOMREQUEST, \"PUT\");\n"
            + "  curl_easy_setopt(curl, CURLOPT_CONNECTTIMEOUT_MS, 2000L);\n"
            + "  curl_easy_setopt(curl, CURLOPT_IPRESOLVE, 1L);\n"
            + KeepAlive
            + "  curl_easy_setopt(curl, CURLOPT_RESOLVE, slist3);\n";

        Assert.AreEqual(expected, TransferLinesFor(arguments));
    }

    [TestMethod]
    [DataRow("-x|http://p:1|--proxy-insecure|--proxy-cert|c|--proxy-ca-native|--proxy-tlsv1|--location-trusted|--aws-sigv4|a|-e|;auto|" + HttpUrl, HttpStart + "  curl_easy_setopt(curl, CURLOPT_PROXY, \"http://p:1\");\n  curl_easy_setopt(curl, CURLOPT_HTTPAUTH, (long)CURLAUTH_NONE |\n" + Indent + "128UL);\n" + Agent + "  curl_easy_setopt(curl, CURLOPT_FOLLOWLOCATION, 1L);\n  curl_easy_setopt(curl, CURLOPT_UNRESTRICTED_AUTH, 1L);\n  curl_easy_setopt(curl, CURLOPT_AWS_SIGV4, \"a\");\n  curl_easy_setopt(curl, CURLOPT_AUTOREFERER, 1L);\n" + MaxRedirs + "  curl_easy_setopt(curl, CURLOPT_PROXY_SSLCERT, \"c\");\n  curl_easy_setopt(curl, CURLOPT_PROXY_SSL_VERIFYPEER, 0L);\n  curl_easy_setopt(curl, CURLOPT_PROXY_SSL_VERIFYHOST, 0L);\n" + Tls + "  curl_easy_setopt(curl, CURLOPT_PROXY_SSLVERSION, (long)CURL_SSLVERSION_TLSv1);\n  curl_easy_setopt(curl, CURLOPT_PROXY_SSL_OPTIONS, (long)CURLSSLOPT_NATIVE_CA);\n" + KeepAlive)]
    [DataRow("-x|socks5h://p:1|" + HttpUrl, HttpStart + "  curl_easy_setopt(curl, CURLOPT_PROXY, \"socks5h://p:1\");\n" + Plain)]
    [DataRow("-x|http://p:1|-p|" + HttpUrl, HttpStart + "  curl_easy_setopt(curl, CURLOPT_PROXY, \"http://p:1\");\n  curl_easy_setopt(curl, CURLOPT_HTTPPROXYTUNNEL, 1L);\n" + Agent + MaxRedirs + HeaderOpt + Tls + KeepAlive)]
    [DataRow("--anyauth|--proxy-digest|-x|http://p:1|" + Url, Start + "  curl_easy_setopt(curl, CURLOPT_PROXY, \"http://p:1\");\n  curl_easy_setopt(curl, CURLOPT_PROXYAUTH, (long)CURLAUTH_DIGEST);\n  curl_easy_setopt(curl, CURLOPT_HTTPAUTH, (long)CURLAUTH_ANY);\n" + Agent + MaxRedirs + HeaderOpt + Tls + KeepAlive)]
    [DataRow("--proxy-basic|--proxy-negotiate|-x|p:1|" + HttpUrl, HttpStart + "  curl_easy_setopt(curl, CURLOPT_PROXY, \"p:1\");\n  curl_easy_setopt(curl, CURLOPT_PROXYAUTH, (long)CURLAUTH_GSSNEGOTIATE);\n" + Plain)]
    [DataRow("-R|--socks5-gssapi-nec|--service-name|s|-X|G|--delegation|none|" + HttpUrl, HttpStart + Agent + MaxRedirs + Tls + "  curl_easy_setopt(curl, CURLOPT_FILETIME, 1L);\n  curl_easy_setopt(curl, CURLOPT_CUSTOMREQUEST, \"G\");\n  curl_easy_setopt(curl, CURLOPT_SOCKS5_GSSAPI_NEC, 1L);\n  curl_easy_setopt(curl, CURLOPT_SERVICE_NAME, \"s\");\n" + KeepAlive)]
    [DataRow("--socks5-gssapi-nec|--service-name|s|--sasl-ir|--connect-timeout|2|-4|--resolve|h:1:1.1.1.1|--connect-to|h:1:h:2|-X|G|" + Url, Start + Agent + MaxRedirs + Tls + "  curl_easy_setopt(curl, CURLOPT_CUSTOMREQUEST, \"G\");\n  curl_easy_setopt(curl, CURLOPT_CONNECTTIMEOUT_MS, 2000L);\n  curl_easy_setopt(curl, CURLOPT_IPRESOLVE, 1L);\n  curl_easy_setopt(curl, CURLOPT_SOCKS5_GSSAPI_NEC, 1L);\n  curl_easy_setopt(curl, CURLOPT_SERVICE_NAME, \"s\");\n" + KeepAlive + "  curl_easy_setopt(curl, CURLOPT_RESOLVE, slist1);\n  curl_easy_setopt(curl, CURLOPT_CONNECT_TO, slist2);\n  curl_easy_setopt(curl, CURLOPT_SASL_IR, 1L);\n")]
    [DataRow("-k|--ssl-no-revoke|--ciphers|E|--proxy-ciphers|F|--no-alpn|-R|-X|G|--connect-timeout|2|-4|--socks5-gssapi-nec|--proxy-cert|pc:pp|--proxy-key|pkcs11:x|--socks4|p:1|-U|u:p|" + HttpUrl, HttpStart + "  curl_easy_setopt(curl, CURLOPT_PROXY, \"p:1\");\n  curl_easy_setopt(curl, CURLOPT_PROXYTYPE, (long)CURLPROXY_SOCKS4);\n  curl_easy_setopt(curl, CURLOPT_PROXYUSERPWD, \"u:p\");\n" + Agent + MaxRedirs + "  curl_easy_setopt(curl, CURLOPT_PROXY_KEYPASSWD, \"pp\");\n  curl_easy_setopt(curl, CURLOPT_PROXY_SSLCERT, \"pc\");\n  curl_easy_setopt(curl, CURLOPT_PROXY_SSLKEY, \"pkcs11:x\");\n  curl_easy_setopt(curl, CURLOPT_PROXY_SSLKEYTYPE, \"ENG\");\n  curl_easy_setopt(curl, CURLOPT_SSL_VERIFYPEER, 0L);\n  curl_easy_setopt(curl, CURLOPT_SSL_VERIFYHOST, 0L);\n" + Tls + "  curl_easy_setopt(curl, CURLOPT_SSL_OPTIONS, (long)CURLSSLOPT_NO_REVOKE);\n  curl_easy_setopt(curl, CURLOPT_SSL_CIPHER_LIST, \"E\");\n  curl_easy_setopt(curl, CURLOPT_PROXY_SSL_CIPHER_LIST, \"F\");\n  curl_easy_setopt(curl, CURLOPT_SSL_ENABLE_ALPN, 0L);\n  curl_easy_setopt(curl, CURLOPT_FILETIME, 1L);\n  curl_easy_setopt(curl, CURLOPT_CUSTOMREQUEST, \"G\");\n  curl_easy_setopt(curl, CURLOPT_CONNECTTIMEOUT_MS, 2000L);\n  curl_easy_setopt(curl, CURLOPT_IPRESOLVE, 1L);\n  curl_easy_setopt(curl, CURLOPT_SOCKS5_GSSAPI_NEC, 1L);\n" + KeepAlive)]
    [DataRow("-x|http://p:1|--proxy-header|X: 1|--oauth2-bearer|t|--ntlm|-u|a:b|-k|--cert|c|ftp://127.0.0.1:1/", "  curl_easy_setopt(curl, CURLOPT_BUFFERSIZE, 102400L);\n  curl_easy_setopt(curl, CURLOPT_URL, \"ftp://127.0.0.1:1/\");\n  curl_easy_setopt(curl, CURLOPT_NOPROGRESS, 1L);\n  curl_easy_setopt(curl, CURLOPT_XOAUTH2_BEARER, \"t\");\n  curl_easy_setopt(curl, CURLOPT_PROXY, \"http://p:1\");\n" + Credentials + "  curl_easy_setopt(curl, CURLOPT_HTTPAUTH, (long)CURLAUTH_NTLM |\n" + Indent + "(long)CURLAUTH_NONE |\n" + Indent + "64UL);\n" + Agent + "  curl_easy_setopt(curl, CURLOPT_FTP_SKIP_PASV_IP, 1L);\n  curl_easy_setopt(curl, CURLOPT_SSLCERT, \"c\");\n  curl_easy_setopt(curl, CURLOPT_SSL_VERIFYPEER, 0L);\n  curl_easy_setopt(curl, CURLOPT_SSL_VERIFYHOST, 0L);\n" + Tls + KeepAlive)]
    public void Generate_OptionsTogether_WriteCurlsLinesInCurlsOrder(string arguments, string transfer)
    {
        Assert.AreEqual(transfer, TransferLinesFor(arguments));
    }

    [TestMethod]
    public void Generate_ProxyHeaderOnAnFtpUrl_DeclaresNoList()
    {
        string source = GenerateFor("--cert|a\\\\b:pw|--proxy-header|X: 1|-x|http://p:1|ftp://127.0.0.1:1/");

        Assert.DoesNotContain("slist", source);
        Assert.Contains("  curl_easy_setopt(curl, CURLOPT_SSLCERT, \"a\\\\b\");\n", source);
    }

    [TestMethod]
    public void Generate_ProxyHeader_DeclaresFillsAndFreesItsList()
    {
        string source = GenerateFor("--proxy-header|X-A: 1|-x|http://p:3128|" + Url);

        Assert.Contains("  CURL *curl;\n  struct curl_slist *slist1;\n\n  slist1 = NULL;\n  slist1 = curl_slist_append(slist1, \"X-A: 1\");\n\n  curl = curl_easy_init();\n", source);
        Assert.Contains(Agent + "  curl_easy_setopt(curl, CURLOPT_PROXYHEADER, slist1);\n" + MaxRedirs + HeaderOpt, source);
        Assert.Contains("  curl = NULL;\n  curl_slist_free_all(slist1);\n  slist1 = NULL;\n", source);
    }

    private static string GenerateFor(string arguments)
    {
        string[] parts = arguments.Split('|');
        CommandLineOptions options = CommandLineParser.Parse(["-s", .. parts], _ => true).Options!;
        return LibcurlSourceCode.Generate([(options, parts[^1])]);
    }

    private static string TransferLinesFor(string arguments)
    {
        string source = GenerateFor(arguments);
        int start = source.IndexOf("  curl_easy_setopt(curl, CURLOPT_BUFFERSIZE", StringComparison.Ordinal);
        int end = source.IndexOf("\n  /* Here is a list", StringComparison.Ordinal);
        return source[start..end];
    }
}
