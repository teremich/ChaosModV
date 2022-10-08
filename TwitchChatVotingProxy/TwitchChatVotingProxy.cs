using Serilog;
using Shared;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TwitchChatVotingProxy.ChaosPipe;
using TwitchChatVotingProxy.VotingReceiver;

namespace TwitchChatVotingProxy
{
    class TwitchChatVotingProxy
    {
        private static readonly string KEY_RETAIN_INITIAL_VOTES = "VotingRetainInitialVotes";
        private static readonly string KEY_OVERLAY_MODE = "OverlayMode";
        private static readonly string KEY_VOTING_RECEIVER = "VotingReceiver";
        private static ILogger logger;

        static TwitchChatVotingProxy()
        {
            Log.Logger = new LoggerConfiguration()
                   .MinimumLevel.Debug()
                   .WriteTo.Console()
                   .WriteTo.File("./chaosmod/chaosProxy.log", outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext:l}] {Message:lj}{NewLine}{Exception}")
                   .CreateLogger();
            logger = Log.Logger.ForContext<TwitchChatVotingProxy>();
        }

        private static void Main(string[] args)
        {
            logger.Information("===============================");
            logger.Information("Starting chaos mod twitch proxy");
            logger.Information("===============================");

            var optionsFile = new OptionsFile("./chaosmod/voting.ini");
            var retainInitialVotes = optionsFile.RequireBool(KEY_RETAIN_INITIAL_VOTES);
            var overlayMode = optionsFile.RequireEnum<EOverlayMode>(KEY_OVERLAY_MODE);
            var overlayServer = GetOverlayServer(optionsFile, overlayMode, retainInitialVotes);
            var votingReceiver = GetVotingReceiver(optionsFile);

            // TODO: Ask pongo what should be in the "mutex guard".

            Mutex mutex = new Mutex(false, "ChaosModVVotingMutex");
            mutex.WaitOne();

            try
            {
                var chaosPipe = new ChaosPipeClient();
                var controller = new ChaosModController(
                    optionsFile,
                    overlayMode,
                    retainInitialVotes,
                    chaosPipe,
                    votingReceiver,
                    overlayServer
                );

                while (chaosPipe.IsConnected())
                {
                    Task.WaitAll(votingReceiver.GetMessages());
                }
            }
            finally
            {
                mutex.ReleaseMutex();
            }

            logger.Information("pipe disconnected, exiting gracefully...");
        }

        private static IVotingReceiver GetVotingReceiver(OptionsFile optionsFile)
        {
            var which = optionsFile.RequireEnum<EVotingReceiver>(KEY_VOTING_RECEIVER);

            logger.Information($"Using voting provider: '{which}'...");

            switch (which)
            {
                case EVotingReceiver.Twitch:
                    return new TwitchVotingReceiver(optionsFile);
                case EVotingReceiver.YouTube:
                    return new YouTubeVotingReceiver(optionsFile);
            }

            throw new Exception($"an unaccounted for voting receiver '{which}' was specified");
        }

        private static Overlay.IServer? GetOverlayServer(
            OptionsFile optionsFile,
            EOverlayMode overlayMode,
            bool retainInitialVotes
        )
        {
            if (IsOverlayServerNeeded(overlayMode))
            {
                return new Overlay.Server(optionsFile, retainInitialVotes);
            }

            return null;
        }

        private static bool IsOverlayServerNeeded(EOverlayMode overlayMode)
        {
            switch (overlayMode)
            {
                case EOverlayMode.Browser:
                    return true;
            }

            return false;
        }
    }
}