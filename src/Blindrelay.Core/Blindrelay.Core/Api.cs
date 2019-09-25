using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Polly;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Blindrelay.Core.Api
{
    internal class ApiService
    {
        static ServiceCollection services;
        static ServiceProvider serviceProvider;

        Configuration configuration;

        public ApiService(Configuration configuration)
        {
            this.configuration = configuration;

            ServicePointManager.Expect100Continue = false;
            ServicePointManager.UseNagleAlgorithm = false;
            ServicePointManager.DefaultConnectionLimit = 200;


            if (services == null)
            {
                services = new ServiceCollection();
                services.AddHttpClient("brapi", c =>
                {
                    c.BaseAddress = new Uri(configuration.ApiUrl);
                    c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                })
                .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromMinutes(2)));

                services.AddHttpClient("brapistream", c =>
                {
                    c.BaseAddress = new Uri(configuration.ApiUrl);
                    c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                })
               .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromMinutes(10)));

                serviceProvider = services.BuildServiceProvider();
            }
        }

        #region Api POST Handling

        private static T DeserializeJsonFromStream<T>(Stream stream)
        {

            if (stream == null || stream.CanRead == false)
                return default;

            using (var sr = new StreamReader(stream))
            using (var jtr = new JsonTextReader(sr))
            {
                var js = new JsonSerializer();
                var searchResult = js.Deserialize<T>(jtr);
                return searchResult;
            }
        }

        private static ApiErrorCollection DeserializeErrors(string content, HttpStatusCode statusCode)
        {
            try
            {
                var r = JsonConvert.DeserializeObject<ApiErrorCollection>(content);
                if (r != null && r.Errors != null)
                    return r;
                return new ApiErrorCollection { Errors = new ApiError[] { new ApiError { Code = $"{statusCode}", Message = "HTTPS error." } } };
            }
            catch (Exception)
            {
                return new ApiErrorCollection { Errors = new ApiError[] { new ApiError { Code = $"{statusCode}", Message = "HTTPS error." } } };
            }
        }

        private static async Task<string> StreamToStringAsync(Stream stream)
        {
            string content = null;

            if (stream != null)
                using (var sr = new StreamReader(stream))
                    content = await sr.ReadToEndAsync();

            return content;
        }

        private async Task<ResponseType> PostAsync<ResponseType, RequestType>(string function, RequestType request, string token = null, string msClientPrincipalId = null)
        {
            var client = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("brapi");

            if (msClientPrincipalId != null)
            {
                client.DefaultRequestHeaders.Remove("x-ms-client-principal-id");
                client.DefaultRequestHeaders.Add("x-ms-client-principal-id", msClientPrincipalId);
            }

            if (token != null)
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            else
                client.DefaultRequestHeaders.Authorization = null;

            using (var requestContent = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json"))
            {
                using (var response = await client.PostAsync($"api/{function}", requestContent))
                {
                    var stream = await response.Content.ReadAsStreamAsync();

                    if (response.IsSuccessStatusCode)
                        return DeserializeJsonFromStream<ResponseType>(stream);

                    var content = await StreamToStringAsync(stream);
                    var errors = DeserializeErrors(content, response.StatusCode);

                    throw new ApiException
                    {
                        Errors = errors,
                        StatusCode = response.StatusCode
                    };
                }
            }
        }


        private async Task StreamingPostAsync<RequestType>(string function, RequestType request,
                string token, Func<Stream, Task> streamHandlerCallback)
        {
            var client = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("brapistream");

            if (token != null)
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            else
                client.DefaultRequestHeaders.Authorization = null;

            using (var requestContent = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json"))
            {
                using (var response = await client.PostAsync($"api/{function}", requestContent))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        var stream = await response.Content.ReadAsStreamAsync();
                        await streamHandlerCallback(stream);
                    }
                    else
                    {
                        var stream = await response.Content.ReadAsStreamAsync();
                        var content = await StreamToStringAsync(stream);
                        var errors = DeserializeErrors(content, response.StatusCode);

                        throw new ApiException
                        {
                            Errors = errors,
                            StatusCode = response.StatusCode
                        };
                    }
                }
            }
        }

        #endregion

        #region User Methods

        public async Task<UserLoginResponse> UserLoginAsync(UserLoginRequest request)
        {
            return await PostAsync<UserLoginResponse, UserLoginRequest>("UserLogin", request);
        }
        #endregion

        #region UserFiles Methods

        public async Task UserFileGetAsync(UserFileGetRequest request, string authToken, Func<Stream, Task> downloadHandler)
        {
            await StreamingPostAsync<UserFileGetRequest>("UserFileGet", request, authToken, downloadHandler);
        }

        public async Task<UserFilesGetResponse> UserFilesInfoGetAsync(UserFilesGetRequest request, string authToken)
        {
            return await PostAsync<UserFilesGetResponse, UserFilesGetRequest>("UserFilesInfoGet", request, authToken);
        }

        public async Task<UserAesKeysGetResponse> UserAesKeysGetAsync(UserAesKeysGetRequest request, string authToken)
        {
            return await PostAsync<UserAesKeysGetResponse, UserAesKeysGetRequest>("UserAesKeysGet", request, authToken);
        }

        public async Task<UserGroupPermissionsGetRespose> UserGroupPermissionsGetAsync(UserGroupPermissionsGetRequest request, string authToken)
        {
            return await PostAsync<UserGroupPermissionsGetRespose, UserGroupPermissionsGetRequest>("UserGroupPermissionsGet", request, authToken);
        }

        public async Task<TokenRenewResponse> TokenRenewAsync(TokenRenewRequest request, string authToken)
        {
            return await PostAsync<TokenRenewResponse, TokenRenewRequest>("TokenRenew", request, authToken);
        }

        #endregion

        #region SignalR Methods        
        public async Task<SignalRHubConnectNotificationResponse> NotificationsConnectSignalR(SignalRHubConnectNotificationRequest request, string authToken, string userId)
        {
            return await PostAsync<SignalRHubConnectNotificationResponse, SignalRHubConnectNotificationRequest>("SignalRHubConnectNotification", request, authToken, userId);
        }

        public async Task<NotificationsGroupAddRemoveResponse> NotificationsGroupAdd(NotificationsGroupAddRemoveRequest request, string authToken, string userId)
        {
            return await PostAsync<NotificationsGroupAddRemoveResponse, NotificationsGroupAddRemoveRequest>("NotificationsGroupAdd", request, authToken, userId);
        }

        public async Task<NotificationsGroupAddRemoveResponse> NotificationsGroupRemove(NotificationsGroupAddRemoveRequest request, string authToken, string userId)
        {
            return await PostAsync<NotificationsGroupAddRemoveResponse, NotificationsGroupAddRemoveRequest>("NotificationsGroupRemove", request, authToken, userId);
        }
        #endregion
    }

}
