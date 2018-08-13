using System;
using System.Collections.Generic;
using System.Text;

namespace BlockPics
{
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
