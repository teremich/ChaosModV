using Serilog;
using Shared;
using System;
using System.Threading.Tasks;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Events;

namespace TwitchChatVotingProxy.VotingReceiver
{
    internal class TwitchVotingReceiver : IDisposable, IVotingReceiver
    {
        public event EventHandler<OnMessageArgs>? OnMessage;

        private static readonly int RECONNECT_INTERVAL = 1000;

        private ILogger logger = Log.Logger.ForContext<TwitchVotingReceiver>();
        private TwitchClient twitchClient;
        private string channelName;

        public TwitchVotingReceiver(OptionsFile options)
        {
            var userName = options.RequireString("TwitchUserName");
            logger.Information($"specified username: '{userName}'");

            channelName = options.RequireString("TwitchChannelName");
            logger.Information($"trying to connect to cannel '{channelName}'...");

            var oauth = options.RequireString("TwitchChannelOAuth");

            twitchClient = new TwitchClient(new WebSocketClient());
            twitchClient.Initialize(
                new ConnectionCredentials(userName, oauth),
                channelName
            );
            twitchClient.OnConnected += OnConnected;
            twitchClient.OnError += OnError;
            twitchClient.OnIncorrectLogin += OnIncorrectLogin;
            twitchClient.OnJoinedChannel += OnJoinedChannel;
            twitchClient.OnMessageReceived += OnMessageReceived;
            twitchClient.Connect();
        }

        public void Dispose()
        {
            twitchClient.Disconnect();
        }

        public void SendMessage(string message)
        {
            logger.Information($@"sending message to twitch chat '{message}'");

            try
            {
                twitchClient.SendMessage(channelName, message);
            }
            catch (Exception e)
            {
                logger.Error($"failed sending message to twitch chat", e);
            }
        }

        private void OnConnected(object? sender, OnConnectedArgs e)
        {
            logger.Information("successfully connected to twitch!");
        }

        private async void OnDisconnect(object sender, OnDisconnectedArgs e)
        {
            logger.Error(
                "disconnected from twitch channel... trying to reconnect..."
            );

            await Task.Delay(RECONNECT_INTERVAL);

            twitchClient.Connect();
        }

        private void OnError(object? sender, OnErrorEventArgs e)
        {
            logger.Error(
                "experienced an exception, disconnecting...",
                e.Exception
            );
            twitchClient.Disconnect();
        }

        private void OnIncorrectLogin(object? sender, OnIncorrectLoginArgs e)
        {
            logger.Error("incorrect twitch login, verify user name and oauth");
            twitchClient.Disconnect();
        }

        private void OnJoinedChannel(object? sender, OnJoinedChannelArgs e)
        {
            logger.Information($"successfully joined twitch channel '{e.Channel}'");
        }

        private void OnMessageReceived(object? sender, OnMessageReceivedArgs e)
        {
            var chatMessage = e.ChatMessage;

            var args = new OnMessageArgs(
                chatMessage.UserId,
                chatMessage.Message.Trim(),
                chatMessage.Username.ToLower()
            );

            OnMessage?.Invoke(this, args);
        }
    }
}
