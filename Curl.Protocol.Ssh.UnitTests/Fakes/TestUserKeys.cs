namespace Curl.Protocol.Ssh.Fakes;

/// <summary>
/// User key files for the key-reading and <c>publickey</c> tests: throwaway keys generated
/// once on 2026-09-29 with <c>ssh-keygen</c> and OpenSSL 3.5 for these tests alone, never
/// a real user's key. Each RSA text is the same 1024-bit key in another format, and each
/// P-256 text the same P-256 key, so every reading of one must give the same public blob.
/// Encrypted texts use the passphrase <see cref="Passphrase" />.
/// </summary>
internal static class TestUserKeys
{
    /// <summary>The passphrase of every encrypted text here.</summary>
    internal const string Passphrase = "secret";

    /// <summary>The RSA key as <c>ssh-keygen -m PEM</c> writes it: PKCS #1.</summary>
    internal const string RsaPkcs1 = """
        -----BEGIN RSA PRIVATE KEY-----
        MIICXAIBAAKBgQDa4PWNrMQ1eTqXVZcJDgV+jZPWlKE8zJxxAGZ2a6wZRHtzDijJ
        CUvauAP71DRlLF53sE0Qu6gX/XQod0aMdd8YyQ68hnj50cFzZ0Fso8f4LiRcd+AU
        daYS0Pn2v6DYHSF0LlAXZ7qxFBRGEutohNlFpayajlojAD2q0iDw9ODyRwIDAQAB
        AoGAQ750wdDDObm3xLcc9BR+5thy9WszLFo34VG6ZNSWg4uKmeW8Adz6uUtTEFJx
        lQ7SfkD22UL7l9E+bnfcQgUJc1OCeYwYoK3dGla5Jj2hlSrdl3wvY6ZqQZsFAQyT
        EUdLul9F85BhtImvhgPzOrpkZjOOU4pPgXEeQP4TKJa+PQECQQD+AX1DfYP2qY4S
        HA4ijoVmoHUlQ/cLlXORPbsjuhxNdaozhq0NciUZbSu80G7KGCJa7zQl3FJanShF
        RzSwwXVnAkEA3JjewYQbwzE8XiNwSk0NDNghWxruPYPGGtmDH6kr2pz584NcCVfH
        9fUAi83Aw5maITxtUPp2BZlqakSGLLawIQJAMFaoOAfS7UYnO1sLdZgZ2wX+RTFB
        +npem+1bh9kLOdKCqXufw0dNuCEGidBAxUUChLuw+OOM6KGv4D9Ez76BUQJBAJfk
        0w0gsBUZI94WPM2MfH3pnB4lTwIgaBo4x2bXj5C6Igmg25C7Vr5u8u9Qv3hvnYdh
        Gkx2CSoV1wZRJKpJKMECQCSIxpZp/5jJ+f/GievXAp9DCm0Glo21AWsn90pmyL6n
        YoJqWTam/Am2DbffVBYE13QuPqdAj0wUe71kefxbR8k=
        -----END RSA PRIVATE KEY-----
        """;

    /// <summary>The RSA key's <c>.pub</c> file.</summary>
    internal const string RsaPublicKeyFile = "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAAAgQDa4PWNrMQ1eTqXVZcJDgV+jZPWlKE8zJxxAGZ2a6wZRHtzDijJCUvauAP71DRlLF53sE0Qu6gX/XQod0aMdd8YyQ68hnj50cFzZ0Fso8f4LiRcd+AUdaYS0Pn2v6DYHSF0LlAXZ7qxFBRGEutohNlFpayajlojAD2q0iDw9ODyRw== test\n";

    /// <summary>The RSA key in <c>openssh-key-v1</c>, unencrypted.</summary>
    internal const string RsaOpenSsh = """
        -----BEGIN OPENSSH PRIVATE KEY-----
        b3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAAAlwAAAAdzc2gtcn
        NhAAAAAwEAAQAAAIEA2uD1jazENXk6l1WXCQ4Ffo2T1pShPMyccQBmdmusGUR7cw4oyQlL
        2rgD+9Q0ZSxed7BNELuoF/10KHdGjHXfGMkOvIZ4+dHBc2dBbKPH+C4kXHfgFHWmEtD59r
        +g2B0hdC5QF2e6sRQURhLraITZRaWsmo5aIwA9qtIg8PTg8kcAAAH4C78SeAu/EngAAAAH
        c3NoLXJzYQAAAIEA2uD1jazENXk6l1WXCQ4Ffo2T1pShPMyccQBmdmusGUR7cw4oyQlL2r
        gD+9Q0ZSxed7BNELuoF/10KHdGjHXfGMkOvIZ4+dHBc2dBbKPH+C4kXHfgFHWmEtD59r+g
        2B0hdC5QF2e6sRQURhLraITZRaWsmo5aIwA9qtIg8PTg8kcAAAADAQABAAAAgEO+dMHQwz
        m5t8S3HPQUfubYcvVrMyxaN+FRumTUloOLipnlvAHc+rlLUxBScZUO0n5A9tlC+5fRPm53
        3EIFCXNTgnmMGKCt3RpWuSY9oZUq3Zd8L2OmakGbBQEMkxFHS7pfRfOQYbSJr4YD8zq6ZG
        YzjlOKT4FxHkD+EyiWvj0BAAAAQCSIxpZp/5jJ+f/GievXAp9DCm0Glo21AWsn90pmyL6n
        YoJqWTam/Am2DbffVBYE13QuPqdAj0wUe71kefxbR8kAAABBAP4BfUN9g/apjhIcDiKOhW
        agdSVD9wuVc5E9uyO6HE11qjOGrQ1yJRltK7zQbsoYIlrvNCXcUlqdKEVHNLDBdWcAAABB
        ANyY3sGEG8MxPF4jcEpNDQzYIVsa7j2DxhrZgx+pK9qc+fODXAlXx/X1AIvNwMOZmiE8bV
        D6dgWZampEhiy2sCEAAAAAAQID
        -----END OPENSSH PRIVATE KEY-----
        """;

