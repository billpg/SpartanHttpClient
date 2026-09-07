using billpg.SpartanHttpClient;
using billpg.SpartanHttpClient.Internal;

namespace billpg.SpartanHttpClientTests;

[TestClass]
public class ResponseLineParserTests
{
    [TestMethod]
    public void StatusLine_ParsesCode()
    {
        var state = ResponseParseState.Initial.WithResponseLine("HTTP/1.1 200 OK");
        Assert.AreEqual(200, state.StatusCode);
        Assert.AreEqual(ParseStage.Headers, state.Stage);
    }

    [TestMethod]
    public void Headers_ParseNameAndValue()
    {
        var state = ResponseParseState.Initial
            .WithResponseLine("HTTP/1.1 200 OK")
            .WithResponseLine("X-Farm: Rutabaga Farms Inc");

        Assert.AreEqual("Rutabaga Farms Inc", state.Headers["X-Farm"]);
    }

    [TestMethod]
    public void RepeatedHeaderName_CombinesWithComma()
    {
        var state = ResponseParseState.Initial
            .WithResponseLine("HTTP/1.1 200 OK")
            .WithResponseLine("X-Vegetable: Rutabaga")
            .WithResponseLine("X-Vegetable: Turnip");

        Assert.AreEqual("Rutabaga,Turnip", state.Headers["X-Vegetable"]);
    }

    [TestMethod]
    public void FoldedContinuationLine_AppendsToPreviousHeader()
    {
        var state = ResponseParseState.Initial
            .WithResponseLine("HTTP/1.1 200 OK")
            .WithResponseLine("X-Vegetable: Rutabaga")
            .WithResponseLine("    and Swede");

        Assert.AreEqual("Rutabaga and Swede", state.Headers["X-Vegetable"]);
    }

    [TestMethod]
    public void BlankLine_EndsHeaders()
    {
        var state = ResponseParseState.Initial
            .WithResponseLine("HTTP/1.1 200 OK")
            .WithResponseLine("X-Vegetable: Rutabaga")
            .WithResponseLine("");

        Assert.AreEqual(ParseStage.Done, state.Stage);
    }

    [TestMethod]
    public void NonStatusFirstLine_Throws()
    {
        Assert.ThrowsExactly<SpartanHttpException>(
            () => ResponseParseState.Initial.WithResponseLine("not a status line"));
    }

    [TestMethod]
    public void HeaderLineWithNoColon_Throws()
    {
        var afterBanner = ResponseParseState.Initial.WithResponseLine("HTTP/1.1 200 OK");
        Assert.ThrowsExactly<SpartanHttpException>(
            () => afterBanner.WithResponseLine("not a header"));
    }

    [TestMethod]
    public void FoldedContinuationWithNoPrecedingHeader_Throws()
    {
        var afterBanner = ResponseParseState.Initial.WithResponseLine("HTTP/1.1 200 OK");
        Assert.ThrowsExactly<SpartanHttpException>(
            () => afterBanner.WithResponseLine("    stray continuation"));
    }
}
