using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Output;

/// <summary>
/// Pins the extension and key records <see cref="OpenSslPeerCertificateText" /> prints beyond the
/// first few printers (BL-3875). Each expected record was measured with curl 8.18.0 on OpenSSL
/// 3.5.5 (<c>-w '%{certs}'</c> against <c>openssl s_server -www</c>) unless its test says it
/// follows OpenSSL's source, and each is built here as the DER such a certificate carries.
/// </summary>
[TestClass]
public sealed class OpenSslPeerCertificateTextTests
{
    private static readonly byte[] LogId = Enumerable.Repeat((byte)0xAB, 32).ToArray();

    [TestMethod]
    public void Format_AuthorityInformationAccess_PrintsEachMethodAndLocation()
    {
        string text = Format(Extension("1.3.6.1.5.5.7.1.1", w =>
        {
            using (w.PushSequence())
            {
                Access(w, "1.3.6.1.5.5.7.48.1", 6, "http://ocsp.example.com");
                Access(w, "1.3.6.1.5.5.7.48.2", 6, "http://ca.example.com/ca.crt");
            }
        }));

        Assert.AreEqual("OCSP - URI:http://ocsp.example.com\nCA Issuers - URI:http://ca.example.com/ca.crt", RecordOf(text, "Authority Information Access"));
    }

    [TestMethod]
    public void Format_AccessMethodsWithoutAName_PrintDotted()
    {
        string text = Format(Extension("1.3.6.1.5.5.7.1.1", w =>
        {
            using (w.PushSequence())
            {
                Access(w, "1.3.6.1.5.5.7.48.1", 2, "o.example");
                Access(w, "1.3.6.1.5.5.7.48.9", 6, "http://x");
                Access(w, "1.3.6.1.5.5.7.48.3", 1, "a@b.c");
                Access(w, "1.3.6.1.5.5.7.48.4", 6, "http://d");
            }
        }));

        Assert.AreEqual("OCSP - DNS:o.example\n1.3.6.1.5.5.7.48.9 - URI:http://x\nAD Time Stamping - email:a@b.c\nad dvcs - URI:http://d", RecordOf(text, "Authority Information Access"));
    }

    [TestMethod]
    public void Format_SubjectInformationAccess_PrintsCaRepository()
    {
        string text = Format(Extension("1.3.6.1.5.5.7.1.11", w =>
        {
            using (w.PushSequence())
            {
                Access(w, "1.3.6.1.5.5.7.48.5", 6, "http://r");
            }
        }));

        Assert.AreEqual("CA Repository - URI:http://r", RecordOf(text, "Subject Information Access"));
    }

    [TestMethod]
    public void Format_CrlDistributionPoints_PrintsEachFullName()
    {
        string text = Format(Extension("2.5.29.31", w =>
        {
            using (w.PushSequence())
            {
                DistributionPoint(w, "http://crl.example.com/a.crl");
                DistributionPoint(w, "http://crl2.example.com/b.crl");
            }
        }));

        Assert.AreEqual(
            "Full Name:\n  URI:http://crl.example.com/a.crl\nFull Name:\n  URI:http://crl2.example.com/b.crl",
            RecordOf(text, "X509v3 CRL Distribution Points"));
    }

    [TestMethod]
    public void Format_FreshestCrlWithReasonsAndIssuer_PrintsThemUnderTheName()
    {
        string text = Format(Extension("2.5.29.46", w =>
        {
            using (w.PushSequence())
            using (w.PushSequence())
            {
                using (w.PushSequence(Context(0)))
                using (w.PushSequence(Context(0)))
                {
                    w.WriteCharacterString(UniversalTagNumber.IA5String, "http://a.example/x.crl", Context(6, false));
                }

                w.WriteBitString([0x68], 3, Context(1, false));
                using (w.PushSequence(Context(2)))
                {
                    DirectoryName(w, "Issuer");
                }
            }
        }));

        Assert.AreEqual(
            "Full Name:\n  URI:http://a.example/x.crl\nReasons:\n  Key Compromise, CA Compromise, Superseded\nCRL Issuer:\n  DirName:CN = Issuer",
            RecordOf(text, "X509v3 Freshest CRL"));
    }