    /// <summary>The RSA key in PKCS #8, unencrypted.</summary>
    internal const string RsaPkcs8 = """
        -----BEGIN PRIVATE KEY-----
        MIICdgIBADANBgkqhkiG9w0BAQEFAASCAmAwggJcAgEAAoGBANrg9Y2sxDV5OpdV
        lwkOBX6Nk9aUoTzMnHEAZnZrrBlEe3MOKMkJS9q4A/vUNGUsXnewTRC7qBf9dCh3
        Rox13xjJDryGePnRwXNnQWyjx/guJFx34BR1phLQ+fa/oNgdIXQuUBdnurEUFEYS
        62iE2UWlrJqOWiMAParSIPD04PJHAgMBAAECgYBDvnTB0MM5ubfEtxz0FH7m2HL1
        azMsWjfhUbpk1JaDi4qZ5bwB3Pq5S1MQUnGVDtJ+QPbZQvuX0T5ud9xCBQlzU4J5
        jBigrd0aVrkmPaGVKt2XfC9jpmpBmwUBDJMRR0u6X0XzkGG0ia+GA/M6umRmM45T
        ik+BcR5A/hMolr49AQJBAP4BfUN9g/apjhIcDiKOhWagdSVD9wuVc5E9uyO6HE11
        qjOGrQ1yJRltK7zQbsoYIlrvNCXcUlqdKEVHNLDBdWcCQQDcmN7BhBvDMTxeI3BK
        TQ0M2CFbGu49g8Ya2YMfqSvanPnzg1wJV8f19QCLzcDDmZohPG1Q+nYFmWpqRIYs
        trAhAkAwVqg4B9LtRic7Wwt1mBnbBf5FMUH6el6b7VuH2Qs50oKpe5/DR024IQaJ
        0EDFRQKEu7D444zooa/gP0TPvoFRAkEAl+TTDSCwFRkj3hY8zYx8femcHiVPAiBo
        GjjHZtePkLoiCaDbkLtWvm7y71C/eG+dh2EaTHYJKhXXBlEkqkkowQJAJIjGlmn/
        mMn5/8aJ69cCn0MKbQaWjbUBayf3SmbIvqdigmpZNqb8CbYNt99UFgTXdC4+p0CP
        TBR7vWR5/FtHyQ==
        -----END PRIVATE KEY-----
        """;

    /// <summary>The RSA key in encrypted PKCS #8: PBES2, PBKDF2 with HMAC-SHA-256, AES-256-CBC.</summary>
    internal const string RsaPkcs8Aes256Sha256 = """
        -----BEGIN ENCRYPTED PRIVATE KEY-----
        MIIC5TBfBgkqhkiG9w0BBQ0wUjAxBgkqhkiG9w0BBQwwJAQQzGhNmATaL/kGMoxe
        mztqCAICCAAwDAYIKoZIhvcNAgkFADAdBglghkgBZQMEASoEEGIcer+dEyQXCuQ5
        R/B4sxEEggKA6BtlNcI4YyOX9DmXoNUpWRMUz691b+MHZfYvEplCPrPMuhOILMsZ
        1NsnjwEpyTEh598qdSm8tDLqM9z1rYH1+wT6nNY0RBbPIIqywhL4sVHBWbvE2brA
        Ub9WexczRKMl17enQ4WgEopbsJG2nT+rBjPLdtb/Lk6kAQzAb0yG8loeO1RSNvR+
        4s823IsRItSsJtRSDRVovD3gQ/wBXBQ/aOvie2TURTOZCQWLNPOSCrvh3aK99rTR
        n41RnazGw600RpnpmNtCsBx+S/vy+U1sDxgbmflAN/22MOJRuXay/X0Y/EARrt3X
        KW4rr9NW4jFG93hCdE0EKsy+AZrte92ZdN4551nt4jAIxeA+uOf3VE13PxIPI2ka
        N/xwD7aYKD1EoA9s7L8H46WnyNbKKr0KNRKW1X3xS5CfTeuK7ueCVpbaSDyKiDbx
        1G0rgCloW7FMhjFLuKhIMfMTXi+P32IuaAAlcz1e2tfsjL3YZfauv4WohgqFZEWH
        uNfi7QQwH3DQxfCTzUX/EqeBOhJlecCFGQIUrg5H6yoKCqc0wKBRWQvKypeCWMib
        YG4twovIrAhO24EPWMWqEBrp0zvdrdrwy7TkKjd05gkKsvmdewPW/FO/vQ39DPuA
        lygScWvYB3sG6+e8bv89AKhMoPRttOiGPOscmOJHaKOe6PVdul+IG+ATyNpRfFEC
        2IfL82WQ1AFKtN3admlcWKDo1ZIIdS0uRMxSGsaFib+wwV8jDlZb42P/qQczg2Z2
        QUHCPvzL0jdKkjRB7bT8k7MBPruotZHGZi6jFxaC6IbMNw6EJq6dBGAtCPiQuOe+
        3UQyVfzd6xV/2lqXDIBQkPU37dBCh2RZ6Q==
        -----END ENCRYPTED PRIVATE KEY-----
        """;

    /// <summary>The RSA key in encrypted PKCS #8: PBES2, PBKDF2 with the default HMAC-SHA-1, AES-128-CBC.</summary>
    internal const string RsaPkcs8Aes128Sha1 = """
        -----BEGIN ENCRYPTED PRIVATE KEY-----
        MIIC1zBRBgkqhkiG9w0BBQ0wRDAjBgkqhkiG9w0BBQwwFgQQMNVE7lCYIx7z8yN/
        LN8+ZAICCAAwHQYJYIZIAWUDBAECBBAqO4U3+JQJbyEVWk7M87qOBIICgGD4oaRC
        x+sZGoQugqYyHmmFGaSlL9JhP0R0AlFZa8Nfl7ad/e8OrXu01xp6a/AY3qNfhHiB
        RtXtADlvpO5/NcNxJxNC9rNlkVhK2bejmi7wy0MwgXQ68C3Z90vHI1kFPHaFSw0M
        nUeik21SaxMwO1P25+gBTP/f6iyLdmzwjOyvTOULbeBjmzqLFeIt/y2XFnf4zGiw
        0DxBvvtEXIPXqSC9HGB1vbM+RvMKGFkIyxQ5iigc88vTrlYV1ntAck9UClGsVT23
        J9rEQCLPzl1AIXpw7iVF/G0Q/uHIEOCcZ+HVLe75CZuAGVRPZRBvD8sTKhjV+PEs
        er607PRECo+Lwj58Yr9zN7u9Q9tspzHIe6rrPVOGSmyN+wXESiolrT0BGuZpn72p
        3CIF1hitKcH3VaCfemwXiaNvqTRbODuyRExyNhia3enXx7oiyGY/pkN/CE0UAOho
        avIDHrCinhxPmrZBS4MdksWGkR+7zWCbyzrJxxfSHWyVLHTz/wddalTe6Bnf+27v
        gu4jh/KbCiWotrg1p/21mXfnhVpqMF21OkFpIfNWH55kD1CJqjFriDxO5MhJqCtp
        Ygf0FzjJ4QVTIGSHDDcxjE+fXDo8wZeU39oGAEv0Cw8NC6F0UgX04j3DLaQMz+VL
        2NPaHLnuUbHKQ7rhGE721mj8aCI64UbcAelRNloa2q3d5FO8e2IDPHX4KZNywX6J
        /3G+SGm3cjDY80W8VJscFNboSD4eSnU5RV1hZIPXP84pnZMWeoM1pdBiO5QGiW/X
        0u4Jyf/pKqHml9pqybU9tgti1klVoWJTtP6TxGTL7jpeBU88XrlMlj9p74yM72dw
        OViHJEkPd4gL6nI=
        -----END ENCRYPTED PRIVATE KEY-----
        """;

