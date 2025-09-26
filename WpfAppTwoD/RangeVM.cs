using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfAppTwoD
{
    public class RangeVM
    {
        public string axis { get; set; }
        public int rounding { get; set; }
        public int roundingDigits { get; set; }
        public string min { get; set; }
        public string max { get; set; }
        public double step { get; set; }
        public string expression { get; set; }
        public int expressionDigits { get; set; }
    }
}
