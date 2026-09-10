namespace billpg.SpartanHttpClient;

public interface ISpartanEngine
{
    SpartanRequest Request(Uri url);
}

/// <summary>The real engine - builds a SpartanRequest with its default Runner, which is
/// already SpartanHttpFetcher.RunAsync, so there's nothing further to wire up here. Tests
/// swap this out for a fake ISpartanEngine whose requests run against pre-built responses
/// instead.</summary>
public sealed class SpartanEngine : ISpartanEngine
{
    public SpartanRequest Request(Uri url)
        => new SpartanRequest(url);
}

public static class SpartanEngineExtensions
{
    public static SpartanRequest Request(this ISpartanEngine engine, string url)
        => engine.Request(new Uri(url));
}