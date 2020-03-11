using System;
using System.Collections.Generic;
using System.Linq;

namespace Analyse
{
    class Avg
    {
        readonly List<int> data = new List<int>();
        public void Add(int i)
        {
            data.Add(i);
        }

        public int Value => (int)Math.Round(data.Average(), 0);
    }
}
