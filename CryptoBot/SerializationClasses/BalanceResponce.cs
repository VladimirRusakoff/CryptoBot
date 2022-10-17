namespace CryptoBot.SerializationClasses
{
    public class BalanceResponce
    {
        public string accountAlias { get; set; }
        public string asset { get; set; }
        public decimal balance { get; set; }
        public decimal crossWalletBalance { get; set; }
        public decimal crossUnPnl { get; set; }
        public decimal availableBalance { get; set; }
        public decimal maxWithdrawAmount { get; set; }
    }
}
