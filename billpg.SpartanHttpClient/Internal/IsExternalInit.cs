#if NETSTANDARD2_0
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Polyfill so `init`-only properties and records compile on netstandard2.0, which
    /// predates this marker type. The compiler only checks for its presence by name -
    /// it's never instantiated or referenced at runtime.
    /// </summary>
    internal static class IsExternalInit
    {
    }
}
#endif
