using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
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

        public List<int>[] ProcessedData { get; set; }
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
            int[,] rawData = ReadRawData();

            // Preprocess each channel with a median filter
            MedianFilter(rawData);

            // Store a new file
            using (var s = new System.IO.StreamWriter(System.IO.Path.GetDirectoryName(openFileDialog1.FileName) + "\\medianfiltered.txt"))
            {
                for (int row = 0; row < ProcessedData[0].Count; row++)
                {
                    for (int col = 0; col < ProcessedData.Length; col++)
                    {
                        s.Write(ProcessedData[col][row]);

                        if (col != (ProcessedData.Length - 1))
                            s.Write(' ');
                    }


                    s.Write('\n');
                }
            }


            // Create stats
            ExtractFrames();

            dataGridView1.Rows.Clear();
            for (int i = 0; i < Channels.Length; i++)
            {
                var channel = Channels[i];

                // Calc how wide the shortest sequence is
                //channel.OversamplingRate = Math.Min(channel.shortestOnesSequence, channel.shortestZerosSequence);

                channel.OversamplingRate = OVERSAMPLINGRATE;

                // Take the amount of samples which should equal 1s -> bitrate
                channel.BitRate = ProcessedData[i].Count / 1 / channel.OversamplingRate;
                int[] commonbitrate = new int[] { 50, 110, 150, 300, 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200, 230400, 460800 };
                channel.BitRate = commonbitrate.OrderBy(x => Math.Abs(x - channel.BitRate)).First();

                dataGridView1.Rows.Add(
                    i, // Channel number
                    channel.BitRate,
                    channel.OversamplingRate
                    );
            }

            /* POSTPROCESS OVERSAMPLING */
            ExtractFrameData();

            // ONLY 2!
            ListView[] lists = new ListView[] { listView1, listView2 };
            for (int i = 0; i < lists.Length; i++)
            {
                var listView = lists[i];

                listView.Items.Clear();

                for (int j = 0; j < Channels[i].Frames.Count; j++)
                {
                    var frame = Channels[i].Frames[j];
                    listView.Items.Add(new ListViewItem(new string[]
                                        {
                        j.ToString(), // Frame number
                        frame.Count.ToString(), // frame length
                        string.Join(" ", frame.Select(x=> x.ToString("X2"))), // Hex
                        string.Join(" ", frame.Select(x=> x.ToString()))
                                        }));
                }
            }


            var builder = new StringBuilder();
            foreach (ListViewItem item in listView2.Items)
                builder.AppendLine(item.SubItems[3].Text);

            Clipboard.SetText(builder.ToString());
        }

        private void ExtractFrameData()
        {
            for (int i = 0; i < Channels.Length; i++)
            {
                var channel = Channels[i];

                for (int j = 0; j < channel.Frames.Count; j++)
                {
                    var frame = channel.Frames[j];

                    List<int> binaryString = new List<int>();

                    int lastbit = frame[0];
                    int clockCount = 1;
                    int toggleState = 0;
                    // Idea: have a clock that runs at double the speed that toggles state
                    for (int k = 0; k < frame.Count; k++)
                    {
                        if ((frame[k] != lastbit))
                        {
                            // Reset "clock"
                            clockCount = 0;

                            // toggle state to be ready to sample
                            toggleState = 0;

                            lastbit = frame[k];
                        }

                        clockCount++;

                        if (clockCount >= channel.OversamplingRate / 2)
                        {
                            if (toggleState == 0)
                            {
                                // Sample at this point
                                binaryString.Add(frame[k]);

                                // And restart clock
                                clockCount = 0;

                                // And disable sampling until next clock cycle
                                toggleState = 1;
                            }
                            else
                            {
                                // restart clock
                                clockCount = 0;

                                // toggle state to be ready
                                toggleState = 0;
                            }
                        }

                    }

                    // parse binary string
                    // skip first bit (is always 0 SOF)
                    channel.Frames[j] = new List<int>();
                    for (int bitCounter = 1; bitCounter < (binaryString.Count - 8); bitCounter += 8)
                    {
                        int b = binaryString[bitCounter + 1] + 2 * binaryString[bitCounter + 2] + 4 * binaryString[bitCounter + 3] + 8 * binaryString[bitCounter + 4]
                            + 16 * binaryString[bitCounter + 5] + 32 * binaryString[bitCounter + 6] + 64 * binaryString[bitCounter + 7] + 128 * binaryString[bitCounter + 8];
                        channel.Frames[j].Add(b);
                    }

                }
            }
        }

        private void ExtractFrames()
        {
            Channels = new ChannelDescription[ProcessedData.Length];
            AnalyseStateFlow[] flows = new AnalyseStateFlow[ProcessedData.Length];
            for (int i = 0; i < ProcessedData.Length; i++)
            {
                Channels[i] = new ChannelDescription();
                flows[i] = new AnalyseStateFlow();
            }

            for (int row = 0; row < ProcessedData[0].Count; row++)
            {
                for (int col = 0; col < ProcessedData.Length; col++)
                {
                    // Check if is in frame
                    ChannelDescription currentDescription = Channels[col];
                    AnalyseStateFlow currentFlow = flows[col];

                    if (!currentFlow.insideFrame)
                    {
                        if (ProcessedData[col][row] == 1)
                            continue;

                        currentFlow.insideFrame = true;
                        currentFlow.currentFrame = new List<int>();
                    }

                    // Check if still in frame
                    if (ProcessedData[col][row] == 1)
                        currentFlow.onesCounter++;
                    else
                        currentFlow.onesCounter = 0;

                    if (currentFlow.onesCounter >= 25 * 8)
                    {
                        currentFlow.insideFrame = false;
                        currentDescription.Frames.Add(currentFlow.currentFrame);
                        continue;
                    }

                    currentFlow.currentFrame.Add(ProcessedData[col][row]);

                    if (ProcessedData[col][row] != currentFlow.lastBit)
                    {
                        if (currentFlow.sequenceCounter > 0)
                            currentFlow.SequenceLengthList.Add(currentFlow.sequenceCounter);

                        currentFlow.lastBit = ProcessedData[col][row];
                        currentFlow.sequenceCounter = 0;
                    }

                    currentFlow.sequenceCounter++;
                }
            }

            var adsdasd = flows[0].SequenceLengthList.OrderBy(x => x).ToList();
        }

        private void MedianFilter(int[,] rawData)
        {
            ProcessedData = new List<int>[rawData.GetLength(1)];
            for (int i = 0; i < ProcessedData.Length; i++)
            {
                ProcessedData[i] = new List<int>();
            }

            for (int row = MEDIANFILTERWINDOW; row < rawData.GetLength(0); row++)
            {
                for (int col = 0; col < 2; col++)
                {
                    // Backtrack and sum
                    int sum = 0;
                    for (int i = 0; i < MEDIANFILTERWINDOW; i++)
                        sum += rawData[row - 1 - i, col];

                    ProcessedData[col].Add(sum > (MEDIANFILTERWINDOW / 2) ? 1 : 0);
                }
            }
        }

        private int[,] ReadRawData()
        {
            var lines = System.IO.File.ReadAllLines(openFileDialog1.FileName);
            var rawData = new int[lines.Length, lines.First().Split(' ').Length];
            for (int row = 0; row < lines.Length; row++)
            {
                var pieces = lines[row].Split(' ');
                for (int col = 0; col < rawData.GetLength(1); col++)
                {
                    rawData[row, col] = int.Parse(pieces[col]);
                }
            }

            return rawData;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Properties.Settings.Default.Reload();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Properties.Settings.Default.Save();
        }
    }
}
