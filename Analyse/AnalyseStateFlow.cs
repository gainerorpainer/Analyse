using System.Collections.Generic;

namespace Analyse
{
    class AnalyseStateFlow
    {
        public bool insideFrame = false;
        public int lastBit = -1;
        public int onesCounter;
        public int onesSequenceCounter = 0;
        public int zerosSequenceCounter;
        internal List<int> currentFrame;
    }
}
