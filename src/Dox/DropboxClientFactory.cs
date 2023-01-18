using Dox.Properties;

namespace Dox;

public static class DropboxClientFactory
{
    internal static void ResetAuthentication()
    {
        Settings.Default.UserSecret = string.Empty;
        Settings.Default.UserToken = string.Empty;
        Settings.Default.Save();
    }

    public static async Task<DropboxClient> CreateDropboxClient(int timeoutSeconds)
    {
        var assembly = Assembly.GetExecutingAssembly();

        await using var stream = assembly.GetManifestResourceStream("Dox.appSettings.json");
        using var textStreamReader = new StreamReader(stream!);
        var key = (await textStreamReader.ReadLineAsync())?.Trim();
        var secret = (await textStreamReader.ReadLineAsync())?.Trim();

        var result = await GetAccessTokens(key, secret);

        var config = new DropboxClientConfig("Dox")
        {
            HttpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(timeoutSeconds)
            }
        };

        return new DropboxClient(result.UserToken, result.RefreshToken, key, secret, config);
    }

    private static async Task<TokenResult> GetAccessTokens(string? key, string? secret)
    {
        if (!string.IsNullOrEmpty(Settings.Default.UserToken) && !string.IsNullOrEmpty(Settings.Default.RefreshToken))
        {
            return new TokenResult(Settings.Default.UserToken, Settings.Default.RefreshToken);
        }

        Console.WriteLine(
            "You'll need to authorize this account with PneumaticTube; a browser window will now open asking you to log into Dropbox and allow the app. When you've done that, you'll be given an access key. Enter the key here and hit Enter:");

        var oauth2State = Guid.NewGuid().ToString("N");

        // Pop open the authorization page in the default browser
        var url = DropboxOAuth2Helper.GetAuthorizeUri(OAuthResponseType.Code, key, (Uri)null, oauth2State, tokenAccessType: TokenAccessType.Offline);
        Process.Start(url.ToString());

        // Wait for the user to enter the key
        var token = Console.ReadLine();

        var response = await DropboxOAuth2Helper.ProcessCodeFlowAsync(token, key, secret);

        // Save the token 
        Settings.Default.UserToken = response.AccessToken;
        Settings.Default.RefreshToken = response.RefreshToken;
        Settings.Default.Save();


        return new TokenResult(response.AccessToken, response.RefreshToken);
    }

    private struct TokenResult
    {
        public readonly string UserToken;
        public readonly string RefreshToken;

        public TokenResult(string userToken, string refreshToken)
        {
            UserToken = userToken;
            RefreshToken = refreshToken;
        }
    }
}