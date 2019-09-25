using Blindrelay.Core.Api;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading.Tasks;

namespace Blindrelay.Core
{
    public partial class Client
    {
        public async Task<bool> SubscribeToGroupNotificationsAsync(string groupId)
        {
            try
            {
                if (LoggedInUser == null || hub == null)
                    return false;

                var response = await apiService.NotificationsGroupAdd(new NotificationsGroupAddRemoveRequest
                {
                    GroupId = groupId
                }, authToken.ToString(), userId.ToString());


                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task UnsubscribeFromGroupNotificationsAsync(string groupId)
        {
            try
            {
                if (LoggedInUser == null || hub == null)
                    return;

                await apiService.NotificationsGroupRemove(new NotificationsGroupAddRemoveRequest
                {
                    GroupId = groupId
                }, authToken.ToString(), userId.ToString());
            }
            catch { }
        }

        public async Task<bool> ConnectNotificationsAsync(Func<FilePublishedNotification, Task> filePublishedHandler)
        {
            if (LoggedInUser == null)
                return false;

            try
            {
                var hr = await apiService.NotificationsConnectSignalR(new SignalRHubConnectNotificationRequest { }, authToken.ToString(), userId.ToString());
                if (string.IsNullOrWhiteSpace(hr.AccessToken) || string.IsNullOrWhiteSpace(hr.Url))
                    return false;

                hub = new HubConnectionBuilder().WithUrl(hr.Url, options =>
                {
                    options.AccessTokenProvider = () =>
                    {
                        return Task.FromResult(hr.AccessToken);
                    };
                })
                .Build();

                hub.On<byte[]>("OnFilePublished",
                          b =>
                          {
                              var n = FilePublishedNotification.FromBytes(b);
                              if (n != null)
                              {
                                  _ = filePublishedHandler(n);
                              }
                          });

                await hub.StartAsync();

                return true;
            }
            catch
            {
                try
                {
                    await hub.DisposeAsync();
                }
                catch { }
                finally
                {
                    hub = null;
                }
            }

            return false;
        }
    }
}
