namespace Curl.Kerberos;

/// <summary>
/// A credential cache and a keytab written by MIT Kerberos 1.22.1 (Ubuntu's
/// <c>krb5-user</c>, <c>krb5-kdc</c> and <c>krb5-admin-server</c> 1.22.1-2ubuntu4.1, run
/// from their extracted packages under WSL) against a throwaway local KDC for realm
/// <c>EXAMPLE.TEST</c> on 2026-09-28. Its keys protect nothing.
/// </summary>
/// <remarks>
/// Recorded with:
/// <code>
/// kdb5_util create -s -r EXAMPLE.TEST -P masterpw
/// kadmin.local -q "addprinc -pw alicepw alice"
/// kadmin.local -q "addprinc -randkey HTTP/server.example.test"
/// kadmin.local -q "ktadd -k http.keytab -e aes256-cts-hmac-sha1-96:normal,aes128-cts-hmac-sha1-96:normal HTTP/server.example.test"
/// krb5kdc -n &amp;
/// KRB5CCNAME=FILE:alice.ccache kinit -f alice        # password alicepw
/// KRB5CCNAME=FILE:alice.ccache kvno HTTP/server.example.test
/// base64 -w0 alice.ccache; base64 -w0 http.keytab
/// </code>
/// <c>klist -f -e</c> then showed <c>krbtgt/EXAMPLE.TEST@EXAMPLE.TEST</c> (flags <c>FI</c>)
/// and <c>HTTP/server.example.test@EXAMPLE.TEST</c> (flags <c>FT</c>), both
/// aes256-cts-hmac-sha1-96, valid 09/28/26 19:47:39 to 09/29/26 19:47:39 (UTC-7), and
/// <c>klist -k -t -e -K</c> showed KVNO 2 keys
/// <c>601fea01…4e6c</c> (aes256) and <c>4af9eed7…e677</c> (aes128).
/// </remarks>
internal static class RecordedKerberosFiles
{
    /// <summary>The cache <c>kinit -f alice</c> then <c>kvno HTTP/server.example.test</c> wrote.</summary>
    public static readonly byte[] AliceCredentialCache = Convert.FromBase64String(
        "BQQADAABAAgAAAAAAAAAAAAAAAEAAAABAAAADEVYQU1QTEUuVEVTVAAAAAVhbGljZQAAAAEAAAABAAAADEVYQU1QTEUuVEVTVAAAAAVhbGljZQAAAAEAAAADAAAADFgtQ0FDSEVDT05GOgAAABVrcmI1X2NjYWNoZV9jb25mX2RhdGEAAAAKZmFzdF9hdmFpbAAAACBrcmJ0Z3QvRVhBTVBMRS5URVNUQEVYQU1QTEUuVEVTVAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA3llcwAAAAAAAAABAAAAAQAAAAxFWEFNUExFLlRFU1QAAAAFYWxpY2UAAAACAAAAAgAAAAxFWEFNUExFLlRFU1QAAAAGa3JidGd0AAAADEVYQU1QTEUuVEVTVAASAAAAIOAm+okLwpD2hz8sI2bq0jt4AEQdd0elZo0c1iHTnnM/arsmy2q7JstqvHhLAAAAAABAQQAAAAAAAAAAAAAAAAGcYYIBmDCCAZSgAwIBBaEOGwxFWEFNUExFLlRFU1SiITAfoAMCAQKhGDAWGwZrcmJ0Z3QbDEVYQU1QTEUuVEVTVKOCAVgwggFUoAMCARKhAwIBAaKCAUYEggFCZxkJXXk8e71N2v0kZ8JgJeDzXhLN/6g51X03PBahZTWJz/C1Nn9+W9s7jZEL5ke5bOZmiumM9e4k7ssUaDFjdHyDSkx0dhpz+WbZ28GRXGEDs8R/EKPOTgzVpTgxwJJ5/5Cf59bCsk3vFTg7WEKr+hP4ZSOHQaHomtEvJfYe7Y9EEhp7RzeIaQzck/SyFpapLECH9YKs6VPQlif+ULT1ztdlIIfEXoAPQ4L9MPUs3u1wna1nUeCVPcwoIBdKhXXNbzOfdxfN4MRIgXSag0dUz6dMFIBcYE2gBsI8j3KxBMu2fdFhoPYNjaqk60iM9PkdHHmIRF2PUPVHFPTRt5mAlPlu7qRlJszQ8XPG/j0IXTMzNLAxTz6JQqQwPHG97vLkxeLIhc5qOplJ51OGqV7b/y0o3WvaNHl6ySnQlhkCpZnBBQAAAAAAAAABAAAAAQAAAAxFWEFNUExFLlRFU1QAAAAFYWxpY2UAAAABAAAAAgAAAAxFWEFNUExFLlRFU1QAAAAESFRUUAAAABNzZXJ2ZXIuZXhhbXBsZS50ZXN0ABIAAAAgb0+GUPOGaJPi71qvO0nuis2L769lWp4P7BwSvND5RuRquybLarsmy2q8eEsAAAAAAEAJAAAAAAAAAAAAAAAAAeZhggHiMIIB3qADAgEFoQ4bDEVYQU1QTEUuVEVTVKImMCSgAwIBAaEdMBsbBEhUVFAbE3NlcnZlci5leGFtcGxlLnRlc3SjggGdMIIBmaADAgESoQMCAQKiggGLBIIBhx8JCz0RSiTCTaQNdR64KC7qd8/srGH+C5GCFtKBDMgWRE4Rlq7/8z5DOjUTPEm67+c4e4mMSTdvKFHn27nrCOozvTNjFoMvWSSa00TmnWOD6T/zm8fe32BSLoYlRC/IZf+RQtvqrZd9HItlQPFZ2pI9raVQQMi1lNJXNjkF1nRd0MmwUQx1pK1lpp6kdANnGXyxM+UZV3lZBnD12menMe1msomK7faY7RcKwmsF/m9Ql4JKNOeVL03tKpc6Yk7vWh40bdjI7VvaRAlGocnmWivaB5eolpMCYjfXnKsLtbXP1cuWXifirmKtUTWfaOM8xa29pnGur2h5ze6Zy2+MnGZH/taaTgddJoVIy20RDtBRRSOOyZgWpZMeq9pxTh2wI6MqklvFcjfPXr1ePm2atGIj+5p5LWlWg6aW3AXrXhyd7ElpdkAjZn3LnfgBbuZd/CGtyvfZVScwST+8CkR1W8/6s3SjyaY14b9/2PXmRgjZC9dnHjN2s5xanJy4/Ni++Ra/NK1X/VUAAAAA");

    /// <summary>The keytab <c>kadmin.local ktadd</c> wrote for <c>HTTP/server.example.test</c>.</summary>
    public static readonly byte[] HttpServiceKeytab = Convert.FromBase64String(
        "BQIAAABcAAIADEVYQU1QTEUuVEVTVAAESFRUUAATc2VydmVyLmV4YW1wbGUudGVzdAAAAAFquybJAgASACBgH+oBL6vwOLpEnL3fVIvHHaN3YsEmQ/m4yS5l13VObAAAAAIAAABMAAIADEVYQU1QTEUuVEVTVAAESFRUUAATc2VydmVyLmV4YW1wbGUudGVzdAAAAAFquybJAgARABBK+e7XsYVA7/7ylEbD5OZ3AAAAAg==");
}
