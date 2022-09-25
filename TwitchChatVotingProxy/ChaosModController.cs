using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using TwitchChatVotingProxy.ChaosPipe;
using TwitchChatVotingProxy.VotingReceiver;

namespace TwitchChatVotingProxy
{
    class ChaosModController
    {
        private List<IVoteOption> activeVoteOptions = new List<IVoteOption>();
        private IChaosPipeClient chaosPipe;
        private Timer displayUpdateTick;
        private ILogger logger = Log.Logger.ForContext<ChaosModController>();
        private Overlay.IServer? overlayServer;
        private Dictionary<string, int> userVotedFor = new Dictionary<string, int>();
        private Random random = new Random();
        private ChaosModControllerOptions options;
        private int voteCounter = 0;
        private bool voteRunning = false;
        private IVotingReceiver votingReceiver;

        public ChaosModController(
            ChaosModControllerOptions options,
            IChaosPipeClient chaosPipe,
            IVotingReceiver votingReceiver,
            Overlay.IServer? overlayServer
        )
        {
            this.options = options;
            this.chaosPipe = chaosPipe;
            this.overlayServer = overlayServer;
            this.votingReceiver = votingReceiver;

            // Setup listeners that connect to the "game" part of the mod
            this.chaosPipe.OnGetCurrentVotes += OnGetCurrentVotes;
            this.chaosPipe.OnGetVoteResult += OnGetVoteResult;
            this.chaosPipe.OnNewVote += OnNewVote;
            this.chaosPipe.OnNoVotingRound += OnNoVotingRound;

            // Setup listener for the votes
            this.votingReceiver.OnMessage += OnVoteReceiverMessage;

            // Setup the timer that will update the display
            displayUpdateTick = new Timer(options.VotingDisplayUpdateMs);
            displayUpdateTick.Elapsed += DisplayUpdateTick;
            displayUpdateTick.Enabled = true;
        }

