using System.Collections.Generic;

namespace Analyse
{

    class ChannelDescription
    {
        public List<List<int>> Frames = new List<List<int>>();

        public int OversamplingRate;
        internal int BitRate;
    }
}
