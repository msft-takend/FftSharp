﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FftSharp.Demo
{
    public partial class FormMicrophone : Form
    {
        private const int SAMPLE_RATE = 48000;
        private NAudio.Wave.WaveInEvent wvin;

        public FormMicrophone()
        {
            InitializeComponent();
            formsPlot1.Configure(middleClickMarginX: 0);
            cbDevices.Items.Clear();
            for (int i = 0; i < NAudio.Wave.WaveIn.DeviceCount; i++)
                cbDevices.Items.Add(NAudio.Wave.WaveIn.GetCapabilities(i).ProductName);
            if (cbDevices.Items.Count > 0)
                cbDevices.SelectedIndex = 0;
            else
                MessageBox.Show("ERROR: no recording devices available");
        }

        private void cbDevices_SelectedIndexChanged(object sender, EventArgs e)
        {
            wvin?.Dispose();
            wvin = new NAudio.Wave.WaveInEvent();
            wvin.DeviceNumber = cbDevices.SelectedIndex;
            wvin.WaveFormat = new NAudio.Wave.WaveFormat(rate: SAMPLE_RATE, bits: 16, channels: 1);
            wvin.DataAvailable += OnDataAvailable;
            wvin.BufferMilliseconds = 20;
            wvin.StartRecording();
        }

        double[] lastBuffer;
        private void OnDataAvailable(object sender, NAudio.Wave.WaveInEventArgs args)
        {
            int bytesPerSample = wvin.WaveFormat.BitsPerSample / 8;
            int samplesRecorded = args.BytesRecorded / bytesPerSample;
            if (lastBuffer is null || lastBuffer.Length != samplesRecorded)
                lastBuffer = new double[samplesRecorded];
            for (int i = 0; i < samplesRecorded; i++)
                lastBuffer[i] = BitConverter.ToInt16(args.Buffer, i * bytesPerSample);
        }

        ScottPlot.PlottableSignal signalPlot;
        ScottPlot.PlottableVLine peakLine;
        private void timer1_Tick(object sender, EventArgs e)
        {
            if (lastBuffer is null)
                return;

            double[] window = FftSharp.Window.Hanning(lastBuffer.Length);
            double[] windowed = FftSharp.Window.Apply(window, lastBuffer);
            double[] zeroPadded = FftSharp.Pad.ZeroPad(windowed);
            double[] fftPower = cbDecibel.Checked ?
                FftSharp.Transform.FFTpower(zeroPadded) :
                FftSharp.Transform.FFTmagnitude(zeroPadded);
            double[] fftFreq = FftSharp.Transform.FFTfreq(SAMPLE_RATE, fftPower.Length);

            // determine peak frequency
            double peakFreq = 0;
            double peakPower = 0;
            for (int i = 0; i < fftPower.Length; i++)
            {
                if (fftPower[i] > peakPower)
                {
                    peakPower = fftPower[i];
                    peakFreq = fftFreq[i];
                }
            }
            lblPeak.Text = $"Peak Frequency: {peakFreq:N0} Hz";

            formsPlot1.plt.XLabel("Frequency Hz");

            // make the plot for the first time, otherwise update the existing plot
            if (formsPlot1.plt.GetPlottables().Count == 0)
            {
                signalPlot = formsPlot1.plt.PlotSignal(fftPower, 2.0 * fftPower.Length / SAMPLE_RATE);
                peakLine = formsPlot1.plt.PlotVLine(peakFreq, ColorTranslator.FromHtml("#66FF0000"), 2);
            }
            else
            {
                signalPlot.ys = fftPower;
                peakLine.position = peakFreq;
                peakLine.visible = cbPeak.Checked;
            }

            if (cbAutoAxis.Checked)
                formsPlot1.plt.AxisAuto(horizontalMargin: 0);

            formsPlot1.Render();
        }
    }
}
