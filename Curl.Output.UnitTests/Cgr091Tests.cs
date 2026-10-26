using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Testing;

namespace Curl.Output;

/// <summary>
/// Pins CGR-091: <c>-w %{certs}</c> in the OpenSSL build's layout (<c>ossl_certchain</c>),
/// as curl 8.18.0 with OpenSSL 3.5.5 printed it on Linux on 2026-10-26 for
/// <c>curl -s -k -o /dev/null -w '%{certs}' https://localhost:&lt;p&gt;/index.txt</c> against
/// <c>openssl s_server -WWW</c> with each certificate below (BL-3620's Notes). The
/// crafted certificates' records follow OpenSSL 3's printers.
/// </summary>
[TestClass]
public sealed class Cgr091Tests
{
    // openssl req -x509 -newkey rsa:2048 -subj /CN=localhost -addext subjectAltName=DNS:localhost,IP:127.0.0.1
    // -addext keyUsage=critical,digitalSignature,keyEncipherment -addext extendedKeyUsage=serverAuth,clientAuth
    private const string SelfSigned =
        "MIIDVjCCAj6gAwIBAgIUVWzbTyCHHOhzBWucRWmgWdUri0EwDQYJKoZIhvcNAQELBQAwFDESMBAGA1UEAwwJbG9jYWxob3N0MB4XDTI2MTAyNjIwMTMxOVoXDTI2MTEyNTIwMTMxOVowFDESMBAGA1UEAwwJbG9jYWxob3N0MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAricJbImUqNV9gAVPztJEbGlQ358RDoVU9rAD8otVCHBDlCce3smkpAZ2Pn5offHGNlALcXOfqWtLfbR5bL/WprtzwLbqSwV7sn8GCq+bM+k+xvtkK0dMIoinHzvexb55mteq4aU5T4zDK34SnzdRh4csk2uBu2RO/SuoFFZWIiXNUaMOFcgMCjlI0WM/MkvvOtSRWFY5eYhTth5NKA4xyeLBZ6ZjwH84WZj0y+T2OOIOSnoSZOu+/yVDOo5Z2LF7rxn+TMq5/fId16+Efhx9j6on8T4JOhyul8CgIqR7PiNsGap4qjLT+xi0v/LsiuEmoSxM3mTVS3/yPhd+A/Z9iwIDAQABo4GfMIGcMB0GA1UdDgQWBBQJeo7amzdulX1nmdRZNEvo73YseTAfBgNVHSMEGDAWgBQJeo7amzdulX1nmdRZNEvo73YseTAPBgNVHRMBAf8EBTADAQH/MBoGA1UdEQQTMBGCCWxvY2FsaG9zdIcEfwAAATAOBgNVHQ8BAf8EBAMCBaAwHQYDVR0lBBYwFAYIKwYBBQUHAwEGCCsGAQUFBwMCMA0GCSqGSIb3DQEBCwUAA4IBAQCkoQGk1kEbE3ahH10hkr5/yGoWuXUBGRVvNiQwzpV7S8AfXD/ZVDpG/WauQchVaBChDy/joyykEAp3kLUk89X3I99mRCWFtuu5c0RRaNe+8UHuWVUr8gRzV9DbAB7Sm4KfY4XI7zwaL3HqoseYmiznJtuAOEyCZaaShDbTsQd9deePF/HKjS9grVCs6k0iALUT/KoemEI1O0OHlK+nO1MzR66LyaowzEzSc1Uxb2TMPSIbaiFvlNZ+NBOeuRTYL2wxDRE174kZlqi/N+bFd0KqwmmsoYd0XDSV+piCsM7lZ0CRX/OL97vRfyhL7HqEEccEnnSd51ASJHH6gVwhmK4B";

