using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScreener.Screeners
{
    public class VolumeScreener
    {
        public short _percent;
        public VolumeScreener(short percent)
        {
            _percent = percent;
        }
        public void SetPercent(short percent)
        {
            _percent = percent;
            //_alertsHistory.Clear();
        }
    }
}
