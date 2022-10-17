namespace CryptoBot.SerializationClasses
{
    class ChangeLeverageResponce
    {
        public string symbol { get; set; }
        public int leverage { get; set; }
        public string maxNotionalValue { get; set; }
    }
}