    // A leaf signed by /C=US/O=Test CA/CN=Root with serial 0x0080, authorityKeyIdentifier=keyid,issuer:always,
    // an IPv6, e-mail and URI subjectAltName, the custom extension 1.2.3.4=ASN1:UTF8String:hi there and
    // every other key usage.
    private const string CaSigned =
        "MIIECDCCAvCgAwIBAgICAIAwDQYJKoZIhvcNAQELBQAwLjELMAkGA1UEBhMCVVMxEDAOBgNVBAoMB1Rlc3QgQ0ExDTALBgNVBAMMBFJvb3QwHhcNMjYxMDI2MjAxOTI0WhcNMjYxMTI1MjAxOTI0WjAoMRIwEAYDVQQDDAlsZWFmLnRlc3QxEjAQBgNVBAoMCUFjbWUsIEluYzCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAO+4X3MNI6hSDPt6Pl6mi8JfHuyG3yYtGhLPGkaPUgpUQeXOVNe7HotrYAUytCbqfPpjnaKr65SrD+BcSZIMgDIPk7NMPWAQYYzNb7cS1hD2M1HwP3fkLnsIoJQAjHXiX4HeA2dVqAF2bCCUoJezgsBFYvekmDMxBduUx4BLZdCJnkmxqrJX9+J95ykHpjfyolZYIHOA+wSVXXWrhksTKjNSjNDORCcGPdjuYpTAhArZ1PA52kuXh0qvE54nzyrO/EH6QmaaV5cW8+Lg6UcjRFf738Vz7ZHt8iZpWJRKxpk4aLM0EN9nAeaWwaBVYFsgVmQZaZaT9ocEKKg+AnHwb4UCAwEAAaOCATQwggEwMAkGA1UdEwQCMAAwaQYDVR0jBGIwYIAUVqJj+CyfCqbEvZun6qRCm8dApLWhMqQwMC4xCzAJBgNVBAYTAlVTMRAwDgYDVQQKDAdUZXN0IENBMQ0wCwYDVQQDDARSb290ghRUQx3jaa9lRQM3M27zgXWiJktZ2DBABgNVHREEOTA3gglsZWFmLnRlc3SHEAAAAAAAAAAAAAAAAAAAAAGBCGFAYi50ZXN0hg5odHRwOi8veC50ZXN0LzARBgMqAwQECgwIaGkgdGhlcmUwDAYDVR0PBAUDAwdfgDA2BgNVHSUELzAtBggrBgEFBQcDAwYIKwYBBQUHAwQGCCsGAQUFBwMIBggrBgEFBQcDCQYDKgMFMB0GA1UdDgQWBBQ6RyXqdqz4bE+Uzexy3feDTg1NczANBgkqhkiG9w0BAQsFAAOCAQEAb/o79SPjEi8tfra7qC1u46X4yJD71d7TNEMoYXqS7wtAnWvy84s5S5JfEsj76l3zSjKSqUYSVnbYNxX9374HArtI4slr6iUdmhxs7hQ54J3RUZcNrg4vGSIU+tCxXrXGem97ZycTne4fxFOaJWveJlamjnXf9Pumvgpnn/e+3eG7lNvlM0IbIRBD5nyzzjy45gO6Y2kOoMASQTyS88ZuwXF4oUvYOKi1r1zzbzt7ExJvQlmn7XSliQog4QBvZyUcw0MKwgjpUJVwa4ICMnd+Tb9aFqUvACUmgHtL+Q6SOJ0r9I9vzPHID/AsAQgj202oPtdQXLn0bX/V0ECtDPC4Yg==";

