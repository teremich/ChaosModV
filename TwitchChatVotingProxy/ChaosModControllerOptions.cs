using Shared;
using System;
using System.Linq;

namespace TwitchChatVotingProxy
{
    class ChaosModControllerOptions
    {
        private static readonly int VOTING_DISPLAY_UPDATE_MS = 200;
        private static readonly string KEY_OVERLAY_SERVER_PORT = "OverlayServerPort";
        // TODO: generalize value of key
        private static readonly string KEY_VOTING_EVALUATION_MODE = "TwitchVotingChanceSystem";
        // TODO: generalize value of key
        private static readonly string KEY_OVERLAY_MODE = "TwitchVotingOverlayMode";
        // TODO: generalize value of key
        private static readonly string KEY_RETAIN_INITIAL_VOTES = "TwitchVotingChanceSystemRetainChance";
        // TODO: generalize value of key
        private static readonly string KEY_PERMITTED_USERNAMES = "TwitchPermittedUsernames";

        public int VotingDisplayUpdateMs = VOTING_DISPLAY_UPDATE_MS;
        public int OverlayServerSocketPort;
        public EVotingMode VotingEvaluationMode;
        public EOverlayMode OverlayMode;
        public bool RetainInitialVotes;
        public string[] PermittedUsernames;

        public ChaosModControllerOptions(OptionsFile optionsFile)
        {
            OverlayServerSocketPort = optionsFile.RequireInt(KEY_OVERLAY_SERVER_PORT);
            // TODO: use Enum.TryParse and have literals in the file instead of 
            // indexes.
            VotingEvaluationMode = optionsFile.RequireInt(KEY_VOTING_EVALUATION_MODE) == 0
                ? EVotingMode.MAJORITY
                : EVotingMode.PERCENTAGE;
            OverlayMode = Enum.Parse<EOverlayMode>(optionsFile.RequireString(KEY_OVERLAY_MODE));
            RetainInitialVotes = optionsFile.RequireBool(KEY_RETAIN_INITIAL_VOTES);
            PermittedUsernames = ParsePermittedUsernames(optionsFile.ReadValue(KEY_PERMITTED_USERNAMES));
        }

        private static string[] ParsePermittedUsernames(string? input)
        {
            if (input == null)
            {
                return new string[0];
            }

            return input
                .Trim()
                .ToLower()
                .Split(",")
                // Remove whitespace around usernames
                .Select(name => name.Trim())
                .ToArray();
        }
    }
}