    // Follows i2r_crldp and print_reasons: OpenSSL 3.5's config cannot build a relative name.
    [TestMethod]
    public void Format_RelativeNameWithNoReasons_PrintsTheNameAndNone()
    {
        string text = Format(Extension("2.5.29.31", w =>
        {
            using (w.PushSequence())
            using (w.PushSequence())
            {
                using (w.PushSequence(Context(0)))
                using (w.PushSequence(Context(1)))
                using (w.PushSequence())
                {
                    w.WriteObjectIdentifier("2.5.4.3");
                    w.WriteCharacterString(UniversalTagNumber.UTF8String, "Rel");
                }

                w.WriteBitString([0x00], 0, Context(1, false));
            }
        }));

        Assert.AreEqual("Relative Name:\n  CN = Rel\nReasons:\n  <none>", RecordOf(text, "X509v3 CRL Distribution Points"));
    }

    [TestMethod]
    public void Format_CertificatePoliciesWithQualifiers_PrintsPolicyCpsAndNotice()
    {
        string text = Format(Extension("2.5.29.32", w =>
        {
            using (w.PushSequence())
            {
                Policy(w, "1.2.3.4", null);
                Policy(w, "1.3.5.8", () =>
                {
                    Cps(w, "http://cps.example.com");
                    Notice(w, () =>
                    {
                        using (w.PushSequence())
                        {
                            w.WriteCharacterString(UniversalTagNumber.UTF8String, "Org");
                            using (w.PushSequence())
                            {
                                w.WriteInteger(1);
                                w.WriteInteger(2);
                            }
                        }

                        w.WriteCharacterString(UniversalTagNumber.UTF8String, "Explicit Text Here");
                    });
                });
                Policy(w, "2.23.140.1.2.1", null);
            }
        }));

        Assert.AreEqual(
            "Policy: 1.2.3.4\nPolicy: 1.3.5.8\n  CPS: http://cps.example.com\n  User Notice:\n    Organization: Org\n    Numbers: 1, 2\n    Explicit Text: Explicit Text Here\nPolicy: 2.23.140.1.2.1",
            RecordOf(text, "X509v3 Certificate Policies"));
    }

    [TestMethod]
    public void Format_CertificatePoliciesWithSeveralNotices_PrintsEachNotice()
    {
        string text = Format(Extension("2.5.29.32", w =>
        {
            using (w.PushSequence())
            {
                Policy(w, "1.2.3", () =>
                {
                    Cps(w, "http://a");
                    Cps(w, "http://b");
                });
                Policy(w, "2.3.4", () =>
                {
                    Notice(w, () => w.WriteCharacterString(UniversalTagNumber.VisibleString, "Only text"));
                    Notice(w, () =>
                    {
                        using (w.PushSequence())
                        {
                            w.WriteCharacterString(UniversalTagNumber.UTF8String, "Org2");
                            using (w.PushSequence())
                            {
                                w.WriteInteger(7);
                            }
                        }
                    });
                });
            }
        }));

        Assert.AreEqual(
            "Policy: 1.2.3\n  CPS: http://a\n  CPS: http://b\nPolicy: 2.3.4\n  User Notice:\n    Explicit Text: Only text\n  User Notice:\n    Organization: Org2\n    Number: 7",
            RecordOf(text, "X509v3 Certificate Policies"));
    }

    // Follows print_notice: a reference with no numbers prints only its organization.
    [TestMethod]
    public void Format_NoticeReferenceWithoutNumbers_PrintsOnlyTheOrganization()
    {
        string text = Format(Extension("2.5.29.32", w =>
        {
            using (w.PushSequence())
            {
                Policy(w, "1.2.3", () => Notice(w, () =>
                {
                    using (w.PushSequence())
                    {
                        w.WriteCharacterString(UniversalTagNumber.UTF8String, "Org");
                        using (w.PushSequence())
                        {
                        }
                    }
                }));
            }
        }));

        Assert.AreEqual("Policy: 1.2.3\n  User Notice:\n    Organization: Org", RecordOf(text, "X509v3 Certificate Policies"));
    }