    private const string SelfSignedRecords =
        "Subject:CN = localhost\n" +
        "Issuer:CN = localhost\n" +
        "Version:2\n" +
        "Serial Number:556cdb4f20871ce873056b9c4569a059d52b8b41\n" +
        "Signature Algorithm:sha256WithRSAEncryption\n" +
        "Public Key Algorithm:rsaEncryption\n" +
        "X509v3 Subject Key Identifier:09:7A:8E:DA:9B:37:6E:95:7D:67:99:D4:59:34:4B:E8:EF:76:2C:79\n" +
        "X509v3 Authority Key Identifier:09:7A:8E:DA:9B:37:6E:95:7D:67:99:D4:59:34:4B:E8:EF:76:2C:79\n" +
        "X509v3 Basic Constraints:CA:TRUE\n" +
        "X509v3 Subject Alternative Name:DNS:localhost, IP Address:127.0.0.1\n" +
        "X509v3 Key Usage:Digital Signature, Key Encipherment\n" +
        "X509v3 Extended Key Usage:TLS Web Server Authentication, TLS Web Client Authentication\n" +
        "Start date:Oct 26 20:13:19 2026 GMT\n" +
        "Expire date:Nov 25 20:13:19 2026 GMT\n" +
        "RSA Public Key:2048\n" +
        "rsa(n):AE27096C8994A8D57D80054FCED2446C6950DF9F110E8554F6B003F28B5508704394271EDEC9A4A406763E7E687DF1C636500B71739FA96B4B7DB4796CBFD6A6BB73C0B6EA4B057BB27F060AAF9B33E93EC6FB642B474C2288A71F3BDEC5BE799AD7AAE1A5394F8CC32B7E129F375187872C936B81BB644EFD2BA81456562225CD51A30E15C80C0A3948D1633F324BEF3AD491585639798853B61E4D280E31C9E2C167A663C07F385998F4CBE4F638E20E4A7A1264EBBEFF25433A8E59D8B17BAF19FE4CCAB9FDF21DD7AF847E1C7D8FAA27F13E093A1CAE97C0A022A47B3E236C19AA78AA32D3FB18B4BFF2EC8AE126A12C4CDE64D54B7FF23E177E03F67D8B\n" +
        "rsa(e):10001\n" +
        "Signature:a4:a1:01:a4:d6:41:1b:13:76:a1:1f:5d:21:92:be:7f:c8:6a:16:b9:75:01:19:15:6f:36:24:30:ce:95:7b:4b:c0:1f:5c:3f:d9:54:3a:46:fd:66:ae:41:c8:55:68:10:a1:0f:2f:e3:a3:2c:a4:10:0a:77:90:b5:24:f3:d5:f7:23:df:66:44:25:85:b6:eb:b9:73:44:51:68:d7:be:f1:41:ee:59:55:2b:f2:04:73:57:d0:db:00:1e:d2:9b:82:9f:63:85:c8:ef:3c:1a:2f:71:ea:a2:c7:98:9a:2c:e7:26:db:80:38:4c:82:65:a6:92:84:36:d3:b1:07:7d:75:e7:8f:17:f1:ca:8d:2f:60:ad:50:ac:ea:4d:22:00:b5:13:fc:aa:1e:98:42:35:3b:43:87:94:af:a7:3b:53:33:47:ae:8b:c9:aa:30:cc:4c:d2:73:55:31:6f:64:cc:3d:22:1b:6a:21:6f:94:d6:7e:34:13:9e:b9:14:d8:2f:6c:31:0d:11:35:ef:89:19:96:a8:bf:37:e6:c5:77:42:aa:c2:69:ac:a1:87:74:5c:34:95:fa:98:82:b0:ce:e5:67:40:91:5f:f3:8b:f7:bb:d1:7f:28:4b:ec:7a:84:11:c7:04:9e:74:9d:e7:50:12:24:71:fa:81:5c:21:98:ae:01:\n";

