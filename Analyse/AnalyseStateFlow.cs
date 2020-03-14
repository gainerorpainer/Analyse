using System.Collections.Generic;

namespace Analyse
{
    class AnalyseStateFlow
    {
        public bool insideFrame = false;
        public int lastBit = -1;
        public int onesCounter;
        internal List<DataPoint> currentFrame = new List<DataPoint>();
        internal int sequenceCounter = 0;
        public List<int> SequenceLengthList = new List<int>();
    }
}
