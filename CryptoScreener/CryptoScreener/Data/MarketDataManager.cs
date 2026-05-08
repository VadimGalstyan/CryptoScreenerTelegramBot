using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;


namespace CryptoScreener.Data
{
    public class MarketDataManager
    {
        private static MarketDataManager _manager = new MarketDataManager();
        private static readonly object _lock = new object();
        private readonly object _dictLock = new object();

        public ConcurrentDictionary<string, Dictionary<string, PriceBuffer>> _exchanges { get; }

        private MarketDataManager()
        {
            _exchanges = new ConcurrentDictionary<string, Dictionary<string, PriceBuffer>>();
        }

        public void UpdatePrice(string exchange, string symbol, float price)
        {
            lock (_dictLock)
            {
                if (!_exchanges.ContainsKey(exchange))
                {
                    _exchanges[exchange] = new Dictionary<string, PriceBuffer>();
                }

                if (!_exchanges[exchange].ContainsKey(symbol))
                {
                    _exchanges[exchange][symbol] = new PriceBuffer();
                }
            }

            _exchanges[exchange][symbol].AddPrice(price);
        }
        public static MarketDataManager Manager
        {
            get
            {
                lock (_lock)
                {
                    if (_manager == null)
                        _manager = new MarketDataManager();
                    return _manager;
                }
            }
        }
    }
}
