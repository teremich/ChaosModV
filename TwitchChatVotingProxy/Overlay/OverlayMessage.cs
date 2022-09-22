namespace TwitchChatVotingProxy.Overlay
{
    class Message
    {
        public bool retainInitialVotes { get; set; }
        /// <summary>
        /// request type
        /// </summary>
        public string request { get; set; }
        public int totalVotes { get; set; }
        /// <summary>
        /// what voting mode should be used, this results in display changes
        /// </summary>
        public string votingMode { get; set; }
        /// <summary>
        /// Voting options them self
        /// </summary>
        public VoteOption[] voteOptions { get; set; }

        public Message(string request, string votingMode, VoteOption[] voteOptions)
        {
            this.request = request;
            this.votingMode = votingMode;
            this.voteOptions = voteOptions;
        }
    }
}
