using Blindrelay.Core.Api;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Blindrelay.Core.Example
{
    class Program
    {
        static Client client;
        static Configuration configuration;
        static readonly string workingDirectory = "c:\\temp\\working";
        static readonly string downloadDirectory = "c:\\temp\\encrypted";
        static readonly string decryptedDirectory = "c:\\temp\\decrypted";
        //static long MaxDownloadFileSize = 1000000000;
        static readonly ConcurrentQueue<FilePublishedNotification> filesPublishedNotifications = new ConcurrentQueue<FilePublishedNotification>();
        static readonly CancellationTokenSource notificationCancellation = new CancellationTokenSource();

        static Dictionary<string, UserGroupPermissions> groupPermissionLookup = new Dictionary<string, UserGroupPermissions>();

        static async Task Main(string[] args)
        {
            // would probably be better to store api key, email address, and password in a system keyvault instead of hardcoded :-)
            var BR_API_KEY = "<blindrelay API key>";
            var EMAIL_ADDRESS = "<email address>"; // email address used when registering with the blindrelay app
            var PASSWORD = "<blindrelay password>";

            Console.WriteLine("blindrelay login and download example");

            configuration = new Configuration
            {
                ApiKey = BR_API_KEY,
                ApiUrl = "https://api.blindrelay.com",
                ApplicationId = "BR-PUBLIC-API" // must be this value
            };

            client = new Client(configuration);

            // login 
            Console.WriteLine("Logging in...");            
            var loginResults = await client.LoginAsync(EMAIL_ADDRESS, PASSWORD);
            if (loginResults.Ok == false)
            {
                if (loginResults.Unauthorized)
                    Console.WriteLine(loginResults.Error);
                else
                    Console.WriteLine("Login failed.");
                return;
            }

            // get group permissions
            var groupPermissions = await client.GetGroupPermissionsAsync();
            if (groupPermissions.Any() == false)
            {
                Console.WriteLine("No group permissions.");
                return;
            }
            
            // connect to notifications
            Console.WriteLine("Connecting to notifications...");
            if (await client.ConnectNotificationsAsync(async (fpn) => await OnFilePublishedAsync(fpn)) == false)
            {
                Console.WriteLine("Could not connect to notifications.");
                return;
            }

            // subscribe to notifications for groups for which user has subscriber permissions
            foreach (var gp in groupPermissions)
            {
                groupPermissionLookup.Add(gp.GroupId, gp);

                if (gp.CanSubscribe)
                {
                    Console.WriteLine($"Subscribed to group: {gp.GroupId}");

                    if (await client.SubscribeToGroupNotificationsAsync(gp.GroupId) == false)
                        Console.Write($"Could not subscribe to group notifications for group {gp.GroupId}");
                }
            }

            var notificationHandlerTask = Task.Run(() => NotificationHandler(), notificationCancellation.Token);

            Console.WriteLine("Waiting for files to download...");
            Console.ReadKey();
            notificationCancellation.Cancel();
            await notificationHandlerTask;
        }

        static async Task NotificationHandler()
        {
            try
            {
                var token = notificationCancellation.Token;

                var fpns = new List<FilePublishedNotification>();

                while (token.IsCancellationRequested == false)
                {
                    fpns.Clear();
                    while (filesPublishedNotifications.IsEmpty == false)
                    {
                        if (filesPublishedNotifications.TryDequeue(out FilePublishedNotification fn))
                            fpns.Add(fn);
                        else break;
                    }

                    var groupCounts = new Dictionary<string, int>();
                    foreach (var fpn in fpns)
                    {
                        if (groupCounts.TryGetValue(fpn.GroupId, out int count) == false)
                            groupCounts[fpn.GroupId] = 0;

                        groupCounts[fpn.GroupId] = groupCounts[fpn.GroupId] + 1;
                    }

                    foreach (var groupId in groupCounts.Keys)
                    {
                        if (token.IsCancellationRequested)
                            return;

                        var maxCount = groupCounts[groupId];

                        UserGroupPermissions permissions = null;
                        if (groupPermissionLookup.TryGetValue(groupId, out permissions) == false)
                            continue;

                        var filesInfo = await client.GetDownloadableFilesInfoAsync(groupId, permissions.GroupOrganizerUserId, maxCount);

                        foreach (var fi in filesInfo)
                        {
                            // NOTE: if file is too large to download within the allowed timeout, might want to skip it
                            // if (fi.FileSize > MaxDownloadFileSize)
                            //    continue;

                            // download, decrypt, and uncompress file
                            Console.WriteLine("-------------------------------------");
                            Console.WriteLine($"Downloading {fi.FileName}, Decrypted Size {fi.FileSize} bytes");
                            await client.DownloadFileAsync(fi.FileId, fi.GroupId, async (downloadCipherTextStream) =>
                            {
                                string directory = null;
                                try
                                {
                                    directory = Path.Combine(downloadDirectory, fi.GroupId, fi.PublisherUserId);
                                    if (Directory.Exists(directory) == false)
                                        Directory.CreateDirectory(directory);

                                    directory = Path.Combine(decryptedDirectory, $"{fi.OrganizerEmail} - {fi.GroupName}", fi.PublisherEmail);
                                    if (Directory.Exists(directory) == false)
                                        Directory.CreateDirectory(directory);
                                }
                                catch (Exception dx)
                                {
                                    Console.WriteLine($"Error creating directories. Exception: {dx.Message}");
                                    return;
                                }

                                try
                                {
                                    var plainTextPath = Path.Combine(directory, $"{fi.FileName}-{fi.FileId}{Path.GetExtension(fi.FileName)}");
                                    var cipherTextPath = Path.Combine(workingDirectory, $"{fi.FileId}.brx");
                                    var uncompressPath = Path.Combine(workingDirectory, $"{fi.FileId}.bru");

                                    FileMetadata metadata = null;
                                    byte[] md5 = null;
                                    using (var plainTextStream = File.Create(plainTextPath))
                                    using (var cipherTextStream = File.Create(cipherTextPath))
                                    using (var uncompressStream = File.Create(uncompressPath))
                                    {
                                        // download
                                        await downloadCipherTextStream.CopyToAsync(cipherTextStream);

                                        // decrypt and uncompress
                                        metadata = await client.UnprotectGroupFileAsync(cipherTextStream, uncompressStream, plainTextStream);

                                        plainTextStream.Position = 0;
                                        md5 = Client.ComputeMD5Hash(plainTextStream);
                                    }

                                    File.Delete(uncompressPath);
                                    File.Delete(cipherTextPath);

                                    if (md5.SequenceEqual(metadata.MD5Hash) == false)
                                        Console.WriteLine($"WARNING: Possible corruption or tampering with file {plainTextPath} for group {fi.GroupId}");
                                    else
                                    {
                                        Console.WriteLine($"Saved decrypted file {fi.FileName} to {plainTextPath}");
                                        Console.WriteLine();
                                    }
                                }
                                catch (Exception x)
                                {
                                    Console.WriteLine($"Error downloading file {fi.FileId} for group {fi.GroupId}. Exception: {x.Message}");
                                }
                            });
                        }
                    }

                    await Task.Delay(2000);
                }
            }
            catch (Exception nx)
            {
                Console.WriteLine($"Notification handler exception: {nx.Message} stack: {nx.StackTrace}");
            }
        }

        static async Task OnFilePublishedAsync(FilePublishedNotification fpn)
        {
            filesPublishedNotifications.Enqueue(fpn);
            await Task.CompletedTask;
        }
    }
}
