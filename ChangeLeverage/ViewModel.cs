using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Newtonsoft.Json;
using RestSharp;

namespace ChangeLeverage
{
    class BinanceFutures
    {
        private string baseURL = "https://fapi.binance.com";

        public string apiKey = "";
        public string secKey = "";

        public BinanceFutures()
        {

        }

        object _lock = new object();

        ConcurrentDictionary<string, Security> symbols = new ConcurrentDictionary<string, Security>();

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
                    //LogMessage(ex.ToString());
                }
            }
        }

        object queryHttpLocker = new object();

        private string CreateQuery(Method method, string endPoint, Dictionary<string, string> param = null, bool auth = false)
        {
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
                    request.AddHeader("X-MBX-APIKEY", apiKey);

                    string bUrl = baseURL;

                    var response = new RestClient(bUrl).Execute(request).Content;

                    if (response.Contains("code"))
                    {
                        var error = JsonConvert.DeserializeAnonymousType(response, new ErrorMessage());
                        if (error.code == -2021)
                            //LogMessage(string.Format("error | CreateQuery | Error code | {0} {1}", error.code, error.msg));
                        return string.Format("error code {0} {1}", error.code, error.msg);
                        //throw new Exception(error.msg);
                    }

                    return response;
                }
            }
            catch (Exception ex)
            {
                //if (ex.ToString().Contains("This listenKey does not exist"))
                //{

                //}

                //LogMessage(string.Format("error | CreateQuery | {0} | {1} | {2}", method, endPoint, ex.ToString()));
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
            var keyBytes = Encoding.UTF8.GetBytes(secKey);
            var hash = new HMACSHA256(keyBytes);
            var computedHash = hash.ComputeHash(messageBytes);
            return BitConverter.ToString(computedHash).Replace("-", "").ToLower();
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
    }
    class ViewModel : INotifyPropertyChanged
    {
        public ViewModel()
        {

        }

        public ObservableCollection<SymbolsLeverage> symbLeverage { get; } = new ObservableCollection<SymbolsLeverage>();

        public event PropertyChangedEventHandler PropertyChanged;
    }

    class SymbolsLeverage
    {
        public string symbol { get; set; }
        public MarginMode marMode { get; set; }
        public int Leverage { get; set; }
    }

    enum MarginMode
    {
        Cross,
        Isolate
    }
}
