using hashstream.bitcoin_lib;
using hashstream.bitcoin_lib.BlockChain;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace BlockPics
{
    internal class Config
    {
        public string MastodonHost { get; set; }
        public string MastodonToken { get; set; }

        public string BitcoinAverageSecret { get; set; }
        public string BitcoinAveragePublicKey { get; set; }
    }

    class Program
    {
        private static Config Config { get; set; }

        static bool IsRunning { get; set; } = true;

        static BufferBlock<Block> BlockStream { get; set; } = new BufferBlock<Block>(); //The REAL BlockStream!

        static dynamic Pools { get; set; }

        static Dictionary<string, PoolInfo> CoinbaseTags => Pools.coinbase_tags.ToObject<Dictionary<string, PoolInfo>>();

        static Dictionary<string, PoolInfo> CoinbaseAddress => Pools.payout_addresses.ToObject<Dictionary<string, PoolInfo>>();

        static Task Main(string[] args)
        {
            return SendPics();
        }

        static async Task SendPics()
        {
            Console.WriteLine("Loading config..");
            using (var sr = new StreamReader("config.json"))
            {
                Config = JsonConvert.DeserializeObject<Config>(await sr.ReadToEndAsync());
            }

            Console.WriteLine($"Loading pools..");
            Pools = await GetPools();

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

                File.WriteAllBytes("block.dat", block_data.Concat(new byte[padding]));

                igen.Arguments = $"-y -f rawvideo -pix_fmt rgb24 -s {sres}x{sres} -i block.dat -vframes 1 {block_hash}.png";

                var fp = Process.Start(igen);
                //await fp.StandardInput.BaseStream.WriteAsync(block_data.Concat(new byte[padding]));

                fp.WaitForExit();

                var btc = block.Txns.Sum(a => a.TxOut.Sum(b => b.Value * 1e-8));
                var segwit = block.Txns.Count(a => a.HasWitness());

                var btcusd = await GetBTCPrice();
                var pool = GetPoolInfo(block);
                var status = $"#Bitcoin tip updated{(pool != null ? $" by {pool.name}" : string.Empty)}! {block_hash} ({(block_data.Length / 1000d).ToString("#,##0.00")} kB with {block.TxnCount.Value.ToString("#,###")} txns [{(100d * (segwit / (double)block.TxnCount)).ToString("0.00")}% segwit], moving ₿{btc.ToString("#,##0.00")}, worth ${(btc * btcusd.last).ToString("#,##0.00")})";

                var media = await UploadImage($"{block_hash}.png");
                if (media != null)
                {
                    var post = await PostStatus(status, new List<string> { media.id });

                    Console.WriteLine($"Block posted! {post.url}");
                }
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

        static PoolInfo GetPoolInfo(Block b)
        {
            var cbd = Encoding.UTF8.GetString(b.Txns[0].TxIn[0].Script.ScriptBytes);
            var cba = b.Txns[0].TxOut.FirstOrDefault(a => a.Value != 0).GetAddress().ToString();

            //try coinbase tags first
            foreach(var ct in CoinbaseTags)
            {
                if (cbd.Contains(ct.Key))
                {
                    return ct.Value;
                }
            }

            //try payout address
            foreach(var ct in CoinbaseAddress)
            {
                if(ct.Key == cba)
                {
                    return ct.Value;
                }
            }

            return default;
        }

        static async Task<MediaAttachment> UploadImage(string filename)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create($"https://{Config.MastodonHost}/api/v1/media");
                req.Headers.Add(HttpRequestHeader.Authorization, $"Bearer {Config.MastodonToken}");
                req.Method = "POST";

                var data = new MultipartFormDataContent();
                data.Add(new ByteArrayContent(File.ReadAllBytes(filename)), "file", filename);

                req.ContentType = data.Headers.ContentType.ToString();
                req.ContentLength = data.Headers.ContentLength.Value;

                using (var ss = await req.GetRequestStreamAsync())
                {
                    await data.CopyToAsync(ss);
                }

                var rsp = (HttpWebResponse)await req.GetResponseAsync();
                if (rsp.StatusCode == HttpStatusCode.OK)
                {
                    using(var sr = new StreamReader(rsp.GetResponseStream()))
                    {
                        return JsonConvert.DeserializeObject<MediaAttachment>(await sr.ReadToEndAsync());
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return default;
        }

        static async Task<Status> PostStatus(string status, List<string> Media)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create($"https://{Config.MastodonHost}/api/v1/statuses");
                req.Headers.Add(HttpRequestHeader.Authorization, $"Bearer {Config.MastodonToken}");
                req.Method = "POST";
                req.ContentType = "application/x-www-form-urlencoded";

                var data = $"status={Uri.EscapeDataString(status)}&media_ids[]={string.Join("&media_ids[]=", Media)}";
                using (var ss = await req.GetRequestStreamAsync())
                {
                    var fd = Encoding.UTF8.GetBytes(data);
                    req.ContentLength = fd.Length;

                    await ss.WriteAsync(fd, 0, fd.Length);
                }

                var rsp = (HttpWebResponse)await req.GetResponseAsync();
                if (rsp.StatusCode == HttpStatusCode.OK)
                {
                    using (var sr = new StreamReader(rsp.GetResponseStream()))
                    {
                        return JsonConvert.DeserializeObject<Status>(await sr.ReadToEndAsync());
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return default;
        }

        static string GetBitcoinAverageSig()
        {
            var pl = $"{DateTimeOffset.Now.ToUnixTimeSeconds()}.{Config.BitcoinAveragePublicKey}";
            byte[] dgst = null;
            using (var hmac = new HMACSHA256(Encoding.ASCII.GetBytes(Config.BitcoinAverageSecret)))
            {
                var pld = Encoding.ASCII.GetBytes(pl);
                dgst = hmac.ComputeHash(pld, 0, pld.Length);
            }

            return $"{pl}.{BitConverter.ToString(dgst).Replace("-", string.Empty).ToLower()}";
        }

        static async Task<Ticker> GetBTCPrice()
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create("https://apiv2.bitcoinaverage.com/indices/global/ticker/BTCUSD");
                req.Headers.Add("X-signature", GetBitcoinAverageSig());

                var rsp = (HttpWebResponse)await req.GetResponseAsync();
                if (rsp.StatusCode == HttpStatusCode.OK)
                {
                    using (var sr = new StreamReader(rsp.GetResponseStream()))
                    {
                        return JsonConvert.DeserializeObject<Ticker>(await sr.ReadToEndAsync());
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return default;
        }

        static async Task<dynamic> GetPools()
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create("https://raw.githubusercontent.com/hashstream/pools/master/pools.json");
                var rsp = (HttpWebResponse)await req.GetResponseAsync();
                if (rsp.StatusCode == HttpStatusCode.OK)
                {
                    using (var sr = new StreamReader(rsp.GetResponseStream()))
                    {
                        return JsonConvert.DeserializeObject<dynamic>(await sr.ReadToEndAsync());
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return default;
        }
    }

    internal class PoolInfo
    {
        public string name { get; set; }
        public string link { get; set; }
    }
}
