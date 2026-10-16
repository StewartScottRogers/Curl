using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Share;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;
using Libcurl.Share;
using Libcurl.UnitTests.Easy;
using Libcurl.UnitTests.Global;

namespace Libcurl.UnitTests.Share;

[TestClass]
public sealed class CurlShareTests
{
    [TestMethod]
    [DataRow(CurlLockData.Cookie)]
    [DataRow(CurlLockData.Dns)]
    [DataRow(CurlLockData.SslSession)]
    [DataRow(CurlLockData.Connect)]
    [DataRow(CurlLockData.Psl)]
    [DataRow(CurlLockData.Hsts)]
    public void SetOption_ShareAndUnshareOfEachKind_AskTheEngine(CurlLockData data)
    {
        var engine = new FakeShareHandle();
        var share = new CurlShare(engine);

        Assert.AreEqual(CurlShareCode.Ok, share.SetOption(CurlShareOption.Share, data));
        Assert.AreEqual(CurlShareCode.Ok, share.SetOption(CurlShareOption.Unshare, data));

        CollectionAssert.AreEqual(new[] { $"Share {data}", $"Unshare {data}" }, engine.Calls);
    }

    [TestMethod]
    [DataRow(2, DisplayName = "an int")]
    [DataRow(2L, DisplayName = "a long")]
    public void SetOption_ShareGivenTheKindsNumber_SharesThatKind(object number)
    {
        var engine = new FakeShareHandle();
        var share = new CurlShare(engine);

        Assert.AreEqual(CurlShareCode.Ok, share.SetOption(CurlShareOption.Share, number));

        CollectionAssert.AreEqual(new[] { "Share Cookie" }, engine.Calls);
    }

    [TestMethod]
    [DataRow(CurlShareOption.Share, CurlLockData.None, DisplayName = "SHARE of NONE")]
    [DataRow(CurlShareOption.Share, CurlLockData.Share, DisplayName = "SHARE of SHARE")]
    [DataRow(CurlShareOption.Unshare, (CurlLockData)8, DisplayName = "UNSHARE of 8")]
    [DataRow(CurlShareOption.Share, "COOKIE", DisplayName = "SHARE given a string")]
    [DataRow(CurlShareOption.Unshare, null, DisplayName = "UNSHARE given null")]
    [DataRow(CurlShareOption.LockFunction, "lock", DisplayName = "LOCKFUNC given a string")]
    [DataRow(CurlShareOption.UnlockFunction, "unlock", DisplayName = "UNLOCKFUNC given a string")]
    [DataRow(CurlShareOption.None, null, DisplayName = "NONE")]
    [DataRow((CurlShareOption)6, null, DisplayName = "option 6")]
    public void SetOption_AnUnshareableKindAWrongValueOrAnUnknownOption_AnswersBadOptionWithoutAskingTheEngine(
        CurlShareOption option, object? value)
    {
        var engine = new FakeShareHandle();
        var share = new CurlShare(engine);

        Assert.AreEqual(CurlShareCode.BadOption, share.SetOption(option, value));

        Assert.IsEmpty(engine.Calls);
    }

    [TestMethod]
    public void SetOption_TheEnginesAnswer_IsReturned()
    {
        var engine = new FakeShareHandle { Answer = CurlShareCode.InUse };
        var share = new CurlShare(engine);

        Assert.AreEqual(CurlShareCode.InUse, share.SetOption(CurlShareOption.Share, CurlLockData.Dns));
    }

    [TestMethod]
    public void SetOption_LockFunctions_ReachTheEngineWhichCallsThemWithItsArguments()
    {
        var engine = new FakeShareHandle();
        var share = new CurlShare(engine);
        var transfer = new FakeEasyTransfer();
        var calls = new List<string>();
        ShareLockCallback lockCallback = (t, data, access) => calls.Add($"lock {ReferenceEquals(t, transfer)} {data} {access}");
        ShareUnlockCallback unlockCallback = (t, data) => calls.Add($"unlock {t is null} {data}");

        Assert.AreEqual(CurlShareCode.Ok, share.SetOption(CurlShareOption.LockFunction, lockCallback));
        Assert.AreEqual(CurlShareCode.Ok, share.SetOption(CurlShareOption.UnlockFunction, unlockCallback));
        engine.LockCallback!(transfer, CurlLockData.Cookie, CurlLockAccess.Single);
        engine.UnlockCallback!(null, CurlLockData.Cookie);

        CollectionAssert.AreEqual(new[] { "lock True Cookie Single", "unlock True Cookie" }, calls);
        CollectionAssert.AreEqual(new[] { "SetLockCallback", "SetUnlockCallback" }, engine.Calls);
    }

