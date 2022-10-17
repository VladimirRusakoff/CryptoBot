using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;
using WebSocket4Net;
using RestSharp;
using Newtonsoft.Json;
using CryptoBot.SerializationClasses;
using Newtonsoft.Json.Linq;

namespace CryptoBot
{
    public class BinanceFutures
    {
        private string baseURL = "https://fapi.binance.com";

        public Settings settings = new Settings();

        public DateTime binanceServerTime;

        private LogFileNew fileErrors;
        private LogFileNew fileLogs;
        //private LogFileNew fileTrades;

        public BinanceFutures(bool useLog = true)
        {
            if (useLog)
            {
                fileErrors = new LogFileNew("error");
                fileLogs = new LogFileNew("log");
                //fileTrades = new LogFileNew("trades", ".csv");
            }

            if (!LoadSettings())
            {
                settings = new Settings();
                settings.apiKey = "";
                settings.secKey = "";
                //
                settings.setSettings.Add(new set() 
                {
                    volume = 20,
                    distance = 0.9m,
                    buffer = 0.3m,
                    take = 0.5m,
                    stop = 0.5m,
                    direction = "Long",
                    symbols = "BNBUSDT" 
                });
                //
                settings.setSettings.Add(new set()
                {
                    volume = 20,
                    distance = 0.9m,
                    buffer = 0.3m,
                    take = 0.5m,
                    stop = 0.5m,
                    direction = "Short",
                    symbols = "BNBUSDT"
                });
                //
                settings.setSettings.Add(new set()
                {
                    volume = 20,
                    distance = 0.9m,
                    buffer = 0.3m,
                    take = 0.5m,
                    stop = 0.5m,
                    direction = "Long",
                    symbols = "ETHUSDT"
                });
                //
                settings.setSettings.Add(new set()
                {
                    volume = 20,
                    distance = 0.9m,
                    buffer = 0.3m,
                    take = 0.5m,
                    stop = 0.5m,
                    direction = "Short",
                    symbols = "ETHUSDT"
                });
                //
                settings.LogIsOn = true;
                settings.secondsForClosingIn0 = 30;
                settings.secondsSecondsOrder = 0.5;
                // 
                settings.isUseBTCdelta = false;
                settings.btcMinutes = 10;
                settings.btcDelta = 1.5m;
                settings.btcSecondsOff = 60;
            }
        }

        private object mainLogicLocker = new object();

        private List<ConcurrentDictionary<string, LifeCircle>> listLifeCircle = new List<ConcurrentDictionary<string, LifeCircle>>();

        private ConcurrentDictionary<string, symbInfo> allSymbolsLastPrice = new ConcurrentDictionary<string, symbInfo>();

        private void makeListWithAllSymbols()
        {
            if (listLifeCircle == null || listLifeCircle.Count == 0)
                return;

            foreach (var lc in listLifeCircle)
            {
                foreach (var sy in lc.Keys)
                {
                    if (!allSymbolsLastPrice.ContainsKey(sy))
                        allSymbolsLastPrice.TryAdd(sy, new symbInfo() { lastPrice = 0 });
                }
            }

            // если используем дельту по BTCUSDT
            if (settings.isUseBTCdelta == false)
                return;

            if (!allSymbolsLastPrice.ContainsKey("BTCUSDT"))
            {
                allSymbolsLastPrice.TryAdd("BTCUSDT", new symbInfo() { lastPrice = 0 });
            }
        }

        public void StopStrategy()
        {
            lock (mainLogicLocker)
            {
                CancelAllOpenOrdes();
                CloseAllOpenPositions();
            }
        }

        /// <summary>
        /// отмена всех ордеров 
        /// </summary>
        private void CancelAllOpenOrdes()
        {
            if (allSymbolsLastPrice == null || allSymbolsLastPrice.Count == 0)
                makeListWithAllSymbols();

            foreach (var sy in allSymbolsLastPrice.Keys)
                CancelOpenOrdersByBinance(sy);
        }

        /// <summary>
        /// закрытие всех открытых позиций
        /// </summary>
        private void CloseAllOpenPositions()
        {
            if (dictPositions.Count == 0)
                return;

            foreach (var cpp in dictPositions.Keys)
            {
                PositionUp cp = dictPositions[cpp];
                if (cp.pa == 0)
                    continue;

                if (cp.pa > 0) // лонговая позиция
                    SendMarketOrder(cp.s, "SELL", cp.pa, GetClientOrderId());
                else if (cp.pa < 0) // шортовая позиция
                    SendMarketOrder(cp.s, "BUY", (-1) * cp.pa, GetClientOrderId());
            }
        }

        public void StartStrategy()
        {
            if (string.IsNullOrEmpty(settings.apiKey))
                return;

            if (string.IsNullOrEmpty(settings.secKey))
                return;

            if (!CheckBinanceServer())
                return;

            lock (mainLogicLocker)
            {
                binanceServerTime = GetServerTime();

                GetSymbols();

                for (int i = 0; i < settings.setSettings.Count; i++)
                    AddSymbolsInLifeCircleDictionary(i);

                // заполнеяем ВсеСимволы и подписываемся на сделки и свечи
                if (allSymbolsLastPrice == null || allSymbolsLastPrice.Count == 0)
                    makeListWithAllSymbols();

                foreach (var k in allSymbolsLastPrice.Keys)
                {
                    // подписываемся на получение трейдов и стаканов для каждой бумаги
                    SubscribleTradesAndDepths(k);
                    // подписываем на получение свечей
                    //allSymbolsLastPrice[k].candles = GetCandles(k, "1m");
                    //SubscribeCandles1m(k);
                }
                if (!allSymbolsLastPrice.ContainsKey("BTCUSDT"))
                    allSymbolsLastPrice.TryAdd("BTCUSDT", new symbInfo());
                
                allSymbolsLastPrice["BTCUSDT"].candles = GetCandles("BTCUSDT", "1m");
                SubscribeCandles1m("BTCUSDT");

                CreateThreads();
            }
        }

        private void AddSymbolsInLifeCircleDictionary(int i)
        {
            if (string.IsNullOrEmpty(settings.setSettings[i].symbols))
                return;

            if (listLifeCircle.Count != 2)
            {
                listLifeCircle.Add(new ConcurrentDictionary<string, LifeCircle>()); // словарь для Long
                listLifeCircle.Add(new ConcurrentDictionary<string, LifeCircle>()); // словарь для Short
            }

            // определяем, заполняем мы лонг или шорт
            int j = 0;
            if (settings.setSettings[i].direction != "Long")
                j = 1;

            string foundSymbols = "";
            string notFoundSymbols = "";
            //
            string[] ss = settings.setSettings[i].symbols.Split(',');
            foreach (string s in ss)
            {
                // проверяем, есть ли такой символ в списке всех символов
                string sy = s.Trim();
                if (symbols.ContainsKey(sy))
                {
                    if (!listLifeCircle[j].ContainsKey(sy))
                    {
                        listLifeCircle[j].TryAdd(sy, new LifeCircle());
                        listLifeCircle[j][sy].security = symbols[sy];
                        listLifeCircle[j][sy].direction = settings.setSettings[i].direction;
                        listLifeCircle[j][sy].settingsDistance = settings.setSettings[i].distance;
                        listLifeCircle[j][sy].settingsBuffer = settings.setSettings[i].buffer;
                        listLifeCircle[j][sy].settingsStop = settings.setSettings[i].stop;
                        listLifeCircle[j][sy].settingsTake = settings.setSettings[i].take;
                        listLifeCircle[j][sy].settingsVolume = settings.setSettings[i].volume;

                        foundSymbols += string.Format("{0}, ", sy);
                    }
                }
                else
                {
                    notFoundSymbols += string.Format("{0}, ", sy);
                }
            }

            LogMessage(string.Format("StartStrategy | foundSymbols | {0} | not found Symbols | {1}",
                   foundSymbols, notFoundSymbols));
        }



        public void SubscribleTradesAndDepths(string securityName)
        {
            string urlStr = "wss://fstream.binance.com/stream?streams="
                //+ securityName.ToLower() + "@depth20/"
                + securityName.ToLower() + "@trade";

            WebSocket wsClient = new WebSocket(urlStr);

            wsClient.Opened += new EventHandler(Connect);
            wsClient.Closed += new EventHandler(Disconnect);
            wsClient.Error += new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs>(WsError);
            wsClient.MessageReceived += new EventHandler<MessageReceivedEventArgs>(GetRes);
            wsClient.Open();
        }

        public void SubscribeCandles1m(string securityName)
        {
            string urlStr = "wss://fstream.binance.com/stream?streams="
                + securityName.ToLower() + "@kline_1m";

            WebSocket wsCandles = new WebSocket(urlStr);

            wsCandles.Opened += new EventHandler(ConnectCandles);
            wsCandles.Closed += new EventHandler(DisconnectCandles);
            wsCandles.Error += new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs>(WsErrorCandles);
            wsCandles.MessageReceived += new EventHandler<MessageReceivedEventArgs>(GetResCandles);
            wsCandles.Open();
        }

        /// <summary>
        /// очередь новых сообщений с сервера (маркет-дата)
        /// </summary>
        private ConcurrentQueue<string> newMessage = new ConcurrentQueue<string>();

        private void GetRes(object sender, MessageReceivedEventArgs e)
        {
            //if (countOfTransactions > 1300)
            //{
            //    // замедляем получение сделок (каждые 10 секунд)
            //    if (timeSlowingDeals.Year == 1 || DateTime.Now >= timeSlowingDeals)
            //    {
            //        LogMessage("Slow down trades");
            //        timeSlowingDeals = DateTime.Now.AddSeconds(10);
            //        if (e.Message.Contains("error") || e.Message.Contains("\"e\":\"trade\""))
            //            newMessage.Enqueue(e.Message);
            //    }
            //}
            //else
            //{
            if (e.Message.Contains("error") || e.Message.Contains("\"e\":\"trade\""))
                newMessage.Enqueue(e.Message);
            //}
        }

        private void CreateThreads()
        {
            CreateUserDataStream("/fapi/v1/listenKey");

            Thread keepAlive = new Thread(KeepAliveUserDataStream);
            keepAlive.CurrentCulture = new CultureInfo("ru-RU");
            keepAlive.IsBackground = true;
            keepAlive.Start();

            Thread converter = new Thread(convertTradesDepth);
            converter.CurrentCulture = new CultureInfo("ru-RU");
            converter.IsBackground = true;
            converter.Start();

            //Thread converterCandles = new Thread(convertCandles);
            //converterCandles.CurrentCulture = new CultureInfo("ru-RU");
            //converterCandles.IsBackground = true;
            //converterCandles.Start();

            //Thread SendCancelOrders = new Thread(sendCancelOrders);
            //SendCancelOrders.CurrentCulture = new CultureInfo("ru-RU");
            //SendCancelOrders.IsBackground = true;
            //SendCancelOrders.Start();

            Thread converterUserData = new Thread(convertUserData);
            converterUserData.CurrentCulture = new CultureInfo("ru-RU");
            converterUserData.IsBackground = true;
            converterUserData.Start();

            Thread checkCurrentPositions = new Thread(checkCurrentPoses);
            checkCurrentPositions.CurrentCulture = new CultureInfo("ru-RU");
            checkCurrentPositions.IsBackground = true;
            checkCurrentPositions.Start();

            //Thread checkCurrentPositionsAdd = new Thread(checkCurrentPosesAdditional);
            //checkCurrentPositionsAdd.CurrentCulture = new CultureInfo("ru-RU");
            //checkCurrentPositionsAdd.IsBackground = true;
            //checkCurrentPositionsAdd.Start();

            // поток для отслеживания кол-ва транзакций в минуту
            Thread checkCountOfTransaction = new Thread(checkCountTransaction);
            checkCountOfTransaction.CurrentCulture = new CultureInfo("ru-RU");
            checkCountOfTransaction.IsBackground = true;
            checkCountOfTransaction.Start();

            // поток для отслеживания кол-ва транзакций за 10 секунд
            Thread checkCountOfTransaction10 = new Thread(checkCountTransaction10);
            checkCountOfTransaction10.CurrentCulture = new CultureInfo("ru-RU");
            checkCountOfTransaction10.IsBackground = true;
            checkCountOfTransaction10.Start();

            // поток, смотрящий за изменением ВТСUSDT если стоит галочка Use BTCUSDT Delta
            Thread btcDeltaFollowing = new Thread(BTCdeltaFollowing);
            btcDeltaFollowing.CurrentCulture = new CultureInfo("ru-RU");
            btcDeltaFollowing.IsBackground = true;
            btcDeltaFollowing.Start();
        }