    /// <summary>The RSA key in encrypted PKCS #8: PBES2, PBKDF2 with HMAC-SHA-384, AES-192-CBC.</summary>
    internal const string RsaPkcs8Aes192Sha384 = """
        -----BEGIN ENCRYPTED PRIVATE KEY-----
        MIIC5TBfBgkqhkiG9w0BBQ0wUjAxBgkqhkiG9w0BBQwwJAQQCDmEDNrvkjs/BzC4
        IRoUIAICCAAwDAYIKoZIhvcNAgoFADAdBglghkgBZQMEARYEEJOscnQprAHv6KyS
        abOkFHgEggKAdUX+h0SbSBl93efV0eyJGX+frVUUlxkPogiG7AhzCaLcUL8va70j
        xR/nv0yyDwlC6mMA2kR1fXHr6mD8QHxKtplNnHBlLSseielqF2PtP6K+3o2OHx/G
        HtZTj37SbszfegtjGQ39ZjMwMBZMQ1zn2NvEsrZYU0c+6dKUftFqe+VlODhShgpd
        OmlfVRAy+8bsgcxWIvG8kKBeL5Eg3x9XT1oN6O9a/zWA1etBR3veiL/AtLlhNxvk
        T6Y8CR2EFcjzMx+WxgYErkIbKTIZ70pwdEkMO3zn1jBiEpJEcceDlVRKQu9LTnlG
        YciwIfMKQVdizCu9f0bXJwjAyUeLOIC27oeNZ/hbVRN++c/qlXTQwyFQGJn366xF
        utyRZhvWAx0xubjrlZxnXY1F1edAzvxH+4kewJYvQCrME9UgxZEOrc6/7J/vXKZ+
        99HfnUIVOhg7b+OsQ24+vJNLeT2t1jchjmIO3T1Cy7HsolY7FmVd4Oik8lyjUxwX
        zbra14aTCkBJFTYC1RreWkK6Y2kcW+P491vLOtaoLHRIdDYBOKMk5t8r5OU7FgB7
        6mHNMG/Hr1Qh7e5EInajOnR8IxQvWE5g7oRKB2EPFdwMyavfjcVrHz7yJNQT4XWk
        UbcQRtGmPlJSsDzOoj7CyqDdf989ArtMEPLOIlQ/uYpyGbQgdwe/sXrLgVmeSCaH
        mtcJpX1pUTlJiIzgnqV61FUbE0k2qF5V2RmRft9RTByfgHbI6ToU8Jnqo9YX/Ef+
        jHqOohHZL+3FrjpRUT6yhQ3F3aHsknSCJedmx3N65C6mt16N/rOAKxuEjzsRGGn+
        y4hNzNm38+6HE3sTSL9RjDkjmlKvOGcG1A==
        -----END ENCRYPTED PRIVATE KEY-----
        """;

    /// <summary>The RSA key in encrypted PKCS #8: PBES2, PBKDF2 with HMAC-SHA-512, DES-EDE3-CBC.</summary>
    internal const string RsaPkcs8TripleDesSha512 = """
        -----BEGIN ENCRYPTED PRIVATE KEY-----
        MIIC3DBWBgkqhkiG9w0BBQ0wSTAxBgkqhkiG9w0BBQwwJAQQKtmXHHIC+bnKcImj
        9Hc2nAICCAAwDAYIKoZIhvcNAgsFADAUBggqhkiG9w0DBwQIxdtMyZmETlcEggKA
        Qo4s/1q9q1eZqNZbRsAFC24m7d772WzycloYyGJRtH1F+6YXhGBOQQMH7Lna6Euz
        qwupIwBk4+dLOnBZ5aR3K87AsRhyHMQAJKyLZ+KR3KrcaMsYwGfGnI3qwGWFFrTD
        xI/PRAND6lMHoo7U3tcSjrel4xulVe1KmMvRav8qI+AklJqK27hHVM+bnlu09UHJ
        apxAqg77P3HFOtAbftVZxWZAZB2LNYXCyT+Ztkd0Ek87w6WDoLHRVWKL1FnqWKoH
        pAHdyMewECkB2CB6QAzsHmoZN9ELSWsmvnenxG6v354UdxKiKtfSbrleonwX3RqH
        sckYSW4lYX6Q2I4pp372VWomUj3UCdbPp7f/YGnoSnnPQuRSvpQnihZ3SmoK4PP2
        Kav5x6wbHOXK+dif8/iIfgQ2HdYQ9ZcHBduvFB/5feHLOcm9dOHzWbi6xGhpgK3/
        MDJlXYBdY0QhGaMI9A9E4QSMA7CQ10oPgjYREm1eM5NFUROIIBYwr/9z/V2FzqNR
        cIn3lhUt+XVXQkFjqOq+Ze83JoZiwZMA2cX3TKhQKSwgnvUaBPDrLRzhOT4ejQSX
        vhKtU+gQ5v/oFAapchmckGH180hZAcgB7ilfKTDPkuj8oClzgH8peSKMT0jiWYeN
        LTNS3XcXYIZJzbB22Mg3s/A4hMccBYPUcIyGGMh114UIUAmm0c8PIe4UPedRw+W0
        MkRFIuuhvBaoHX5xj+5OrbnW02F0u8fiGgYwh41+KKhDA55/7fKYnzy//DKqW7tg
        ZmfJ5AjUwXnC9mt9bgcr0WpcUnaCFNlwZzBS1pvC2+IbBXrIWgGd+2PxAB/JSFoI
        n1MU5tyRKlrJN72RRXShCQ==
        -----END ENCRYPTED PRIVATE KEY-----
        """;

