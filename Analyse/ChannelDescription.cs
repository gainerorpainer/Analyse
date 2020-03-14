using System.Collections.Generic;

namespace Analyse
{
    class ParsedFrame
    {
        public List<int> Bits;
        public bool HasErrors;
    }

    class ChannelDescription
    {
        //public List<List<DataPoint>> Frames = new List<List<DataPoint>>();

        public List<ParsedFrame> ParsedFrames = new List<ParsedFrame>();

        internal int BitRate;
        public double BitTime => 1e6 / BitRate;
    }
}
