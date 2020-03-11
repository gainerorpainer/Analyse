using System.Collections.Generic;

namespace Analyse
{
    class AnalyseStateFlow
    {
        public bool insideFrame = false;
        public int lastBit = -1;
        public int onesCounter;
        internal List<int> currentFrame = new List<int>();
        internal int sequenceCounter = 0;
    }
}
