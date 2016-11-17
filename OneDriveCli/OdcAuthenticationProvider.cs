using Microsoft.Graph;
using Microsoft.OneDrive.Sdk.Authentication;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace OneDriveCli
{
    public class OdcAuthenticationProvider : IAuthenticationProvider
    {
        private OAuthHelper oh;
        public AccountSession accountSession { get; private set; }

        private string ClientId { get; set; }
        private string ClientSecret { get; set; }

        public OdcAuthenticationProvider(string clientId, string clientSecret, AccountSession session = null)
        {
            oh = new OAuthHelper();

            this.ClientId = clientId;
            this.ClientSecret = clientSecret;

            accountSession = session;
        }

        public Uri AuthenticationUrl()
        {
            return new Uri(oh.GetAuthorizationCodeRequestUrl(ClientId, null, new string[] { "onedrive.readwrite", "offline_access" }));
        }

        public async Task<AccountSession> AuthenticateTokenAsync(string authCode)
        {
            HttpWebRequest webReq = HttpWebRequest.CreateHttp(Microsoft.OneDrive.Sdk.Authentication.OAuthConstants.MicrosoftAccountTokenServiceUrl);
            webReq.Method = "POST";
            webReq.ContentType = "application/x-www-form-urlencoded";

            using (StreamWriter sw = new StreamWriter(await webReq.GetRequestStreamAsync()))
                await sw.WriteAsync(oh.GetAuthorizationCodeRedemptionRequestBody(authCode, ClientId, null, null, ClientSecret));

            using (HttpWebResponse webRes = (HttpWebResponse)webReq.GetResponse())
            using (StreamReader sr = new StreamReader(webRes.GetResponseStream()))
                return accountSession = new AccountSession(JsonConvert.DeserializeObject<IDictionary<string, string>>(await sr.ReadToEndAsync()));
        }

        private async Task RedeemTokenAsync()
        {
            if (!accountSession.CanRefresh) throw new Exception("AccountSession is not refreshable (scope \"offline_access\" not fullfilled, RefreshToken not set)!");

            HttpWebRequest webReq = HttpWebRequest.CreateHttp(Microsoft.OneDrive.Sdk.Authentication.OAuthConstants.MicrosoftAccountTokenServiceUrl);
            webReq.Method = "POST";
            webReq.ContentType = "application/x-www-form-urlencoded";

            using (StreamWriter sw = new StreamWriter(await webReq.GetRequestStreamAsync()))
                await sw.WriteAsync(oh.GetRefreshTokenRequestBody(accountSession.RefreshToken, ClientId, null, null, ClientSecret));

            using (HttpWebResponse webRes = (HttpWebResponse)await webReq.GetResponseAsync())
            using (StreamReader sr = new StreamReader(webRes.GetResponseStream()))
            {
                AccountSession ac = new AccountSession(Newtonsoft.Json.JsonConvert.DeserializeObject<IDictionary<string, string>>(await sr.ReadToEndAsync()));
                if (ac != null && !string.IsNullOrEmpty(ac.AccessToken))
                    accountSession = ac;
            }
        }

        public async Task AuthenticateRequestAsync(HttpRequestMessage request)
        {
            if (accountSession == null) throw new Exception("AccountSession must not be null!");
            if (accountSession.ShouldRefresh)
                await RedeemTokenAsync();

            if (accountSession == null || string.IsNullOrEmpty(accountSession.AccessToken))
                throw new Exception("Could not Authenticate!");

            if (!string.IsNullOrEmpty(accountSession.AccessToken))
            {
                var tokenTypeString = string.IsNullOrEmpty(accountSession.AccessTokenType) ? OAuthConstants.Headers.Bearer : accountSession.AccessTokenType;
                request.Headers.Authorization = new AuthenticationHeaderValue(tokenTypeString, accountSession.AccessToken);
            }
        }
    }
}
