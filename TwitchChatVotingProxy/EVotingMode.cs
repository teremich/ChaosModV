using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TwitchLib.Api.Core.Enums;

namespace TwitchChatVotingProxy
{
    // TODO: Rename to evaluation mode
    // TODO: move to it's own file
    enum EVotingMode
    {
        MAJORITY,
        PERCENTAGE,
    }
}