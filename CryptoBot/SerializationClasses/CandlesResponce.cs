namespace CryptoBot.SerializationClasses
{
    public class candleData
    {
        public string t { get; set; } // Kline start time
        public string T { get; set; } // Kline close time
        public string s { get; set; } // Symbol
        public string i { get; set; } // Interval
        public string f { get; set; } // First trade ID
        public string L { get; set; } // Last trade ID
        public decimal o { get; set; } // Open price
        public decimal c { get; set; } // Close price
        public decimal h { get; set; } // High price
        public decimal l { get; set; } // Low price
        public decimal v { get; set; } // Base asset volume
        public string n { get; set; } // Number of trades
        public bool x { get; set; } // Is this kline closed?
        public string q { get; set; } // Quote asset volume
        public string V { get; set; } // Taker buy base asset volume
        public string Q { get; set; } // Taker buy quote asset volume
        public string B { get; set; } // Ignore
    }

    public class CandlesResponce
    {
        public string e { get; set; } // Event type
        public string E { get; set; } // Event time
        public string s { get; set; } // Symbol
        public candleData k { get; set; }
    }

    public class CandlesStream
    {
        public string stream { get; set; }
        public CandlesResponce data { get; set; }
    }
}
