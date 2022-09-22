using System.Collections.Generic;

// TODO: Fix the naming of the namespace
// Given that only one of the modes here is an actual "overlay", the name seems
// an odd choice. Other modes are "char" or "in game". "VotingPresentation", or
// "VotingDisplay" may be a more suitable name.
namespace TwitchChatVotingProxy.Overlay
{
    interface IServer
    {
        /// <summary>
        /// Informs the overlay server that the voting has ended
        /// </summary>
        void EndVoting();
        /// <summary>
        /// Informs the overlay server about a new vote
        /// </summary>
        /// <param name="voteOptions">The new voting options</param>
        void NewVoting(List<IVoteOption> voteOptions);
        /// <summary>
        /// Informs the overlay about a no voting round
        /// </summary>
        void NoVotingRound();
        /// <summary>
        /// Informs the overlay about possible updates
        /// </summary>
        /// <param name="votes"></param>
        void UpdateVoting(List<IVoteOption> votes);
    }
}
