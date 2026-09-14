using System.Text.Json.Serialization;

namespace WinLLMUsage.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter<ErrorCategory>))]
public enum ErrorCategory
{
    NotLoggedIn,
    AuthExpired,
    AuthInvalid,
    CredentialAccess,
    Network,
    Decoding,
    Http4xx,
    Http5xx,
    RateLimited,
    NotAvailable,
    Other,
    UnsupportedCredentialSource,
}

public static class ErrorCategoryWire
{
    public static string ToWire(ErrorCategory category) => category switch
    {
        ErrorCategory.NotLoggedIn => "not_logged_in",
        ErrorCategory.AuthExpired => "auth_expired",
        ErrorCategory.AuthInvalid => "auth_invalid",
        ErrorCategory.CredentialAccess => "credential_access",
        ErrorCategory.Network => "network",
        ErrorCategory.Decoding => "decoding",
        ErrorCategory.Http4xx => "http_4xx",
        ErrorCategory.Http5xx => "http_5xx",
        ErrorCategory.RateLimited => "rate_limited",
        ErrorCategory.NotAvailable => "not_available",
        ErrorCategory.UnsupportedCredentialSource => "unsupported_credential_source",
        _ => "other",
    };

    public static ErrorCategory FromHttpStatus(int status) => status switch
    {
        429 => ErrorCategory.RateLimited,
        >= 400 and < 500 => ErrorCategory.Http4xx,
        >= 500 and < 600 => ErrorCategory.Http5xx,
        _ => ErrorCategory.Other,
    };
}
