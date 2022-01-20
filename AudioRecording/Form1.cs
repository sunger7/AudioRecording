using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Threading;
using System.Runtime.InteropServices;

namespace AudioRecording
{
    public partial class Form1 : Form
    {
        string outputFolder;
        string outputFilePath;
        WaveInEvent waveIn;
        WaveFileWriter writer = null;
        private WaveOutEvent outputDevice;
        private AudioFileReader audioFile;
        bool closing = false;
        bool reDraw = false;
        bool isPlay = false;//是否在播放文件
        byte[] samples = null;
        float[] audioPlayingData = null;
        public Form1()
        {
            InitializeComponent();
            outputFolder = Path.Combine(Environment.CurrentDirectory, "NAudio");
            Directory.CreateDirectory(outputFolder);
            //outputFilePath = Path.Combine(outputFolder, "recorded.wav");
            waveIn = new WaveInEvent();
            //Console.WriteLine(waveIn.WaveFormat.SampleRate+"_"+ waveIn.WaveFormat.BitsPerSample+"_"
            //    + waveIn.WaveFormat.AverageBytesPerSecond);
            waveIn.DataAvailable += (s, a) =>
            {
                writer.Write(a.Buffer, 0, a.BytesRecorded);
                if (!reDraw)
                {
                    unsafe
                    {
                        samples = new byte[a.BytesRecorded];
                        System.Array.Copy(a.Buffer, samples, a.BytesRecorded);
                        Console.WriteLine(a.BytesRecorded);
                        Thread mythread = new Thread(new ParameterizedThreadStart(DrawWave));
                        mythread.Start(samples);
                    }

                }
                
                if (writer.Position > waveIn.WaveFormat.AverageBytesPerSecond * 30)
                {
                    waveIn.StopRecording();
                }
            };
            waveIn.RecordingStopped += (s, a) =>
            {
                writer?.Dispose();
                writer = null;
                button1.Enabled = true;
                button2.Enabled = false;
                if (closing)
                {
                    waveIn.Dispose();
                }
            };
            this.FormClosing += (s, a) => { 
                closing = true; 
                waveIn.StopRecording();
                if(outputDevice!=null)
                outputDevice.Stop(); 
            };
            this.SizeChanged += (s, a) =>
            {
                if (outputDevice == null)
                    DrawPlayingWave(audioPlayingData, audioPlayingData.Length);
            };

            InitializeTreeView();
            treeView1.NodeMouseDoubleClick += treeView1_NodeMouseDoubleClick;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if(outputDevice != null || outputDevice != null)
            {
                outputDevice.Dispose();
                outputDevice = null;
                audioFile.Dispose();
                audioFile = null;
                isPlay = false;
            }

            if (true)
            {
                int i = 1;
                do
                {
                    string fileName = "recorded" + i + ".wav";
                    outputFilePath = Path.Combine(outputFolder, fileName);
                    i++;
                } while (File.Exists(outputFilePath));

                writer = new WaveFileWriter(outputFilePath, waveIn.WaveFormat);
                waveIn.StartRecording();
                button1.Enabled = false;
                button2.Enabled = true;
                button2.Text = "Stop";
            }

        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (!isPlay)
            {
                waveIn.StopRecording();
                TreeNode curNode = treeView1.Nodes[0];
                var fi2 = new FileInfo(outputFilePath);
                curNode.Nodes.Add(fi2.FullName, fi2.Name);
            }
            else
            {
                if(button2.Text == "Pause On") { 
                    outputDevice.Play();
                    button2.Text = "Playing";
                    button1.Enabled = false;
                    button2.Enabled = true;
                }
                else
                {
                    outputDevice.Pause();
                    button2.Text = "Pause On";
                    button1.Enabled = true;
                    button2.Enabled = true;
                }

            }

        }
        // Populates a TreeView control with example nodes. 
        private void InitializeTreeView()
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(outputFolder);
            treeView1.BeginUpdate();
            TreeNode curNode = treeView1.Nodes.Add(directoryInfo.Name);
            //treeView1.Nodes.Add("Parent");
            foreach (FileInfo file in directoryInfo.GetFiles())
            {
                curNode.Nodes.Add(file.FullName, file.Name);
            }
            treeView1.EndUpdate();
            treeView1.Nodes[0].Expand();
        }
        private void DrawWave(object bytes)
        {
            byte[] nbytes = (byte[])(bytes);
            float[] shorts = new float[nbytes.Length/2];
            for (int i = 0; i < shorts.Length; i++)
            {
                shorts[i] = (short)(nbytes[i * 2] | (nbytes[i * 2 + 1] << 8))/32768f;
                // absolute value 
                //if (shorts[i] < 0) shorts[i] = -shorts[i];
                // is this the max value?
                
            }
            float max = shorts.Max(), min = shorts.Min();
            reDraw = true;
            
            {
                // Draw next line and...
                pictureBox1.Image = null;
                int wid = pictureBox1.ClientSize.Width;
                int hgt = pictureBox1.ClientSize.Height;

                // Draw with double-buffering.
                Bitmap bm = new Bitmap(wid, hgt);
                using (Graphics gr = Graphics.FromImage(bm))
                {
                    //DrawButterfly(gr, wid, hgt);
                    PointF[] points = new PointF[ Convert.ToInt16( shorts.Length / 5)];
                    for (int i = 0;i< points.Length;i++)
                    {
                        points[i].Y = (shorts[i * 5] - min) / (max - min) * hgt;
                        points[i].X = (i*1.0f)/ (points.Length*1.0f) * wid;
                    }

                    gr.DrawCurve(new Pen(Brushes.Red), points);
                }
                this.Invoke(new Action(() =>
                {
                    pictureBox1.Image = bm;
                    //pictureBox1.Refresh();
                }));

                // Draw without double-buffering.
                //DrawButterfly(picCanvas2.CreateGraphics(), wid, hgt);
                // ... Remember the location
                
            }
            reDraw = false;
            
        }

