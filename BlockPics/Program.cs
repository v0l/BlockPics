using hashstream.bitcoin_lib;
using hashstream.bitcoin_lib.BlockChain;
using NetMQ;
using NetMQ.Sockets;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace BlockPics
{
    class Program
    {
        static bool IsRunning { get; set; } = true;

        static BufferBlock<Block> BlockStream { get; set; } = new BufferBlock<Block>(); //The REAL BlockStream!

        static Task Main(string[] args)
        {
            return SendPics(args[0], args[1]);
        }

        static async Task SendPics(string host, string token)
        {
            var authClient = new Mastodot.MastodonClient(host, token);
            var acc = await authClient.GetCurrentAccount();

            Console.WriteLine($"Logged into: {acc.FullUserName}");

            Console.WriteLine($"Starting ZMQ thread...");
            var zqt = new Thread(new ThreadStart(ReadBlocks));
            zqt.Start();

            var igen = new ProcessStartInfo("ffmpeg");
            while (IsRunning)
            {
                var block = await BlockStream.ReceiveAsync();
                var block_hash = block.Hash.ToString();
                var block_data = block.ToArray();

                var pixels = block_data.Length / 3;

                var sres = (int)Math.Ceiling(Math.Sqrt(pixels));
                var padding = (sres * sres * 3) - block_data.Length;

                Console.WriteLine($"Adding {padding} bytes padding..");

                await File.WriteAllBytesAsync("block.dat", block_data.Concat(new byte[padding]));

                igen.Arguments = $"-y -f rawvideo -pix_fmt rgb24 -s {sres}x{sres} -i block.dat -vframes 1 {block_hash}.png";

                var fp = Process.Start(igen);
                //await fp.StandardInput.BaseStream.WriteAsync(block_data.Concat(new byte[padding]));

                fp.WaitForExit();

                try
                {
                    var media = await authClient.UploadMedia($"{block_hash}.png");
                    await authClient.PostNewStatus($"#Bitcoin tip updated! {block_hash} ({block.TxnCount.Value.ToString("#,###")} txns, moving {block.Txns.Sum(a => a.TxOut.Sum(b => b.Value / 1e-8)).ToString("#,##0.0000 BTC")})", null, new List<int>() { media.Id });
                    
                    Console.WriteLine($"Block posted! {media.Url}");
                }
                catch { }
            }
            
        }

        static void ReadBlocks()
        {
            var zmq = new SubscriberSocket("tcp://127.0.0.1:18333");
            zmq.Subscribe("rawblock");
            //zmq.Subscribe("rawtx");
            //zmq.SubscribeToAnyTopic();

            while (IsRunning)
            {
                var msg = zmq.ReceiveMultipartMessage();
                var tag = msg[0].ConvertToString();
                var msg_data = msg[1].Buffer;

                if (tag == "rawtx")
                {
                    var tx = new Tx();
                    tx.ReadFromPayload(msg_data, 0);

                    Console.WriteLine($"Got new tx! {tx.TxHash}");
                }
                else if (tag == "rawblock")
                {
                    var bp = new Block();
                    bp.ReadFromPayload(msg_data, 0);

                    BlockStream.Post(bp);
                }
            }
        }
    }
}
