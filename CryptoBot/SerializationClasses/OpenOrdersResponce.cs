namespace CryptoBot.SerializationClasses
{
    public class OpenOrdersResponce
    {
        public string avgPrice { get; set; }
        public string clientOrderId { get; set; }
        public string cumQuote { get; set; }
        public string executedQty { get; set; }
        public string orderId { get; set; }
        public string origQty { get; set; }
        public string origType { get; set; }
        public string price { get; set; }
        public bool reduceOnly { get; set; }
        public string side { get; set; }
        public string positionSide { get; set; }
        public string status { get; set; }
        public string stopPrice { get; set; }
        public string closePosition { get; set; }
        public string symbol { get; set; }
        public string time { get; set; }
        public string timeInForce { get; set; }
        public string type { get; set; }
        public string activatePrice { get; set; }
        public string priceRate { get; set; }
        public string updateTime { get; set; }
        public string workingType { get; set; }
        public bool priceProtect { get; set; }
    }
}
