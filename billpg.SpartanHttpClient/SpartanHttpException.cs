using System;

namespace billpg.SpartanHttpClient;

/// <summary>
/// Thrown when a request could not be completed - a malformed URL, a DNS or TCP
/// connection failure, a rejected TLS certificate, or a timeout. Title is a short,
/// user-facing summary; Message (inherited) carries the diagnostic detail.
/// </summary>
public class SpartanHttpException : Exception
{
    public string Title { get; }

    public SpartanHttpException(string title, string message)
        : base(message)
    {
        Title = title;
    }
}
