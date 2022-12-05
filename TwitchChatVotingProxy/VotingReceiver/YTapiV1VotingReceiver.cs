using Serilog;
using Shared;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using System.Web;

namespace TwitchChatVotingProxy.VotingReceiver
{
    internal class YTapiV1VotingReceiver : IVotingReceiver
    {
        private class Magic
        {
            private static string? continuation = null;
            private string channelId;
            private string videoId;
            private static Dictionary<string, string> headers = new Dictionary<string, string> { { "user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/86.0.4240.183 Safari/537.36 Edg/86.0.622.63,gzip(gfe)" } };
            public Magic(string cID, string vID)
            {
                channelId = cID;
                videoId = vID;
            }
            byte[] vn(int val)
            {
                if (val < 0)
                {
                    throw new Exception("ValueError");
                }
                int bufSize = (int)(Math.Log2(val) / 7) + 1;
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
            private byte[] concat(byte[] one, byte[] two)
            {
                byte[] ret = new byte[one.Length + two.Length];
                Array.Copy(one, ret, one.Length);
                Array.Copy(two, 0, ret, one.Length, two.Length);
                return ret;
            }

            private byte[] concat(byte[][] bytes)
            {
                int accumulatedSize = 0;
                for (int i = 0; i < bytes.Length; i++)
                {
                    accumulatedSize += bytes[i].Length;
                }
                byte[] ret = new byte[accumulatedSize];
                int destinationIndex = 0;
                for (int i = 0; i < bytes.Length; i++)
                {
                    Array.Copy(bytes[i], 0, ret, destinationIndex, bytes[i].Length);
                    destinationIndex += bytes[i].Length;
                }
                return ret;
            }
            private byte[] tp(int a, int b, byte[] ary)
            {
                byte[] res = vn((b << 3) | a);
                byte[] ret = concat(res, ary);
                return ret;
            }

            private byte[] rs(int a, string ary)
            {
                byte[] ary2 = Encoding.Default.GetBytes(ary);
                int len = ary2.Length;

                return tp(2, a, concat(vn(len), ary2));
            }

            private byte[] rs(int a, byte[] ary)
            {
                return tp(2, a, concat(vn(ary.Length), ary));
            }

            private byte[] nm(int a, int ary)
            {
                return tp(0, a, vn(ary));
            }

            private string _header()
            {
                var S1_3 = rs(1, videoId);
                var S1_5 = concat(rs(1, channelId), rs(2, videoId));
                var S1 = concat(rs(3, S1_3), rs(5, S1_5));
                var S3 = rs(48687757, rs(1, videoId));
                var header_replay = concat(concat(rs(1, S1), rs(3, S3)), new byte[] { 0x20, 0x01 });
                return Convert.ToBase64String(header_replay);
            }

            private string build(int ts1, int ts2, int ts3, int ts4, int ts5)
            {
                var b1 = new byte[] { 0x08, 0x00 };
                var b2 = new byte[] { 0x10, 0x00 };
                var b3 = new byte[] { 0x18, 0x00 };
                var b4 = new byte[] { 0x20, 0x00 };
                var b7 = new byte[] { 0x3a, 0x00 };
                var b8 = new byte[] { 0x40, 0x00 };
                var b9 = new byte[] { 0x4a, 0x00 };
                var timestamp2 = nm(10, ts2);
                var b11 = new byte[] { 0x58, 0x03 };
                var b15 = new byte[] { 0x78, 0x00 };

                var header = rs(3, _header());
                var timestamp1 = nm(5, ts1);
                var s6 = new byte[] { 0x30, 0x00 };
                var s7 = new byte[] { 0x38, 0x00 };
                var s8 = new byte[] { 0x40, 0x01 };
                var body = rs(9, concat(new byte[][] { b1, b2, b3, b4, b7, b8, b9, timestamp2, b11, b15 }));
                var timestamp3 = nm(10, ts3);
                var timestamp4 = nm(11, ts4);
                var s13 = new byte[] { 0x68, 0x01 };
                var chattype = new byte[] { 0x82, 0x01, 0x02, 0x08, 0x01 };
                var s17 = new byte[] { 0x88, 0x01, 0x00 };
                var str19 = new byte[] { 0x9a, 0x01, 0x02, 0x08, 0x00 };
                var timestamp5 = nm(20, ts5);
                var entity = concat(new byte[][] {
                    header, timestamp1, s6, s7, s8, body, timestamp3,
                    timestamp4, s13, chattype, s17, str19, timestamp5
                });
                var continuation = rs(119693434, entity);
                return HttpUtility.UrlEncode(Convert.ToBase64String(continuation));
            }
            private static int[] times(int pastSec)
            {
                int[] ret = new int[5];
                TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
                int n = (int)t.TotalSeconds;
                double ts1 = n - 1.7596119363236733;
                double ts2 = n - 0.6937422130801;
                double ts3 = n - pastSec + 0.5887400473806613;
                double ts4 = n - 1876.0119363876886;
                double ts5 = n - 0.45889037558640905;
                ret[0] = (int)(1000000 * ts1);
                ret[1] = (int)(1000000 * ts2);
                ret[2] = (int)(1000000 * ts3);
                ret[3] = (int)(1000000 * ts4);
                ret[4] = (int)(1000000 * ts5);
                return ret;
            }

            private string getContinuationToken(int pastSec = 5)
            {
                int[] timeStamps = times(pastSec);
                int ts1 = timeStamps[0];
                int ts2 = timeStamps[1];
                int ts3 = timeStamps[2];
                int ts4 = timeStamps[3];
                int ts5 = timeStamps[4];
                return build(ts1, ts2, ts3, ts4, ts5);
            }

            public List<Message> get()
            {
                if (continuation == null)
                {
                    continuation = getContinuationToken();
                }
                const string apiURL = "https://www.youtube.com/youtubei/v1/live_chat/get_live_chat?key=AIzaSyAO_FJ2SlqU8Q4STEHLGCilw_Y9_11qcW8&prettyPrint=false";

                // body = {
                //     "context": {
                //         "client": {
                //             "deviceMake": "",
                //             "deviceModel": "",
                //             "userAgent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/108.0.0.0 Safari/537.36,gzip(gfe)",
                //             "clientName": "WEB",
                //             "clientVersion": "2.20221205.01.00",
                //             "osName": "Windows",
                //             "originalUrl": "https://www.youtube.com/live_chat?is_popout=1&v=XXXXXXXXXXX",
                //             "platform": "DESKTOP",
                //             "clientFormFactor": "UNKNOWN_FORM_FACTOR",
                //             "configInfo": {
                //             },
                //             "browserName": "Chrome",
                //             "acceptHeader": "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9",
                //             "mainAppWebInfo": {
                //                 "graftUrl": "https://www.youtube.com/live_chat?is_popout=1&v=YYYYYYYYYYY",
                //                 "webDisplayMode": "WEB_DISPLAY_MODE_BROWSER",
                //                 "isWebNativeShareAvailable": true
                //             }
                //         },
                //         "user": {
                //             "lockedSafetyMode": false
                //         },
                //         "request": {
                //             "useSsl": true,
                //             "internalExperimentFlags": [],
                //             "consistencyTokenJars": []
                //         }
                //     },
                //     "continuation": continuation,
                //     "webClientInfo": {
                //         "isDocumentHidden": false
                //     },
                //     "isInvalidationTimeoutRequest": "true"
                // }

                return new List<Message>();
            }
        }; // class Magic

        public event EventHandler<OnMessageArgs>? OnMessage;
        private ILogger logger = Log.Logger.ForContext<YTapiV1VotingReceiver>();
        private Magic details;
        public YTapiV1VotingReceiver(OptionsFile optionsFile)
        {
            string channelId = optionsFile.RequireString("YoutubeChannelId");
            string videoId = optionsFile.RequireString("LiveStreamVideoId");
            details = new Magic(channelId, videoId);
        }

        private class Message
        {
            public string authorChannelId;
            public string authorDisplayName;
            public string message;
            public Message(string aChannelId, string aDisplayname, string msg)
            {
                authorChannelId = aChannelId;
                authorDisplayName = aDisplayname;
                message = msg;
            }
        };

        async Task IVotingReceiver.GetMessages()
        {
            throw new Exception("this function is unimplemented");
            List<Message> response = details.get();
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

        private void DispatchOnMessageWith(Message item)
        {
            var args = new OnMessageArgs(
                item.authorChannelId,
                item.message.Trim(),
                item.authorDisplayName
            );

            OnMessage?.Invoke(this, args);
        }



    } // internal class YTapiV1VotingReceiver : IVotingReceiver
} // namespace TwitchChatVotingProxy.VotingReceiver