    /// <summary>The RSA key in encrypted PKCS #8: PBES2, PBKDF2 with HMAC-SHA-256, DES-CBC.</summary>
    internal const string RsaPkcs8DesSha256 = """
        -----BEGIN ENCRYPTED PRIVATE KEY-----
        MIIC2TBTBgkqhkiG9w0BBQ0wRjAxBgkqhkiG9w0BBQwwJAQQgTq04ljrQieGoWsl
        MADS/AICCAAwDAYIKoZIhvcNAgkFADARBgUrDgMCBwQIvDk9B12MkecEggKAco1E
        DTqk2/cttmkzKT5Un5yKFs3c6O6JDv+D7My+nGToOSPwexUk96p8sXI+xdqsW4Hl
        UEkmfdL1ZPgCYNRijTmyksyrL2v9/ksIbr9wAH1dQVnE79dbUcYIcgWfQOM460ZR
        9PEBzB3K6iby/PDY94ijLSToLHs3801Sek7LTK1j/q60Ar3NCSoQxwrhMvUHVYdA
        ZkaROCkimsE2FRJOflGAxmAlNKetosDBelsU99eq5BeQ+N0o7iq9umbMpQODHtB7
        Xqqr7rz6o2psN5peXtm3jilbFCXI7CcFbfkWTnjbcgszzkTT+RJOp+/OMk37upuL
        NM1xbHL2TmctKUwZa/Nc1bEnSJS70aJ+ra0nW8hssL0BuP8Q7L4xMSoWYy4jg4s+
        cyfiaYESdzV5okUsdkyso0/Voq4RFlyL66IzJllqj4cldnl8QCCyf7DMh6mJenx1
        9y8CG1+Fq+PH354LpDhE7lv/M8qtaq0X55rdhIeagxAmXFtJzkHkMP39AXQ3ifz/
        kscWOY/PC/VeG/VE0Yx+QnzkOe2U3T+bn/7J/AQ33AOcvjwN+RiUUfRzhq8OLCG7
        04CnWwyN0wsQhdfU3M4WECGbTgs9cIoaIdGlQ38jyp97yeDDSDXQaCokDkQl9CB4
        TyOwOHb8oPRtGgCYi0uSKp0hrY5Jv7q/PCaBHRBn9Kt4yi3/nZ5CWD5uDoyC3sl9
        D+zXeTuplcGN21EsvWMs2C9Q8ASwZUoXAe6+hRGipXlZAR9MMQKCzXHiCwuhGBO7
        UTkDWfuM/sxBR7Wezwrl5lsWMNqo50Xcn+PkPmunYh+NODYxi7QbYJKw/zxa1365
        BUznk083yqA2QKaPfg==
        -----END ENCRYPTED PRIVATE KEY-----
        """;

    /// <summary>The RSA key in encrypted PKCS #8: PBES1 <c>pbeWithMD5AndDES-CBC</c>.</summary>
    internal const string RsaPkcs8Md5Des = """
        -----BEGIN ENCRYPTED PRIVATE KEY-----
        MIICoTAbBgkqhkiG9w0BBQMwDgQIia3T8DvnvzUCAggABIICgH8Z11uxvyUPh4Xg
        k3c8Q8DYpBhQxzKceiAPd4VbEUJoibRKsE9Fez1wfDdjB9FXxIlggUqUkRIpD4rs
        xbMZ6iKhjPag97cketgmbmZbo4oZp2o1qB2D2aGrdYVsaE5NdSp1Fy0ULiY0KQ2j
        Am1xe3Vs1SPAkH36Sp0i3bHWYNsR+bYKdyToXip92jf6WhGV3s+ICrWxmVYC1Rxv
        cFWjUsyvgjHaouWJR1mXS+iUzM0UIQHxT5yozaXFrbnfrve7My7U2lWXvMxYmk7l
        9QNLmUw/NoPHuTB4vfKqifw61Ced66WRoh0MlNxWyM7kaxH3hTtHnNKiZRoJSVhp
        aEt4a879NSHI1Xp3dAZ9IrrYNbjBpHAtIa3dn6X021n/UQAB7ObMDBWPnBtmWKDJ
        /9NF00wBcoDmwKchFU7PtWaMrqefXnYyZi/WQEp8VVGFgoK5K6PaCvRLNtXTWYII
        AcaoN5Lk8OfvD/W/dT3AlPb3pMGpq18Isfd8Cw1+SOvYpBnhnlnld5p1KMOsMYBD
        R83vi6FJGHP6k/BQvIs4NH8NokowTk/05LM8P0UP8O+Y0xhVDM1cyRqerZeXf6u8
        kPgZII8WgOHAf6yqNKLJlamsB7o/7aqjlxA+oKA6LxA2+laxzwGAEAB6wYW7c4uc
        wNOCopcRKixuy3Pr+yuqwC9LOB/h88lKGqgjnEnScA+Qrj6P5zT1SRZiLWCe6C6D
        qZJwll59SXKyI/yS+chdbWOzT2j/tF1RBCRQquunOU09Ek1XRA7LvmEDYepz0bu/
        FexLmzw/zC0Ia9o8L0e3edI3B2KT7XaCWhGR2m2znmdvaqx2BCaSgMDFTLU+Tcaq
        WuWeKGs=
        -----END ENCRYPTED PRIVATE KEY-----
        """;

    /// <summary>The RSA key in encrypted PKCS #8: PBES1 <c>pbeWithSHA1AndDES-CBC</c>.</summary>
    internal const string RsaPkcs8Sha1Des = """
        -----BEGIN ENCRYPTED PRIVATE KEY-----
        MIICoTAbBgkqhkiG9w0BBQowDgQIF0VtGM9iYvsCAggABIICgD2t+TSfemUt0PXl
        K07HtKnnWw9YTmwJoHK1qahPK4jHaF4mN+USCkADKyKHtgS8O6KPXPXH2Sc7qf/o
        ThaL0jTDhxjUC9Fhiq6SAR2qmut8oaOXucvv7UAzQ6f0BeW9KOayH+li9+9CgIMd
        yrjIz3MPrqeqlK/sFqhwqF7pjJPzAm5FHHt8lyXj3sIwvchgr3eHRFumvNBE9XLM
        GkZR68jN1HY0rft5EdoMkyRiMoQdHGGNpRXRfaZvHPWaXAFJzkE8TmecVeM8uJwp
        qv3yITkBZ+HSpez/76bqor0xmYD2yzW+0Pu38+zUwXKRU6X6V9kB0lDSLQ+l8Htl
        PEF5iER5R8ieKBJn8522knrNmS8gFadYi7WrHFJxjRgLNw2E81bmSO44n1kKOagp
        AIrWuQrIk+LUiPjcjoaqogQfoSBM7ehouEJcYKyd4LAYeJ9C+cKbl7fymYmFuXSx
        TFpsLUflp+WNDdje3Fw6KBPjhTDgnVlcLGdJoSnnbOAtwm7Oz8Em0VhnGjbjEDws
        TcnRdw0m09sQHgpiDp8PyosHGxth2dm0mj8swHbJ8GeU85OVve/xYqCKs0vrjKXm
        BlaEMRh6ZSjl02eOAUXzHdMY0GvZQEZq7MiZxjrau+tscsz+IXzameyOKEX6QQP9
        +FSRKeyv98FFRUh/LKjRoXrZMJ1TU2f1PbZpbGSJ/ZbK3uNrms/RrX17Gt+OFdgV
        Q4uY2aXkXXRMKQMvYj3aIHRu2A1BcYvMMBaNoqTLNop7o44H4rJmb71BLpWnlSIW
        WNIa5J7SJIGnBFwbEDq4c3CqdagTN/oBqTgpMJhLIm31Gu3v5WZAXqRQQk3D62q2
        mLUA95Y=
        -----END ENCRYPTED PRIVATE KEY-----
        """;

