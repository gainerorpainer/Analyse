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

        public int[,] RawData { get; set; } 
        internal ChannelDescription[] Channels { get; set; }

        public Form1()
        {
            InitializeComponent();

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                // Read into model
                var lines = System.IO.File.ReadAllLines(openFileDialog1.FileName);
                RawData = new int[lines.Length, lines.First().Split(' ').Length];
                for (int row = 0; row < lines.Length; row++)
                {
                    var pieces = lines[row].Split(' ');
                    for (int col = 0; col < RawData.GetLength(1); col++)
                    {
                        RawData[row, col] = int.Parse(pieces[col]);
                    }
                }

                // Create stats
                Channels = new ChannelDescription[RawData.GetLength(1)];
                AnalyseStateFlow[] flows = new AnalyseStateFlow[RawData.GetLength(1)];
                for (int i = 0; i < RawData.GetLength(1); i++)
                {
                    Channels[i] = new ChannelDescription();
                    flows[i] = new AnalyseStateFlow();
                }

                for (int row = 0; row < RawData.GetLength(0); row++)
                {
                    for (int col = 0; col < 1; col++)
                    {
                        // Check if is in frame
                        ChannelDescription currentDescription = Channels[col];
                        AnalyseStateFlow currentFlow = flows[col];

                        if (!currentFlow.insideFrame)
                        {
                            if (RawData[row, col] == 1)
                                continue;

                            currentFlow.insideFrame = true;
                            currentFlow.currentFrame = new List<int>();
                        }

                        // Check if still in frame
                        if (RawData[row, col] == 1)
                            currentFlow.onesCounter++;
                        else
                            currentFlow.onesCounter = 0;

                        if (currentFlow.onesCounter >= LONGESTONESSEQUENCE)
                        {
                            currentFlow.insideFrame = false;
                            currentDescription.Frames.Add(currentFlow.currentFrame);
                            continue;
                        }

                        currentFlow.currentFrame.Add(RawData[row, col]);

                        if (RawData[row, col] != currentFlow.lastBit)
                        {
                            // New sequence?


                            if (currentFlow.onesSequenceCounter > currentDescription.longestOnesSequence)
                                currentDescription.longestOnesSequence = currentFlow.onesSequenceCounter;
                            if (currentFlow.zerosSequenceCounter > currentDescription.longestZerosSequence)
                                currentDescription.longestZerosSequence = currentFlow.zerosSequenceCounter;

                            // Do not check on first frame ever
                            if (currentFlow.onesSequenceCounter > 0)
                                if (currentFlow.onesSequenceCounter < currentDescription.shortestOnesSequence)
                                    currentDescription.shortestOnesSequence = currentFlow.onesSequenceCounter;
                            if (currentFlow.zerosSequenceCounter > 0)
                                if (currentFlow.zerosSequenceCounter < currentDescription.shortestZerosSequence)
                                    currentDescription.shortestZerosSequence = currentFlow.zerosSequenceCounter;


                            currentFlow.lastBit = RawData[row, col];

                            currentFlow.onesSequenceCounter = 0;
                            currentFlow.zerosSequenceCounter = 0;
                        }

                        if (RawData[row, col] == 1)
                            currentFlow.onesSequenceCounter++;
                        else
                            currentFlow.zerosSequenceCounter++;

                    }
                }
            }
            else
            {
                Close();
            }

            dataGridView1.Rows.Clear();
            for (int i = 0; i < Channels.Length; i++)
            {
                var channel = Channels[i];

                // Calc how wide the shortest sequence is
                channel.OversamplingRate = Math.Min(channel.shortestOnesSequence, channel.shortestZerosSequence);

                // Take the amount of samples which should equal 1s -> bitrate
                channel.BitRate = RawData.GetLength(0) / 1 / channel.OversamplingRate;

                dataGridView1.Rows.Add(
                    i, // Channel number
                    channel.BitRate,
                    channel.OversamplingRate
                    );
            }

            /* POSTPROCESS OVERSAMPLING */
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
