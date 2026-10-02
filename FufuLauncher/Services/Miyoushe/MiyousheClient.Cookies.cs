namespace FufuLauncher.Services.Miyoushe;

public sealed partial class MiyousheClient
{
    private string[] LTokenCookies => CredentialCookies("ltoken", "ltuid", "ltoken_v2", "ltuid_v2", "ltmid_v2");
    private string[] CookieTokenCookies => CredentialCookies("cookie_token", "account_id", "cookie_token_v2", "account_id_v2", "account_mid_v2");

    private string[] CredentialCookies(string tokenV1, string uidV1, string tokenV2, string uidV2, string midV2)
    {
        if (_context == null || AccountUid.Length == 0) return [];
        string token = FirstCookie(tokenV2), uid = FirstCookie(uidV2), mid = FirstCookie(midV2);
        if (token.Length > 0 && mid.Length > 0 && uid == AccountUid)
            return [tokenV2 + "=" + token, uidV2 + "=" + uid, midV2 + "=" + mid];
        token = FirstCookie(tokenV1); uid = FirstCookie(uidV1);
        if (token.Length > 0 && uid == AccountUid) return [tokenV1 + "=" + token, uidV1 + "=" + uid];
        return [];
    }

    private string BuildCookies(bool ltokenOnly) => string.Join("; ",
        ltokenOnly ? LTokenCookies : CookieTokenCookies.Concat(LTokenCookies));
}