    /// <summary>The RSA key in PKCS #1 with legacy PEM encryption, AES-128-CBC, as <c>ssh-keygen -m PEM</c> encrypts.</summary>
    internal const string RsaPkcs1Aes128 = """
        -----BEGIN RSA PRIVATE KEY-----
        Proc-Type: 4,ENCRYPTED
        DEK-Info: AES-128-CBC,EFD1384ECE3AD173AC9400C2B148F6FE

        EbLO21ThqeR57pZvJyAYNA0EVHLxs7gec2SxK8OkFqRUjMeNN/ZpmhxOTSXKs6Hq
        H9ppuVv68xBgLrYpKuu1GzsR3/84zhlngqcCJ3WZRSbmASIjeZqtnXS/hbfI4LDD
        N1OOeom9Tt9qKdU56YLrQ8TVJotmmhTlDwSFMbUv+78muPkS14kzuinjaoSzP5B6
        71uVW2bGTlHHH+YAff0wlBM0UdvmsNyqgVDlIuf111fVxp929+m/myZKXNH98W5j
        82J7vsmfVVhzVPpgHS8+hmUBD11X7pj9ENp2Zcz0/0shCDGPqR6BVsybyF0D6wvF
        hoUW2McXYjhaxFW9aMgVqxFaiWz3+SyKDGf0TwouQQ2oRgZ6mJrlPvx6V/PIe8vU
        o5pRfFv7tugo8LpZ30EbA5NHTmqvxdimZXzW2ckX4rihtYIS/dTc8J99tVJgg5MR
        l8kORSeAv3eh8b6ZSrtWaeL3yQbTGypSK5AaFs5ppVBRQRCkoDffSOJR12PmDIzO
        kyp+Tq5JU33XuXVgKan1Uxm+J1mVPWbphDcferw7X83gzIks2yNdczLbrKTrpz62
        U1wH1Ur8MXPhYhCzXe5F+V/UMUhbSGQ7sZNfYUzMkAFR3EBnq76TMyFwIRwuOHr/
        KvCHzi987EieMOYTYam/ZjQqA3kKu+iRDGTPOogELyrZS5z7/EljePI8ELqUSwIQ
        IAB+U9buCN9XXS8GI73mfW/+Xoa+uX+ZvOcId6PbX6k9SXkqOGb7CFGIt6WvsKTW
        sIsKN1S7j50S84fKnUNzJzJcQc2q1We0b7pEcxKZ3gTezgnwwTFdPZNbXWcvJg44
        -----END RSA PRIVATE KEY-----
        """;

    /// <summary>The RSA key in PKCS #1 with legacy PEM encryption, AES-192-CBC.</summary>
    internal const string RsaPkcs1Aes192 = """
        -----BEGIN RSA PRIVATE KEY-----
        Proc-Type: 4,ENCRYPTED
        DEK-Info: AES-192-CBC,8E4FFD89045C57D4E573C14D6680FE58

        9zGHoOD4cjb9dGsDjrq2PdtayXdWUio/8nRTtmVEJeBndVbkGxlb/xnlJbJWflsO
        9ZTuhJrTIp1rcsQ4MAGCnxuWbxI2COVniqRAIgn7ootnCrAPvt+R/5rL/biGhWYT
        jK8BzCfAhO0tgI8IlME1aqBO7qMFU69Gz636UfFj1raoe2yUYjQeysCHnenZtjct
        yjgNbxEl5DXqipoSuE3bZUsIjGuQO01oO02+IBB/TCQbYhrJxZUpxMir3SCnOzUw
        5K3sFgC1zfZjR/lV5cwibW7jTa2yUfQoPV5fOR1vaI3JiHM7DQtBpQmV2bRBtXPu
        5Z0rIq7jalj9Z8pl0Hn74B8QEGW8Hb/r6GDAUOYg3EhrDPI58JwWa2lcnwK9ykeL
        zCVG/ClENxo4pyxUhbOSXRJLwS+RvPV5qsfRFVLG17ow7x8pN8Yu+Hr25XtBL3yz
        nYHchl7XlXUj+/M5vSHTTx7N0wHHrjpMbnD53n1BkI2ecxkPsWmtlR/FAWEHCzS+
        YkVlWhRFBLARWWRbmiwVD/hkdNn/Yrm4y3h2f94h/HbQAyFaQLMUl3Abk5/K+7RS
        x+i0zpZYE1HslEZ9ri7CRpVEK/9rpAKgY/lrTOjEhLIev4TWyOgUw3cOYEFPET79
        240IzyawOtroBsDpjj42jOaMYLWowXLzDIYrMJl2Shz+fOy2JHYzXHo/+ikGyNUX
        Mi4PtAmemzSkFxO3vvCCyyNX6P+jY6rsvja3hLA1bBnu+SklHp7Anhq3HwFweHF1
        3rR3mRty7iG+dBoxRq0P7aKMPQfZ0CGaChgKSNxd41yCrixXcSE7iZxD333m3vZJ
        -----END RSA PRIVATE KEY-----
        """;

