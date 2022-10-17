using System;
using System.Collections.Generic;
using System.Linq;

namespace CryptoBot
{
    public class WorkSettings
    {
        // здесь устанавливаем дату прекращения работы бота (год, месяц, день, часы, минуты, секунды)
        public static DateTime LastTime = new DateTime(2085, 11, 15, 0, 0, 0);

        // здесь устанавливаем публичный ключ клиента. в ковычках
        public static string PublicKey = "";

        // устанавливаем максимальный объем для торговли (если установлен 0, то значит можно торговать любым объемом)
        public static decimal maxVolume = 1000;
    }
    public class Settings
    {
        public string apiKey { get; set; }
        public string secKey { get; set; }

        public List<set> setSettings = new List<set>();
        public bool LogIsOn { get; set; }
        //public bool isUseDelta { get; set; }
        public int secondsForClosingIn0 { get; set; }
        public double secondsSecondsOrder { get; set; }
        public string botNickname { get; set; }
        //
        public bool isUseBTCdelta { get; set; }
        public decimal btcDelta { get; set; }
        public int btcSecondsOff { get; set; }
        public int btcMinutes { get; set; }
    }

    public class set
    {
        public string symbols { get; set; }
        public string direction { get; set; }
        public decimal volume { get; set; }
        public decimal distance { get; set; }
        public decimal buffer { get; set; }
        public decimal stop { get; set; }
        public decimal take { get; set; }
        //public decimal delta0 { get; set; }
        //public decimal delta1 { get; set; }
    }

    public class BinanceTime
    {
        public string serverTime { get; set; }
    }

    public class BinanceUserMessage
    {
        public string MessageStr;
    }

    public class Security
    {
        public string symbol { get; set; }
        public string baseAsset { get; set; }
        public string quoteAsset { get; set; }
        public string tickSize { get; set; }
        public string minQty { get; set; }
        public string stepSize { get; set; }
        public int precisPrice { get; set; }
        public int precisVolume { get; set; }
    }

    public class LifeCircle
    {
        public Position position = new Position();

        public OrderParam orderFirst = new OrderParam();

        public OrderParam orderStop = new OrderParam();

        public OrderParam orderTake = new OrderParam();

        //public OrderParam orderProfit0 = new OrderParam(); // ордера закрытия по безубытку
        public decimal lastPrice { get; set; }
        public string direction { get; set; }
        public decimal volume { get; set; }
        public decimal bufferUp { get; set; }
        public decimal bufferDown { get; set; } = 0;
        public decimal distanceUp { get; set; } = 0;
        public decimal distanceDown { get; set; }
        public decimal settingsDistance { get; set; }
        public decimal settingsBuffer { get; set; }
        public decimal settingsTake { get; set; }
        public decimal settingsStop { get; set; }
        public decimal settingsVolume { get; set; }

        public Security security = new Security();

        public DateTime lastTimeLifeCircleFinished = new DateTime();
        public bool isNuleVolume { get; set; } // показывает, что нулевой объем
        public isCancelOrder isCancelOrd { get; set; } = new isCancelOrder();

        public string lastCanceledId = "";
    }

    public class isCancelOrder
    {
        public DateTime timeActivateBuy { get; set; } // время выставления заявки на покупку
        public DateTime timeActivateSell { get; set; } // время выставления заявки на продажу
        public bool isCancelBuy { get; set; }
        public bool isCancelSell { get; set; }
    }

    public class OrderParam
    {
        public DateTime datetime { get; set; }
        public string datetimeBinance { get; set; }
        public string orderIDmy { get; set; }
        public string orderIDBinance { get; set; }
        public string side { get; set; }
        public decimal price { get; set; }
        public decimal volumeExecuted { get; set; } = 0;
        public decimal volumeSent { get; set; } = 0;
        public DateTime errorTime { get; set; } = new DateTime();

        public OrderState state = OrderState.None;
    }

    public class Position
    {
        public string side { get; set; }

        public OrderParam openOrder = new OrderParam();

        public OrderParam closeOrder = new OrderParam();
        public decimal takeProfit { get; set; }
        public decimal stopLoss { get; set; }
        public bool isOpened { get; set; }
    }

    public class Candles
    {
        public DateTime timeStart { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Volume { get; set; }
    }

    public class symbInfo
    {
        public decimal lastPrice { get; set; }
        public Dictionary<DateTime, Candles> candles { get; set; } = new Dictionary<DateTime, Candles>(); // последние 60 свечей (1-минутки)
        public decimal maxValue { get; set; }
        public decimal minValue { get; set; }
        //public decimal delta { get; set; }
    }

    public enum OrderState
    {
        None, // не выставляли ещё ордера
        Active, // выставленный ордер
        PartlyExecute, // частично исполнена
        Executed, // исполненный ордер
        Canceled, // отмененный ордер
        Rejected, 
        Expired
    }
}