        /// <summary>
        /// Does the display update tick and is called by a timer
        /// </summary>
        private void DisplayUpdateTick(object? sender, ElapsedEventArgs e)
        {
            overlayServer?.UpdateVoting(activeVoteOptions);
        }
        /// <summary>
        /// Calculate the voting result by counting them, and returning the one
        /// with the most votes.
        /// </summary>
        private int GetVoteResultByMajority()
        {
            // Find the highest vote count
            var highestVoteCount = activeVoteOptions.Max(_ => _.Votes);
            // Get all options that have the highest vote count
            var choosenOptions = activeVoteOptions.FindAll(_ => _.Votes == highestVoteCount);
            IVoteOption choosenOption;
            // If we only have one choosen option, use that
            if (choosenOptions.Count == 1) choosenOption = choosenOptions[0];
            // Otherwise we have more than one option with the same vote count
            // and choose one at random
            else choosenOption = choosenOptions[random.Next(0, choosenOptions.Count)];

            return activeVoteOptions.IndexOf(choosenOption);
        }
        /// <summary>
        /// Calculate the voting result by assigning them a percentage based on votes,
        /// and choosing a random option based on that percentage.
        private int GetVoteResultByPercentage()
        {
            var votesForOption = activeVoteOptions
                .Select(_ => options.RetainInitialVotes ? _.Votes + 1 : _.Votes)
                .ToList();
            var totalVotes = votesForOption.Sum();

            // If we have no votes, choose one at random
            if (totalVotes == 0) return random.Next(0, votesForOption.Count);
            // Select a random vote from all votes
            var selectedVote = random.Next(1, totalVotes + 1);
            // Now find out in what vote range/option that vote is
            var voteRange = 0;
            var selectedOption = 0;
            for (var i = 0; i < votesForOption.Count; i++)
            {
                voteRange += votesForOption[i];
                if (selectedVote <= voteRange)
                {
                    selectedOption = i;
                    break;
                }
            }

            // Return the selected vote range/option
            return selectedOption;
        }
        /// <summary>
        /// Is called when the chaos mod pipe requests the current votes (callback)
        /// </summary>
        private void OnGetCurrentVotes(object? sender, OnGetCurrentVotesArgs args)
        {
            args.CurrentVotes = activeVoteOptions.Select(_ => _.Votes).ToList();
        }
        /// <summary>
        /// Is called when the chaos mod wants to know the voting result (callback)
        /// </summary>
        private void OnGetVoteResult(object? sender, OnGetVoteResultArgs evt)
        {
            try
            {
                overlayServer?.EndVoting();

            }
            catch (Exception err)
            {
                throw new Exception("failed to end the voting", err);
            }

            // Evaluate what result calculation to use
            switch (options.VotingEvaluationMode)
            {
                case EVotingMode.MAJORITY:
                    evt.ChosenOption = GetVoteResultByMajority();
                    break;
                case EVotingMode.PERCENTAGE:
                    evt.ChosenOption = GetVoteResultByPercentage();
                    break;
            }

            voteRunning = false;
        }
        /// <summary>
        /// Is called when the chaos mod start a new vote (callback)
        /// </summary>
        private void OnNewVote(object? sender, OnNewVoteArgs e)
        {
            activeVoteOptions = e.VoteOptionNames.ToList().Select((voteOptionName, index) =>
            {
                // We want the options to alternate between matches.
                // If we are on an even round we basically select the index (+1 for non programmers).
                // If we are on an odd round, we add to the index the option count.
                // This gives us a pattern like following:
                // Round 0: [O1, O2, O3, ...]
                // Round 1: [O4, O5, O6, ...]
                var match = voteCounter % 2 == 0
                    ? (index + 1).ToString()
                    : (index + 1 + activeVoteOptions.Count).ToString();

                return (IVoteOption)new VoteOption(voteOptionName, new List<string>() { match });
            }).ToList();
            // Depending on the overlay mode either inform the overlay server about the new vote or send a chat message
            switch (options.OverlayMode)
            {
                case EOverlayMode.CHAT_MESSAGES:
                    votingReceiver.SendMessage("Time for a new effect! Vote between:");
                    foreach (IVoteOption voteOption in activeVoteOptions)
                    {
                        string msg = string.Empty;

                        bool firstIndex = true;
                        foreach (string match in voteOption.Matches)
                        {
                            msg += firstIndex ? $"{match} " : $" / {match}";

                            firstIndex = true;
                        }

                        msg += $": {voteOption.Label}\n";

                        votingReceiver.SendMessage(msg);
                    }

                    if (options.VotingEvaluationMode == EVotingMode.PERCENTAGE)
                    {
                        votingReceiver.SendMessage("Votes will affect the chance for one of the effects to occur.");
                    }

                    break;
                case EOverlayMode.OVERLAY_OBS:
                    overlayServer?.NewVoting(activeVoteOptions);
                    break;
            }
            // Clear the old voted for information
            userVotedFor.Clear();
            // Increase the vote counter
            voteCounter++;

            // Vote round started now
            voteRunning = true;
        }
        /// <summary>
        /// Is called when the chaos mod stars a no voting round (callback)
        /// </summary>
        private void OnNoVotingRound(object? sender, EventArgs e)
        {
            overlayServer?.NoVotingRound();
        }
        /// <summary>
        /// Is called when the voting receiver receives a message
        /// </summary>
        private void OnVoteReceiverMessage(object? sender, OnMessageArgs e)
        {
            if (!voteRunning) return;

            if (!IsUserAllowedToVote(e.Username)) return;

            for (int i = 0; i < activeVoteOptions.Count; i++)
            {
                var voteOption = activeVoteOptions[i];

                if (!voteOption.Matches.Contains(e.Message))
                {
                    continue;
                }

                int previousVote;

                // Check if the player has already voted
                if (!userVotedFor.TryGetValue(e.UserId, out previousVote))
                {
                    // If they haven't voted, count his vote
                    userVotedFor.Add(e.UserId, i);
                    voteOption.Votes++;
                }
                else if (previousVote != i)
                {
                    // If the player has already voted, and it's not the same as before,
                    // remove the old vote, and add the new one.
                    userVotedFor.Remove(e.UserId);
                    activeVoteOptions[previousVote].Votes--;

                    userVotedFor.Add(e.UserId, i);
                    voteOption.Votes++;
                }

                break;
            }
        }
        private bool IsUserAllowedToVote(string username)
        {
            // No names means everyone is allowed to vote
            if (options.PermittedUsernames.Length == 0)
            {
                return true;
            }

            return options.PermittedUsernames.Contains(username);
        }
    }
}
