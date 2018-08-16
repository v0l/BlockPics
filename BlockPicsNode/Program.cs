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
using BlockPics;

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
                    if (line.StartsWith(":") || line == string.Empty)
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
                                await fp.StandardInput.BaseStream.WriteAsync(img_data, 0, img_data.Length);
                                fp.StandardInput.Close();

                                var block_buf = new byte[img_data.Length];
                                var rlen = await fp.StandardOutput.BaseStream.ReadAsync(block_buf, 0, block_buf.Length);
                                fp.StandardOutput.Close();

                                fp.WaitForExit();

                                var block_parsed = new Block();
#if NETCOREAPP2_1
                                block_parsed.ReadFromPayload(block_buf);
#else
                                block_parsed.ReadFromPayload(block_buf, 0);
#endif

                                if (block_parsed != null)
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
}
