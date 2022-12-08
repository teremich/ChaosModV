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

        private static ILogger logger = Log.Logger.ForContext<YouTubeVotingReceiver>();
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
            public static byte[] concat(byte[] one, byte[] two)
            {
                byte[] ret = new byte[one.Length + two.Length];
                Array.Copy(one, ret, one.Length);
                Array.Copy(two, 0, ret, one.Length, two.Length);
                return ret;
            }

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
                byte[] ret = concat(res, ary);
                return ret;
            }

            public static byte[] rs(long a, string ary)
            {
                byte[] ary2 = Encoding.Default.GetBytes(ary);
                long len = ary2.Length;
                return tp(2, a, concat(vn(len), ary2));
            }

            public static byte[] rs(long a, byte[] ary)
            {
                return tp(2, a, concat(vn(ary.Length), ary));
            }

            private byte[] nm(long a, long ary)
            {
                return tp(0, a, vn(ary));
            }

            private string _header()
            {
                var S1_3 = rs(1, videoId);
                var S1_5 = concat(rs(1, channelId), rs(2, videoId));

                var S1 = concat(rs(3, S1_3), rs(5, S1_5));
                var S3 = rs(48687757, rs(1, videoId));
                var header_replay = concat(new byte[][] { rs(1, S1), rs(3, S3), new byte[] { 0x20, 0x01 } });
                var realb64 = Convert.ToBase64String(header_replay);
                var pyb64 = realb64.Replace('/', '_');
                pyb64 = pyb64.Replace('+', '-');
                return pyb64;
            }

            private string build(long[] ts)
            {
                var b1 = new byte[] { 0x08, 0x00 };
                var b2 = new byte[] { 0x10, 0x00 };
                var b3 = new byte[] { 0x18, 0x00 };
                var b4 = new byte[] { 0x20, 0x00 };
                var b7 = new byte[] { 0x3a, 0x00 };
                var b8 = new byte[] { 0x40, 0x00 };
                var b9 = new byte[] { 0x4a, 0x00 };
                var timestamp2 = nm(10, ts[1]);
                var b11 = new byte[] { 0x58, 0x03 };
                var b15 = new byte[] { 0x78, 0x00 };

                var header = rs(3, _header());

                var timestamp1 = nm(5, ts[0]);
                var s6 = new byte[] { 0x30, 0x00 };
                var s7 = new byte[] { 0x38, 0x00 };
                var s8 = new byte[] { 0x40, 0x01 };
                var body = rs(9, concat(new byte[][] { b1, b2, b3, b4, b7, b8, b9, timestamp2, b11, b15 }));
                var timestamp3 = nm(10, ts[2]);
                var timestamp4 = nm(11, ts[3]);
                var s13 = new byte[] { 0x68, 0x01 };
                var chattype = new byte[] { 0x82, 0x01, 0x02, 0x08, 0x01 };
                var s17 = new byte[] { 0x88, 0x01, 0x00 };
                var str19 = new byte[] { 0x9a, 0x01, 0x02, 0x08, 0x00 };
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



            public async Task<List<OnMessageArgs>> get()
            {
                if (continuation == null)
                {
                    continuation = getContinuationToken();
                }
                const string apiURL = "https://www.youtube.com/youtubei/v1/live_chat/get_live_chat?key=AIzaSyAO_FJ2SlqU8Q4STEHLGCilw_Y9_11qcW8";
                string body = "{\"context\": {\"client\": {\"visitorData\": \"\", \"userAgent\": \"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/86.0.4240.183 Safari/537.36 Edg/86.0.622.63,gzip(gfe)\", \"clientName\": \"WEB\", \"clientVersion\": \"2.20221206.01.00\"}}, \"continuation\": \"" + continuation + "\"}";

                var content = new StringContent(body);
                var response = await client.PostAsync(apiURL, content);
                JsonObject res = JsonObject.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
                if (res == null || res["continuationContents"] == null)
                {
                    // is it bad, when we constantly write this warning and try again immediatly?
                    // or should we just throw an exception because clearly the request isn't working right
                    logger.Warning("videoId is probably incorrect or the video is private or something else happend so that the youtube api v1 didn't return the needed data");
                    continuation = null;
                    return new List<OnMessageArgs>();
                }
                JsonObject responseData = res["continuationContents"]!["liveChatContinuation"]!.AsObject();
                var continuations = responseData!["continuations"]!.AsArray()![0]!.AsObject();
                JsonNode? continuationData;
                if (continuations.TryGetPropertyValue("invalidationContinuationData", out continuationData))
                {
                    continuation = continuationData!["continuation"]!.GetValue<string>();
                }
                else if (continuations.TryGetPropertyValue("timedContinuationData", out continuationData))
                {
                    continuation = continuationData!["continuation"]!.GetValue<string>();
                }
                else if (continuations.TryGetPropertyValue("reloadContinuationData", out continuationData))
                {
                    continuation = continuationData!["continuation"]!.GetValue<string>();
                }
                else if (continuations.TryGetPropertyValue("liveChatReplayContinuationData", out continuationData))
                {
                    continuation = continuationData!["continuation"]!.GetValue<string>();
                }

                var actions = responseData["actions"]!.AsArray();
                var ret = new List<OnMessageArgs>();
                foreach (var action in actions)
                {
                    JsonNode? acia;
                    if (!action!.AsObject().TryGetPropertyValue("addChatItemAction", out acia))
                    {
                        continue;
                    }
                    var lctmr = acia!["item"]!["liveChatTextMessageRenderer"]!.AsObject();
                    JsonNode? messageContent;
                    if (!lctmr["message"]!["runs"]![0]!.AsObject().TryGetPropertyValue("text", out messageContent))
                    {
                        continue;
                    }
                    OnMessageArgs m = new OnMessageArgs(
                        userId: lctmr["authorExternalChannelId"]!.GetValue<string>(),
                        message: messageContent!.GetValue<string>().Trim(),
                        username: lctmr["authorName"]!["simpleText"]!.GetValue<string>()
                    );
                    ret.Add(m);

                }
                return ret;
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