namespace CryptoBot.SerializationClasses
{
    public class Data
    {
        public string e { get; set; } // "trade"
        public long E { get; set; } // 1615133818871
        public string s { get; set; } // ETHUSDT
        public long t { get; set; } // 387870331
        public decimal p { get; set; } // 1666.90
        public decimal q { get; set; } // 1.269
        //public long b { get; set; } // -
        //public long a { get; set; } // -
        public long T { get; set; } // 1615133818867
        public bool m { get; set; } // false
        //public bool M { get; set; } // -
        public object X { get; set; } // MARKET
        //public object x { get; set; } // -
    }

    public class TradeResponse
    {
        public string stream { get; set; }
        public Data data { get; set; }
    }
}