    [TestMethod]
    public void Format_NoticeWithNoContent_PrintsOnlyTheHeading()
    {
        string text = Format(Extension("2.5.29.32", w =>
        {
            using (w.PushSequence())
            {
                Policy(w, "1.2.3", () => Notice(w, () => { }));
            }
        }));

        Assert.AreEqual("Policy: 1.2.3\n  User Notice:", RecordOf(text, "X509v3 Certificate Policies"));
    }

    [TestMethod]
    public void Format_EmptyTimestamp_PrintsItAsUnknownWithNoBytes()
    {
        string text = Format(SctExtension(Array.Empty<byte>()));

        Assert.AreEqual("Signed Certificate Timestamp:\n    Version   : unknown\n" + new string(' '
, 16), RecordOf(text, "CT Precertificate SCTs"));
    }

    // Follows i2r_certpol: an unknown qualifier prints its id, and anyPolicy its name.
    [TestMethod]
    public void Format_UnknownQualifierAndAnyPolicy_PrintTheIdAndTheName()
    {
        string text = Format(Extension("2.5.29.32", w =>
        {
            using (w.PushSequence())
            {
                Policy(w, "2.5.29.32.0", () =>
                {
                    using (w.PushSequence())
                    {
                        w.WriteObjectIdentifier("1.2.3.9");
                        w.WriteNull();
                    }
                });
            }
        }));

        Assert.AreEqual("Policy: X509v3 Any Policy\n  Unknown Qualifier: 1.2.3.9", RecordOf(text, "X509v3 Certificate Policies"));
    }

    [TestMethod]
    public void Format_NameConstraints_PrintsPermittedAndExcludedBases()
    {
        string text = Format(Extension("2.5.29.30", w =>
        {
            using (w.PushSequence())
            {
                using (w.PushSequence(Context(0)))
                {
                    Subtree(w, () => w.WriteCharacterString(UniversalTagNumber.IA5String, "example.com", Context(2, false)));
                    Subtree(w, () => w.WriteOctetString([10, 0, 0, 0, 255, 0, 0, 0], Context(7, false)));
                }

                using (w.PushSequence(Context(1)))
                {
                    Subtree(w, () => w.WriteCharacterString(UniversalTagNumber.IA5String, "bad.example.com", Context(1, false)));
                }
            }
        }));

        Assert.AreEqual(
            "Permitted:\n  DNS:example.com\n  IP:10.0.0.0/255.0.0.0\nExcluded:\n  email:bad.example.com",
            RecordOf(text, "X509v3 Name Constraints"));
    }

