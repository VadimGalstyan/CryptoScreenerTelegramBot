using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace ScreenerTest.Models
{
    public class Alert
    {
        public string Exchange { get; set; }
        public string Pair { get; set; }
        public decimal ChangePercent { get; set; }
        public DateTime Time { get; set; }
        public int Timeframe { get; set; }
    }
}