    /// <summary>The RSA key in PKCS #1 with legacy PEM encryption, AES-256-CBC.</summary>
    internal const string RsaPkcs1Aes256 = """
        -----BEGIN RSA PRIVATE KEY-----
        Proc-Type: 4,ENCRYPTED
        DEK-Info: AES-256-CBC,A4DC326ED1A63A84A5B646B6049AE245

        qGocHL+6I5X90u/N7sSiDI8WrKqQLDmW4xf34UmbSk6e/FLlb3LNaCGBarJoYnmo
        D0I+nDDMUhPRchtoIWVdoPlp/hahMBQkRWMEC1gb/J7vrnsZKEuSBn3Q9PmSRSQ6
        kZYkT9DMi75q6Az8Wj8BQs7afYWAwOhQQ75kFVmEnpZX+dbBDGIUaslIrIjMllPz
        js+tahnBxYD6uWSG0v0zXp5YdwMQgVWqPhlVSIK5IScdhOrZTExBttff8gbpfjq+
        reoLGZmY/so5K3qZAIDFS4Zr4E6jphyZ6xha82wMWpwCVMhaWFs7DGQVuyaY6W+1
        IgGnZ/Udr+7e+S30jGe3pP0abB7tK8k7cTm7mu9uaKeFaCX0BRIfpYyKYUqifZqI
        o2XoUZQehp3fLyzP6uLvptFwKkEWoaIC25o7Wk4xukyqpt11EZcbo5tv6+3y/Z1t
        Kc/BLzK+gq0TrYNUQaxIL5whQwpbP12kyuHgc7jyVejwYv3Bw9qJgL/IxhzlRf0J
        Zq6I7Oo/qHyRtdBigUS1yi5JMsUGy8kCMGso4zHOGABaXTSd44+x6nguir+hnCji
        x9jV39EbBuWGOd1qshW152/2ouH/QOes3UFu7hh0Nte67gTZaRmee2y7YbEtBpME
        ZJiZgzqisRjQakbXcAIEZZJ64oUFUrfPvi6t6nKMsDJ1JGTRMlt59T7AmSGteWNL
        VUV0bnXxqf/FH/v9ZLRG3CJBApnoZHrpwbhpfVy9vm3CQrEvGwYKJDyKGJqqW81J
        CVk9xIlBHhReeKOUaHQvRA23m8vBFUW1uNsA4DxwPEanFaN2ikrnGQrAOaZDki43
        -----END RSA PRIVATE KEY-----
        """;

    /// <summary>The RSA key in PKCS #1 with legacy PEM encryption, DES-CBC.</summary>
    internal const string RsaPkcs1Des = """
        -----BEGIN RSA PRIVATE KEY-----
        Proc-Type: 4,ENCRYPTED
        DEK-Info: DES-CBC,5400131D7975FF44

        5qMBG2/qMcrvHvgvfl9K5tvr0R3Z1y3HAcDPcznBmSVWJ1HwsOuJ0RV9S6LieYQM
        9kuXWJsou/yURKLnm5q42l6FPYU4LQA2r5lNmmVxxn5WoGnFUgWOWv2KeNkt3l+Q
        hX2TI9TQ6sS7v9nrwQDkekz3AjtdorV1nLuzgLAoRwclErL9fyOP+G0qIBuXIjDL
        GvJOrLRg8Dyp19On6q2O9Hk/UiVN2SxYyOH/SYt+34G1btixMrZRJmPKG/kbDF/s
        AlX37nh0Zv9wGjEvB6L1KanxNDPlgAGQ1YWjgX67eLpF2IhLsgvZgHyOrddjpKAy
        itCQVBFrmpo8RP0S/zUJbwLRCAhKTpNAN+FOtipt9CzmTEE9V5FQI9oyhpdrNRzR
        UP6aDCU6YNVF+1f5twwv9xS+d4Hzve8gR0KoK5vo95SoeeXwj+6JjHzP60HpnReP
        UpjXr7DghnAD5EzTwUFYQ0lK5e37rWN0oaO7tMZnxReLQNkHAbhZPufkeJY2z+3M
        0bdkRuBFyaceaqGbSjB/pAPGTu2chUHPWECMp2swEumSegq3radV5lztMJlu3Qgc
        4sficlBJhEeyHqSjFez79xZKl2FcGWQVLczmwUmpGQHJWw50iGws9srH6rYqHZYw
        4orUWDcJfCsoCg2N3edTxKCoGvDE5LeXLKXqynbmGXIYkzpSLiXxh0ihl4lXR1Bp
        xhPzkHI8vC0kBKqdrJ59ogeqY31jF5RksczF1No+lXqA/Bz+4KTxcBg0AdiCvPo0
        N3c0jATfTBZoFDLkyettXj/6QL2AaXe/hnds7vEePDcGF7YllzAgIg==
        -----END RSA PRIVATE KEY-----
        """;

    /// <summary>The RSA key in PKCS #1 with legacy PEM encryption, DES-EDE3-CBC.</summary>
    internal const string RsaPkcs1TripleDes = """
        -----BEGIN RSA PRIVATE KEY-----
        Proc-Type: 4,ENCRYPTED
        DEK-Info: DES-EDE3-CBC,D23D9D78C5F3CD9D

        gvwe7Bj4HliwZ9Q7xbw65Pz4es7jDFJvTiBZBYN6B1WF+ZLEd85v1a5UeaC7d435
        zEC35LMQfnqpMd96/I8cttkeCo4sHF0Zre0O2UGBx0JoF+EDvjKFVvWFSjw58rUu
        qch0PD1YW+92iFvtlvp+PgC6HXCzPK0YRD2ChIxXqO30tcqLvUgAyMn8lrWQxbeF
        h1JJxCfucOctUrYkM+us2NLyY4vro4HIOfE76WJoCIxGUcB4SRSdASbKJFYuGUe6
        /Ulc6m7Nhhsfny7aSz28JOeTCwCyqpqJ2RyLq2E/FPOoHjTfh/RS75XwyafBQKRT
        iySAjk/rLJkNbg57/uTgXw5R503KJrYRal31geEXkULvVCftbhvolf7zQq+p4hPh
        PXjEF/eXLwagqQCNEmX1LPuvtMxiIxV+JDZLKhP5x2x452fCLbBaLu+WHHkjMOr8
        nMjgnULVBkeZdsfXpS6ry+HABJclBDJ3afi3f2y5TkEYmwfncovn72idv7qiuB0f
        irNVi1/Pd4jzo8G6XyGJ8UMBzRQFb3bMYRy1HUifThG53jEY0X+TmWIat9RQzfRy
        tisTBnDynLOO/5qDdooYCZ9qqGuMXIoyiAdru78k2hdbyBTLAQ7+t5RWP4sJRvzy
        07OMQkMUVyUxgAw+PYMg4eaxp7Jg+GVQjdamU+NBgW3sPLVILF4sAF9sDObCex1a
        AqTAswIB6NqWinMbsz4zACdopV1d8Hr8dYvKMooUMLLR1loBfJvX+BwMxQHEokA7
        r1sc6YQX0DXaVCL1hmvLOdsmmrWDaPzQOcuUmmtil2bM/vl50flzfA==
        -----END RSA PRIVATE KEY-----
        """;

