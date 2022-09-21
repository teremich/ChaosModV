namespace TwitchChatVotingProxy.VotingReceiver
{
    /// <summary>
    /// Event which should be dispatched when the voting receiver receives
    /// a message.
    /// </summary>
    class OnMessageArgs
    {
        public string UserId { get; set; }
        public string Message { get; set; }
        public string Username { get; set; }

        public OnMessageArgs(string userId, string message, string username)
        {
            UserId = userId;
            Message = message;
            Username = username;
        }
    }
}
