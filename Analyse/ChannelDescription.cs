using System.Collections.Generic;

namespace Analyse
{

    class ChannelDescription
    {
        public int longestOnesSequence = int.MinValue;
        public int longestZerosSequence = int.MinValue;
        public int shortestZerosSequence = int.MaxValue;
        public int shortestOnesSequence = int.MaxValue;

        public List<List<int>> Frames = new List<List<int>>();

        public int OversamplingRate;
        internal int BitRate;
    }
}