    /// <summary>A second RSA key, in <c>openssh-key-v1</c> encrypted with bcrypt and <c>aes256-ctr</c> (passphrase <c>enc</c>): BL-681's to read.</summary>
    internal const string RsaOpenSshEncrypted = """
        -----BEGIN OPENSSH PRIVATE KEY-----
        b3BlbnNzaC1rZXktdjEAAAAACmFlczI1Ni1jdHIAAAAGYmNyeXB0AAAAGAAAABBoL1S/Z/
        LoLAeyT8Af63PkAAAAGAAAAAEAAACXAAAAB3NzaC1yc2EAAAADAQABAAAAgQCSPzx3I6LT
        TWTIABCIMdVgTN29bAMf8EXUMiCajmGykPYNDy5yTazUPMqaJgQaRmCYglsMp9hfMSPRp2
        ee2UVziuyEnbw6fKKbh0Whg7qE3ibRbKpwI4+N45Ugw5s89yoZmGMTCTmpiNLPwhAJquL/
        xSDNvBp5Fm5YFjbotvOL+QAAAgBMFDF2ZfwtnJxqyVcxIBKo3X9I8IQMCWj7QGcvjmTJhn
        Z1Wa1Lr3GP2+Rryr26EX6flV3vH9XAk+Ygy+5hQ50oS80QLji/NfSLJ3pI0+T9KiX4Rc/Z
        DrBNTPhf8uWerG4E4in9HtNiFdmUn5m/Su/7T9/iJrsVXaqVCqFiqkk/xQFhNpBbi2M6az
        1F3rqCMKizpB1ObcGxZg9x9K74JuVXj2Xewquygm5LH+cnkGFKRnW6HuI/YlQj5em0EcOA
        IRTGlGijC0tnsBP54kKWvoQ6Wm3WM561rEYHrIIA4Avr6ZZZylLy7ZP61EDw5AC6c5hY/o
        xYF3Sw7QLrRHFxfJsQn6PqjIjdVDqsL8NpecepeR2d+tJd+8hCCMLksPmu/lVzNRJunXVj
        ZNAzDblIcLyS4kc4z5eAi+N3SEOMe7bSvjbl+VXOhUpykB9SKEoHUKiOECEG1ESe2b2rsa
        lHAvNroGBmiacnPpISBXrOxwXEJ47mSZEYKyAVvd3PQ/sVCl3f3uwflZOlOpT6zDD6H/z1
        EvwjoyFMjryAO75/6I3LDgjMO87CpthuoCyp+edSJQU6AZxMicCWBaVkV9jK/4nCc4DzuT
        8e2XUENN6tUTtoJQWd080s90zU9jF3BdfSbWgaWhan7ClSS70dx+IOUlWMcz63Ty9nhI9b
        cSywUqdcBQ==
        -----END OPENSSH PRIVATE KEY-----
        """;

    /// <summary>The P-256 key in SEC 1 (<c>EC PRIVATE KEY</c>), with its curve and public point.</summary>
    internal const string EcdsaP256Sec1 = """
        -----BEGIN EC PRIVATE KEY-----
        MHcCAQEEIBS4klIjY15vtrWMLRTyhR+41Zj1X93F+h9y1/9XWKBhoAoGCCqGSM49
        AwEHoUQDQgAEXf3/11N2f7mSTyKgSsIBP8836D9s27s+NXp+BlVVh8EMcDkzVoJh
        qt1um0FSZEYkrAE7crA9AzmwwnQnt17/Ag==
        -----END EC PRIVATE KEY-----
        """;

    /// <summary>The P-256 key's <c>.pub</c> file.</summary>
    internal const string EcdsaP256PublicKeyFile = "ecdsa-sha2-nistp256 AAAAE2VjZHNhLXNoYTItbmlzdHAyNTYAAAAIbmlzdHAyNTYAAABBBF39/9dTdn+5kk8ioErCAT/PN+g/bNu7PjV6fgZVVYfBDHA5M1aCYardbptBUmRGJKwBO3KwPQM5sMJ0J7de/wI= test\n";

    /// <summary>The P-256 key in <c>openssh-key-v1</c>.</summary>
    internal const string EcdsaP256OpenSsh = """
        -----BEGIN OPENSSH PRIVATE KEY-----
        b3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAAAaAAAABNlY2RzYS
        1zaGEyLW5pc3RwMjU2AAAACG5pc3RwMjU2AAAAQQRd/f/XU3Z/uZJPIqBKwgE/zzfoP2zb
        uz41en4GVVWHwQxwOTNWgmGq3W6bQVJkRiSsATtysD0DObDCdCe3Xv8CAAAAmJ/kFxaf5B
        cWAAAAE2VjZHNhLXNoYTItbmlzdHAyNTYAAAAIbmlzdHAyNTYAAABBBF39/9dTdn+5kk8i
        oErCAT/PN+g/bNu7PjV6fgZVVYfBDHA5M1aCYardbptBUmRGJKwBO3KwPQM5sMJ0J7de/w
        IAAAAgFLiSUiNjXm+2tYwtFPKFH7jVmPVf3cX6H3LX/1dYoGEAAAAA
        -----END OPENSSH PRIVATE KEY-----
        """;

    /// <summary>The P-256 key in PKCS #8.</summary>
    internal const string EcdsaP256Pkcs8 = """
        -----BEGIN PRIVATE KEY-----
        MIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQgFLiSUiNjXm+2tYwt
        FPKFH7jVmPVf3cX6H3LX/1dYoGGhRANCAARd/f/XU3Z/uZJPIqBKwgE/zzfoP2zb
        uz41en4GVVWHwQxwOTNWgmGq3W6bQVJkRiSsATtysD0DObDCdCe3Xv8C
        -----END PRIVATE KEY-----
        """;

    /// <summary>The P-256 key in SEC 1 without its public point (<c>openssl ec -no_public</c>).</summary>
    internal const string EcdsaP256Sec1WithoutPublicKey = """
        -----BEGIN EC PRIVATE KEY-----
        MDECAQEEIBS4klIjY15vtrWMLRTyhR+41Zj1X93F+h9y1/9XWKBhoAoGCCqGSM49
        AwEH
        -----END EC PRIVATE KEY-----
        """;

