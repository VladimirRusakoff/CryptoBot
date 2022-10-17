using System.Collections.Generic;

namespace CryptoBot.SerializationClasses
{
    public class PositionUp
    {
        public string s { get; set; } // Symbol
        public decimal pa { get; set; } // Position Amount
        public decimal ep { get; set; } // Entry Price
        public decimal cr { get; set; } // (Pre-fee) Accumulated Realized
        public decimal up { get; set; } // Unrealized PnL
        public string mt { get; set; } // Margin Type
        public decimal iw { get; set; } // Isolated Wallet (if isolated position)
        public string ps { get; set; } // Position Side
    }
    public class Balances
    {
        public string a { get; set; } // Asset
        public string wb { get; set; } // Wallet Balance
        public string cw { get; set; } // Cross Wallet Balance
    }
    public class updateDate
    {
        public string m { get; set; } // Event reason type
        public List<Balances> B { get; set; } // Balances
        public List<PositionUp> P { get; set; } // Positions
    }
    public class AccountUpdate
    {
        public string e { get; set; } // Event Type
        public string E { get; set; } // Event Time
        public string T { get; set; } // Transaction
        public updateDate a { get; set; } // Update Data
    }
}
