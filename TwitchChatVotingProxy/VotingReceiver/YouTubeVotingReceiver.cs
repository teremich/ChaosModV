using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Serilog;
using System;
using Shared;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TwitchChatVotingProxy.VotingReceiver
{

    internal class YouTubeVotingReceiver : IVotingReceiver
    {
        private static string KEY_CLIENT_ID = "YouTubeClientId";
        private static string KEY_CLIENT_SECRET = "YouTubeClientSecret";

        public event EventHandler<OnMessageArgs>? OnMessage;

        private ILogger logger = Log.Logger.ForContext<YouTubeVotingReceiver>();
        private string? nextPageToken = null;
        private string clientId;
        private string clientSecret;
        /// <summary>
        /// Use the getter <see cref="GetYouTubeService" />.
        /// </summary>
        private YouTubeService? cacheYouTubeService = null;
        /// <summary>
        /// Use the getter <see cref="GetCurrentLiveBroadcast" />.
        /// </summary>
        private LiveBroadcast? cacheCurrentLiveBroadcast = null;

        public YouTubeVotingReceiver(OptionsFile optionsFile)
        {
            
            clientId = optionsFile.RequireString(KEY_CLIENT_ID);
            clientSecret = optionsFile.RequireString(KEY_CLIENT_SECRET);
        }
        
        async Task IVotingReceiver.GetMessages()
        {
            var youtubeService = await GetYouTubeService();
            var currentBroadcast = await GetCurrentLiveBroadcast();
            var liveChatId = currentBroadcast.Snippet.LiveChatId;

            var req = youtubeService.LiveChatMessages.List(liveChatId, "snippet,authorDetails");
            // Each requests returns a token, that the API can use to identify
            // what data we already received.
            req.PageToken = nextPageToken;

            var res = await req.ExecuteAsync();

            nextPageToken = res.NextPageToken;

            foreach (var item in res.Items)
            {
                DispatchOnMessageWith(item);
            }

            var delay = System.TimeSpan.FromMilliseconds(res.PollingIntervalMillis ?? 200);
            await Task.Delay(delay);
        }

        void IVotingReceiver.SendMessage(string message)
        {
            throw new Exception("sending messages currently not implemented");
        }

        private static LiveBroadcast? FindLiveBroadCast(IList<LiveBroadcast> broadcasts)
        {
            foreach (var broadcast in broadcasts)
            {
                if (broadcast.Status.LifeCycleStatus == "live")
                {
                    return broadcast;
                }
            }

            return null;
        }

        private void DispatchOnMessageWith(LiveChatMessage item)
        {
            var args = new OnMessageArgs(
                item.AuthorDetails.ChannelId,
                item.Snippet.DisplayMessage.Trim(),
                item.AuthorDetails.DisplayName
            );

            OnMessage?.Invoke(this, args);
        }

        private async Task<LiveBroadcast> GetCurrentLiveBroadcast()
        {
            if (cacheCurrentLiveBroadcast != null)
            {
                return cacheCurrentLiveBroadcast;
            }

            const int RetryTimeout = 5000;

            while (true)
            {
                var broadcasts = await GetAllBroadCasts();

                cacheCurrentLiveBroadcast = FindLiveBroadCast(broadcasts);

                if (cacheCurrentLiveBroadcast != null)
                {
                    return cacheCurrentLiveBroadcast;
                }

                logger.Information(
                    $"could not find any live broadcast, retrying in {RetryTimeout}ms"
                );

                await Task.Delay(RetryTimeout);
            }
        }

        /// <summary>
        /// Accesses the live broadcast list
        /// Note that this does not only include current live streams, but also
        /// "complete" past ones.
        /// </summary>
        private async Task<IList<LiveBroadcast>> GetAllBroadCasts()
        {
            var youtubeService = await GetYouTubeService();

            var req = youtubeService.LiveBroadcasts.List("snippet,status");
            // Tell the API we're want to fetch the user's ("my" / "mine") streams.
            req.Mine = true;

            var res = await req.ExecuteAsync();

            return res.Items;
        }

        private async Task<YouTubeService> GetYouTubeService()
        {
            if (cacheYouTubeService != null)
            {
                return cacheYouTubeService;
            }

            var secret = new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret };

            var credentials = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                secret,
                new[] { YouTubeService.Scope.Youtube },
                "user",
                System.Threading.CancellationToken.None
            );

            cacheYouTubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credentials,
                ApplicationName = typeof(YouTubeVotingReceiver).ToString(),
            });

            return cacheYouTubeService;
        }
    }
}