using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Analyse
{

    public partial class Form1 : Form
    {
        private const int LONGESTONESSEQUENCE = 250;
        // Must be odd!
        private const int MEDIANFILTERWINDOW = 30;
        private const int OVERSAMPLINGRATE = 27;

        internal DataPoint[,] ProcessedData { get; set; }
        internal ChannelDescription[] Channels { get; set; }

        public Form1()
        {
            InitializeComponent();

            if (openFileDialog1.ShowDialog() != DialogResult.OK)
            {
                Close();
                return;
            }

            // Read into model
            ProcessedData = ReadRawData();

            // Preprocess each channel with a median filter
            MedianFilter();

            // Store a new file
            //WriteFile();


            // Create stats
            var rawFrames = ExtractFrames();

            dataGridView1.Rows.Clear();
            for (int i = 0; i < Channels.Length; i++)
            {
                var channel = Channels[i];

                // Calc how wide the shortest sequence is
                //channel.OversamplingRate = Math.Min(channel.shortestOnesSequence, channel.shortestZerosSequence);

                // Take the amount of samples which should equal 1s -> bitrate
                channel.BitRate = 115200;
                int[] commonbitrate = new int[] { 50, 110, 150, 300, 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200, 230400, 460800 };
                channel.BitRate = commonbitrate.OrderBy(x => Math.Abs(x - channel.BitRate)).First();

                dataGridView1.Rows.Add(
                    i, // Channel number
                    channel.BitRate,
                    channel.BitTime
                    );
            }

            /* POSTPROCESS OVERSAMPLING */
            ExtractFrameData(rawFrames);

            // ONLY 2!
            ListView[] lists = new ListView[] { listView1, listView2 };
            for (int channel = 0; channel < lists.Length; channel++)
            {
                var listView = lists[channel];

                listView.Items.Clear();

                for (int j = 0; j < Channels[channel].ParsedFrames.Count; j++)
                {
                    var frame = Channels[channel].ParsedFrames[j];
                    listView.Items.Add(new ListViewItem(new string[]
                                        {
                        j.ToString(), // Frame number
                        frame.HasErrors ? "ERROR" : "OK",
                        frame.Bits.Count.ToString(), // frame length
                        string.Join(" ", frame.Bits.Select(x=> x.ToString("X2"))), // Hex
                        string.Join(" ", frame.Bits.Select(x=> x.ToString()))
                                        }));
                }
            }


            var builder = new StringBuilder();
            foreach (ListViewItem item in listView2.Items)
                builder.AppendLine(item.SubItems[3].Text);

            Clipboard.SetText(builder.ToString());
        }

        private void WriteFile()
        {
            using (var s = new System.IO.StreamWriter(System.IO.Path.GetDirectoryName(openFileDialog1.FileName) + "\\medianfiltered.txt"))
            {
                for (int row = 0; row < ProcessedData.GetLength(1); row++)
                {
                    s.Write(ProcessedData[0, row].MicroSeconds);
                    s.Write(' ');
                    for (int col = 0; col < ProcessedData.GetLength(0); col++)
                    {
                        s.Write(ProcessedData[col, row].Value);

                        if (col != (ProcessedData.GetLength(0) - 1))
                            s.Write(' ');
                    }


                    s.Write('\n');
                }
            }
        }

        private void ExtractFrameData(List<List<DataPoint>>[] rawFrames)
        {
            for (int channel = 0; channel < Channels.Length; channel++)
            {
                var currentChannel = Channels[channel];

                for (int frame = 0; frame < rawFrames[channel].Count; frame++)
                {
                    var currentFrame = rawFrames[channel][frame];

                    List<int> binaryString = new List<int>();

                    int lastbit = currentFrame[0].Value;
                    double timerEnd = currentFrame[0].MicroSeconds + currentChannel.BitTime / 2.0;
                    // Idea: have a clock that runs at double the speed that toggles state
                    for (int bit = 0; bit < currentFrame.Count; bit++)
                    {
                        // Sync on each bit flip
                        if ((currentFrame[bit].Value != lastbit))
                        {
                            // Reset clock the hard way (to next half bit time)
                            timerEnd = currentFrame[bit].MicroSeconds + currentChannel.BitTime / 2.0;

                            lastbit = currentFrame[bit].Value;

                            // No need to check
                            continue;
                        }

                        if (currentFrame[bit].MicroSeconds >= timerEnd)
                        {
                            // Sample here
                            binaryString.Add(currentFrame[bit].Value);

                            // And restart clock
                            timerEnd += currentChannel.BitTime;
                        }

                    }

                    // parse binary string
                    // skip first bit (is always 0 SOF)
                    List<int> parsedFrame = new List<int>();
                    bool error = false;
                    for (int bitCounter = 0; bitCounter < (binaryString.Count - 11); bitCounter += 11)
                    {
                        // Check start bit
                        if (binaryString[bitCounter] != 0)
                        {
                            error = true;
                            break;
                        }

                        int b = binaryString[bitCounter + 1] + 2 * binaryString[bitCounter + 2] + 4 * binaryString[bitCounter + 3] + 8 * binaryString[bitCounter + 4]
                            + 16 * binaryString[bitCounter + 5] + 32 * binaryString[bitCounter + 6] + 64 * binaryString[bitCounter + 7] + 128 * binaryString[bitCounter + 8];

                        // Check stop bits
                        if ((binaryString[bitCounter + 9] != 1) || (binaryString[bitCounter + 10] != 1))
                        {
                            error = true;
                            break;
                        }

                        parsedFrame.Add(b);
                    }

                    currentChannel.ParsedFrames.Add(new ParsedFrame()
                    {
                        Bits = parsedFrame,
                        HasErrors = error
                    });
                }
            }
        }

        private List<List<DataPoint>>[] ExtractFrames()
        {
            List<List<DataPoint>>[] result = new List<List<DataPoint>>[ProcessedData.GetLength(0)];

            Channels = new ChannelDescription[ProcessedData.GetLength(0)];
            AnalyseStateFlow[] flows = new AnalyseStateFlow[ProcessedData.GetLength(0)];
            for (int i = 0; i < ProcessedData.GetLength(0); i++)
            {
                result[i] = new List<List<DataPoint>>();
                Channels[i] = new ChannelDescription();
                flows[i] = new AnalyseStateFlow();
            }

            for (int col = 0; col < ProcessedData.GetLength(0); col++)
            {
                for (int row = 0; row < ProcessedData.GetLength(1); row++)
                {
                    // Check if is in frame
                    ChannelDescription currentDescription = Channels[col];
                    AnalyseStateFlow currentFlow = flows[col];

                    if (!currentFlow.insideFrame)
                    {
                        if (ProcessedData[col, row].Value == 1)
                            continue;

                        currentFlow.insideFrame = true;
                        currentFlow.currentFrame = new List<DataPoint>();
                    }

                    // Check if still in frame
                    if (ProcessedData[col, row].Value == 1)
                        currentFlow.onesCounter++;
                    else
                        currentFlow.onesCounter = 0;

                    if (currentFlow.onesCounter >= 25 * 8)
                    {
                        currentFlow.insideFrame = false;
                        result[col].Add(currentFlow.currentFrame);
                        continue;
                    }

                    currentFlow.currentFrame.Add(ProcessedData[col, row]);

                    if (ProcessedData[col, row].Value != currentFlow.lastBit)
                    {
                        if (currentFlow.sequenceCounter > 0)
                            currentFlow.SequenceLengthList.Add(currentFlow.sequenceCounter);

                        currentFlow.lastBit = ProcessedData[col, row].Value;
                        currentFlow.sequenceCounter = 0;
                    }

                    currentFlow.sequenceCounter++;
                }
            }

            return result;
        }

        private void MedianFilter()
        {
            // ProcessedData = new DataPoint[ProcessedData.GetLength(0), ProcessedData.GetLength(1) - MEDIANFILTERWINDOW];
            Parallel.For(0, ProcessedData.GetLength(0), col =>
            {
                for (int row = MEDIANFILTERWINDOW; row < ProcessedData.GetLength(1); row++)
                {
                    // Backtrack and sum
                    int sum = 0;
                    for (int i = 0; i < MEDIANFILTERWINDOW; i++)
                        sum += ProcessedData[col, row - i].Value;

                    ProcessedData[col, row - MEDIANFILTERWINDOW].Value = (sum > (MEDIANFILTERWINDOW / 2) ? 1 : 0);
                }
            });
            //for (int col = 0; col < ProcessedData.GetLength(0); col++)
            //{
            //    for (int row = MEDIANFILTERWINDOW; row < ProcessedData.GetLength(1); row++)
            //    {
            //        // Backtrack and sum
            //        int sum = 0;
            //        for (int i = 0; i < MEDIANFILTERWINDOW; i++)
            //            sum += ProcessedData[col, row - i].Value;

            //        ProcessedData[col, row - MEDIANFILTERWINDOW].Value = (sum > (MEDIANFILTERWINDOW / 2) ? 1 : 0);
            //    }
            //}
        }

        private DataPoint[,] ReadRawData()
        {
            var lines = System.IO.File.ReadAllLines(openFileDialog1.FileName);
            var rawData = new DataPoint[lines.First().Split(' ').Length - 1, lines.Length];
            Parallel.For(0, lines.Length, row =>
           {
               var pieces = lines[row].Split(' ');
               for (int col = 0; col < rawData.GetLength(0); col++)
               {
                   rawData[col, row] = new DataPoint()
                   {
                       MicroSeconds = int.Parse(pieces[0]),
                       Value = int.Parse(pieces[col + 1])
                   };
               }
           });
            //for (int row = 0; row < lines.Length; row++)
            //{
            //    var pieces = lines[row].Split(' ');
            //    for (int col = 0; col < rawData.GetLength(0); col++)
            //    {
            //        rawData[col, row] = new DataPoint()
            //        {
            //            MicroSeconds = int.Parse(pieces[0]),
            //            Value = int.Parse(pieces[col + 1])
            //        };
            //    }
            //}

            return rawData;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Properties.Settings.Default.Reload();

            Close();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Properties.Settings.Default.Save();
        }
    }
}
