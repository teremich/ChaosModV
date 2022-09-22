﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchChatVotingProxy.Overlay
{
    /// <summary>
    /// Represents how the vote options are being sent to the client after
    /// JSON serializing them.
    /// </summary>
    class VoteOption
    {
        public VoteOption(IVoteOption voteOption)
        {
            label = voteOption.Label;
            value = voteOption.Votes;
            matches = voteOption.Matches.ToArray();
        }
        public string label { get; set; }
        public string[] matches { get; set; }
        public int value { get; set; }
    }
}
