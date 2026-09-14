using System.Security.Cryptography;
using System.Text;

namespace WinLLMUsage.Core.Accounts;

public static class ProviderAccountId
{
    public static readonly IReadOnlySet<string> Families = new HashSet<string>(StringComparer.Ordinal)
    {
        "claude",
        "codex",
    };

    public static string Make(string family, string identityKey)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(identityKey.ToLowerInvariant()));
        var hash8 = Convert.ToHexString(digest.AsSpan(0, 4)).ToLowerInvariant();
        return $"{family}@{hash8}";
    }

    public static string FamilyOf(string cardId)
    {
        var at = cardId.IndexOf('@');
        return at < 0 ? cardId : cardId[..at];
    }

    public static bool Matches(string cardId, string token)
    {
        var normalized = token.ToLowerInvariant();
        return cardId.Equals(normalized, StringComparison.Ordinal)
               || FamilyOf(cardId).Equals(normalized, StringComparison.Ordinal);
    }
}