    [TestMethod]
    public void SetOption_LockFunctionsGivenNull_ClearTheEnginesCallbacks()
    {
        var engine = new FakeShareHandle();
        var share = new CurlShare(engine);
        share.SetOption(CurlShareOption.LockFunction, new ShareLockCallback((_, _, _) => { }));
        share.SetOption(CurlShareOption.UnlockFunction, new ShareUnlockCallback((_, _) => { }));

        Assert.AreEqual(CurlShareCode.Ok, share.SetOption(CurlShareOption.LockFunction, null));
        Assert.AreEqual(CurlShareCode.Ok, share.SetOption(CurlShareOption.UnlockFunction, null));

        Assert.IsNull(engine.LockCallback);
        Assert.IsNull(engine.UnlockCallback);
    }

    [TestMethod]
    public void SetOption_UserData_KeepsAnyValueWithoutAskingTheEngine()
    {
        var engine = new FakeShareHandle();
        var share = new CurlShare(engine);
        var value = new object();

        Assert.AreEqual(CurlShareCode.Ok, share.SetOption(CurlShareOption.UserData, value));
        Assert.AreSame(value, share.UserData);

        Assert.IsEmpty(engine.Calls);
    }

    [TestMethod]
    public void Cleanup_TheEngineFreesTheHandle_CleansItUpAndLaterCallsAnswerInvalid()
    {
        var engine = new FakeShareHandle();
        var share = new CurlShare(engine);

        Assert.AreEqual(CurlShareCode.Ok, share.Cleanup());

        Assert.IsTrue(share.IsCleanedUp);
        Assert.AreEqual(CurlShareCode.Invalid, share.Cleanup());
        Assert.AreEqual(CurlShareCode.Invalid, share.SetOption(CurlShareOption.Share, CurlLockData.Dns));
        CollectionAssert.AreEqual(new[] { "Cleanup" }, engine.Calls);
    }

    [TestMethod]
    public void Cleanup_TheHandleInUse_AnswersInUseAndStaysUsable()
    {
        var engine = new FakeShareHandle { Answer = CurlShareCode.InUse };
        var share = new CurlShare(engine);

        Assert.AreEqual(CurlShareCode.InUse, share.Cleanup());

        Assert.IsFalse(share.IsCleanedUp);
        engine.Answer = CurlShareCode.Ok;
        Assert.AreEqual(CurlShareCode.Ok, share.SetOption(CurlShareOption.Share, CurlLockData.Dns));
    }

    [TestMethod]
    public void EasySetOptionShare_ACurlShare_AttachesTheEnginesHandleAndNullDetachesIt()
    {
        var engine = new FakeShareHandle();
        var share = new CurlShare(engine);
        var transfer = new FakeEasyTransfer();
        using var easy = new CurlEasy(transfer, EasyOptionDispatch.Default);

        Assert.AreEqual(CurlCode.Ok, easy.SetOption(CurlOption.Share, share));
        Assert.AreSame(engine, transfer.Options.Behaviour.Share);

        Assert.AreEqual(CurlCode.Ok, easy.SetOption(CurlOption.Share, null));
        Assert.IsNull(transfer.Options.Behaviour.Share);
    }

    [TestMethod]
    public void EasySetOptionShare_ACleanedUpCurlShare_Detaches()
    {
        var share = new CurlShare(new FakeShareHandle());
        var transfer = new FakeEasyTransfer();
        using var easy = new CurlEasy(transfer, EasyOptionDispatch.Default);
        easy.SetOption(CurlOption.Share, share);
        share.Cleanup();

        Assert.AreEqual(CurlCode.Ok, easy.SetOption(CurlOption.Share, share));

        Assert.IsNull(transfer.Options.Behaviour.Share);
    }

    [TestMethod]
    public async Task ShareInit_CreatesAShareOverTheEnginesNewShareHandle()
    {
        var engine = new FakeCurlEngine();
        var library = CurlGlobal.InitCounting(engine, CurlGlobalFlags.Default, new GlobalInitCounter());

        var share = library.ShareInit();

        Assert.AreSame(engine.Created.Single(), share.EngineHandle);
        await library.DisposeAsync();
        Assert.ThrowsExactly<ObjectDisposedException>(() => library.ShareInit());
    }
}