    /// <summary>The P-256 key in SEC 1 with legacy PEM encryption, AES-128-CBC.</summary>
    internal const string EcdsaP256Sec1Aes128 = """
        -----BEGIN EC PRIVATE KEY-----
        Proc-Type: 4,ENCRYPTED
        DEK-Info: AES-128-CBC,A0F14DCDEAC85F9AC36D2EB6ED17C31C

        1rLVRU85C4fVVPDTq3bPTt/q7Z1WHk1n/zJV0xgzvJ4lCCEHcEg7vEn7RSc+lcUX
        fju5o4PKa5D0OEKvtv5EBWBf0H0AgBsJOoklCVtMUM17srnEzTBFBFzdcMfjJcYq
        zEG0vfG6ZPVYPQga3G1zjHZGXUV5xpNRDrrtfk9r4YU=
        -----END EC PRIVATE KEY-----
        """;

    /// <summary>A P-384 key in SEC 1.</summary>
    internal const string EcdsaP384Sec1 = """
        -----BEGIN EC PRIVATE KEY-----
        MIGkAgEBBDAWiUUrcLpKS41HR8g5FMTbDN2zpLYg17wm1clJXBZqGDBCGC9xmdsX
        Hdm3K3l+K2mgBwYFK4EEACKhZANiAAS+vLw+XvPxxCwbwk7BEdyA8sLbB0G5hLQ6
        /GVAAGTPl3cWswvRTzfCyz0nv/7X+3niT8BWokY9/os4/h6y6vM9re6dD90FQugs
        HR2Sh8CSwInRx1WhB4EqfD5X0VOZDzs=
        -----END EC PRIVATE KEY-----
        """;

    /// <summary>The P-384 key's <c>.pub</c> file.</summary>
    internal const string EcdsaP384PublicKeyFile = "ecdsa-sha2-nistp384 AAAAE2VjZHNhLXNoYTItbmlzdHAzODQAAAAIbmlzdHAzODQAAABhBL68vD5e8/HELBvCTsER3IDywtsHQbmEtDr8ZUAAZM+XdxazC9FPN8LLPSe//tf7eeJPwFaiRj3+izj+HrLq8z2t7p0P3QVC6CwdHZKHwJLAidHHVaEHgSp8PlfRU5kPOw== test\n";

    /// <summary>A P-521 key in <c>openssh-key-v1</c>.</summary>
    internal const string EcdsaP521OpenSsh = """
        -----BEGIN OPENSSH PRIVATE KEY-----
        b3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAAArAAAABNlY2RzYS
        1zaGEyLW5pc3RwNTIxAAAACG5pc3RwNTIxAAAAhQQBPQcZl8aVwI6aomal28LEVsdRLVjE
        +IS7gpGiyem5u/tWF9L6BfbdzQEIaAKxwvidzOmU/tBcUohwOsrDW2rY3L8BLcwTmeefif
        lt/9ojn9z6ePrrJT1vtQ4mY/GZ9obC/NEEerJS3avDivzuzLVQCEKM6uUThkUgrOst+uSJ
        RYA6EDQAAAEIJFOiVSRTolUAAAATZWNkc2Etc2hhMi1uaXN0cDUyMQAAAAhuaXN0cDUyMQ
        AAAIUEAT0HGZfGlcCOmqJmpdvCxFbHUS1YxPiEu4KRosnpubv7VhfS+gX23c0BCGgCscL4
        nczplP7QXFKIcDrKw1tq2Ny/AS3ME5nnn4n5bf/aI5/c+nj66yU9b7UOJmPxmfaGwvzRBH
        qyUt2rw4r87sy1UAhCjOrlE4ZFIKzrLfrkiUWAOhA0AAAAQgGtco7sxXqJGtOqxIbB1p59
        /YMMCz/OS9lTCvVeIpg+uN4OGIKuU8yOxBZoNOK/8Q0V2oG++WIjzNHWyfsbsLhtxwAAAA
        R0ZXN0AQIDBAUG
        -----END OPENSSH PRIVATE KEY-----
        """;

    /// <summary>The P-521 key's <c>.pub</c> file.</summary>
    internal const string EcdsaP521PublicKeyFile = "ecdsa-sha2-nistp521 AAAAE2VjZHNhLXNoYTItbmlzdHA1MjEAAAAIbmlzdHA1MjEAAACFBAE9BxmXxpXAjpqiZqXbwsRWx1EtWMT4hLuCkaLJ6bm7+1YX0voF9t3NAQhoArHC+J3M6ZT+0FxSiHA6ysNbatjcvwEtzBOZ55+J+W3/2iOf3Pp4+uslPW+1DiZj8Zn2hsL80QR6slLdq8OK/O7MtVAIQozq5ROGRSCs6y365IlFgDoQNA== test\n";

    /// <summary>A secp256k1 key in SEC 1: a curve SSH does not use.</summary>
    internal const string EcdsaSecp256k1Sec1 = """
        -----BEGIN EC PRIVATE KEY-----
        MHQCAQEEIMpaoj7eM9ZRP78HnhhCEVmoSOe0vDFKyfL1nsa/AkCFoAcGBSuBBAAK
        oUQDQgAEwjO/VgmibqaD9clDZGUb25+WTQaI9PF9xk1yuKd2rcy6NlBm9lheuKdo
        YdBP3CmWJiIqOO9j/2fDyVmhXGvrcg==
        -----END EC PRIVATE KEY-----
        """;

    /// <summary>An Ed25519 key in <c>openssh-key-v1</c>: BL-681's to read.</summary>
    internal const string Ed25519OpenSsh = """
        -----BEGIN OPENSSH PRIVATE KEY-----
        b3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAAAMwAAAAtzc2gtZW
        QyNTUxOQAAACCbVP8GDPMQ+Tk9vSmKp6PpTPcybdYvmRJnW1BHD2RHFwAAAIhIh3ZgSId2
        YAAAAAtzc2gtZWQyNTUxOQAAACCbVP8GDPMQ+Tk9vSmKp6PpTPcybdYvmRJnW1BHD2RHFw
        AAAEAIbvGXwX/x07bGNHsQNDMl+xvBCWz7XeVcmnVi3xfmuJtU/wYM8xD5OT29KYqno+lM
        9zJt1i+ZEmdbUEcPZEcXAAAABHRlc3QB
        -----END OPENSSH PRIVATE KEY-----
        """;

    /// <summary>An Ed25519 key in PKCS #8: BL-681's to read.</summary>
    internal const string Ed25519Pkcs8 = """
        -----BEGIN PRIVATE KEY-----
        MC4CAQAwBQYDK2VwBCIEIGg6YP+pEjIoQ/aN3n1F+EzXS4wlRWrtBB5VPeSn6Eox
        -----END PRIVATE KEY-----
        """;
}