        private string listenKey = "";

        public WebSocket clientWS;
        private void CreateUserDataStream(string url)
        {
            try
            {
                var res = CreateQuery(Method.POST, url, null, false);

                listenKey = JsonConvert.DeserializeAnonymousType(res, new ListenKey()).listenKey;

                string urlStr = "wss://fstream.binance.com/ws/" + listenKey;

                clientWS = new WebSocket(urlStr);

                clientWS.Opened += Connect;
                clientWS.Closed += Disconnect;
                clientWS.Error += WsError;
                clientWS.MessageReceived += delegate (object sender, MessageReceivedEventArgs args)
                {
                    UserDataMessageHandler(sender, args);
                };
                clientWS.Open();
            }
            catch (Exception ex)
            {
                LogMessage(string.Format("error | CreateUserDataStream | {0}", ex.ToString()));
            }
        }

        private DateTime _timeStart;

        /// <summary>
        /// каждые полчаса отправляем сообщение, чтобы поток не закрылся
        /// </summary>
        private void KeepAliveUserDataStream()
        {
            while (true)
            {
                Thread.Sleep(30000);

                if (listenKey == "")
                    return;

                if (_timeStart.AddMinutes(25) < DateTime.Now)
                {
                    _timeStart = DateTime.Now;

                    CreateQuery(Method.PUT, "/fapi/v1/listenKey",
                        new Dictionary<string, string>() { { "listenKey=", listenKey } }, false);

                    // проверка лицензий
                    if (LicenseNotOrFinished()) // закончилось время лицензии
                    {
                        LogMessage(string.Format("error | Your License is Finished {0}", WorkSettings.LastTime));
                        CancelAllOpenOrdes();
                        CloseAllOpenPositions();
                        IsOn = false;
                    }
                }
            }
        }

