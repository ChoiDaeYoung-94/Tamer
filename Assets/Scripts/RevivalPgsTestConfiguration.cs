#if UNITY_EDITOR || TAMER_PGS_HARNESS
using System;
using System.Text.RegularExpressions;
using PlayFab;
using PlayFab.ClientModels;

namespace AD
{
    [Serializable]
    public sealed class RevivalPgsTestConfiguration
    {
        public const string ApplicationId = "com.AeDeong.MonsterTamer.iaptest";
        public const string Title = "12B656";
        public string testTitle;
        public string webClientId;
        public string gameId;

        public void Validate(string package)
        {
            if (package != ApplicationId || testTitle != Title)
                throw new InvalidOperationException("PGS test requires the dedicated package and title 12B656.");
            if (string.IsNullOrWhiteSpace(webClientId) ||
                !Regex.IsMatch(webClientId, @"^\d+-[A-Za-z0-9_-]+\.apps\.googleusercontent\.com$"))
                throw new InvalidOperationException("Configure a local Web OAuth client ID before building the PGS test.");
            if (string.IsNullOrEmpty(gameId) || !Regex.IsMatch(gameId, @"^\d+$"))
                throw new InvalidOperationException("Configure the approved PGS game project ID.");
        }

        public LoginWithGooglePlayGamesServicesRequest CreateRequest(string code)
        {
            Validate(ApplicationId);
            if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Fresh server code required.");
            return new LoginWithGooglePlayGamesServicesRequest
            {
                TitleId = Title, ServerAuthCode = code, CreateAccount = false,
                AuthenticationContext = new PlayFabAuthenticationContext()
            };
        }
    }
}
#endif