    [TestMethod]
    public void Format_NameConstraintsWithDirectoryAndAddressAndId_PrintsEach()
    {
        byte[] ipv6 = [0xFE, 0x80, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0xFF, 0xFF, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        string text = Format(Extension("2.5.29.30", w =>
        {
            using (w.PushSequence())
            {
                using (w.PushSequence(Context(0)))
                {
                    Subtree(w, () => w.WriteCharacterString(UniversalTagNumber.IA5String, ".example.com", Context(6, false)));
                }

                using (w.PushSequence(Context(1)))
                {
                    Subtree(w, () => DirectoryName(w, "Issuer"));
                    Subtree(w, () => w.WriteOctetString(ipv6, Context(7, false)));
                    Subtree(w, () => w.WriteOctetString([1, 2, 3], Context(7, false)));
                    Subtree(w, () => w.WriteObjectIdentifier("1.2.3", Context(8, false)));
                }
            }
        }));

        Assert.AreEqual(
            "Permitted:\n  URI:.example.com\nExcluded:\n  DirName:CN = Issuer\n  IP:FE80:0:0:0:0:0:0:0/FFFF:0:0:0:0:0:0:0\n  IP:<invalid>\n  Registered ID:1.2.3",
            RecordOf(text, "X509v3 Name Constraints"));
    }

    [TestMethod]
    [DataRow(true, true, "Require Explicit Policy:3, Inhibit Policy Mapping:2")]
    [DataRow(false, true, "Inhibit Policy Mapping:0")]
    [DataRow(true, false, "Require Explicit Policy:0")]
    public void Format_PolicyConstraints_PrintsEachConstraintPresent(bool require, bool inhibit, string expected)
    {
        string text = Format(Extension("2.5.29.36", w =>
        {
            using (w.PushSequence())
            {
                if (require)
                {
                    w.WriteInteger(inhibit ? 3 : 0, Context(0, false));
                }

                if (inhibit)
                {
                    w.WriteInteger(require ? 2 : 0, Context(1, false));
                }
            }
        }));

        Assert.AreEqual(expected, RecordOf(text, "X509v3 Policy Constraints"));
    }

    [TestMethod]
    public void Format_InhibitAnyPolicy_PrintsTheNumber()
    {
        string text = Format(Extension("2.5.29.54", w => w.WriteInteger(5)));

        Assert.AreEqual("5", RecordOf(text, "X509v3 Inhibit Any Policy"));
    }

    [TestMethod]
    public void Format_NetscapeComment_PrintsTheText()
    {
        string text = Format(Extension("2.16.840.1.113730.1.13", w => w.WriteCharacterString(UniversalTagNumber.IA5String, "hello comment")));

        Assert.AreEqual("hello comment", RecordOf(text, "Netscape Comment"));
    }

    [TestMethod]
    [DataRow(new byte[] { 0xE0 }, 5, "SSL Client, SSL Server, S/MIME")]
    [DataRow(new byte[] { 0x1F }, 0, "Object Signing, Unused, SSL CA, S/MIME CA, Object Signing CA")]
    public void Format_NetscapeCertType_PrintsTheNamesOfTheBitsSet(byte[] bits, int unused, string expected)
    {
        string text = Format(Extension("2.16.840.1.113730.1.1", w => w.WriteBitString(bits, unused)));

        Assert.AreEqual(expected, RecordOf(text, "Netscape Cert Type"));
    }

    [TestMethod]
    public void Format_SignedCertificateTimestamp_PrintsEveryField()
    {
        string text = Format(SctExtension(Sct(0, 0x0403, [0xDE, 0xAD, 0xBE, 0xEF], [])));

        Assert.AreEqual(
            "Signed Certificate Timestamp:\n    Version   : v1 (0x0)\n    Log ID    : AB:AB:AB:AB:AB:AB:AB:AB:AB:AB:AB:AB:AB:AB:AB:AB:\n"
            + "                AB:AB:AB:AB:AB:AB:AB:AB:AB:AB:AB:AB:AB:AB:AB:AB\n    Timestamp : Apr 24 22:22:51.000 2024 GMT\n"
            + "    Extensions: none\n    Signature : ecdsa-with-SHA256\n                DE:AD:BE:EF",
            RecordOf(text, "CT Precertificate SCTs"));
    }

    // Follows ct_prn: an extension prints as hex, a SHA-256 RSA signature by its name, any other as unknown.
    [TestMethod]
    [DataRow(0x0401, "sha256WithRSAEncryption")]
    [DataRow(0x0402, "unknown")]
    [DataRow(0x0503, "unknown")]
    public void Format_SignedCertificateTimestampSignatureAlgorithm_PrintsItsName(int algorithm, string name)
    {
        string text = Format(SctExtension(Sct(0, algorithm, [1], [0x0A, 0x0B])));

        StringAssert.Contains(RecordOf(text, "CT Precertificate SCTs"), "    Extensions: 0A:0B\n    Signature : " + name + "\n                01");
    }

    [TestMethod]
    public void Format_SeveralTimestampsWithALongSignature_PrintsRowsOfSixteenAndSeparatesTheTimestamps()
    {
        byte[] signature = Enumerable.Range(0, 17).Select(index => (byte)index).ToArray();
        string text = Format(SctExtension(Sct(0, 0x0403, signature, []), Sct(5, 0x0403, [], [])));

        string record = RecordOf(text, "CT Precertificate SCTs");
        StringAssert.Contains(record, "                00:01:02:03:04:05:06:07:08:09:0A:0B:0C:0D:0E:0F:\n                10\nSigned Certificate Timestamp:\n    Version   : unknown\n");
    }

    [TestMethod]
    public void Format_SignedCertificateTimestampOfAnotherVersion_PrintsItsBytes()
    {
        string text = Format(SctExtension([1, 2, 3]));

        Assert.AreEqual("Signed Certificate Timestamp:\n    Version   : unknown\n                01:02:03", RecordOf(text, "CT Precertificate SCTs"));
    }

    [TestMethod]
    [DataRow(new byte[] { 0x00, 0x05, 0x00, 0x01, 0x00 }, DisplayName = "list shorter than declared")]
    [DataRow(new byte[] { 0x00 }, DisplayName = "no list length")]
    [DataRow(new byte[] { 0x00, 0x01, 0x00 }, DisplayName = "no timestamp length")]
    [DataRow(new byte[] { 0x00, 0x04, 0x00, 0x09, 0x00, 0x00 }, DisplayName = "timestamp shorter than declared")]
    [DataRow(new byte[] { 0x00, 0x05, 0x00, 0x03, 0x00, 0x00, 0x00 }, DisplayName = "version 1 timestamp too short")]
    public void Format_MalformedTimestampList_PrintsTheRawValue(byte[] list)
    {
        string text = Format(new X509Extension(new Oid("1.3.6.1.4.1.11129.2.4.2"), Der(w => w.WriteOctetString(list)), false));

        string record = RecordOf(text, "CT Precertificate SCTs");
        Assert.IsFalse(record.Contains("Signed Certificate", StringComparison.Ordinal), record);
        Assert.AreEqual(list.Length + 2, record.Length);
    }

    [TestMethod]
    public void Format_TimestampWhoseSignatureLengthIsWrongOrWhoseTimeIsOutOfRange_PrintsTheRawValue()
    {
        byte[] wrongLength = Sct(0, 0x0403, [1, 2], []);
        wrongLength[^3] = 9;
        byte[] outOfRange = Sct(0, 0x0403, [1], []);
        outOfRange[33] = 0xFF;
        byte[] extensionPastEnd = Sct(0, 0x0403, [1], []);
        extensionPastEnd[42] = 0xF0;

        foreach (byte[] timestamp in new[] { wrongLength, outOfRange, extensionPastEnd })
        {
            string text = Format(SctExtension(timestamp));

            Assert.IsFalse(RecordOf(text, "CT Precertificate SCTs").Contains("Signed Certificate", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public void Format_DsaKey_PrintsPQGAndPublicKey()
    {
        string text = FormatKey("1.2.840.10040.4.1", Der(w =>
        {
            using (w.PushSequence())
            {
                w.WriteInteger(23);
                w.WriteInteger(11);
                w.WriteInteger(4);
            }
        }), Der(w => w.WriteInteger(5)));

        StringAssert.Contains(text, " GMT\ndsa(p):17\ndsa(q):B\ndsa(g):4\ndsa(pub_key):5\nSignature:");
    }

    // Follows get_pkey_dh and pubkey_show: a PKCS #3 key has no q, which is pushed empty.
    [TestMethod]
    public void Format_DhKey_PrintsPGAndPublicKeyWithAnEmptyQ()
    {
        string text = FormatKey("1.2.840.113549.1.3.1", Der(w =>
        {
            using (w.PushSequence())
            {
                w.WriteInteger(23);
                w.WriteInteger(4);
            }
        }), Der(w => w.WriteInteger(5)));

        StringAssert.Contains(text, " GMT\ndh(p):17\ndh(q):\ndh(g):4\ndh(pub_key):5\nSignature:");
    }

    [TestMethod]
    public void Format_DhKeyWithUnreadableParameters_PrintsNoKeyRecords()
    {
        string text = FormatKey("1.2.840.113549.1.3.1", [0x30, 0x01, 0x02], Der(w => w.WriteInteger(5)));

        StringAssert.Contains(text, " GMT\nSignature:");
    }

    private static string Format(X509Extension extension)
    {
        using RSA key = RSA.Create(2048);
        return FormatCrafted(new PublicKey(key), extension);
    }

    private static string FormatKey(string algorithm, byte[] parameters, byte[] keyValue)
    {
        return FormatCrafted(new PublicKey(new Oid(algorithm), new AsnEncodedData(parameters), new AsnEncodedData(keyValue)));
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

    // The value of the one extension record named label: the text up to the "Start date" record.
    private static string RecordOf(string text, string label)
    {
        string start = "\n" + label + ":";
        int from = text.IndexOf(start, StringComparison.Ordinal);
        Assert.IsTrue(from >= 0, "no record " + label + " in " + text);
        from += start.Length;
        return text[from..text.IndexOf("\nStart date:", from, StringComparison.Ordinal)];
    }

    private static X509Extension Extension(string oid, Action<AsnWriter> write)
    {
        return new X509Extension(new Oid(oid), Der(write), false);
    }

    private static byte[] Der(Action<AsnWriter> write)
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        write(writer);
        return writer.Encode();
    }

    private static Asn1Tag Context(int number, bool constructed = true)
    {
        return new Asn1Tag(TagClass.ContextSpecific, number, constructed);
    }

    private static void Access(AsnWriter w, string method, int nameTag, string location)
    {
        using (w.PushSequence())
        {
            w.WriteObjectIdentifier(method);
            w.WriteCharacterString(UniversalTagNumber.IA5String, location, Context(nameTag, false));
        }
    }

    private static void DistributionPoint(AsnWriter w, string uri)
    {
        using (w.PushSequence())
        using (w.PushSequence(Context(0)))
        using (w.PushSequence(Context(0)))
        {
            w.WriteCharacterString(UniversalTagNumber.IA5String, uri, Context(6, false));
        }
    }

    private static void DirectoryName(AsnWriter w, string commonName)
    {
        using (w.PushSequence(Context(4)))
        using (w.PushSequence())
        using (w.PushSetOf())
        using (w.PushSequence())
        {
            w.WriteObjectIdentifier("2.5.4.3");
            w.WriteCharacterString(UniversalTagNumber.UTF8String, commonName);
        }
    }

    private static void Subtree(AsnWriter w, Action writeBase)
    {
        using (w.PushSequence())
        {
            writeBase();
        }
    }

    private static void Policy(AsnWriter w, string id, Action? writeQualifiers)
    {
        using (w.PushSequence())
        {
            w.WriteObjectIdentifier(id);
            if (writeQualifiers is not null)
            {
                using (w.PushSequence())
                {
                    writeQualifiers();
                }
            }
        }
    }

    private static void Cps(AsnWriter w, string uri)
    {
        using (w.PushSequence())
        {
            w.WriteObjectIdentifier("1.3.6.1.5.5.7.2.1");
            w.WriteCharacterString(UniversalTagNumber.IA5String, uri);
        }
    }

    private static void Notice(AsnWriter w, Action writeNotice)
    {
        using (w.PushSequence())
        {
            w.WriteObjectIdentifier("1.3.6.1.5.5.7.2.2");
            using (w.PushSequence())
            {
                writeNotice();
            }
        }
    }

    // A version 1 SCT: 2024-04-24 22:22:51.000 UTC, the 0xAB log, no or the given extensions.
    private static byte[] Sct(byte version, int algorithm, byte[] signature, byte[] extensions)
    {
        List<byte> sct = [version, .. LogId, 0, 0, 0x01, 0x8F, 0x12, 0x34, 0x56, 0x78];
        sct.AddRange([(byte)(extensions.Length >> 8), (byte)extensions.Length]);
        sct.AddRange(extensions);
        sct.AddRange([(byte)(algorithm >> 8), (byte)algorithm, (byte)(signature.Length >> 8), (byte)signature.Length]);
        sct.AddRange(signature);
        return [.. sct];
    }

    private static X509Extension SctExtension(params byte[][] timestamps)
    {
        List<byte> list = [];
        foreach (byte[] timestamp in timestamps)
        {
            list.AddRange([(byte)(timestamp.Length >> 8), (byte)timestamp.Length]);
            list.AddRange(timestamp);
        }

        list.InsertRange(0, [(byte)(list.Count >> 8), (byte)list.Count]);
        return new X509Extension(new Oid("1.3.6.1.4.1.11129.2.4.2"), Der(w => w.WriteOctetString([.. list])), false);
    }
}
