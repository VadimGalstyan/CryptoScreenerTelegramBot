using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScreener.Data
{
    public class PriceBuffer
    {
        private float[] _buffer;
        private short _index = 0;
        private short _size { get; } = 3600; // one price in every second
        private bool _isFull = false;

        public PriceBuffer(short size = 3600)
        {
            _buffer = new float[size];
        }

        public void AddPrice(float price)
        {
            _buffer[_index] = price;
            _index++;

            if (_index >= _size)
            {
                _index = 0;
                _isFull = true;
            }
        }

        public float GetCurrentPrice() //Getting the newest price,it is for arbitrage screener
        {
            int currentIndex = _index == 0 ? _size - 1 : _index - 1;
            return _buffer[currentIndex];
        }

        public float GetPrice(short minutes)
        {
            int seconds = minutes * 60;
            if (seconds > _size) return 0;
            if(seconds < 0) return 0;
            if(seconds > _index && !_isFull) return 0;

            short tempIndex = (short)((_index - 1 - seconds + _size) % _size);
            return _buffer[tempIndex];
        }
    }
}
