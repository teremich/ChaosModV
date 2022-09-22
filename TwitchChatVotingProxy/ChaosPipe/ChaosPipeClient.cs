using Serilog;
using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace TwitchChatVotingProxy.ChaosPipe
{
    class ChaosPipeClient : IChaosPipeClient
    {
        /// <summary>
        /// Speed at which the chaos mod pipe gets processed
        /// </summary>
        public static readonly int PIPE_TICKRATE = 100;

        public event EventHandler<OnGetCurrentVotesArgs>? OnGetCurrentVotes;
        public event EventHandler<OnGetVoteResultArgs>? OnGetVoteResult;
        public event EventHandler<OnNewVoteArgs>? OnNewVote;
        public event EventHandler? OnNoVotingRound;

        private ILogger logger = Log.Logger.ForContext<ChaosPipeClient>();
        private NamedPipeClientStream pipe = new NamedPipeClientStream(
            ".",
            "ChaosModVTwitchChatPipe",
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        private StreamReader pipeReader;
        private Timer pipeTick = new Timer();
        private StreamWriter pipeWriter;
        private Task<string?>? readPipeTask;

        public ChaosPipeClient()
        {
            // Setup pipe tick
            pipeTick.Interval = PIPE_TICKRATE;
            pipeTick.Elapsed += PipeTick;

            // Connect to the chaos mod pipe
            try
            {
                pipe.Connect(1000);
                pipeReader = new StreamReader(pipe);
                pipeWriter = new StreamWriter(pipe);
                pipeWriter.AutoFlush = true;

                SendMessageToPipe("hello");

                logger.Information("successfully connected to chaos mod pipe");

                pipeTick.Enabled = true;
            }
            catch (Exception e)
            {
                throw new Exception("failed to connect to chaos mod pipe", e);
            }
        }

        /// <summary>
        /// Check if the chaos mod pipe is still connected
        /// </summary>
        /// <returns>If the chaos mod pipe is still connected</returns>
        public bool IsConnected()
        {
            return pipe.IsConnected;
        }

        /// <summary>
        /// Disconnects the stream reader/writer and the pipe itself
        /// </summary>
        private void DisconnectFromPipe()
        {
            pipeReader.Close();
            pipeWriter.Close();
            pipe.Close();
        }

        private void GetCurrentVotes()
        {
            var args = new OnGetCurrentVotesArgs();

            OnGetCurrentVotes?.Invoke(this, args);

            if (args.CurrentVotes == null)
            {
                throw new Exception("the listener failed to set the current votes");
            }

            var currentVotes = string.Join(":", args.CurrentVotes.Select(_ => _.ToString()).ToArray());

            SendMessageToPipe($"currentvotes:{currentVotes}");
        }
        /// <summary>
        /// Gets called when the chaos mod requests vote results
        /// </summary>
        private void GetVoteResult()
        {
            logger.Debug("asking listeners for vote result");

            var e = new OnGetVoteResultArgs();

            OnGetVoteResult?.Invoke(this, e);

            if (e.ChosenOption == null)
            {
                throw new Exception("the listener failed to set the chosen option");
            }

            logger.Debug($"vote result sent to pipe: {e.ChosenOption}");

            SendMessageToPipe($"voteresult:{e.ChosenOption}");
        }
        /// <summary>
        /// Gets called every pipe tick
        /// </summary>
        private void PipeTick(object? sender, ElapsedEventArgs e)
        {
            try
            {
                SendHeartBeat();
                ReadPipe();
            }
            catch (Exception exception)
            {
                logger.Fatal(exception, "chaos mod pipe tick failed, disconnecting");
                DisconnectFromPipe();
            }
        }
        /// <summary>
        /// Reads the contents of the chaos mod pipe and evaluates its message
        /// </summary>
        private void ReadPipe()
        {
            if (readPipeTask == null)
            {
                readPipeTask = pipeReader.ReadLineAsync();
                return;
            }

            if (!readPipeTask.IsCompleted)
            {
                return;
            }

            var message = readPipeTask.Result;

            // Null the reading task so the next read is dispatched
            readPipeTask = null;

            if (message == null)
            {
                return;
            }

            if (message.StartsWith("vote:")) StartNewVote(message);
            else if (message == "getvoteresult") GetVoteResult();
            else if (message == "novoteround") StartNoVotingRound();
            else if (message == "getcurrentvotes") GetCurrentVotes();
            else logger.Warning($"unknown request: {message}");

        }
        /// <summary>
        /// Sends a message to the chaos mod pipe
        /// </summary>
        /// <param name="message">Message to be sent</param>
        private void SendMessageToPipe(string message)
        {
            try
            {
                pipeWriter.Write($"{message}\0");
            }
            catch (Exception e)
            {
                logger.Information(e, "error that ocurred when writing pipe");
                DisconnectFromPipe();
            }
        }
        private void StartNewVote(string message)
        {
            // TODO: Should receive the options directly — separation of concern.

            // The vote options names are separated by ':'
            var optionNames = message.Split(':').ToList();

            // TODO: Investigate why this "indicator" is needed.
            // Either, a) a different piece should have checked it already (and
            // removed it — separation of concern), or b) if it's not needed,
            // don't send it at all.

            // Remove the first option (which is basically the indicator that this
            // is a new vote)
            optionNames.RemoveAt(0);

            OnNewVote?.Invoke(this, new OnNewVoteArgs(optionNames.ToArray()));
        }
        /// <summary>
        /// Start a no-voting round.
        /// The chaos mod will decide over the options
        /// </summary>
        private void StartNoVotingRound()
        {
            OnNoVotingRound?.Invoke(this, EventArgs.Empty);
        }
        private void SendHeartBeat()
        {
            SendMessageToPipe("ping");
        }
    }
}
