using Shared;
using Serilog;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using System.Net.Http;
using System.Text.Json.Nodes;

namespace TwitchChatVotingProxy.VotingReceiver
{
    internal class YTapiV1VotingReceiver : IVotingReceiver
    {

        public event EventHandler<OnMessageArgs>? OnMessage;

        private static ILogger logger = Log.Logger.ForContext<YTapiV1VotingReceiver>();
        private Magic details;
        private class Magic
        {
            private static HttpClient client = new HttpClient();
            private static string? continuation = null;
            private string channelId;
            private string videoId;
            public Magic(string cID, string vID)
            {
                client.DefaultRequestHeaders.Add(
                    "User-Agent", "C# dotnet 6"
                );
                channelId = cID;
                videoId = vID;
            }
            public static byte[] vn(long val)
            {
                if (val < 0)
                {
                    throw new Exception("ValueError");
                }
                int bufSize = (int)(Math.Log2(val + 1) / 7) + 1;
                byte[] buf = new byte[bufSize];
                int index = 0;
                while ((val >> 7) > 0)
                {
                    byte m = (byte)(val & 0xFF | 0x80);
                    buf[index++] = m;
                    val >>= 7;
                    if (index >= bufSize)
                    {
                        throw new Exception("index was larger than the buffer size. This shouldn't happen");
                    }
                }
                buf[index] = (byte)val;
                return buf;
            }
            // public static byte[] concat(byte[] one, byte[] two)
            // {
            //     byte[] ret = new byte[one.Length + two.Length];
            //     Array.Copy(one, ret, one.Length);
            //     Array.Copy(two, 0, ret, one.Length, two.Length);
            //     return ret;
            // }

            public static byte[] concat(byte[][] byteArrays)
            {
                int accumulatedSize = 0;
                for (int i = 0; i < byteArrays.Length; i++)
                {
                    accumulatedSize += byteArrays[i].Length;
                }
                byte[] ret = new byte[accumulatedSize];
                int destinationIndex = 0;
                for (int i = 0; i < byteArrays.Length; i++)
                {
                    Array.Copy(byteArrays[i], 0, ret, destinationIndex, byteArrays[i].Length);
                    destinationIndex += byteArrays[i].Length;
                }
                return ret;
            }
            public static byte[] tp(long a, long b, byte[] ary)
            {
                byte[] res = vn((b << 3) | a);
                byte[] ret = concat(new byte[][] { res, ary });
                return ret;
            }

            public static byte[] rs(long a, string ary)
            {
                byte[] ary2 = Encoding.Default.GetBytes(ary);
                long len = ary2.Length;
                return tp(2, a, concat(new byte[][] { vn(len), ary2 }));
            }

            public static byte[] rs(long a, byte[] ary)
            {
                return tp(2, a, concat(new byte[][] { vn(ary.Length), ary }));
            }

            private byte[] nm(long a, long ary)
            {
                return tp(0, a, vn(ary));
            }

            private string _header()
            {
                var S1_3 = rs(1, videoId);
                var S1_5 = concat(new byte[][] { rs(1, channelId), rs(2, videoId) });

                var S1 = concat(new byte[][] { rs(3, S1_3), rs(5, S1_5) });
                var S3 = rs(48687757, rs(1, videoId));
                var header_replay = concat(new byte[][] { rs(1, S1), rs(3, S3), new byte[] { 0x20, 0x01 } });
                var realb64 = Convert.ToBase64String(header_replay);
                var pyb64 = realb64.Replace('/', '_');
                pyb64 = pyb64.Replace('+', '-');
                return pyb64;
            }

            private string build(long[] ts)
            {
                byte[] b1 = { 0x08, 0x00 };
                byte[] b2 = { 0x10, 0x00 };
                byte[] b3 = { 0x18, 0x00 };
                byte[] b4 = { 0x20, 0x00 };
                byte[] b7 = { 0x3a, 0x00 };
                byte[] b8 = { 0x40, 0x00 };
                byte[] b9 = { 0x4a, 0x00 };
                var timestamp2 = nm(10, ts[1]);
                byte[] b11 = { 0x58, 0x03 };
                byte[] b15 = { 0x78, 0x00 };

                var header = rs(3, _header());

                var timestamp1 = nm(5, ts[0]);
                byte[] s6 = { 0x30, 0x00 };
                byte[] s7 = { 0x38, 0x00 };
                byte[] s8 = { 0x40, 0x01 };
                var body = rs(9, concat(new byte[][] { b1, b2, b3, b4, b7, b8, b9, timestamp2, b11, b15 }));
                var timestamp3 = nm(10, ts[2]);
                var timestamp4 = nm(11, ts[3]);
                byte[] s13 = { 0x68, 0x01 };
                byte[] chattype = { 0x82, 0x01, 0x02, 0x08, 0x01 };
                byte[] s17 = { 0x88, 0x01, 0x00 };
                byte[] str19 = { 0x9a, 0x01, 0x02, 0x08, 0x00 };
                var timestamp5 = nm(20, ts[4]);
                var entity = concat(new byte[][] {
                    header, timestamp1, s6, s7, s8, body, timestamp3,
                    timestamp4, s13, chattype, s17, str19, timestamp5
                });
                var continuation = rs(119693434, entity);
                var realb64 = Convert.ToBase64String(continuation);
                var pyb64 = realb64.Replace('/', '_');
                pyb64 = pyb64.Replace('+', '-');
                return pyb64;
            }
            private static long[] times(int pastSec)
            {
                long[] ret = new long[5];
                TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
                long n = (long)t.TotalSeconds;
                double ts1 = n - 1.7596119363236733;
                double ts2 = n - 0.6937422130801;
                double ts3 = n - pastSec + 0.5887400473806613;
                double ts4 = n - 1876.0119363876886;
                double ts5 = n - 0.45889037558640905;
                ret[0] = (long)(1000000 * ts1);
                ret[1] = (long)(1000000 * ts2);
                ret[2] = (long)(1000000 * ts3);
                ret[3] = (long)(1000000 * ts4);
                ret[4] = (long)(1000000 * ts5);
                return ret;
            }

