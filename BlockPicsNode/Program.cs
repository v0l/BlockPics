using hashstream.bitcoin_lib.BlockChain;
using hashstream.bitcoin_lib;
using hashstream.bitcoin_node_lib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace BlockPicsNode
{
    class Program
    {
        static BitcoinNode Node { get; set; }

        static void Main(string[] args)
        {
            BitcoinNode.UserAgent = "/BlockPics:0.1/";

            Node = new BitcoinNode(new IPEndPoint(IPAddress.Any, 8336));
            Node.OnLog += Node_OnLog;
            Node.OnPeerConnected += Node_OnPeerConnected;
            Node.OnPeerDisconnected += Node_OnPeerDisconnected;
            Node.Start();

            var bt = GetBlocks(args[0], args[1]);

            Console.ReadKey();
        }

        private static void Node_OnPeerDisconnected(BitcoinNodePeer np)
        {

        }

        private static void Node_OnPeerConnected(BitcoinNodePeer np)
        {

        }

        private static void Node_OnLog(string msg)
        {
            Console.WriteLine(msg);
        }

        static async Task GetBlocks(string host, string token)
        {
            var igen = new ProcessStartInfo("ffmpeg");
            igen.RedirectStandardInput = true;
            igen.RedirectStandardOutput = true;

            var req = (HttpWebRequest)WebRequest.Create($"https://{host}/api/v1/streaming/public");
            req.Headers.Add(HttpRequestHeader.Authorization, $"Bearer {token}");

            var rsp = await req.GetResponseAsync();

            using (var sr = new StreamReader(rsp.GetResponseStream()))
            {
                while (true)
                {
                    var line = await sr.ReadLineAsync();
                    //var line = "event: update";
                    if (line.StartsWith(':') || line == string.Empty)
                    {
                        continue;
                    }
                    else if (line == null)
                    {
                        break;
                    }

                    var data = await sr.ReadLineAsync();
                    //var data = $"data: {File.ReadAllText("temp.json")}";
                    var ev = line.Substring(line.IndexOf(':') + 2);
                    var pl = data.Substring(data.IndexOf(':') + 2);

                    if(ev == "update")
                    {
                        var st = JsonConvert.DeserializeObject<Status>(pl);

                        Console.WriteLine($"Got status from: {st.account.username}");
                        if (st.account.username == "BlockPics")
                        {
                            if (st.media_attachments != null && st.media_attachments.Count > 0)
                            {
                                var block_img = st.media_attachments[0];
                                var img_data = await new WebClient().DownloadDataTaskAsync(new Uri(block_img.url));

                                igen.Arguments = $"-i - -f rawvideo -pix_fmt rgb24 -";

                                var fp = Process.Start(igen);
                                await fp.StandardInput.BaseStream.WriteAsync(img_data);
                                fp.StandardInput.Close();

                                var block_buf = new byte[img_data.Length];
                                var rlen = await fp.StandardOutput.BaseStream.ReadAsync(block_buf);
                                fp.StandardOutput.Close();

                                fp.WaitForExit();

                                var block_parsed = new Block();
                                block_parsed.ReadFromPayload(block_buf, 0);

                                if(block_parsed != null)
                                {
                                    Console.WriteLine($"Tip updated: {block_parsed.Hash}");

                                    foreach(var p in Node.EnumeratePeers())
                                    {
                                        await p.WriteMessage(block_parsed);
                                    }
                                }
                            }
                        }
                    }
                }

                Console.WriteLine($"Stream closed!");
            }
        }
    }

    public class Account
    {
        public string id { get; set; }
        public string username { get; set; }
        public string acct { get; set; }
        public string display_name { get; set; }
        public bool locked { get; set; }
        public bool bot { get; set; }
        public DateTime created_at { get; set; }
        public string note { get; set; }
        public string url { get; set; }
        public string avatar { get; set; }
        public string avatar_static { get; set; }
        public string header { get; set; }
        public string header_static { get; set; }
        public int followers_count { get; set; }
        public int following_count { get; set; }
        public int statuses_count { get; set; }
        public List<object> emojis { get; set; }
        public List<object> fields { get; set; }
    }

    public class Original
    {
        public int width { get; set; }
        public int height { get; set; }
        public string size { get; set; }
        public double aspect { get; set; }
    }

    public class Small
    {
        public int width { get; set; }
        public int height { get; set; }
        public string size { get; set; }
        public double aspect { get; set; }
    }

    public class Meta
    {
        public Original original { get; set; }
        public Small small { get; set; }
    }

    public class MediaAttachment
    {
        public string id { get; set; }
        public string type { get; set; }
        public string url { get; set; }
        public string preview_url { get; set; }
        public string remote_url { get; set; }
        public object text_url { get; set; }
        public Meta meta { get; set; }
        public object description { get; set; }
    }

    public class Status
    {
        public string id { get; set; }
        public DateTime created_at { get; set; }
        public object in_reply_to_id { get; set; }
        public object in_reply_to_account_id { get; set; }
        public bool sensitive { get; set; }
        public string spoiler_text { get; set; }
        public string visibility { get; set; }
        public string language { get; set; }
        public string uri { get; set; }
        public string content { get; set; }
        public string url { get; set; }
        public int reblogs_count { get; set; }
        public int favourites_count { get; set; }
        public object reblog { get; set; }
        public object application { get; set; }
        public Account account { get; set; }
        public List<MediaAttachment> media_attachments { get; set; }
        public List<object> mentions { get; set; }
        public List<object> tags { get; set; }
        public List<object> emojis { get; set; }
    }
}
