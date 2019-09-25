using Blindrelay.Core.Api;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Blindrelay.Core
{
    public class LoggedInUser
    {
        public string Email { get; internal set; }
        public string UserId { get; internal set; }
    }

    public partial class Client
    {
        Configuration configuration;
        Api.ApiService apiService;
        HubConnection hub;

        Dictionary<string, Keychain> keychains = new Dictionary<string, Keychain>();

        ProtectedBytes keyKey;
        ProtectedString authToken;
        ProtectedString userEmail;
        ProtectedString userId;

        CancellationTokenSource authTokenCancellation = new CancellationTokenSource();
        int authTokenExpirationMinutes = 0;

        public event EventHandler<EventArgs> LoggedIn;
        public event EventHandler<EventArgs> LoggedOut;

        public Client(Configuration configuration)
        {
            this.configuration = configuration;
            apiService = new Api.ApiService(configuration);
        }

        public LoggedInUser LoggedInUser
        {
            get
            {
                if (userEmail == null || userId == null)
                {
                    authToken = null;
                    userEmail = null;
                    userId = null;
                }

                if (authToken == null)
                    return null;

                return new LoggedInUser
                {
                    Email = userEmail.ToString(),
                    UserId = userId.ToString()
                };
            }
        }

        public bool IsLoggedIn
        {
            get => LoggedInUser != null;
        }

        private string DerivePasswordForApi(string email, string password)
        {
            var ebytes = Encoding.UTF8.GetBytes(email);
            var hash = Crypto.HashPbkdf2Sha256(password, ebytes, 32);
            return Crypto.ConvertToHexString(hash);
        }
        private ProtectedBytes DerivePasswordForEncryption(string password, byte[] encryptionSalt)
        {
            return new ProtectedBytes(Crypto.HashPbkdf2Sha256(password, encryptionSalt, 32));
        }

        private void SetKeyKey(string password, byte[] encryptionSalt)
        {
            keyKey = DerivePasswordForEncryption(password, encryptionSalt);
        }

        public class LoginResult
        {
            public string UserId { get; set; }

            public bool Locked { get; set; }

            public bool HardLocked { get; set; }

            public bool EmailConfirmed { get; set; }
            public string LockedMessage { get; set; }

            public bool Unauthorized { get; set; }

            public string Error { get; set; }

            public bool Ok { get; set; }
        }

        public async Task<LoginResult> LoginAsync(string email, string password)
        {
            try
            {
                await LogOutAsync();

                email = email.Trim().ToLower();

                var cryptoPassword = password;
                password = DerivePasswordForApi(email, password);

                var response = await apiService.UserLoginAsync(new UserLoginRequest
                {
                    Email = email,
                    Password = password,
                    ApplicationId = configuration.ApplicationId,
                    ApiKey = configuration.ApiKey
                });

                var result = new LoginResult
                {
                    EmailConfirmed = response.EmailConfirmed,
                    HardLocked = response.HardLocked,
                    Locked = response.Locked,
                    LockedMessage = response.LockedMessage,
                    UserId = response.UserId
                };

                if (string.IsNullOrWhiteSpace(response.AuthToken))
                    return result;

                authToken = new ProtectedString(response.AuthToken);
                authTokenExpirationMinutes = response.AuthTokenExpirationMinutes - 5;

                userEmail = new ProtectedString(email);
                userId = new ProtectedString(response.UserId);

                SetKeyKey(cryptoPassword, response.EncryptionHashSalt);

                await RefreshKeyCacheAsync();

                authTokenCancellation = new CancellationTokenSource();
                _ = Task.Run(() => AuthTokenHandler(), authTokenCancellation.Token);

                result.Ok = true;

                if (LoggedIn != null)
                    LoggedIn.Invoke(null, null);

                return result;
            }
            catch (ApiException ax)
            {
                if (ax.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    return new LoginResult
                    {
                        Unauthorized = true,
                        Error = $"Are you using the correct API Key? -> {ax.Errors.ToJsonString()}"
                    };
                else return new LoginResult
                {
                    Error = ax.Message
                };

            }
            catch (Exception x)
            {
                return new LoginResult
                {
                    Error = x.Message
                };
            }
        }

        public async Task LogOutAsync()
        {

            keyKey = null;
            userEmail = null;
            authToken = null;
            userId = null;
            keychains.Clear();
            keychains = new Dictionary<string, Keychain>();

            authTokenCancellation.Cancel();

            try
            {
                if (hub != null)
                {
                    try
                    {
                        await hub.StopAsync();
                    }
                    catch { }

                    await hub.DisposeAsync();
                    hub = null;
                }
            }
            catch { }

            if (LoggedOut != null)
                LoggedOut.Invoke(null, null);
        }

        async Task AuthTokenHandler()
        {
            try
            {
                var token = authTokenCancellation.Token;
                while (token.IsCancellationRequested == false)
                {
                    await Task.Delay(TimeSpan.FromMinutes(authTokenExpirationMinutes), token);

                    await RenewAuthTokenAsync();

                    if (authToken == null)
                        return;
                }
            }
            catch { }
        }

        async Task RenewAuthTokenAsync()
        {
            if (authToken == null)
                return;
            try
            {
                var response = await apiService.TokenRenewAsync(new TokenRenewRequest { }, authToken.ToString());
                if (string.IsNullOrWhiteSpace(response.AuthToken) == false)
                {
                    authToken = new ProtectedString(response.AuthToken);
                    return;
                }

                await LogOutAsync();
            }
            catch (ApiException ax)
            {
                if (ax.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    await LogOutAsync();
                }
            }
        }
    }
}