        private void treeView1_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (outputDevice != null || audioFile != null)
            {
                outputDevice.Dispose(); outputDevice = null; audioFile.Dispose(); audioFile = null;
            }
            string fileName = e.Node.Name;
            if (outputDevice == null)
            {
                outputDevice = new WaveOutEvent();
                outputDevice.PlaybackStopped += (s, a) => { if (closing) { outputDevice.Dispose(); outputDevice = null; audioFile.Dispose(); audioFile = null; } };
            }
            if (audioFile == null)
            {
                audioFile = new AudioFileReader(fileName);
                outputDevice.Init(audioFile);
                long len = audioFile.Length;
                audioPlayingData = new float[len / 4];
                audioFile.Read(audioPlayingData, 0, Convert.ToInt32(len / 4));
                audioFile.Position = 0;
            }
            outputDevice.Play();
            button1.Enabled = false;
            button2.Enabled = true;
            button2.Text = "Playing";
            isPlay = true;


        }
        private void DrawPlayingWave(float[] data,long location)
        {
            if (location > data.Length) location = data.Length;
            float max = data.Max(), min = data.Min();

            {
                // Draw next line and...
                pictureBox1.Image = null;
                int wid = pictureBox1.ClientSize.Width;
                int hgt = pictureBox1.ClientSize.Height;

                // Draw with double-buffering.
                Bitmap bm = new Bitmap(wid, hgt);
                using (Graphics gr = Graphics.FromImage(bm))
                {
                    //DrawButterfly(gr, wid, hgt);
                    PointF[] points = new PointF[Convert.ToInt32(data.Length / 5.0)];
                    PointF[] pointsPast = new PointF[Convert.ToInt32(Math.Floor( location*1.0 / 5))];
                    for (int i = 0; i < points.Length; i++)
                    {
                        points[i].Y = (data[i * 5] - min) / (max - min) * hgt;
                        points[i].X = (i * 1.0f) / (points.Length * 1.0f) * wid;
                        if(i< pointsPast.Length)
                        {
                            pointsPast[i].Y = points[i].Y;
                            pointsPast[i].X = points[i].X;
                        }
                    }

                    gr.DrawCurve(new Pen(Brushes.DarkCyan), points);
                    gr.DrawCurve(new Pen(Brushes.Red), pointsPast);
                }
                
                this.Invoke(new Action(() =>
                {
                    pictureBox1.Image = bm;
                    //pictureBox1.Refresh();
                }));
            }

        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (outputDevice != null)
                if (outputDevice.GetPosition() > 0) {
                    DrawPlayingWave(audioPlayingData, Convert.ToInt32(outputDevice.GetPosition() / 4));
                    //Console.WriteLine(audioPlayingData.Length + "_" + Convert.ToInt32( outputDevice.GetPosition()/4));
                    if(outputDevice.GetPosition() > audioPlayingData.Length*4)
                    {
                        outputDevice.Stop();
                        outputDevice.Dispose(); outputDevice = null; audioFile.Dispose(); audioFile = null;
                        button1.Enabled = true;
                        button2.Enabled = false;
                        button2.Text = "Stop";
                    }
                }
                    
        }
    }
}
