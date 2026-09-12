using billpg.SpartanHttpClient;

namespace billpg.SpartanHttpClientTests;

[TestClass]
public class SpartanRequestTests
{
    [TestMethod]
    public void WithHeader_SetsHeaderValue()
    {
        var request = new SpartanRequest("https://rutabaga.example/")
            .WithHeader("X-Farm", "Rutabaga Farms Inc");

        Assert.AreEqual("Rutabaga Farms Inc", request.Headers["X-Farm"]);
    }

    [TestMethod]
    public void WithHeader_RepeatedName_CombinesWithComma()
    {
        var request = new SpartanRequest("https://rutabaga.example/")
            .WithHeader("X-Vegetable", "Rutabaga")
            .WithHeader("X-Vegetable", "Swede");

        Assert.AreEqual("Rutabaga,Swede", request.Headers["X-Vegetable"]);
    }

    [TestMethod]
    public void WithHeader_ReturnsNewInstance_OriginalUnchanged()
    {
        var original = new SpartanRequest("https://rutabaga.example/");
        var withHeader = original.WithHeader("X-Vegetable", "Rutabaga");

        Assert.IsEmpty(original.Headers);
        Assert.HasCount(1, withHeader.Headers);
    }

    [TestMethod]
    public void DefaultTimeout_IsTenSeconds()
    {
        var request = new SpartanRequest("https://rutabaga.example/");
        Assert.AreEqual(TimeSpan.FromSeconds(10), request.Timeout);
    }

    [TestMethod]
    public void WithTimeout_Overrides()
    {
        var request = new SpartanRequest("https://rutabaga.example/")
            .WithTimeout(TimeSpan.FromMilliseconds(250));

        Assert.AreEqual(TimeSpan.FromMilliseconds(250), request.Timeout);
    }

    [TestMethod]
    public void DefaultMaxResponseBytes_IsUnbounded()
    {
        var request = new SpartanRequest("https://rutabaga.example/");
        Assert.IsNull(request.MaxResponseBytes);
    }

    [TestMethod]
    public void WithHeader_NullValue_SkipsHeader()
    {
        var request = new SpartanRequest("https://rutabaga.example/")
            .WithHeader("X-Vegetable", null);

        Assert.IsEmpty(request.Headers);
    }

    [TestMethod]
    public void WithHeader_NullValue_LeavesEarlierValueForSameNameUntouched()
    {
        var request = new SpartanRequest("https://rutabaga.example/")
            .WithHeader("X-Vegetable", "Rutabaga")
            .WithHeader("X-Vegetable", null);

        Assert.AreEqual("Rutabaga", request.Headers["X-Vegetable"]);
    }

    /// <summary>The documented way to make code under test substitute a fake instead of
    /// hitting the network: no engine or interface from this library needed, since
    /// SpartanRequest's own Runner property is already the seam.</summary>
    [TestMethod]
    public async Task WithRunner_ReplacesHowRunIsExecuted()
    {
        var fakeResponse = new SpartanResponse().WithStatusCode(200).WithBody("Rutabaga Farms Inc");
        SpartanRequest? requestSeenByRunner = null;

        var request = new SpartanRequest("https://rutabaga.example/")
            .WithHeader("X-Vegetable", "Rutabaga")
            .WithRunner((req, ct) =>
            {
                requestSeenByRunner = req;
                return Task.FromResult(fakeResponse);
            });

        var response = await request.Run();

        Assert.AreSame(fakeResponse, response);
        Assert.AreSame(request, requestSeenByRunner);
    }
}
