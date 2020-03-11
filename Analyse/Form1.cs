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
        private const int TOOSHORTFORBIT = 5;
        // Must be odd!
        private const int MEDIANFILTERWINDOW = 30;

        public List<int>[] ProcessedData { get; set; }
        internal ChannelDescription[] Channels { get; set; }

        public Form1()
        {
            InitializeComponent();

            if (openFileDialog1.ShowDialog() != DialogResult.OK)
                Close();

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
                channel.OversamplingRate = Math.Min(channel.shortestOnesSequence, channel.shortestZerosSequence);

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
            ListView[] lists = Controls.OfType<ListView>().ToArray();// new ListView[] { listView1, listView2 }; 
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
                        string.Join(" ", frame.Select(x=> x.ToString("X2")))
                                        }));
                }
            }
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
                    int sequenceCount = 1;
                    for (int k = 1; k < frame.Count; k++)
                    {
                        // Sample until oversampling rate or state transistion
                        if ((frame[k] != lastbit) ||
                            (sequenceCount > channel.OversamplingRate))
                        {
                            lastbit = frame[k];
                            sequenceCount = 1;

                            binaryString.Add(frame[k]);
                        }
                        else
                        {
                            sequenceCount++;
                        }
                    }

                    // parse binary string
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

                    if (currentFlow.onesCounter >= LONGESTONESSEQUENCE)
                    {
                        currentFlow.insideFrame = false;
                        currentDescription.Frames.Add(currentFlow.currentFrame);
                        continue;
                    }

                    currentFlow.currentFrame.Add(ProcessedData[col][row]);

                    if (ProcessedData[col][row] != currentFlow.lastBit)
                    {

                        if (currentFlow.lastBit == 1)
                        {
                            if (currentFlow.sequenceCounter > currentDescription.longestOnesSequence)
                                currentDescription.longestOnesSequence = currentFlow.sequenceCounter;

                            if (currentFlow.sequenceCounter > 0)
                                if (currentFlow.sequenceCounter < currentDescription.shortestOnesSequence)
                                    currentDescription.shortestOnesSequence = currentFlow.sequenceCounter;
                        }
                        else
                        {
                            if (currentFlow.sequenceCounter > currentDescription.longestZerosSequence)
                                currentDescription.longestZerosSequence = currentFlow.sequenceCounter;


                            if (currentFlow.sequenceCounter > 0)
                                if (currentFlow.sequenceCounter < currentDescription.shortestZerosSequence)
                                    currentDescription.shortestZerosSequence = currentFlow.sequenceCounter;
                        }

                        currentFlow.lastBit = ProcessedData[col][row];
                        currentFlow.sequenceCounter = 0;
                    }

                    currentFlow.sequenceCounter++;
                }
            }
        }

        private void MedianFilter(int[,] rawData)
        {
            Queue<int>[] buffer = new Queue<int>[rawData.GetLength(1)];
            ProcessedData = new List<int>[rawData.GetLength(1)];
            for (int i = 0; i < ProcessedData.Length; i++)
            {
                buffer[i] = new Queue<int>(Enumerable.Range(0, MEDIANFILTERWINDOW).Select(x => 1));
                ProcessedData[i] = new List<int>();
            }

            for (int row = 0; row < rawData.GetLength(0); row++)
            {
                for (int col = 0; col < 2; col++)
                {
                    // Insert value at the end
                    buffer[col].Enqueue(rawData[row, col]);

                    // Pop one at the front
                    buffer[col].Dequeue();

                    // Take the middle element
                    var median = buffer[col].OrderBy(x => x).ElementAt(MEDIANFILTERWINDOW / 2);
                    ProcessedData[col].Add(median);
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
