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
}