        /// <summary>
        /// получили новую инфу по заявкам, сделкам и прочее
        /// </summary>
        private void convertUserData()
        {
            while (true)
            {
                lock (mainLogicLocker)
                {
                    try
                    {
                        if (!newUserDataMessage.IsEmpty)
                        {
                            BinanceUserMessage message;

                            if (newUserDataMessage.TryDequeue(out message))
                            {
                                string mes = message.MessageStr;

                                if (mes.Contains("code"))
                                {
                                    LogMessage(JsonConvert.DeserializeAnonymousType(mes, new ErrorMessage()).msg);
                                }
                                else if (mes.Contains("\"e\"" + ":" + "\"ORDER_TRADE_UPDATE\""))
                                {
                                    var ord = JsonConvert.DeserializeAnonymousType(mes, new OrderUpdResponse());

                                    var order = ord.o;

                                    string orderNumUser = order.c;

                                    if (order.x == "NEW") // ордер выставился на бирже
                                    {
                                        foreach (var dict in listLifeCircle)
                                        {
                                            if (!dict.ContainsKey(order.s))
                                                continue;

                                            if (orderNumUser == dict[order.s].orderFirst.orderIDmy) // ордер выставился
                                            {
                                                dict[order.s].orderFirst.state = OrderState.Active;
                                                dict[order.s].orderFirst.orderIDBinance = order.i;
                                                dict[order.s].orderFirst.side = order.S;
                                                decimal pr = ConvertStringToDecimal(order.p);
                                                if (pr != 0)
                                                    dict[order.s].orderFirst.price = pr;
                                                dict[order.s].orderFirst.volumeSent = ConvertStringToDecimal(order.q);
                                                //
                                                dict[order.s].isCancelOrd.timeActivateBuy = DateTime.Now.AddMilliseconds(settings.secondsSecondsOrder * 1000);
                                                LogMessage(string.Format("success | MyNewOrderEvent | {4} | {0} | {1} | {2} | {3}", order.S, order.i, order.p, order.q, order.s));
                                            }
                                            else if (orderNumUser == dict[order.s].orderStop.orderIDmy)
                                            {
                                                dict[order.s].orderStop.state = OrderState.Active;
                                                dict[order.s].orderStop.orderIDBinance = order.i;
                                                dict[order.s].orderStop.side = order.S;
                                                dict[order.s].orderStop.price = ConvertStringToDecimal(order.p);
                                                dict[order.s].orderStop.volumeSent = ConvertStringToDecimal(order.q);
                                                LogMessage(string.Format("success | MyNewStopEvent | {4} | {0} | {1} | {2} | {3}",
                                                    order.S, order.i, order.p, order.q, order.s));
                                            }
                                            else if (orderNumUser == dict[order.s].orderTake.orderIDmy)
                                            {
                                                dict[order.s].orderTake.state = OrderState.Active;
                                                dict[order.s].orderTake.orderIDBinance = order.i;
                                                dict[order.s].orderTake.side = order.S;
                                                dict[order.s].orderTake.price = ConvertStringToDecimal(order.p);
                                                dict[order.s].orderTake.volumeSent = ConvertStringToDecimal(order.q);
                                                LogMessage(string.Format("success | MyNewTakeEvent | {4} | {0} | {1} | {2} | {3}",
                                                    order.S, order.i, order.p, order.q, order.s));
                                            }
                                        }
                                    }
                                    else if (order.x == "CANCELED")
                                    {
                                        int i = -1;
                                        foreach (var dict in listLifeCircle)
                                        {
                                            i++;

                                            if (!dict.ContainsKey(order.s))
                                                continue;

                                            if (orderNumUser == dict[order.s].orderFirst.orderIDmy)
                                            {
                                                dict[order.s].orderFirst.state = OrderState.Canceled;

                                                if (dict[order.s].position.isOpened == false) // нет позиции
                                                {
                                                    dict[order.s].orderFirst.orderIDBinance = "";
                                                    dict[order.s].orderFirst.orderIDmy = "";
                                                    LogMessage(string.Format("success | CanceledOrderEvent BufferOut | {4} | {0} | {1} | {2} | {3}",
                                                        order.S, order.i, order.p, order.q, order.s));
                                                }
                                                else // есть позиция
                                                {
                                                    //setTakeAndStop(order.s, "SELL", i);
                                                    LogMessage(string.Format("success | CanceledOrderEvent StopTake | {4} | {0} | {1} | {2} | {3}",
                                                        order.S, order.i, order.p, order.q, order.s));
                                                }
                                            }
                                            else if (orderNumUser == dict[order.s].orderStop.orderIDmy)
                                            {
                                                dict[order.s].orderStop.state = OrderState.Canceled;
                                                LogMessage(string.Format("success | CanceledStopEvent | {4} | {0} | {1} | {2} | {3}", order.S, order.i, order.p, order.q, order.s));
                                            }
                                            else if (orderNumUser == dict[order.s].orderTake.orderIDmy)
                                            {
                                                dict[order.s].orderTake.state = OrderState.Canceled;
                                                dict[order.s].orderTake.orderIDmy = "";
                                                dict[order.s].orderTake.orderIDBinance = "";
                                                LogMessage(string.Format("success | CanceledTakeEvent | {4} | {0} | {1} | {2} | {3}", order.S, order.i, order.p, order.q, order.s));
                                            }
                                        }
                                    }
                                    else if (order.x == "REJECTED")
                                    {
                                        foreach (var dict in listLifeCircle)
                                        {
                                            if (!dict.ContainsKey(order.s))
                                                continue;

                                            if (orderNumUser == dict[order.s].orderFirst.orderIDmy)
                                            {
                                                dict[order.s].orderFirst.state = OrderState.Rejected;
                                                dict[order.s].orderFirst.orderIDBinance = "";
                                                dict[order.s].orderFirst.orderIDmy = "";
                                                LogMessage(string.Format("error | RejectedOrderEvent orderBuy | {0} | {1} | {2} | {3}", order.S, order.i, order.p, order.q));
                                            }
                                            else if (orderNumUser == dict[order.s].orderStop.orderIDmy)
                                            {
                                                dict[order.s].orderStop.state = OrderState.Rejected;
                                                dict[order.s].orderStop.orderIDBinance = "";
                                                dict[order.s].orderStop.orderIDmy = "";
                                                LogMessage(string.Format("error | RejectedOrderEvent orderStop | {0} | {1} | {2} | {3}", order.S, order.i, order.p, order.q));
                                            }
                                            else if (orderNumUser == dict[order.s].orderTake.orderIDmy)
                                            {
                                                dict[order.s].orderTake.state = OrderState.Rejected;
                                                dict[order.s].orderTake.orderIDBinance = "";
                                                dict[order.s].orderTake.orderIDmy = "";
                                                LogMessage(string.Format("error | RejectedOrderEvent orderTake | {0} | {1} | {2} | {3}", order.S, order.i, order.p, order.q));
                                            }
                                        }
                                    }
                                    else if (order.x == "TRADE") // ордер исполнился
                                    {
                                        int i = -1;
                                        foreach (var dict in listLifeCircle)
                                        {
                                            i++;
                                            if (!dict.ContainsKey(order.s))
                                                continue;

                                            decimal executeVolume = ConvertStringToDecimal(order.l);
                                            decimal neededVolume = ConvertStringToDecimal(order.q);

                                            if (orderNumUser == dict[order.s].orderFirst.orderIDmy) // исполнилась заявка на покупку
                                            {
                                                dict[order.s].orderFirst.volumeExecuted += executeVolume;
                                                //if (dict[order.s].orderBuy.volumeExecuted >= dict[order.s].orderBuy.volumeSent) // заявка исполнилась полностью
                                                if (dict[order.s].orderFirst.volumeExecuted < neededVolume) // заявка исполнилась частично
                                                {
                                                    dict[order.s].orderFirst.state = OrderState.PartlyExecute;
                                                    LogMessage(string.Format("success | MyTradePartlyEvent | {4} | {0} | {1} | {2} | {3} | {5}",
                                                                    order.S, order.i, order.p, order.q, order.s, order.l));
                                                }
                                                else
                                                {
                                                    dict[order.s].orderFirst.state = OrderState.Executed;

                                                    // заполняем позицию
                                                    dict[order.s].position.isOpened = true;
                                                    dict[order.s].position.side = dict[order.s].direction == "Long" ? "BUY" : "SELL";
                                                    dict[order.s].position.openOrder.datetime = DateTime.Now;
                                                    dict[order.s].position.openOrder.price = ConvertStringToDecimal(order.p);

                                                    // отменяем заявку на продажу
                                                    //CancelOrder(order.s, dict[order.s].orderSell.orderIDBinance);
                                                    // выставляем тейк / стоп
                                                    setTakeAndStop(order.s, dict[order.s].direction == "Long" ? "BUY" : "SELL", i);

                                                    LogMessage(string.Format("success | MyTradeBuyEvent | {4} | {0} | {1} | {2} | {3}", order.S, order.i, order.p, order.q, order.s));
                                                }
                                            }
                                            else if (orderNumUser == dict[order.s].orderStop.orderIDmy) // исполнился стоп-лосс
                                            {
                                                dict[order.s].orderStop.volumeExecuted += executeVolume;
                                                //if (dict[order.s].orderStop.volumeExecuted >= dict[order.s].orderStop.volumeSent) // заявка исполнилась полностью
                                                if (dict[order.s].orderStop.volumeExecuted < neededVolume) // заявка исполнилась частично
                                                {
                                                    dict[order.s].orderStop.state = OrderState.PartlyExecute;
                                                    LogMessage(string.Format("success | ExecuteStopPartlyEvent | {0} | {1} | {2} | {3} | {4} | {5}",
                                                                                order.s, order.S, order.i, order.p, order.q, order.l));
                                                }
                                                else
                                                {
                                                    dict[order.s].orderStop.state = OrderState.Executed;
                                                    DeleteAllElementsForNewCircle(order.s, i, 90);
                                                    LogMessage(string.Format("success | ExecuteStopEvent | {0} | {1} | {2} | {3} | {4}", order.s, order.S, order.i, order.p, order.q));
                                                    // заполняем позицию

                                                }
                                            }
                                            else if (orderNumUser == dict[order.s].orderTake.orderIDmy) // исполнился тейк-профит
                                            {
                                                dict[order.s].orderTake.volumeExecuted += executeVolume;
                                                //if (dict[order.s].orderTake.volumeExecuted >= dict[order.s].orderTake.volumeSent) // заявка исполнилась полностью
                                                if (dict[order.s].orderTake.volumeExecuted < neededVolume) // заявка исполнилась частично
                                                {
                                                    dict[order.s].orderTake.state = OrderState.PartlyExecute;
                                                    LogMessage(string.Format("success | ExecuteTakePartlyEvent | {0} | {1} | {2} | {3} | {4} | {5}",
                                                                                order.s, order.S, order.i, order.p, order.q, order.l));
                                                }
                                                else
                                                {
                                                    dict[order.s].orderTake.state = OrderState.Executed;
                                                    DeleteAllElementsForNewCircle(order.s, i, 90);
                                                    LogMessage(string.Format("success | ExecuteTakeEvent | {0} | {1} | {2} | {3} | {4}", order.s, order.S, order.i, order.p, order.q));
                                                    // заполняем позицию

                                                }
                                            }
                                        }
                                    }
                                    else if (order.x == "EXPIRED")
                                    {
                                        foreach (var dict in listLifeCircle)
                                        {
                                            if (!dict.ContainsKey(order.s))
                                                continue;

                                            if (orderNumUser == dict[order.s].orderFirst.orderIDmy)
                                            {
                                                dict[order.s].orderFirst.state = OrderState.Expired;
                                                dict[order.s].orderFirst.orderIDBinance = "";
                                                dict[order.s].orderFirst.orderIDmy = "";
                                                LogMessage(string.Format("error | ExpireOrderEvent orderBuy | {4} | {0} | {1} | {2} | {3}", 
                                                    order.S, order.i, order.p, order.q, order.s));
                                            }
                                            else if (orderNumUser == dict[order.s].orderStop.orderIDmy)
                                            {
                                                dict[order.s].orderStop.state = OrderState.Expired;
                                                dict[order.s].orderStop.orderIDBinance = "";
                                                dict[order.s].orderStop.orderIDmy = "";
                                                LogMessage(string.Format("error | ExpireOrderEvent orderStop | {4} | {0} | {1} | {2} | {3}", 
                                                    order.S, order.i, order.p, order.q, order.s));
                                            }
                                            else if (orderNumUser == dict[order.s].orderTake.orderIDmy)
                                            {
                                                dict[order.s].orderTake.state = OrderState.Expired;
                                                dict[order.s].orderTake.orderIDBinance = "";
                                                dict[order.s].orderTake.orderIDmy = "";
                                                LogMessage(string.Format("error | ExpireOrderEvent orderTake | {4} | {0} | {1} | {2} | {3}", 
                                                    order.S, order.i, order.p, order.q, order.s));
                                            }
                                        }
                                    }
                                    else if (order.x == "CALCULATED " || order.o == "LIQUIDATION") // произошла ликвидация позиции Liquidation Execution
                                    {
                                        LogMessage(string.Format(string.Format("error | Liquidation Position | {0} | {1} | {2} | {3} | {4}", order.s, order.S, order.i, order.p, order.q)));
                                        //isLiquadationPosition = true;
                                    }

                                    continue;
                                }
                                else if (mes.Contains("\"e\"" + ":" + "\"ACCOUNT_UPDATE\""))
                                {
                                    AccountUpdate au = JsonConvert.DeserializeAnonymousType(mes, new AccountUpdate());
                                    List<PositionUp> listPositions = au.a.P;
                                    if (listPositions.Count > 0)
                                    {
                                        foreach (PositionUp pu in listPositions)
                                        {
                                            if (dictPositions.ContainsKey(pu.s))
                                                dictPositions[pu.s] = pu;
                                            else
                                                dictPositions.Add(pu.s, pu);
                                        }
                                        timePositionsUpdate = DateTime.Now;
                                    }
                                    // обновление баланса
                                    listBalances = au.a.B;
                                    //LogMessage(string.Format("error | ACCOUNT_UPDATE | {0}", mes));
                                }
                                else if (mes.Contains("\"e\"" + ":" + "\"ORDER_TRADE_UPDATE\""))
                                {
                                    int ii = 8;
                                }
                            }
                            else
                            {
                                Thread.Sleep(1);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage(string.Format("error | convertUserData | {0}", ex.ToString()));
                    }
                }

                Thread.Sleep(1);
            }
        }

        public DateTime timePositionsUpdate { get; set; }

        // выставляем стоп и тейк
        private void setTakeAndStop(string symb, string side, int ii)
        {
            if (listLifeCircle.Count < ii)
                return;

            if (!listLifeCircle[ii].ContainsKey(symb))
                return;

            if (side == "BUY")
            {
                // выставляем стоп и тейк для покупки
                listLifeCircle[ii][symb].position.stopLoss = RoundPrice(listLifeCircle[ii][symb].orderFirst.price * (1 - listLifeCircle[ii][symb].settingsStop / 100), symb);
                listLifeCircle[ii][symb].position.takeProfit = RoundPrice(listLifeCircle[ii][symb].orderFirst.price * (1 + listLifeCircle[ii][symb].settingsTake / 100), symb);
                listLifeCircle[ii][symb].orderTake.orderIDmy = GetClientOrderId();
                SendLimitOrder(symb, "SELL", listLifeCircle[ii][symb].position.takeProfit, listLifeCircle[ii][symb].volume, listLifeCircle[ii][symb].orderTake.orderIDmy);
                //}
                LogMessage(string.Format("success | mainLogic | Stop&Take for BUY | {0} | OpenPrice {3} | Stop {1} | Take {2}", symb,
                    listLifeCircle[ii][symb].position.stopLoss, listLifeCircle[ii][symb].position.takeProfit, listLifeCircle[ii][symb].orderFirst.price));
            }
            else if (side == "SELL")
            {
                // выставляем стоп и тейк для продажи
                listLifeCircle[ii][symb].position.stopLoss = RoundPrice(listLifeCircle[ii][symb].orderFirst.price * (1 + listLifeCircle[ii][symb].settingsStop / 100), symb);
                listLifeCircle[ii][symb].position.takeProfit = RoundPrice(listLifeCircle[ii][symb].orderFirst.price * (1 - listLifeCircle[ii][symb].settingsTake / 100), symb);
                listLifeCircle[ii][symb].orderTake.orderIDmy = GetClientOrderId();
                SendLimitOrder(symb, "BUY", listLifeCircle[ii][symb].position.takeProfit, listLifeCircle[ii][symb].volume, listLifeCircle[ii][symb].orderTake.orderIDmy);
                //}
                LogMessage(string.Format("success | mainLogic | Stop&Take for SELL | {0} | OpenPrice {3} | Stop {1} | Take {2}", symb,
                    listLifeCircle[ii][symb].position.stopLoss, listLifeCircle[ii][symb].position.takeProfit, listLifeCircle[ii][symb].orderFirst.price));
            }
        }

        public bool isLiquadationPosition = false;

        public Dictionary<string, PositionUp> dictPositions { get; set; } = new Dictionary<string, PositionUp>();

        public List<Balances> listBalances { get; set; } = new List<Balances>();

        private DateTime timeForCheckingTransaction = new DateTime();
        private void checkCountTransaction()
        {
            while (true)
            {
                lock (mainLogicLocker)
                {
                    if (timeForCheckingTransaction.Year == 1 || timeForCheckingTransaction.AddMinutes(1) < DateTime.Now)
                    {
                        timeForCheckingTransaction = DateTime.Now;
                        LogMessage(string.Format("success | checkCountTransaction | {0} ", countOfTransactions));
                        countOfTransactions = 0;
                    }
                }

                Thread.Sleep(5000);
            }
        }

        private DateTime timeForCheckingTransaction10 = new DateTime();
        private void checkCountTransaction10()
        {
            while (true)
            {
                lock (mainLogicLocker)
                {
                    if (timeForCheckingTransaction10.Year == 1 || timeForCheckingTransaction10.AddSeconds(10) < DateTime.Now)
                    {
                        timeForCheckingTransaction10 = DateTime.Now;
                        LogMessage(string.Format("success | checkCountTransaction10 | {0} ", countOfTransactions10));
                        countOfTransactions10 = 0;
                    }
                }

                Thread.Sleep(1000);
            }
        }

        private bool btcDeltaIsOn = false;
        private DateTime btcTimeBegin = new DateTime();
        private void BTCdeltaFollowing()
        {
            while (true)
            {
                Thread.Sleep(5000);

                lock (mainLogicLocker)
                {
                    if (settings.isUseBTCdelta == false)
                        return;

                    // проверяем условия, что за N минут BTC не изменился на указанное значение
                    if (!allSymbolsLastPrice.ContainsKey("BTCUSDT"))
                        continue;

                    if (btcTimeBegin >= DateTime.Now)
                        continue;

                    decimal btcDelta = getBTCDelta();
                    if (btcDelta > settings.btcDelta) // снимаем заявки и останавливаем на указанное кол-во секунд
                    {
                        if (btcDeltaIsOn == false)
                        {
                            btcDeltaIsOn = true;
                            CancelAllOpenOrdes();
                            btcTimeBegin = DateTime.Now.AddSeconds(settings.btcSecondsOff);
                            LogMessage(string.Format("success | BTCdeltaFollowing | btcDelta {0}", btcDelta));
                        }
                        else // (btcDeltaIsOn == true)
                        {
                            btcTimeBegin = DateTime.Now.AddSeconds(settings.btcSecondsOff);
                            LogMessage(string.Format("success | BTCdeltaFollowing OneMoreTime | btcDelta {0}", btcDelta));
                        }
                    }
                    else
                    {
                        if (btcDeltaIsOn == true)
                            btcDeltaIsOn = false;
                    }
                }
            }
        }

        private decimal getBTCDelta()
        {
            List<Candles> neededCandles = new List<Candles>();
            if (settings.btcMinutes <= allSymbolsLastPrice["BTCUSDT"].candles.Count)
            {
                List<Candles> cc = allSymbolsLastPrice["BTCUSDT"].candles.Values.ToList();
                for (int i = cc.Count - settings.btcMinutes; i < cc.Count; i++)
                    neededCandles.Add(cc[i]);
            }
            else // кол-во минут, указанное в настройках больше, чем 60
            {
                neededCandles = allSymbolsLastPrice["BTCUSDT"].candles.Values.ToList();
            }

            decimal maxVal = neededCandles.Max(t => t.High);
            decimal minVal = neededCandles.Min(t => t.Low);

            if (maxVal != 0 && minVal != 0)
                return Math.Round((maxVal - minVal) / minVal * 100, 8);

            return 0;
        }

        private void checkCurrentPoses()
        {
            while (true)
            {
                Thread.Sleep(1000);

                checkPositionFromPositionsList(dictPositions.Values.ToList());
            }
        }

        //private void sendCancelOrders()
        //{
        //    while (true)
        //    {
        //        Thread.Sleep(2);

        //        if (listLifeCircle.Count == 0)
        //            continue;

        //        int i = -1;
        //        foreach (var lifeCircle in listLifeCircle)
        //        {
        //            i++;
        //            foreach (string symb in lifeCircle.Keys)
        //            {
        //                if (lifeCircle[symb].position.isOpened == true)
        //                    continue;

        //                #region проверка на то, вышли из буфера или нет
        //                if (!string.IsNullOrEmpty(lifeCircle[symb].orderBuy.orderIDBinance))
        //                {
        //                    if ((lifeCircle[symb].bufferDown != 0
        //                        && lifeCircle[symb].distanceDown != 0
        //                        && lifeCircle[symb].lastPrice < lifeCircle[symb].bufferDown
        //                        && lifeCircle[symb].lastPrice > lifeCircle[symb].distanceDown) // цена вышла из буфера, но не прошла дистанцию (т.е. не исполнилась)
        //                        || (DateTime.Now >= lifeCircle[symb].isCancelOrd.timeCancelBuy) // или прошло время для перестановки заявки
        //                        || lifeCircle[symb].isCancelOrd.isCancelBuy == true)
        //                    {
        //                        CancelOrder(symb, lifeCircle[symb].orderBuy.orderIDBinance);
        //                    }
        //                }

        //                if (!string.IsNullOrEmpty(lifeCircle[symb].orderSell.orderIDBinance))
        //                {
        //                    if ((lifeCircle[symb].bufferUp != 0
        //                        && lifeCircle[symb].distanceUp != 0
        //                        && lifeCircle[symb].lastPrice > lifeCircle[symb].bufferUp
        //                        && lifeCircle[symb].lastPrice < lifeCircle[symb].distanceUp) // цена вышла из буфера, но не прошла дистанцию (т.е. не исполнилась)
        //                        || (DateTime.Now >= lifeCircle[symb].isCancelOrd.timeCancelSell)
        //                        || lifeCircle[symb].isCancelOrd.isCancelSell == true)
        //                    {
        //                        CancelOrder(symb, lifeCircle[symb].orderSell.orderIDBinance);
        //                    }
        //                }
        //                #endregion

        //                #region выставление заявок
        //                CreateTwoOrders(symb, i);
        //                #endregion
        //            }
        //        }
        //    }
        //}

        private void checkCurrentPosesAdditional()
        {
            while (true)
            {
                Thread.Sleep(7500);

                try
                {
                    List<PositionOneWayResponce> powr = GetCurrentPositions();

                    if (powr.Count == 0)
                        continue;

                    foreach (var p in powr)
                    {
                        int ii = searchFirstNumberInList(p.symbol);
                        if (ii == -1) // показывает, что символ не найден в нашем списке словарей
                            continue;

                        if (p.positionAmt == 0)
                            continue;

                        decimal take;
                        decimal stop;
                        if (p.positionAmt > 0) // лонговая позиция
                        {
                            if (listLifeCircle[ii][p.symbol].lastPrice <= p.liquidationPrice * (1 + 0.003m)) // доп.проверка, чтобы не было закрытия по ликвидации
                            {
                                listLifeCircle[ii][p.symbol].orderStop.orderIDmy = GetClientOrderId();
                                SendMarketOrder(p.symbol, "SELL", p.positionAmt, listLifeCircle[ii][p.symbol].orderStop.orderIDmy);
                                LogMessage(string.Format("success | checkCurrentPosesAdditional LIQ | {0} | {1} | {2} | lastPrice {3}",
                                        p.symbol, p.entryPrice, p.positionAmt, listLifeCircle[ii][p.symbol].lastPrice));
                            }
                            else
                            {
                                take = RoundPrice(p.entryPrice * (1 + listLifeCircle[ii][p.symbol].settingsTake / 100), p.symbol);
                                stop = RoundPrice(p.entryPrice * (1 - listLifeCircle[ii][p.symbol].settingsStop / 100), p.symbol);
                                if (listLifeCircle[ii][p.symbol].lastPrice > take * (1 + (5 * constLittle)))
                                {
                                    listLifeCircle[ii][p.symbol].orderTake.orderIDmy = GetClientOrderId();
                                    SendMarketOrder(p.symbol, "SELL", p.positionAmt, listLifeCircle[ii][p.symbol].orderTake.orderIDmy);
                                    LogMessage(string.Format("success | checkCurrentPosesAdditional TAKE | {0} | {1} | {2} | lastPrice {3}",
                                                                 p.symbol, p.entryPrice, p.positionAmt, listLifeCircle[ii][p.symbol].lastPrice));
                                }
                                else if (listLifeCircle[ii][p.symbol].lastPrice < stop * (1 - (5 * constLittle)))
                                {
                                    listLifeCircle[ii][p.symbol].orderStop.orderIDmy = GetClientOrderId();
                                    SendMarketOrder(p.symbol, "SELL", p.positionAmt, listLifeCircle[ii][p.symbol].orderTake.orderIDmy);
                                    LogMessage(string.Format("success | checkCurrentPosesAdditional STOP | {0} | {1} | {2} | lastPrice {3}",
                                                                 p.symbol, p.entryPrice, p.positionAmt, listLifeCircle[ii][p.symbol].lastPrice));
                                }
                            }
                        }
                        else // шортовая позиция
                        {
                            if (listLifeCircle[ii][p.symbol].lastPrice >= p.liquidationPrice * (1 - 0.003m)) // доп.проверка, чтобы не было закрытия по ликвидации
                            {
                                listLifeCircle[ii][p.symbol].orderStop.orderIDmy = GetClientOrderId();
                                SendMarketOrder(p.symbol, "BUY", p.positionAmt, listLifeCircle[ii][p.symbol].orderStop.orderIDmy);
                                LogMessage(string.Format("success | checkCurrentPosesAdditional LIQ | {0} | {1} | {2} | lastPrice {3}",
                                        p.symbol, p.entryPrice, p.positionAmt, listLifeCircle[ii][p.symbol].lastPrice));
                            }
                            else
                            {
                                take = RoundPrice(p.entryPrice * (1 - listLifeCircle[ii][p.symbol].settingsTake / 100), p.symbol);
                                stop = RoundPrice(p.entryPrice * (1 + listLifeCircle[ii][p.symbol].settingsStop / 100), p.symbol);
                                if (listLifeCircle[ii][p.symbol].lastPrice < take * (1 - (5 * constLittle)))
                                {
                                    listLifeCircle[ii][p.symbol].orderTake.orderIDmy = GetClientOrderId();
                                    SendMarketOrder(p.symbol, "BUY", (-1)*p.positionAmt, listLifeCircle[ii][p.symbol].orderTake.orderIDmy);
                                    LogMessage(string.Format("success | checkCurrentPosesAdditional TAKE | {0} | {1} | {2} | lastPrice {3}",
                                                                 p.symbol, p.entryPrice, p.positionAmt, listLifeCircle[ii][p.symbol].lastPrice));
                                }
                                else if (listLifeCircle[ii][p.symbol].lastPrice > stop * (1 + (5 * constLittle)))
                                {
                                    listLifeCircle[ii][p.symbol].orderStop.orderIDmy = GetClientOrderId();
                                    SendMarketOrder(p.symbol, "BUY", (-1) * p.positionAmt, listLifeCircle[ii][p.symbol].orderTake.orderIDmy);
                                    LogMessage(string.Format("success | checkCurrentPosesAdditional STOP | {0} | {1} | {2} | lastPrice {3}",
                                                                 p.symbol, p.entryPrice, p.positionAmt, listLifeCircle[ii][p.symbol].lastPrice));
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogMessage(string.Format("error | checkCurrentPosesAdditional STOP | {0}", ex.ToString()));
                }

                //List<PositionUp> pu = new List<PositionUp>();
                //foreach (var p in powr)
                //{
                //    PositionUp u = new PositionUp();
                //    u.s = p.symbol;
                //    u.pa = p.positionAmt;
                //    u.ep = p.entryPrice;
                //    u.up = p.unRealizedProfit;
                //    u.mt = p.marginType;
                //    u.ps = p.positionSide;
                //    pu.Add(u);
                //}

                //checkPositionFromPositionsList(pu);
            }
        }

        private decimal constLittle = 0.0005m;
        private decimal constProfit0 = 0.002m; // уровень безубытка 0.2% 

        private void checkPositionFromPositionsList(List<PositionUp> listPoses)
        {
            lock (mainLogicLocker)
            {
                try
                {
                    if (listPoses.Count == 0)
                        return;

                    foreach (var cp in listPoses)
                    {
                        if (cp.pa == 0)
                            continue;

                        for (int ii = 0; ii < listLifeCircle.Count; ii++)
                        {
                            if (!listLifeCircle[ii].ContainsKey(cp.s))
                                continue;

                            if ((listLifeCircle[ii][cp.s].direction == "Long" && cp.pa > 0)
                                || (listLifeCircle[ii][cp.s].direction != "Long" && cp.pa < 0))
                            {
                                if (listLifeCircle[ii][cp.s].position.isOpened == true)
                                    continue;

                                listLifeCircle[ii][cp.s].position.isOpened = true;
                                listLifeCircle[ii][cp.s].position.openOrder.price = cp.ep;

                                if (!string.IsNullOrEmpty(listLifeCircle[ii][cp.s].orderFirst.orderIDBinance))
                                    CancelOrder(cp.s, listLifeCircle[ii][cp.s].orderFirst.orderIDBinance);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogMessage(string.Format("error | checkCurrentPoses | {0}", ex.ToString()));
                }
            }
        }

        private bool ChooseApropriateElementFromListDict(string symb) // проверяем есть ли у нас во всем списке открытая позиция, если есть, то ничего не делаем
        {
            bool retVal = false;

            for (int i = 0; i < listLifeCircle.Count; i++)
            {
                if (listLifeCircle[i].ContainsKey(symb))
                {
                    if (listLifeCircle[i][symb].position.isOpened == true)
                        return true;
                }
            }

            return retVal;
        }

        private int searchFirstNumberInList(string symb)
        {
            int retVal = -1;

            for (int i = 0; i < listLifeCircle.Count; i++)
            {
                if (listLifeCircle[i].ContainsKey(symb))
                    return i;
            }

            return retVal;
        }

        public List<PositionOneWayResponce> GetCurrentPositions()
        {
            List<PositionOneWayResponce> curPoses = new List<PositionOneWayResponce>();

            try
            {
                var res = CreateQuery(Method.GET, "/fapi/v2/positionRisk", null, true);
                if (res == null)
                {
                    LogMessage(string.Format("error | GetCurrentPositions"));
                    //StopStrategyForSymbolsWithoutPositions();
                }
                else
                {
                    PositionOneWayResponce[] allPoses = JsonConvert.DeserializeObject<PositionOneWayResponce[]>(res);
                    PositionOneWayResponce[] poses = Array.FindAll(allPoses, x => x.positionAmt != 0);
                    foreach (var po in poses)
                        curPoses.Add(po);
                }
            }
            catch (Exception ex)
            {
                //if (ex.ToString().Contains("Cannot deserialize the current JSON object")) // если появляется такая ошибка, то отменяем все ордера и ждем 5 сек. 
                //{
                //    StopStrategyForSymbolsWithoutPositions();
                //}
                //
                LogMessage(string.Format("error | GetCurrentPositions | {0}", ex.ToString()));
            }

            return curPoses;
        }

        public List<BalanceResponce> GetBalance()
        {
            List<BalanceResponce> balances = new List<BalanceResponce>();

            try
            {
                var res = CreateQuery(Method.GET, "/fapi/v2/balance", null, true);
                if (res == null)
                {
                    LogMessage(string.Format("error | GetBalance"));
                }
                else
                {
                    BalanceResponce[] bala = JsonConvert.DeserializeObject<BalanceResponce[]>(res);
                    BalanceResponce[] ba = Array.FindAll(bala, x => x.balance != 0);
                    foreach (var b in ba)
                        balances.Add(b);
                }
            }
            catch (Exception ex)
            {
                LogMessage(string.Format("error | GetBalance | {0}", ex.ToString()));
            }

            return balances;
        }

        public List<SymbolsLeverage> GetMarginModeLeverage()
        {
            List<SymbolsLeverage> symbolLeverage = new List<SymbolsLeverage>();

            try
            {
                var res = CreateQuery(Method.GET, "/fapi/v2/positionRisk", null, true);
                if (res == null)
                {
                    //LogMessage(string.Format("error | GetCurrentPositions"));
                }
                else
                {
                    PositionOneWayResponce[] allPoses = JsonConvert.DeserializeObject<PositionOneWayResponce[]>(res);
                    foreach (var po in allPoses)
                        symbolLeverage.Add(new SymbolsLeverage() {
                            symbol = po.symbol,
                            marMode = po.marginType == "cross" ? MarginMode.Cross : MarginMode.Isolate,
                            Leverage = po.leverage
                            });
                }
            }
            catch (Exception ex)
            {
                //LogMessage(string.Format("error | GetCurrentPositions | {0}", ex.ToString()));
            }

            return symbolLeverage;
        }

        public void ChangeMarginType(string symb, MarginMode margType)
        {
            try
            {
                Dictionary<string, string> param = new Dictionary<string, string>();

                param.Add("symbol=", symb.ToUpper());
                param.Add("&marginType=", margType == MarginMode.Isolate ? "ISOLATED" : "CROSSED");

                var res = CreateQuery(Method.POST, "/fapi/v1/marginType", param, true);

                if (res.Contains("success")) // изменение типа маржи выполнено успешно
                {

                }
            }
            catch (Exception ex)
            {

            }
        }

        public void ChangeLeverage(string symb, int leverage)
        {
            try
            {
                Dictionary<string, string> param = new Dictionary<string, string>();

                param.Add("symbol=", symb.ToUpper());
                param.Add("&leverage=", leverage.ToString());

                var res = CreateQuery(Method.POST, "/fapi/v1/leverage", param, true);
                ChangeLeverageResponce clr = JsonConvert.DeserializeAnonymousType(res, new ChangeLeverageResponce());

                if (clr.symbol == symb && clr.leverage == leverage) // изменение плеча выполнено успешно
                {

                }
            }
            catch (Exception ex)
            {

            }
        }

        //public void StopStrategyForSymbolsWithoutPositions()
        //{
        //    if (allSymbolsLastPrice == null || allSymbolsLastPrice.Count == 0)
        //        makeListWithAllSymbols();
            
        //    // смотрим открытые ордера и удаляем их
        //    foreach (string symb in allSymbolsLastPrice.Keys)
        //    {
        //        //if (string.IsNullOrEmpty(lifeCircle[symb].orderBuy.orderIDmy) && string.IsNullOrEmpty(lifeCircle[symb].orderSell.orderIDmy))
        //        //    continue;

        //        CancelOpenOrdersByBinance(symb);
        //        for (int ii = 0; ii < listLifeCircle.Count; ii++)
        //            DeleteAllElementsForNewCircle(symb, ii);
        //    }
        //    LogMessage(string.Format("success | StopStrategyForSymbolsWithoutPositions"));
        //}

        /// <summary>
        /// очистка всех элементов, чтобы запустить цикл с начала 
        /// </summary>
        private void DeleteAllElementsForNewCircle(string symb, int ii, int seconds)
        {
            if (!listLifeCircle[ii].ContainsKey(symb))
                return;

            if (!string.IsNullOrEmpty(listLifeCircle[ii][symb].orderTake.orderIDBinance))
                CancelOrder(symb, listLifeCircle[ii][symb].orderTake.orderIDBinance);
            // для того, чтобы подождать 1.5 минуты после того, как исполнился тейк или стоп
            listLifeCircle[ii][symb].lastTimeLifeCircleFinished = DateTime.Now.AddSeconds(seconds);
            listLifeCircle[ii][symb].position.isOpened = false;
            //
            listLifeCircle[ii][symb].orderFirst.orderIDmy = "";
            listLifeCircle[ii][symb].orderFirst.orderIDBinance = "";
            listLifeCircle[ii][symb].orderFirst.state = OrderState.None;
            //
            listLifeCircle[ii][symb].orderStop.orderIDmy = "";
            listLifeCircle[ii][symb].orderStop.state = OrderState.None;
            //
            listLifeCircle[ii][symb].orderTake.orderIDmy = "";
            listLifeCircle[ii][symb].orderTake.state = OrderState.None;
            //
            listLifeCircle[ii][symb].position.takeProfit = 0;
            listLifeCircle[ii][symb].position.stopLoss = 0;
            //
            listLifeCircle[ii][symb].position.openOrder.datetime = new DateTime();
        }

        private void Connect(object sender, EventArgs e)
        {
            LogMessage(string.Format("success | Connect | {0}", ((WebSocket4Net.WebSocket)sender).LocalEndPoint));
        }

        private void ConnectCandles(object sender, EventArgs e)
        {
            LogMessage(string.Format("success | ConnectCandles"));
        }

        public bool isDisconnect = false;

        private void Disconnect(object sender, EventArgs e)
        {
            isDisconnect = true;
            MessageBox.Show("Disconnect!!!");
            //
            LogMessage(string.Format("success | Disconnect | {0}", e.ToString()));
        }

        private void DisconnectCandles(object sender, EventArgs e)
        { 
            LogMessage(string.Format("success | DisconnectCandles"));
        }

        private void WsError(object sender, EventArgs e)
        {
            LogMessage(string.Format("error | WsError | {0}", e.ToString()));
        }

        private void WsErrorCandles(object sender, EventArgs e)
        {
            LogMessage(string.Format("error | WsErrorCandles | {0}", e.ToString()));
        }

        private ConcurrentQueue<BinanceUserMessage> newUserDataMessage = new ConcurrentQueue<BinanceUserMessage>();

        private void UserDataMessageHandler(object sender, MessageReceivedEventArgs e)
        {
            BinanceUserMessage message = new BinanceUserMessage();
            message.MessageStr = e.Message;
            newUserDataMessage.Enqueue(message);
        }

        private bool CheckBinanceServer()
        {
            Uri uri = new Uri(baseURL + "/fapi/v1/time");
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                var httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
                HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            }
            catch (Exception ex)
            {
                LogMessage(string.Format("Server isn't availabe. Internet is absent: {0}", ex.Message));
                return false;
            }

            return true;
        }

        public DateTime GetServerTime()
        {
            DateTime servTime = new DateTime();

            string endPoint = "/fapi/v1/time";

            string res = CreateQuery(Method.GET, endPoint);

            if (res == null)
            {

            }
            else
            {
                BinanceTime bt = JsonConvert.DeserializeAnonymousType(res, new BinanceTime());
                servTime = getDateTimeFromUNIX(bt.serverTime);
            }

            return servTime;
        }

        private DateTime getDateTimeFromUNIX(string unix)
        {
            DateTime dt = new DateTime();

            try
            {
                long unixTime = Convert.ToInt64(unix);
                DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
                dt = epoch.AddMilliseconds(unixTime);
            }
            catch
            {

            }

            return dt;
        }

        public ConcurrentDictionary<string, Security> symbols = new ConcurrentDictionary<string, Security>();

        private object _lock = new object();

        public void GetSymbols()
        {
            lock (_lock)
            {
                try
                {
                    var res = CreateQuery(Method.GET, "/fapi/v1/exchangeInfo", null, false);

                    SecurityResponce secResp = JsonConvert.DeserializeAnonymousType(res, new SecurityResponce());
                    foreach (var symb in secResp.symbols)
                    {
                        Security sec = new Security();
                        sec.symbol = symb.symbol;
                        sec.baseAsset = symb.baseAsset;
                        sec.quoteAsset = symb.quoteAsset;
                        sec.tickSize = symb.filters[0].tickSize;
                        sec.minQty = symb.filters[1].minQty;
                        sec.stepSize = symb.filters[1].stepSize;
                        sec.precisPrice = getPrecision(sec.tickSize);
                        sec.precisVolume = getPrecision(sec.stepSize);

                        symbols.TryAdd(symb.symbol, sec);
                    }
                }
                catch (Exception ex)
                {
                    LogMessage(ex.ToString());
                }

                //List<Security> dd = symbols.Values.Where(p => p.precisPrice > 6).ToList();
            }
        }

        private int countOfTransactions = 0;
        private int countOfTransactions10 = 0;

        private object lockOrder = new object();

        public void CancelOpenOrdersByBinance(string symb)
        {
            lock (lockOrder)
            {
                try
                {
                    if (string.IsNullOrEmpty(symb))
                        return;

                    countOfTransactions++;
                    countOfTransactions10++;

                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param.Add("symbol=", symb.ToUpper());

                    string res = CreateQuery(Method.DELETE, "/fapi/v1/allOpenOrders", param, true);
                    if (res == null)
                    {
                        LogMessage(string.Format("error | CancelOpenOrdersByBinance | {0}", symb));
                    }
                    else
                    {
                        LogMessage(string.Format("success | CancelOpenOrdersByBinance | {0}", symb));
                    }
                }
                catch (Exception ex)
                {
                    LogMessage(string.Format("error | CancelOpenOrdersByBinance | {0} | {1}", symb, ex.ToString()));
                }
            }
        }

        private int getPrecision(string value)
        {
            if (value.Contains("."))
            {
                string[] sv = value.Split('.');
                return sv[1].Length;
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// получили новый тик или изменение по стакану
        /// </summary>
        private void convertTradesDepth()
        {
            while (true)
            {
                if (IsOn == false)
                    return;

                string mes = "";
                try
                {
                    if (!newMessage.IsEmpty)
                    {
                        //string mes;
                        if (newMessage.TryDequeue(out mes))
                        {
                            if (mes.Contains("error"))
                            {
                                LogMessage(mes);
                            }
                            else if (mes.Contains("\"e\":\"trade\""))
                            {
                                if (!mes.Contains("MARKET"))
                                    continue;

                                //var quotes = JsonConvert.DeserializeAnonymousType(mes, new TradeResponse());
                                TradeResponse quotes = JsonConvert.DeserializeObject<TradeResponse>(mes);

                                //if (quotes.data.X.ToString() != "MARKET")
                                //    continue;

                                NewTrade(quotes.data.s, quotes.data.p, getDateTimeFromUNIX(quotes.data.T.ToString()));
                                //LogMessage(string.Format("New trade got {0}", lastTrade));
                                continue;
                            }
                            //else if (mes.Contains("\"depthUpdate\""))
                            //{
                            //    //var quotes = JsonConvert.DeserializeAnonymousType(mes, new DepthResponse());

                            //    //bestBid = Convert.ToDecimal(((string)quotes.data.b[0][0]).Replace(".", ","));//CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator));
                            //    //bestAsk = Convert.ToDecimal(((string)quotes.data.a[0][0]).Replace(".", ","));//CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator));
                            //    //LogMessage(string.Format("New depth got: bestBid {0}, bestAsk {0}", bestBid, bestAsk));
                            //    continue;
                            //}
                        }
                        else
                        {
                            Thread.Sleep(1);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogMessage(string.Format("error | convertTradesDepth | {0} | {1}", mes, ex.ToString()));
                }

                Thread.Sleep(1);
            }
        }

        private void NewTrade(string symb, decimal lastPrice, DateTime tradeTime)
        {
            if (IsOn == false)
                return;

            binanceServerTime = tradeTime;

            // основная логика
            lock (mainLogicLocker)
            {
                if (allSymbolsLastPrice == null || allSymbolsLastPrice.Count == 0)
                    makeListWithAllSymbols();

                if (allSymbolsLastPrice.ContainsKey(symb))
                    allSymbolsLastPrice[symb].lastPrice = lastPrice;

                int i = -1;
                foreach (var lifeCircle in listLifeCircle)
                {
                    i++;

                    if (!lifeCircle.ContainsKey(symb))
                        continue;

                    // заполняем последнюю цену
                    lifeCircle[symb].lastPrice = lastPrice;

                    // расчет объема для торговли для каждой бумаги
                    if (lifeCircle[symb].volume == 0 && lifeCircle[symb].isNuleVolume == false)
                        lifeCircle[symb].volume = RoundVolume(lastPrice, symb, i);

                    if (lifeCircle[symb].position.isOpened == false) // нет открытых позиций
                    {
                        #region выставление заявок (начала стратегии)
                        if (CreateTwoOrders(symb, i) == true)
                            continue;
                        #endregion

                        #region проверка на то, вышли ли мы из зоны буфера или нет (для заявок на покупку и продажу)
                        //if (lifeCircle[symb].orderBuy.state == OrderState.Active)
                        if (lifeCircle[symb].direction == "Long")
                        {
                            if (!string.IsNullOrEmpty(lifeCircle[symb].orderFirst.orderIDBinance))
                            {
                                if (lifeCircle[symb].bufferDown != 0
                                    && lifeCircle[symb].bufferUp != 0
                                    && (lastPrice < lifeCircle[symb].bufferDown || lastPrice > lifeCircle[symb].bufferUp * (1 + (1m * constLittle))))
                                    //&& lifeCircle[symb].distanceDown != 0
                                    //&& lastPrice > lifeCircle[symb].distanceDown)
                                {
                                    CancelOrder(symb, lifeCircle[symb].orderFirst.orderIDBinance);
                                }
                            }
                        }

                        //if (lifeCircle[symb].orderSell.state == OrderState.Active)
                        if (lifeCircle[symb].direction == "Short")
                        {
                            if (!string.IsNullOrEmpty(lifeCircle[symb].orderFirst.orderIDBinance))
                            {
                                if (lifeCircle[symb].bufferUp != 0
                                    && lifeCircle[symb].bufferDown != 0
                                    && (lastPrice > lifeCircle[symb].bufferUp || lastPrice < lifeCircle[symb].bufferDown * (1 - (1m * constLittle))))
                                    //&& lifeCircle[symb].distanceUp != 0
                                    //&& lastPrice < lifeCircle[symb].distanceUp)
                                {
                                    CancelOrder(symb, lifeCircle[symb].orderFirst.orderIDBinance);
                                }
                            }
                        }
                        #endregion
                    }
                    else
                    {
                        #region проверка на закрытие по тейку
                        if (string.IsNullOrEmpty(lifeCircle[symb].orderTake.orderIDmy))
                        {
                            if (lifeCircle[symb].direction == "Long")
                            {
                                if (lifeCircle[symb].position.takeProfit == 0)
                                    lifeCircle[symb].position.takeProfit = lifeCircle[symb].position.openOrder.price * (1 + lifeCircle[symb].settingsTake / 100);

                                if (lastPrice >= lifeCircle[symb].position.takeProfit * (1 + constLittle)) // цена больше тейка
                                {
                                    lifeCircle[symb].orderTake.orderIDmy = GetClientOrderId();
                                    //SendLimitOrder(symb, "SELL", lifeCircle[symb].position.takeProfit, lifeCircle[symb].volume, lifeCircle[symb].orderTake.orderIDmy);
                                    SendMarketOrder(symb, "SELL", lifeCircle[symb].volume, lifeCircle[symb].orderTake.orderIDmy);
                                    LogMessage(string.Format("success | ClosePosTakeProfit BUY | {0} | {1} | {2} | lastPrice {3}",
                                        symb, lifeCircle[symb].position.openOrder.price, lifeCircle[symb].volume, lifeCircle[symb].lastPrice));
                                }
                            }
                            else // if (lifeCircle[symb].direction == "Short")
                            {
                                if (lifeCircle[symb].position.takeProfit == 0)
                                    lifeCircle[symb].position.takeProfit = lifeCircle[symb].position.openOrder.price * (1 - lifeCircle[symb].settingsTake / 100);

                                if (lastPrice <= lifeCircle[symb].position.takeProfit * (1 - constLittle)) // цена меньше тейка
                                {
                                    lifeCircle[symb].orderTake.orderIDmy = GetClientOrderId();
                                    //SendLimitOrder(symb, "BUY", lifeCircle[symb].position.takeProfit, lifeCircle[symb].volume, lifeCircle[symb].orderTake.orderIDmy);
                                    SendMarketOrder(symb, "BUY", lifeCircle[symb].volume, lifeCircle[symb].orderTake.orderIDmy);
                                    LogMessage(string.Format("success | ClosePosTakeProfit SELL | {0} | {1} | {2} | lastPrice {3}",
                                        symb, lifeCircle[symb].position.openOrder.price, lifeCircle[symb].volume, lifeCircle[symb].lastPrice));
                                }
                            }
                        }
                        #endregion

                        #region проверка на закрытие по безубытку за указанное кол-во секунд
                        //if (string.IsNullOrEmpty(lifeCircle[symb].orderProfit0.orderIDmy)
                        //    && settings.secondsForClosingIn0 != 0
                        //    && lifeCircle[symb].position.openOrder.datetime.Year != 1
                        //    && lifeCircle[symb].position.openOrder.datetime.AddSeconds(settings.secondsForClosingIn0) < DateTime.Now)
                        //{
                        //    if (lifeCircle[symb].position.side == "BUY")
                        //    {
                        //        if (lifeCircle[symb].lastPrice > lifeCircle[symb].position.openOrder.price * (1 + (constLittle + constProfit0))) // вышли в безубыток
                        //        {
                        //            lifeCircle[symb].orderProfit0.orderIDmy = GetClientOrderId();
                        //            decimal priceP = lifeCircle[symb].position.openOrder.price * (1 + constProfit0);
                        //            SendLimitOrder(symb, "SELL", priceP, lifeCircle[symb].volume, lifeCircle[symb].orderProfit0.orderIDmy);
                        //            LogMessage(string.Format("success | ClosePosProfit0 BUY | {0} | {1} | {2} | lastPrice {3}",
                        //                symb, priceP, lifeCircle[symb].volume, lifeCircle[symb].lastPrice));
                        //        }
                        //    }
                        //    else
                        //    {
                        //        if (lifeCircle[symb].lastPrice < lifeCircle[symb].position.openOrder.price * (1 - (constLittle + constProfit0)))
                        //        {
                        //            lifeCircle[symb].orderProfit0.orderIDmy = GetClientOrderId();
                        //            decimal priceP = lifeCircle[symb].position.openOrder.price * (1 - constProfit0);
                        //            SendLimitOrder(symb, "BUY", priceP, lifeCircle[symb].volume, lifeCircle[symb].orderProfit0.orderIDmy);
                        //            LogMessage(string.Format("success | ClosePosProfit0 SELL | {0} | {1} | {2} | lastPrice {3}",
                        //                symb, priceP, lifeCircle[symb].volume, lifeCircle[symb].lastPrice));
                        //        }
                        //    }
                        //}
                        #endregion

                        #region доп.проверяем, достиг ли стоп-лосс своего значения
                        if (string.IsNullOrEmpty(lifeCircle[symb].orderStop.orderIDmy))
                        {
                            if (lifeCircle[symb].direction == "Long")
                            {
                                if (lifeCircle[symb].position.stopLoss == 0)
                                    lifeCircle[symb].position.stopLoss = lifeCircle[symb].position.openOrder.price * (1 - lifeCircle[symb].settingsStop / 100);

                                if (lastPrice <= lifeCircle[symb].position.stopLoss) //* (1 - (3 * constLittle))) // цена меньше стопа
                                {
                                    lifeCircle[symb].orderStop.orderIDmy = GetClientOrderId();
                                    //SendLimitOrder(symb, "SELL", lifeCircle[symb].position.stopLoss, lifeCircle[symb].volume, lifeCircle[symb].orderStop.orderIDmy);
                                    SendMarketOrder(symb, "SELL", lifeCircle[symb].volume, lifeCircle[symb].orderStop.orderIDmy);
                                    //SendStopMarketOrder(symb, "SELL", lifeCircle[symb].volume, lifeCircle[symb].position.stopLoss, lifeCircle[symb].orderStop.orderIDmy);
                                    LogMessage(string.Format("success | ClosePosStopLoss BUY | {0} | {1} | {2} | lastPrice {3}",
                                        symb, lifeCircle[symb].position.openOrder.price, lifeCircle[symb].volume, lifeCircle[symb].lastPrice));
                                }
                            }
                            else // if (lifeCircle[symb].position.side == "SELL")
                            {
                                if (lifeCircle[symb].position.stopLoss == 0)
                                    lifeCircle[symb].position.stopLoss = lifeCircle[symb].position.openOrder.price * (1 + lifeCircle[symb].settingsStop / 100);

                                if (lastPrice >= lifeCircle[symb].position.stopLoss) //* (1 + (3 * constLittle))) // цена больше стопа
                                {
                                    lifeCircle[symb].orderStop.orderIDmy = GetClientOrderId();
                                    //SendLimitOrder(symb, "BUY", lifeCircle[symb].position.stopLoss, lifeCircle[symb].volume, lifeCircle[symb].orderStop.orderIDmy);
                                    SendMarketOrder(symb, "BUY", lifeCircle[symb].volume, lifeCircle[symb].orderStop.orderIDmy);
                                    //SendStopMarketOrder(symb, "BUY", lifeCircle[symb].volume, lifeCircle[symb].position.stopLoss, lifeCircle[symb].orderStop.orderIDmy);
                                    LogMessage(string.Format("success | ClosePosStopLoss SELL | {0} | {1} | {2} | lastPrice {3}",
                                        symb, lifeCircle[symb].position.openOrder.price, lifeCircle[symb].volume, lifeCircle[symb].lastPrice));
                                }
                            }
                        }
                        #endregion
                    }
                }
            }
        }

        private decimal RoundVolume(decimal lTrade, string symb, int ii)
        {
            decimal val = 0;
            if (lTrade == 0)
                return val;

            if (listLifeCircle[ii].ContainsKey(symb))
            {
                val = Math.Round(listLifeCircle[ii][symb].settingsVolume / lTrade, listLifeCircle[ii][symb].security.precisVolume);

                if (val == 0 && listLifeCircle[ii][symb].isNuleVolume == false)
                {
                    LogMessage(string.Format("error | RoundVolume | {0} | {1}", symb, val));
                    listLifeCircle[ii][symb].isNuleVolume = true;
                }
            }
           
            return val;
        }

        private bool CreateTwoOrders(string symb, int ii)
        {
           
            if (btcDeltaIsOn == true)
                return false;

            if (countOfTransactions > 1050
                || countOfTransactions10 > 270)
                return false;

            if (listLifeCircle[ii][symb].lastTimeLifeCircleFinished.Year != 1 && DateTime.Now < listLifeCircle[ii][symb].lastTimeLifeCircleFinished) // проверка на то, что прошла 1 минута после последнего тейка или стопа
                return false;

            if (listLifeCircle[ii][symb].lastPrice == 0)
                return false;

            if (listLifeCircle[ii][symb].volume == 0)
                return false;

            // выставление заявки на покупку
            if (listLifeCircle[ii][symb].direction == "Long")
            {
                if (string.IsNullOrEmpty(listLifeCircle[ii][symb].orderFirst.orderIDmy))
                {
                    //bool isOpenBuy = true;
                    //if (settings.isUseDelta) // если дельта используется
                    //{
                    //    if (allSymbolsLastPrice.ContainsKey(symb)
                    //        && ii < settings.setSettings.Count)
                    //    {
                    //        isOpenBuy = allSymbolsLastPrice[symb].delta > settings.setSettings[ii].delta0 && allSymbolsLastPrice[symb].delta < settings.setSettings[ii].delta1;
                    //    }
                    //    else
                    //    {
                    //        isOpenBuy = false;
                    //    }
                    //}

                    //isOpenBuy = isOpenBuy && DateTime.Now > listLifeCircle[ii][symb].isCancelOrd.timeActivateSell; // проверка на то, чтобы была задержка 0.5 секунды межуд выставлениями ордеров 

                    decimal priceBuy = RoundPrice(listLifeCircle[ii][symb].lastPrice * (1 - listLifeCircle[ii][symb].settingsDistance / 100), symb);
                    listLifeCircle[ii][symb].distanceDown = priceBuy;
                    listLifeCircle[ii][symb].bufferDown = RoundPrice(listLifeCircle[ii][symb].lastPrice * (1 - listLifeCircle[ii][symb].settingsBuffer / 100), symb);
                    listLifeCircle[ii][symb].bufferUp = RoundPrice(listLifeCircle[ii][symb].lastPrice * (1 + listLifeCircle[ii][symb].settingsBuffer / 100), symb);
                    listLifeCircle[ii][symb].orderFirst.orderIDmy = GetClientOrderId();
                    listLifeCircle[ii][symb].orderFirst.price = priceBuy;
                    SendLimitOrder(symb, "BUY", priceBuy, listLifeCircle[ii][symb].volume, listLifeCircle[ii][symb].orderFirst.orderIDmy);
                    LogMessage(string.Format("success | CreateTwoOrders | SendLimitBuyOrder | {4} | {0} | LastPrice Buffer Distance | {1} {2} {3}",
                        listLifeCircle[ii][symb].orderFirst.orderIDmy, listLifeCircle[ii][symb].lastPrice, listLifeCircle[ii][symb].bufferDown, priceBuy, symb));
                    return true;
                }
            }

            if (listLifeCircle[ii][symb].direction == "Short")
            {
                // выставление заявки на продажу
                if (string.IsNullOrEmpty(listLifeCircle[ii][symb].orderFirst.orderIDmy))
                {
                    //bool isOpenSell = true;
                    //if (settings.isUseDelta)
                    //{
                    //    if (allSymbolsLastPrice.ContainsKey(symb)
                    //        && ii < settings.setSettings.Count)
                    //    {
                    //        isOpenSell = allSymbolsLastPrice[symb].delta > settings.setSettings[ii].delta0 && allSymbolsLastPrice[symb].delta < settings.setSettings[ii].delta1;
                    //    }
                    //    else
                    //    {
                    //        isOpenSell = false;
                    //    }
                    //}

                    //isOpenSell = isOpenSell && DateTime.Now > listLifeCircle[ii][symb].isCancelOrd.timeActivateBuy; // проверка на то, чтобы была задержка 0.5 секунды межуд выставлениями ордер

                    decimal priceSell = RoundPrice(listLifeCircle[ii][symb].lastPrice * (1 + listLifeCircle[ii][symb].settingsDistance / 100), symb);
                    listLifeCircle[ii][symb].distanceUp = priceSell;
                    listLifeCircle[ii][symb].bufferDown = RoundPrice(listLifeCircle[ii][symb].lastPrice * (1 - listLifeCircle[ii][symb].settingsBuffer / 100), symb);
                    listLifeCircle[ii][symb].bufferUp = RoundPrice(listLifeCircle[ii][symb].lastPrice * (1 + listLifeCircle[ii][symb].settingsBuffer / 100), symb);
                    listLifeCircle[ii][symb].orderFirst.orderIDmy = GetClientOrderId();
                    listLifeCircle[ii][symb].orderFirst.price = priceSell;
                    SendLimitOrder(symb, "SELL", priceSell, listLifeCircle[ii][symb].volume, listLifeCircle[ii][symb].orderFirst.orderIDmy);
                    LogMessage(string.Format("success | CreateTwoOrders | SendLimitSellOrder | {4} | {0} | LastPrice Buffer Distance | {1} {2} {3}",
                        listLifeCircle[ii][symb].orderFirst.orderIDmy, listLifeCircle[ii][symb].lastPrice, listLifeCircle[ii][symb].bufferUp, priceSell, symb));
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// рассчитывает уникальный clientOrderId для отправки на Binance
        /// </summary>
        /// <returns></returns>
        public string GetClientOrderId()
        {
            DateTime Jan1St2020 = new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            long clientId = 0;
            //if (side == "Long")
            //    clientId = (long)(DateTime.UtcNow - Jan1St2020).TotalMilliseconds + 3;
            //else
            //    clientId = (long)(DateTime.UtcNow - Jan1St2020).TotalMilliseconds - 3;
            clientId = (long)(DateTime.UtcNow - Jan1St2020).TotalMilliseconds;
            return clientId.ToString();
        }

        private object queryHttpLocker = new object();

        private string CreateQuery(Method method, string endPoint, Dictionary<string, string> param = null, bool auth = false)
        {
            string response = "";

            try
            {
                lock (queryHttpLocker)
                {
                    string fullUrl = "";

                    if (param != null)
                    {
                        fullUrl += "?";

                        foreach (var onePar in param)
                            fullUrl += onePar.Key + onePar.Value;
                    }

                    if (auth)
                    {
                        string message = "";

                        string timeStamp = GetNonce();

                        message += "timestamp=" + timeStamp;

                        if (fullUrl == "")
                        {
                            fullUrl = "?timestamp=" + timeStamp + "&signature=" + CreateSignature(message);
                        }
                        else
                        {
                            message = fullUrl + "&timestamp=" + timeStamp;
                            fullUrl += "&timestamp=" + timeStamp + "&signature=" + CreateSignature(message.Trim('?'));
                        }
                    }

                    var request = new RestRequest(endPoint + fullUrl, method);
                    request.AddHeader("X-MBX-APIKEY", settings.apiKey);

                    string bUrl = baseURL;

                    response = new RestClient(bUrl).Execute(request).Content;

                    if (response.Contains("code"))
                    {
                        var error = JsonConvert.DeserializeAnonymousType(response, new ErrorMessage());
                        return string.Format("error code {0} {1}", error.code, error.msg);
                        //throw new Exception(error.msg);
                    }

                    return response;
                }
            }
            catch (Exception ex)
            {
                LogMessage(string.Format("error | CreateQuery | {0} | {1} | {2} | {3}", method, endPoint, response, ex.ToString()));
                return null;
            }
        }

        private string GetNonce()
        {
            var resTime = CreateQuery(Method.GET, "/fapi/v1/time", null, false);
            var result = JsonConvert.DeserializeAnonymousType(resTime, new BinanceTime());
            return (result.serverTime).ToString();
        }

        private string CreateSignature(string message)
        {
            var messageBytes = Encoding.UTF8.GetBytes(message);
            var keyBytes = Encoding.UTF8.GetBytes(settings.secKey);
            var hash = new HMACSHA256(keyBytes);
            var computedHash = hash.ComputeHash(messageBytes);
            return BitConverter.ToString(computedHash).Replace("-", "").ToLower();
        }

        public bool LicenseNotOrFinished()
        {
            if (WorkSettings.PublicKey != "" && WorkSettings.PublicKey != settings.apiKey)
                return true;

            if (WorkSettings.LastTime < binanceServerTime)
                return true;

            return false;
        }

        private decimal RoundPrice(decimal value, string symb)
        {
            decimal val = 0;

            foreach (var lifeCircle in listLifeCircle)
            {
                if (lifeCircle.ContainsKey(symb))
                {
                    val = Math.Round(value, lifeCircle[symb].security.precisPrice);
                    break;
                }
            }

            return val;
        }

        public decimal ConvertStringToDecimal(string value)
        {
            //string val = value.ToString(CultureInfo.InvariantCulture).Replace(CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator, ",");

            if (value.Contains("E"))
                return Convert.ToDecimal(ConvertStringToDouble(value));

            try
            {
                return Convert.ToDecimal(value.Replace(",",
                        CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                    CultureInfo.InvariantCulture);
            }
            catch
            {
                return Convert.ToDecimal(ConvertStringToDouble(value));
            }
        }

        /// <summary>
        /// отправить лимитный ордера на биржу
        /// </summary>
        /// <param name="Symbol">название монеты, например "bnbusdt"</param>
        /// <param name="Side">покупка или продажа в формате "BUY"</param>
        /// <param name="Price">цена лимитки</param>
        /// <param name="Volume">объем</param>
        public void SendLimitOrder(string Symbol, string Side, decimal Price, decimal volume, string clientOrderId)
        {
            lock (lockOrder)
            {
                countOfTransactions++;
                countOfTransactions10++;
                try
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();

                    param.Add("symbol=", Symbol.ToUpper());
                    param.Add("&side=", Side);
                    param.Add("&type=", "LIMIT");
                    param.Add("&timeInForce=", "GTC");
                    param.Add("&newClientOrderId=", clientOrderId);
                    param.Add("&quantity=", ConvertDecimalToString(volume));
                    string pr = ConvertDecimalToString(Price);
                    string[] pp = pr.Split('.');
                    if (pp.Length == 2)
                    {
                        if (pp[1].Length > 5)
                        {
                            pr = ConvertAndCheckValue(pr, Symbol);
                        }
                    }
                    param.Add("&price=", pr); //ConvertAndCheckValue(Price, Symbol));

                    var res = CreateQuery(Method.POST, "/fapi/v1/order", param, true);

                    if (res != null && res.Contains("clientOrderId"))
                    {
                        LogMessage(string.Format("success | SendLimitOrder | {0} | {1}", Symbol, Side));
                    }    
                    else // результат выставления ордера пустая строка, т.е. ордер не выставился
                    {
                        checkMyId(Symbol, clientOrderId); // заявка не выставилась, обнуляем myID чтобы она выставилась заново
                        LogMessage(string.Format("error | SendLimitOrder | {0} | {1} | {2} | Price {3} | Volume {4}", 
                            Symbol, res, clientOrderId, Price, volume));
                    }
                }
                catch (Exception ex)
                {
                    checkMyId(Symbol, clientOrderId); // заявка не выставилась, обнуляем myID чтобы она выставилась заново
                    LogMessage(string.Format("error | SendLimitOrder | {0} | {1}", Symbol, ex.ToString()));
                }
            }
        }

        private string ConvertAndCheckValue(string val, string symb)
        {
            string retVal = val;
            
            string[] ss = retVal.Split('.');
            if (ss.Length < 2)
                return retVal;

            int i = searchFirstNumberInList(symb);
            if (i < 0)
                return retVal;

            if (ss[1].Length > listLifeCircle[i][symb].security.precisPrice)
                retVal = string.Format("{0}.{1}", ss[0], ss[1].Substring(0, listLifeCircle[i][symb].security.precisPrice));

            return retVal;
        }

        private void checkMyId(string symb, string myID)
        {
            foreach (var dict in listLifeCircle)
            {
                if (dict.ContainsKey(symb))
                {
                    if (myID == dict[symb].orderFirst.orderIDmy) // не выставился ордера на покупку
                    {
                        dict[symb].lastTimeLifeCircleFinished = DateTime.Now.AddSeconds(90);
                        dict[symb].orderFirst.orderIDmy = "";
                        if (!string.IsNullOrEmpty(dict[symb].orderFirst.orderIDBinance))
                        {
                            CancelOrder(symb, dict[symb].orderFirst.orderIDBinance);
                        }
                    }
                    else if (myID == dict[symb].orderStop.orderIDmy)
                    {
                        //dict[symb].orderStop.orderIDmy = "";
                    }
                    else if (myID == dict[symb].orderTake.orderIDmy)
                    {
                        dict[symb].orderTake.orderIDmy = "";
                    }
                }
            }
        }

        public bool IsOn = false;

        public void SendMarketOrder(string Symbol, string Side, decimal volume, string clientOrderId)
        {
            lock (lockOrder)
            {
                countOfTransactions++;
                countOfTransactions10++;
                try
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();

                    param.Add("symbol=", Symbol.ToUpper());
                    param.Add("&side=", Side);
                    param.Add("&type=", "MARKET");
                    param.Add("&newClientOrderId=", clientOrderId);
                    param.Add("&quantity=", ConvertDecimalToString(volume));

                    var res = CreateQuery(Method.POST, "/fapi/v1/order", param, true);

                    if (res != null && res.Contains("clientOrderId"))
                        LogMessage(string.Format("success | SendMarketOrder | {0}", Symbol));
                    else
                        LogMessage(string.Format("error | SendMarketOrder | {0}", Symbol));
                }
                catch (Exception ex)
                {
                    LogMessage(string.Format("error | SendMarketOrder | {0} | {1}", Symbol, ex.ToString()));
                }
            }
        }

        public void SendStopMarketOrder(string Symbol, string Side, decimal volume, decimal stopPrice, string clientOrderId)
        {
            lock (lockOrder)
            {
                countOfTransactions++;
                countOfTransactions10++;
                try
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param.Add("symbol=", Symbol.ToUpper());
                    param.Add("&side=", Side);
                    param.Add("&type=", "STOP_MARKET");
                    param.Add("&newClientOrderId=", clientOrderId);
                    param.Add("&stopPrice=", ConvertDecimalToString(stopPrice));
                    param.Add("&quantity=", ConvertDecimalToString(volume));

                    var res = CreateQuery(Method.POST, "/fapi/v1/order", param, true);

                    if (res != null && res.Contains("clientOrderId"))
                        LogMessage(string.Format("success | SendStopMarketOrder | {0}", Symbol));
                    else if (res.Contains("error code"))
                    {
                        //if (res.Contains("2021"))
                        //    SendMarketOrder(Symbol, Side, volume, clientOrderId);
                    }
                    else
                    {
                        if (allSymbolsLastPrice.ContainsKey(Symbol))
                            LogMessage(string.Format("error | SendStopMarketOrder | {0} | {1} | Last Price {2} | {3}", Symbol, Side, allSymbolsLastPrice[Symbol], stopPrice));
                    }
                }
                catch (Exception ex)
                {
                    LogMessage(string.Format("error | SendStopMarketOrder | {0} | {1}", Symbol, ex.ToString()));
                }
            }
        }

        /// <summary>
        /// отмена ордера по символу и по ордер id
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="orderID"></param>
        private void CancelOrder(string symbol, string orderID)
        {
            lock (lockOrder)
            {
                try
                {
                    if (string.IsNullOrEmpty(orderID))
                        return;

                    if (checkLastOrderID(symbol, orderID) == true)
                        return;

                    countOfTransactions++;
                    countOfTransactions10++;
                    //// проверка на то, есть ли такой ордер в открытых ордерах
                    //List<string> openOrdersBinance = GetOpenOrdersFromBinance(symbol);
                    //if (openOrdersBinance == null) // произошла ошибка и мы не смогли получить данные по ордерам
                    //{

                    //}   
                    //else
                    //{
                    //    if (openOrdersBinance.Count > 0)
                    //    {
                    //        List<string> felem = openOrdersBinance.FindAll(el => el == orderID);
                    //        if (felem.Count == 0)
                    //            return;
                    //    }
                    //    else
                    //    {
                    //        return;
                    //    }
                    //}

                    Dictionary<string, string> param = new Dictionary<string, string>();

                    param.Add("symbol=", symbol.ToUpper());
                    param.Add("&orderId=", orderID);

                    string res = CreateQuery(Method.DELETE, "/fapi/v1/order", param, true);

                    if (res == null)
                    {
                        stopThisSymbol(symbol);
                        LogMessage(string.Format("error | CancelOrder | {0} | {1}", symbol, orderID));
                    }
                    else
                    {
                        LogMessage(string.Format("success | CancelOrder | {0} | {1}", symbol, orderID));
                    }

                }
                catch (Exception ex)
                {
                    stopThisSymbol(symbol);
                    LogMessage(string.Format("error | CancelOrder | {0} | {1}", symbol, ex.ToString()));
                }
            }
        }

        private bool checkLastOrderID(string symb, string orderId)
        {
            bool retVal = false;
            
            foreach (var lifeCircle in listLifeCircle)
            {
                if (lifeCircle.ContainsKey(symb))
                {
                    if (lifeCircle[symb].lastCanceledId == orderId)
                        retVal = true;

                    lifeCircle[symb].lastCanceledId = orderId;
                }
            }
            
            return retVal;
        }

        private void stopThisSymbol(string symb)
        {
            for (int i = 0; i < listLifeCircle.Count; i++)
            {
                if (listLifeCircle[i].ContainsKey(symb))
                {
                    DeleteAllElementsForNewCircle(symb, i, 600); // останавливаем торговлю на 15 минут
                }
            }
        }

        public List<string> GetOpenOrdersFromBinance(string symb)
        {
            List<string> listOpenOrders = new List<string>();
            try
            {
                string endPoint = "/fapi/v1/openOrders";

                var param = new Dictionary<string, string>();
                param.Add("symbol=", symb.ToUpper());

                var res = CreateQuery(Method.GET, endPoint, param, true);

                if (res == null)
                {
                    LogMessage(string.Format("error | GetOpenOrdersFromBinance | {0}", symb));
                    return null;
                }
                else
                {
                    OpenOrdersResponce[] openOrders = JsonConvert.DeserializeObject<OpenOrdersResponce[]>(res);
                    foreach (var oo in openOrders)
                        listOpenOrders.Add(oo.orderId);
                }
            }
            catch (Exception ex)
            {
                LogMessage(string.Format("error | GetOpenOrdersFromBinance | {0} | {1}", symb, ex.ToString()));
                return null;
            }

            return listOpenOrders;
        }

        public string ConvertDecimalToString(decimal value)
        {
            string val = value.ToString(CultureInfo.InvariantCulture).Replace(CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator, ".");
            if (val.Contains(","))
                return val.Replace(',', '.');
            else
                return val;
        }

        public double ConvertStringToDouble(string value)
        {
            return Convert.ToDouble(value.Replace(",",
                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                CultureInfo.InvariantCulture);
        }

        public List<Position> allPositions = new List<Position>();

        // сохраняем файл со сделками
        //private void SaveTrades()
        //{
        //    string fileName = @"log\positions.json";
        //    using (var tw = new StreamWriter(fileName, false))
        //    {
        //        tw.WriteLine()
        //    }
        //}

        public void SaveSettings()
        {
            //
            string jsonSettings = JsonConvert.SerializeObject(settings, Formatting.Indented);

            string fileName = @"log\settings.json";
            using (var tw = new StreamWriter(fileName, false))
            {
                tw.WriteLine(jsonSettings);
                tw.Close();
            }
        }

        public bool LoadSettings()
        {
            string fileName = @"log\settings.json";

            if (!File.Exists(fileName))
                return false;

            try
            {
                using (StreamReader sr = new StreamReader(fileName))
                {
                    string jsonSettings = sr.ReadToEnd();
                    settings = JsonConvert.DeserializeObject<Settings>(jsonSettings);
                    sr.Close();
                }

                return true;
            }
            catch (Exception ex)
            {

            }

            return false;
        }

        #region получение свечей
        /// <summary>
        /// получение свечей через get-запрос
        /// </summary>
        /// <param name="symb">символ</param>
        /// <param name="timeframe">таймфрейм в формате 1m, 5m, 15m, 1h</param>
        public Dictionary<DateTime, Candles> GetCandles(string symb, string timeframe)
        {
            Dictionary<DateTime, Candles> retCandles = new Dictionary<DateTime, Candles>();

            try
            {
                string endPoint = "/fapi/v1/klines";

                DateTime yearBegin = new DateTime(1970, 1, 1);
                DateTime curTime = DateTime.Now;

                TimeSpan time1hour = curTime.AddHours(-1) - yearBegin;
                double rStart = time1hour.TotalMilliseconds;
                string startTime = Convert.ToInt64(rStart).ToString();

                TimeSpan currTime = curTime - yearBegin;
                double rEnd = currTime.TotalMilliseconds;
                string endTime = Convert.ToInt64(rEnd).ToString();

                Dictionary<string, string> param = new Dictionary<string, string>();
                param.Add("symbol=", symb.ToUpper());
                param.Add("&interval=", timeframe);
                param.Add("&limit=", "60");
                //param.Add("&startTime=", startTime);
                //param.Add("&endTime=", endTime);

                string res = CreateQuery(Method.GET, endPoint, param, false);

                if (res == null)
                {

                }
                else
                {
                    retCandles = desezializyCandles(res);
                }
            }
            catch (Exception ex)
            {

            }

            return retCandles;
        }

        private Dictionary<DateTime, Candles> desezializyCandles(string json)
        {
            Dictionary<DateTime, Candles> rv = new Dictionary<DateTime, Candles>();
            
            try
            {
                string res = json.Trim(new char[] { '[', ']' });
                var res2 = res.Split(new char[] { ']' });

                Candles newCandle;

                for (int i = 0; i < res2.Length; i++)
                {
                    if (i != 0)
                    {
                        string upd = res2[i].Substring(2);
                        var param = upd.Split(new char[] { ',' });

                        newCandle = new Candles();
                        newCandle.timeStart = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(param[0]));
                        newCandle.Low = Convert.ToDecimal(param[3].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator).Trim(new char[] { '"', '"' }), CultureInfo.InvariantCulture);
                        newCandle.High = Convert.ToDecimal(param[2].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator).Trim(new char[] { '"', '"' }), CultureInfo.InvariantCulture);
                        newCandle.Open = Convert.ToDecimal(param[1].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator).Trim(new char[] { '"', '"' }), CultureInfo.InvariantCulture);
                        newCandle.Close = Convert.ToDecimal(param[4].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator).Trim(new char[] { '"', '"' }), CultureInfo.InvariantCulture);
                        newCandle.Volume = Convert.ToDecimal(param[5].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator).Trim(new char[] { '"', '"' }), CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        var param = res2[i].Split(new char[] { ',' });

                        newCandle = new Candles();
                        newCandle.timeStart = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(param[0]));
                        newCandle.Low = Convert.ToDecimal(param[3].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator).Trim(new char[] { '"', '"' }), CultureInfo.InvariantCulture);
                        newCandle.High = Convert.ToDecimal(param[2].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator).Trim(new char[] { '"', '"' }), CultureInfo.InvariantCulture);
                        newCandle.Open = Convert.ToDecimal(param[1].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator).Trim(new char[] { '"', '"' }), CultureInfo.InvariantCulture);
                        newCandle.Close = Convert.ToDecimal(param[4].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator).Trim(new char[] { '"', '"' }), CultureInfo.InvariantCulture);
                        newCandle.Volume = Convert.ToDecimal(param[5].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator).Trim(new char[] { '"', '"' }), CultureInfo.InvariantCulture);
                    }

                    if (rv.ContainsKey(newCandle.timeStart))
                        rv[newCandle.timeStart] = newCandle;
                    else
                        rv.Add(newCandle.timeStart, newCandle);
                }

            }
            catch (Exception ex)
            {

            }

            return rv;
        }

        private ConcurrentQueue<string> newMassageCandle = new ConcurrentQueue<string>();

        private void GetResCandles(object sender, MessageReceivedEventArgs e)
        {
            newMassageCandle.Enqueue(e.Message);
        }

        //private void convertCandles()
        //{
        //    while (true)
        //    {
        //        lock (_lock)
        //        {
        //            try
        //            {
        //                if (!newMassageCandle.IsEmpty)
        //                {
        //                    string mes;
        //                    if (newMassageCandle.TryDequeue(out mes))
        //                    {
        //                        if (mes.Contains("error"))
        //                        {
        //                            LogMessage(mes);
        //                        }
        //                        else if (mes.Contains("\"e\":\"kline\""))
        //                        {
        //                            if (mes.Contains("\"x\":true")) // показатель того, что свеча завершенная
        //                            {
        //                                CandlesStream candl = JsonConvert.DeserializeAnonymousType(mes, new CandlesStream());
        //                                if (candl.data.k.x == false) // проверка на то, что свеча законченная
        //                                    return;

        //                                Candles newCand = new Candles();
        //                                newCand.timeStart = new DateTime(1970, 1, 1).AddMilliseconds(Convert.ToDouble(candl.data.k.t));
        //                                newCand.Open = candl.data.k.o;
        //                                newCand.High = candl.data.k.h;
        //                                newCand.Low = candl.data.k.l;
        //                                newCand.Close = candl.data.k.c;
        //                                newCand.Volume = candl.data.k.v;

        //                                if (allSymbolsLastPrice.ContainsKey(candl.data.s))
        //                                {
        //                                    if (allSymbolsLastPrice[candl.data.s].candles.ContainsKey(newCand.timeStart))
        //                                    {
        //                                        allSymbolsLastPrice[candl.data.s].candles[newCand.timeStart] = newCand;
        //                                    }
        //                                    else
        //                                    {
        //                                        if (allSymbolsLastPrice[candl.data.s].candles.Count > 59)
        //                                        {
        //                                            Dictionary<DateTime, Candles> ca = new Dictionary<DateTime, Candles>();
        //                                            int i = -1;
        //                                            foreach (var el in allSymbolsLastPrice[candl.data.s].candles.Keys)
        //                                            {
        //                                                i++;
        //                                                if (i < (allSymbolsLastPrice[candl.data.s].candles.Count - 59))
        //                                                    continue;

        //                                                ca.Add(el, allSymbolsLastPrice[candl.data.s].candles[el]);
        //                                            }
        //                                            ca.Add(newCand.timeStart, newCand);
        //                                            allSymbolsLastPrice[candl.data.s].candles.Clear();
        //                                            allSymbolsLastPrice[candl.data.s].candles = ca;
        //                                        }
        //                                        else
        //                                        {
        //                                            allSymbolsLastPrice[candl.data.s].candles.Add(newCand.timeStart, newCand);
        //                                        }
        //                                    }
        //                                    fillUpdateDelta(candl.data.s);
        //                                }

        //                            }

        //                        }
        //                    }
        //                }
        //            }
        //            catch (Exception ex)
        //            {
        //                LogMessage(string.Format("error | convertCandles | {0}", ex.ToString()));
        //            }
        //        }

        //        Thread.Sleep(3);
        //    }
        //}

        //private void fillUpdateDelta(string symb)
        //{
        //    if (!allSymbolsLastPrice.ContainsKey(symb))
        //        return;
            
        //    if (allSymbolsLastPrice[symb].candles.Count != 0)
        //    {
        //        allSymbolsLastPrice[symb].maxValue = allSymbolsLastPrice[symb].candles.Values.Max(t => t.High);
        //        allSymbolsLastPrice[symb].minValue = allSymbolsLastPrice[symb].candles.Values.Min(t => t.Low);
        //        if (allSymbolsLastPrice[symb].minValue != 0)
        //            allSymbolsLastPrice[symb].delta = Math.Round(((allSymbolsLastPrice[symb].maxValue - allSymbolsLastPrice[symb].minValue) 
        //                                                / allSymbolsLastPrice[symb].minValue) * 100, 4);
        //    }
        //}
        #endregion

        public void LogMessage(string message)
        {
            if (settings.LogIsOn)
            {
                if (message.Contains("error"))
                    fileErrors.WriteLine(message);
                else
                    fileLogs.WriteLine(message);
            }
        }
    }
}