    private const string CaSignedRecords =
        "Subject:CN = leaf.test, O = \"Acme, Inc\"\n" +
        "Issuer:C = US, O = Test CA, CN = Root\n" +
        "Version:2\n" +
        "Serial Number:80\n" +
        "Signature Algorithm:sha256WithRSAEncryption\n" +
        "Public Key Algorithm:rsaEncryption\n" +
        "X509v3 Basic Constraints:CA:FALSE\n" +
        "X509v3 Authority Key Identifier:keyid:56:A2:63:F8:2C:9F:0A:A6:C4:BD:9B:A7:EA:A4:42:9B:C7:40:A4:B5\n" +
        "DirName:/C=US/O=Test CA/CN=Root\n" +
        "serial:54:43:1D:E3:69:AF:65:45:03:37:33:6E:F3:81:75:A2:26:4B:59:D8\n" +
        "X509v3 Subject Alternative Name:DNS:leaf.test, IP Address:0:0:0:0:0:0:0:1, email:a@b.test, URI:http://x.test/\n" +
        "1.2.3.4:..hi there\n" +
        "X509v3 Key Usage:Non Repudiation, Data Encipherment, Key Agreement, Certificate Sign, CRL Sign, Encipher Only, Decipher Only\n" +
        "X509v3 Extended Key Usage:Code Signing, E-mail Protection, Time Stamping, OCSP Signing, 1.2.3.5\n" +
        "X509v3 Subject Key Identifier:3A:47:25:EA:76:AC:F8:6C:4F:94:CD:EC:72:DD:F7:83:4E:0D:4D:73\n" +
        "Start date:Oct 26 20:19:24 2026 GMT\n" +
        "Expire date:Nov 25 20:19:24 2026 GMT\n" +
        "RSA Public Key:2048\n" +
        "rsa(n):EFB85F730D23A8520CFB7A3E5EA68BC25F1EEC86DF262D1A12CF1A468F520A5441E5CE54D7BB1E8B6B600532B426EA7CFA639DA2ABEB94AB0FE05C49920C80320F93B34C3D6010618CCD6FB712D610F63351F03F77E42E7B08A094008C75E25F81DE036755A801766C2094A097B382C04562F7A498333105DB94C7804B65D0899E49B1AAB257F7E27DE72907A637F2A25658207380FB04955D75AB864B132A33528CD0CE4427063DD8EE6294C0840AD9D4F039DA4B97874AAF139E27CF2ACEFC41FA42669A579716F3E2E0E947234457FBDFC573ED91EDF2266958944AC6993868B33410DF6701E696C1A055605B20566419699693F6870428A83E0271F06F85\n" +
        "rsa(e):10001\n" +
        "Signature:6f:fa:3b:f5:23:e3:12:2f:2d:7e:b6:bb:a8:2d:6e:e3:a5:f8:c8:90:fb:d5:de:d3:34:43:28:61:7a:92:ef:0b:40:9d:6b:f2:f3:8b:39:4b:92:5f:12:c8:fb:ea:5d:f3:4a:32:92:a9:46:12:56:76:d8:37:15:fd:df:be:07:02:bb:48:e2:c9:6b:ea:25:1d:9a:1c:6c:ee:14:39:e0:9d:d1:51:97:0d:ae:0e:2f:19:22:14:fa:d0:b1:5e:b5:c6:7a:6f:7b:67:27:13:9d:ee:1f:c4:53:9a:25:6b:de:26:56:a6:8e:75:df:f4:fb:a6:be:0a:67:9f:f7:be:dd:e1:bb:94:db:e5:33:42:1b:21:10:43:e6:7c:b3:ce:3c:b8:e6:03:ba:63:69:0e:a0:c0:12:41:3c:92:f3:c6:6e:c1:71:78:a1:4b:d8:38:a8:b5:af:5c:f3:6f:3b:7b:13:12:6f:42:59:a7:ed:74:a5:89:0a:20:e1:00:6f:67:25:1c:c3:43:0a:c2:08:e9:50:95:70:6b:82:02:32:77:7e:4d:bf:5a:16:a5:2f:00:25:26:80:7b:4b:f9:0e:92:38:9d:2b:f4:8f:6f:cc:f1:c8:0f:f0:2c:01:08:23:db:4d:a8:3e:d7:50:5c:b9:f4:6d:7f:d5:d0:40:ad:0c:f0:b8:62:\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(SelfSigned, SelfSignedRecords, DisplayName = "self-signed localhost")]
    [DataRow(CaSigned, CaSignedRecords, DisplayName = "CA-signed leaf")]
    public void Format_OpenSsl_PrintsTheRecordsOfCurlsOpenSslBuild(string certificate, string records)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] der = Convert.FromBase64String(certificate);
        diagnostics.Arrange("certificate bytes", der.Length);

        string text = PeerCertificateText.FormatFor(der, TlsBackend.OpenSsl);
        diagnostics.Act("text", text);

        string expected = records + Pem(der);
        diagnostics.Diff("text", expected, text);
        Assert.AreEqual(expected, text);
    }

    [TestMethod]
    public void Format_Schannel_PrintsTheX509Asn1Records()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] der = Convert.FromBase64String(SelfSigned);
        diagnostics.Arrange("certificate bytes", der.Length);

        string text = PeerCertificateText.FormatFor(der, TlsBackend.Schannel);
        diagnostics.Act("text", text);

        Assert.AreEqual(PeerCertificateText.Format(der), text);
        StringAssert.StartsWith(text, "Subject:CN=localhost\n");
    }

    [TestMethod]
    public void Format_OpenSslUnreadableCertificate_PrintsNothing()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("certificate", "three bytes");

        string text = PeerCertificateText.FormatFor(new byte[] { 1, 2, 3 }, TlsBackend.OpenSsl);
        diagnostics.Act("text", text);

        Assert.AreEqual(string.Empty, text);
    }

    [TestMethod]
    public void Format_OpenSslRareGeneralNames_PrintsThemAsOpenSsl()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        AsnWriter names = new(AsnEncodingRules.DER);
        using (names.PushSequence())
        {
            using (names.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0, isConstructed: true)))
            {
                names.WriteObjectIdentifier("1.2.3");
            }

            names.WriteEncodedValue([0xA3, 0x00]);
            names.WriteEncodedValue([0xA5, 0x00]);
            names.WriteObjectIdentifier("1.2.3.9", new Asn1Tag(TagClass.ContextSpecific, 8));
            names.WriteOctetString([1, 2, 3, 4, 5], new Asn1Tag(TagClass.ContextSpecific, 7));
            using (names.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 4, isConstructed: true)))
            {
                names.WriteEncodedValue(new X500DistinguishedName("CN=café").RawData);
            }
        }

        string text = FormatCrafted(new X509Extension("2.5.29.18", names.Encode(), false));
        diagnostics.Act("text", text);

        StringAssert.Contains(
            text,
            "\nX509v3 Issuer Alternative Name:othername:<unsupported>, X400Name:<unsupported>, EdiPartyName:<unsupported>, " +
            "Registered ID:1.2.3.9, IP Address:<invalid>, DirName:/CN=\\x00c\\x00a\\x00f\\x00\\xE9\n");
    }

    [TestMethod]
    [DataRow(new byte[] { 0x30, 0x00 }, "CA:FALSE", DisplayName = "empty")]
    [DataRow(new byte[] { 0x30, 0x03, 0x02, 0x01, 0x03 }, "CA:FALSE, pathlen:3", DisplayName = "path length only")]
    [DataRow(new byte[] { 0x30, 0x06, 0x01, 0x01, 0xFF, 0x02, 0x01, 0x00 }, "CA:TRUE, pathlen:0", DisplayName = "authority with path length")]
    public void Format_OpenSslBasicConstraints_PrintsThemAsOpenSsl(byte[] value, string printed)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("value", Convert.ToHexString(value));

        string text = FormatCrafted(new X509Extension("2.5.29.19", value, true));
        diagnostics.Act("text", text);

        StringAssert.Contains(text, "\nX509v3 Basic Constraints:" + printed + "\n");
    }

    [TestMethod]
    [DataRow(new byte[] { 0x30, 0x03, 0x82, 0x01, 0x05 }, "serial:05", DisplayName = "serial only")]
    [DataRow(new byte[] { 0x30, 0x03, 0x80, 0x01, 0xAB }, "AB", DisplayName = "key identifier only")]
    [DataRow(new byte[] { 0x30, 0x02, 0xA1, 0x00 }, "0...", DisplayName = "empty issuer, unreadable")]
    public void Format_OpenSslAuthorityKeyIdentifier_PrintsItAsOpenSsl(byte[] value, string printed)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("value", Convert.ToHexString(value));

        string text = FormatCrafted(new X509Extension("2.5.29.35", value, false));
        diagnostics.Act("text", text);

        StringAssert.Contains(text, "\nX509v3 Authority Key Identifier:" + printed + "\n");
    }

    [TestMethod]
    public void Format_OpenSslKeyIdentifierAndIssuer_NamesTheKeyIdentifier()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] value = [0x30, 0x0B, 0x80, 0x01, 0x01, 0xA1, 0x06, 0x82, 0x04, 0x61, 0x2E, 0x62, 0x63];
        diagnostics.Arrange("value", Convert.ToHexString(value));

        string text = FormatCrafted(new X509Extension("2.5.29.35", value, false));
        diagnostics.Act("text", text);

        StringAssert.Contains(text, "\nX509v3 Authority Key Identifier:keyid:01\nDNS:a.bc\n");
    }

    [TestMethod]
    public void Format_OpenSslUnknownOrUnreadableExtension_PrintsItsRawBytes()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("extensions", "unknown 1.2.3.4 and an unreadable key identifier");

        string text = FormatCrafted(
            new X509Extension("1.2.3.4", [(byte)'\n', (byte)'\r', 0x01, 0x7F, (byte)'a'], false),
            new X509Extension("2.5.29.14", [(byte)'h', (byte)'i'], false),
            new X509Extension("2.5.29.15", [0x03, 0x01, 0x00], false));
        diagnostics.Act("text", text);

        StringAssert.Contains(text, "\n1.2.3.4:\n\r..a\nX509v3 Subject Key Identifier:hi\nX509v3 Key Usage:\n");
    }

    [TestMethod]
    public void Format_OpenSslEcKeyNegativeSerial_PrintsNoKeyRecords()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        AsnWriter subject = new(AsnEncodingRules.DER);
        using (subject.PushSequence())
        {
            using (subject.PushSetOf())
            using (subject.PushSequence())
            {
                subject.WriteObjectIdentifier("2.5.4.3");
                subject.WriteCharacterString(UniversalTagNumber.UTF8String, "café");
            }

            using (subject.PushSetOf())
            using (subject.PushSequence())
            {
                subject.WriteObjectIdentifier("2.5.4.10");
                subject.WriteCharacterString(UniversalTagNumber.UTF8String, "x");
            }
        }

        CertificateRequest request = new(new X500DistinguishedName(subject.Encode()), key, HashAlgorithmName.SHA256);
        diagnostics.Arrange("key", "P-256");
        using X509Certificate2 certificate = request.Create(
            new X500DistinguishedName("CN=café"),
            X509SignatureGenerator.CreateForECDsa(key),
            new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 26, 0, 0, 0, TimeSpan.Zero),
            [0x05]);

        // The serial number INTEGER 05 turned into the negative 85, which no signer here writes.
        byte[] der = certificate.RawData;
        int serial = der.AsSpan().IndexOf(new byte[] { 0xA0, 0x03, 0x02, 0x01, 0x02, 0x02, 0x01, 0x05 }) + 7;
        der[serial] = 0x85;
        string text = PeerCertificateText.FormatFor(der, TlsBackend.OpenSsl);
        diagnostics.Act("text", text);

        StringAssert.StartsWith(text, "Subject:CN = caf\\C3\\A9, O = x\nIssuer:CN = caf\\C3\\A9\n");
        StringAssert.Contains(text, "Serial Number:-7b\nSignature Algorithm:ecdsa-with-SHA256\nPublic Key Algorithm:id-ecPublicKey\n");
        StringAssert.Contains(text, "Expire date:Nov 26 00:00:00 2026 GMT\nSignature:");
    }

    [TestMethod]
    [DataRow(new byte[] { 0x30, 0x06, 0x02, 0x01, 0x00, 0x02, 0x01, 0xFF }, "RSA Public Key:0\nrsa(n):0\nrsa(e):-1\n", DisplayName = "zero and negative")]
    [DataRow(new byte[] { 0x30, 0x01, 0x02 }, "", DisplayName = "unreadable")]
    public void Format_OpenSslOddRsaKey_PrintsItAsBnPrint(byte[] keyValue, string records)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("key", Convert.ToHexString(keyValue));

        string text = FormatCrafted(new PublicKey(new Oid("1.2.840.113549.1.1.1"), new AsnEncodedData([0x05, 0x00]), new AsnEncodedData(keyValue)));
        diagnostics.Act("text", text);

        StringAssert.Contains(text, " GMT\n" + records + "Signature:");
    }

    [TestMethod]
    public void Format_OpenSslUnprintableNames_PrintsEmptySubjectAndIssuer()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        // A BMPString of three bytes, which is no whole number of characters.
        X500DistinguishedName unprintable = new([0x30, 0x0E, 0x31, 0x0C, 0x30, 0x0A, 0x06, 0x03, 0x55, 0x04, 0x03, 0x1E, 0x03, 0x00, 0x61, 0x00]);
        using RSA key = RSA.Create(2048);
        CertificateRequest request = new(unprintable, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        diagnostics.Arrange("name", Convert.ToHexString(unprintable.RawData));

        using X509Certificate2 certificate = request.Create(
            unprintable,
            X509SignatureGenerator.CreateForRSA(key, RSASignaturePadding.Pkcs1),
            new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 26, 0, 0, 0, TimeSpan.Zero),
            [0x01]);
        string text = PeerCertificateText.FormatFor(certificate.RawData, TlsBackend.OpenSsl);
        diagnostics.Act("text", text);

        StringAssert.StartsWith(text, "Subject:\nIssuer:\nVersion:2\n");
    }

    private static string FormatCrafted(params X509Extension[] extensions)
    {
        using RSA key = RSA.Create(2048);
        return FormatCrafted(new PublicKey(key), extensions);
    }

    private static string FormatCrafted(PublicKey publicKey, params X509Extension[] extensions)
    {
        using RSA issuerKey = RSA.Create(2048);
        CertificateRequest request = new(new X500DistinguishedName("CN=crafted"), publicKey, HashAlgorithmName.SHA256);
        foreach (X509Extension extension in extensions)
        {
            request.CertificateExtensions.Add(extension);
        }

        using X509Certificate2 certificate = request.Create(
            new X500DistinguishedName("CN=issuer"),
            X509SignatureGenerator.CreateForRSA(issuerKey, RSASignaturePadding.Pkcs1),
            new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 26, 0, 0, 0, TimeSpan.Zero),
            [0x01]);
        return PeerCertificateText.FormatFor(certificate.RawData, TlsBackend.OpenSsl);
    }

    private static string Pem(byte[] der)
    {
        string base64 = Convert.ToBase64String(der);
        StringBuilder pem = new("-----BEGIN CERTIFICATE-----\n");
        for (int index = 0; index < base64.Length; index += 64)
        {
            pem.Append(base64.AsSpan(index, Math.Min(64, base64.Length - index))).Append('\n');
        }

        return pem.Append("-----END CERTIFICATE-----\n").ToString();
    }
}
