using System;
using System.Collections.Generic;
using System.Text;

namespace BlockPics
{
    public class Open
    {
        public string day { get; set; }
        public string week { get; set; }
        public string month { get; set; }
    }

    public class Averages
    {
        public double daily { get; set; }
        public double weekly { get; set; }
        public double monthly { get; set; }
    }

    public class Price
    {
        public double weekly { get; set; }
        public double monthly { get; set; }
        public double daily { get; set; }
    }

    public class Percent
    {
        public double weekly { get; set; }
        public double monthly { get; set; }
        public double daily { get; set; }
    }

    public class Changes
    {
        public Price price { get; set; }
        public Percent percent { get; set; }
    }

    public class Ticker
    {
        public double ask { get; set; }
        public double bid { get; set; }
        public double last { get; set; }
        public double high { get; set; }
        public double low { get; set; }
        public Open open { get; set; }
        public Averages averages { get; set; }
        public double volume { get; set; }
        public Changes changes { get; set; }
        public double volume_percent { get; set; }
        public int timestamp { get; set; }
        public string display_timestamp { get; set; }
    }
}
