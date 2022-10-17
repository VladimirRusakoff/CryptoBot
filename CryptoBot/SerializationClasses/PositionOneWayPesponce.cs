namespace CryptoBot.SerializationClasses
{
    public class PositionOneWayResponce
    {
        public decimal entryPrice { get; set; }
        public string marginType { get; set; }
        public bool isAutoAddMargin { get; set; }
        public int leverage { get; set; } // размер плеча 10, 20 
        public decimal liquidationPrice { get; set; }
        public decimal markPrice { get; set; }
        public string maxNotionalValue { get; set; }
        public string c { get; set; }
        public decimal positionAmt { get; set; }
        public string symbol { get; set; }
        public decimal unRealizedProfit { get; set; }
        public string positionSide { get; set; } // "BOTH"
    }
}