            private string getContinuationToken(int pastSec = 5)
            {
                long[] timeStamps = times(pastSec);
                return build(timeStamps);
            }

            private async Task<JsonObject?> sendRequest()
            {
                if (continuation is null)
                {
                    continuation = getContinuationToken();
                }
                const string apiURL = "https://www.youtube.com/youtubei/v1/live_chat/get_live_chat?key=AIzaSyAO_FJ2SlqU8Q4STEHLGCilw_Y9_11qcW8";
                string body = "{\"context\": {\"client\": {\"visitorData\": \"\", \"userAgent\": \"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/86.0.4240.183 Safari/537.36 Edg/86.0.622.63,gzip(gfe)\", \"clientName\": \"WEB\", \"clientVersion\": \"2.20221206.01.00\"}}, \"continuation\": \""
                + continuation + "\"}";

                var content = new StringContent(body);
                var response = await client.PostAsync(apiURL, content);
                return JsonObject.Parse(await response.Content.ReadAsStringAsync())?.AsObject();
            }

            private void parseContinuationData(JsonObject responseData)
            {
                var continuations = responseData["continuations"]?.AsArray()?[0]?.AsObject();
                if (continuations is null)
                {
                    logger.Warning("This is very strange. Youtube shouldn't responde like that.");
                    continuation = null;
                    return;
                }
                JsonNode? continuationData;
                if (!continuations.TryGetPropertyValue("invalidationContinuationData", out continuationData)
                && !continuations.TryGetPropertyValue("timedContinuationData", out continuationData)
                && !continuations.TryGetPropertyValue("reloadContinuationData", out continuationData)
                && !continuations.TryGetPropertyValue("liveChatReplayContinuationData", out continuationData))
                {
                    continuation = null;
                    return;
                }
                continuation = continuationData?["continuation"]?.GetValue<string>();
            }

            private List<OnMessageArgs> parseActions(JsonObject responseData)
            {
                var ret = new List<OnMessageArgs>();
                JsonNode? actions;
                if (!responseData.TryGetPropertyValue("actions", out actions))
                {
                    return ret;
                }
                var actionsArray = actions?.AsArray();
                if (actionsArray is null)
                {
                    logger.Warning("This shouldn't happen. Youtube responded with weird JSON");
                    return ret;
                }
                foreach (var action in actionsArray)
                {
                    if (action?.AsObject() is null)
                    {
                        continue;
                    }
                    JsonNode? acia;
                    if (!action.AsObject().TryGetPropertyValue("addChatItemAction", out acia))
                    {
                        continue;
                    }
                    var lctmr = acia?["item"]?["liveChatTextMessageRenderer"]?.AsObject();
                    var runs = lctmr?["message"]?["runs"]?[0]?.AsObject();
                    if (runs is null)
                    {
                        continue;
                    }
                    JsonNode? messageContent;
                    if (!runs.TryGetPropertyValue("text", out messageContent))
                    {
                        continue;
                    }
                    var userid = lctmr?["authorExternalChannelId"]?.GetValue<string>();
                    var messageText = messageContent?.GetValue<string>().Trim();
                    var user = lctmr?["authorName"]?["simpleText"]?.GetValue<string>();
                    if (userid is null || messageText is null || messageText.Length == 0 || user is null)
                    {
                        continue;
                    }
                    OnMessageArgs m = new OnMessageArgs(
                        userId: userid,
                        message: messageText,
                        username: user
                    );
                    ret.Add(m);
                }
                return ret;
            }

            public async Task<List<OnMessageArgs>> get()
            {
                var response = await sendRequest();
                if (response is null || response["continuationContents"] is null)
                {
                    logger.Warning("videoId is probably incorrect or the video is private or something else happend so that the youtube api v1 didn't return the needed data");
                    continuation = null;
                    return new List<OnMessageArgs>();
                }
                JsonObject? responseData = response["continuationContents"]?["liveChatContinuation"]?.AsObject();
                if (responseData is null)
                {
                    logger.Warning("are you sure you have entered the current stream id?");
                    continuation = null;
                    return new List<OnMessageArgs>();
                }
                parseContinuationData(responseData);
                List<OnMessageArgs> messages = parseActions(responseData);
                return messages;
            }
        } // class Magic

        public YTapiV1VotingReceiver(OptionsFile optionsFile)
        {
            string channelId = optionsFile.RequireString("YoutubeChannelId").Substring(0, 24);
            string videoId = optionsFile.RequireString("LiveStreamVideoId").Substring(0, 11);
            details = new Magic(channelId, videoId);
        }

        async Task IVotingReceiver.GetMessages()
        {
            List<OnMessageArgs> response = await details.get();
            foreach (var message in response)
            {
                DispatchOnMessageWith(message);
            }
            var delay = System.TimeSpan.FromMilliseconds(200);
            await Task.Delay(delay);
        }

        void IVotingReceiver.SendMessage(string message)
        {
            throw new Exception("sending messages currently not implemented");
        }

        private void DispatchOnMessageWith(OnMessageArgs args)
        {
            OnMessage?.Invoke(this, args);
        }

    } // internal class YTapiV1VotingReceiver : IVotingReceiver
} // namespace TwitchChatVotingProxy.VotingReceiver