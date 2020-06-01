using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using System.IO;
using ZedGraph;

namespace EEPROM
{
    public partial class EEPROM : Form
    {
        private Setting _setting = new Setting();   //creating object for secondary form
        Int16 ok = 0;
        //Boolean ready = false;
        Int16 temp_data = 0;
        Boolean sampling_freq_value_received = false;

        Int16[] processed_samples_buffer= new Int16[100];       //buffer to store processed data for plot
        Int16 processed_samples_index=0;
        Boolean processed_samples_ready = false;
        Boolean processed_samples_starting = false;

        Int16[] raw_samples_buffer = new Int16[100];            //buffer to store raw samples for plot
        Int16 raw_samples_index = 0;
        Boolean raw_samples_ready = false;
        Boolean raw_samples_starting = false;

        Boolean starting_text = false;
        //Zedgraph objects declaration starts here
        GraphPane mypane = new GraphPane();
        PointPairList listA = new PointPairList();
        PointPairList listB = new PointPairList();

        LineItem teamAcurve;
        LineItem teamBcurve;

        Int32 time = 100;   //dummy variable for plot representing x data

        private delegate void CalldrawGraph();      //initializing a delegate to ensure safe cross thread call for plotting data in backgroundworker

        //private bool end = false;   //signal that tells backgroundworker to stop running
        //zedgraph objects declaration ends here
        public EEPROM()
        {
            InitializeComponent();

            graph_init();

            checkBox1.Checked = true;
            checkBox2.Checked = true;
        }

        private void COMSettingToolStripMenuItem_Click(object sender, EventArgs e)  //event handler for COM PORT settings
        {
            _setting.Show();
            _setting._serial.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
        }

       
        private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort sp = (SerialPort)sender;

            try
            {
                if (sp.BytesToRead > 0)
                {
                    byte[] buffer = new byte[sp.BytesToRead];
                    int count = sp.Read(buffer, 0, sp.BytesToRead);
                    modify_richtextbox1(buffer, count);

                }
            }
            catch
            {
                sp.DiscardInBuffer();
            }
            
        }

        private void modify_richtextbox1(byte[] buffer, int count)
        {
            byte data;
            for (int i = 0; i < count; i++)
            {
                data = buffer[i];
                this.Invoke(new EventHandler(write_richtextbox1), new object[] { data });
            }
        }


        private void write_richtextbox1(object sender, EventArgs e)
        {
            byte data = (byte)sender;
            char s = Convert.ToChar(data);
            if (s == '@')
            {
                sampling_freq_value_received = true;
                temp_data = 0;
            }
            else if (s == '+')
            {
                starting_text = true;
                richTextBox1.Text = "";
            }
            else if (s == '=')
            {
                starting_text = false;
            }
            else if (s == '}')
            {
                textBox3.Text = temp_data.ToString() + " Hz";
                sampling_freq_value_received = false;
            }
            else if (starting_text)
            {
                richTextBox1.AppendText(s.ToString());
            }
            else if (s >= '0' && s <= '9')
            {
                temp_data = (Int16)((temp_data * 10) + (s - 48));
            }
            else if (s == '(')
            {   //processed data incoming
                processed_samples_index = 0;
                processed_samples_starting = true;
                raw_samples_starting = false;
                temp_data = 0;
            }
            else if (s == ')')
            {   //raw data incoming
                temp_data = 0;
                raw_samples_index = 0;
                raw_samples_starting = true;
                processed_samples_starting = false;
            }
            else if (s == ' ' && processed_samples_starting)
            {
                //processed data came in
                processed_samples_buffer[processed_samples_index++] = temp_data;
                temp_data = 0;
                if (processed_samples_index > 99)
                {
                    processed_samples_index = 0;
                    processed_samples_ready = true;
                    processed_samples_starting = false;
                }

            }
            else if (s == ' ' && raw_samples_starting)
            {
                // raw data came in
                raw_samples_buffer[raw_samples_index++] = temp_data;
                temp_data = 0;
                if (raw_samples_index > 99)
                {
                    raw_samples_index = 0;
                    raw_samples_ready = true;
                    raw_samples_starting = false;
                }
            }
            if (processed_samples_ready && raw_samples_ready)
            {
                //both data arrived to plot
                processed_samples_ready = false;
                raw_samples_ready = false;
                //plot the graph
                //do something to plot data on zedgraph

                updatePlot();
            }


        }

        private void Button1_Click(object sender, EventArgs e)
        {
            
            _setting._serial.Write("%");        //start downloading new coefficient
            string FILE_NAME="";
            //selects proper file for coefficients
            if (radioButton1.Checked)
            {
                if (comboBox2.Text == "100 Hz")
                {
                    if (comboBox1.Text == "1 KHz")
                        FILE_NAME = "LPF100_1.h";
                    else if (comboBox1.Text == "1.5 KHz")
                        FILE_NAME = "LPF100_1_5.h";
                    else if (comboBox1.Text == "2 KHz")
                        FILE_NAME = "LPF100_2.h";
                }
                else if (comboBox2.Text == "200 Hz")
                {
                    if (comboBox1.Text == "1 KHz")
                        FILE_NAME = "LPF200_1.h";
                    else if (comboBox1.Text == "1.5 KHz")
                        FILE_NAME = "LPF200_1_5.h";
                    else if (comboBox1.Text == "2 KHz")
                        FILE_NAME = "LPF200_2.h";
                }
                else if (comboBox2.Text == "300 Hz")
                {
                    if (comboBox1.Text == "1 KHz")
                        FILE_NAME = "LPF300_1.h";
                    else if (comboBox1.Text == "1.5 KHz")
                        FILE_NAME = "LPF300_1_5.h";
                    else if (comboBox1.Text == "2 KHz")
                        FILE_NAME = "LPF300_2.h";
                }
            }
            else if (radioButton2.Checked)
            {
                if (comboBox2.Text == "100 Hz")
                {
                    if (comboBox1.Text == "1 KHz")
                        FILE_NAME = "BPF100_1.h";
                    else if (comboBox1.Text == "1.5 KHz")
                        FILE_NAME = "BPF100_1_5.h";
                    else if (comboBox1.Text == "2 KHz")
                        FILE_NAME = "BPF100_2.h";
                }
                else if (comboBox2.Text == "200 Hz")
                {
                    if (comboBox1.Text == "1 KHz")
                        FILE_NAME = "BPF200_1.h";
                    else if (comboBox1.Text == "1.5 KHz")
                        FILE_NAME = "BPF200_1_5.h";
                    else if (comboBox1.Text == "2 KHz")
                        FILE_NAME = "BPF200_2.h";
                }
                else if (comboBox2.Text == "300 Hz")
                {
                    if (comboBox1.Text == "1 KHz")
                        FILE_NAME = "BPF300_1.h";
                    else if (comboBox1.Text == "1.5 KHz")
                        FILE_NAME = "BPF300_1_5.h";
                    else if (comboBox1.Text == "2 KHz")
                        FILE_NAME = "BPF300_2.h";
                }
            }
               
            using (FileStream fs = new FileStream(FILE_NAME, FileMode.Open, FileAccess.Read))
            {
                using (BinaryReader r = new BinaryReader(fs))
                {
                    int ch;
                    Int32 coeff_value=0;
                    Int32[] coefficient = new Int32[64];
                    byte index = 0;
                    int value_received_flag = 0;
                    int negative_sign_flag = 0;
                    while ((ch = r.Read()) != 123)
                    {
                    }

                    while((ch=r.Read()) != -1)
                    {

                        if (ch >= 48 && ch <= 57)
                        {
                            coeff_value = (Int32)((coeff_value * 10) + (ch - 48));
                            value_received_flag = 1;
                            //MessageBox.Show(coeff_value.ToString());
                        }
                        else if (ch == 45)
                        {
                            negative_sign_flag = 1;
                        }
                        else if ((ch == 32 || ch == 125) && (value_received_flag==1))
                        {
                            //store it into the buffer
                            if (negative_sign_flag == 1)
                                coefficient[index++] = 0 - coeff_value;
                            else
                                coefficient[index++] = coeff_value;
                            coeff_value = 0;
                            value_received_flag = 0;
                            negative_sign_flag = 0;
                        }
                        
                            
                    }

                    //sending start signal
                    _setting._serial.Write("{");

                    for (int i = 0; i < index; i++)
                    {
                        //MessageBox.Show("Value: "+coefficient[i].ToString());

                        //send each coefficient value
                        if(coefficient[i]>=0)
                            _setting._serial.Write(coefficient[i].ToString() + " ");
                        else if(coefficient[i] <0)
                        {
                            coefficient[i] = coefficient[i] * -1;
                            _setting._serial.Write("-" + coefficient[i].ToString() + " ");
                        }
                    }

                    //sending end signal
                    _setting._serial.Write("}");
                    //sending DC offset value
                    _setting._serial.Write("~" + textBox1.Text.ToString() + "!");
                    //sending Shifting Value
                    _setting._serial.Write("&" + textBox2.Text.ToString() + "$");
                }
            }

            //MessageBox.Show("Download Successful!", "Filter Coefficient");


        }

        private void Button4_Click(object sender, EventArgs e)
        {
            _setting._serial.Write("~" + textBox1.Text.ToString() + "!");
            //MessageBox.Show("Value Send Successfully", "DC Offset");
        }

        private void Button5_Click(object sender, EventArgs e)
        {
            _setting._serial.Write("&" + textBox2.Text.ToString() + "$");
            //MessageBox.Show("Value Send Successfully", "Shift Number");
        }

        private void Button2_Click(object sender, EventArgs e)
        {
            _setting._serial.Write("^");
        }

        private void Button3_Click(object sender, EventArgs e)
        {
            _setting._serial.Write("#");
        }

        private void Button6_Click(object sender, EventArgs e)
        {
            _setting._serial.Write("@");    //sending sampling frequency request
        }

        private void PlotGraphToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _setting._serial.Write("*");        //sending signal to PIC to plot the graph
            //checkBox1.Checked = true;
            //checkBox2.Checked = true;
        }

        #region Zedgraph
        private double[] teamA()
        {
            double[] a = new double[100];
            for (int i = 0; i < 100; i++)
            {
                //a[i] = Math.Sin(2 * i * Math.PI / 90);
                //a[i] = processed_samples_buffer[i];
                //sampled data buffer
                a[i] = raw_samples_buffer[i];
            }
            return a;
        }

        private double[] teamB()
        {
            double[] a = new double[100];
            for (int i = 0; i < 100; i++)
            {
                //a[i] = 2 * Math.Sin(2 * Math.PI * (i / 2) / 45);
                a[i] = processed_samples_buffer[i];
            }
            return a;

        }
        private void graph_init()
        {
            mypane = zedGraphControl1.GraphPane;
            mypane.Title.Text = "Sampled Data and Filtered Data";
            mypane.XAxis.Title.Text = "Time(us)";
            mypane.YAxis.Title.Text = "Voltage(mV)";
            

            double[] a = teamA();
            double[] b = teamB();

            for (int i = 0; i < 100; i++)
            {
                listA.Add(i, a[i]);
                listB.Add(i, b[i]);
            }
            teamAcurve = mypane.AddCurve("Sampled Data", listA, Color.Blue, SymbolType.None);
            teamBcurve = mypane.AddCurve("Filtered Data", listB, Color.Tomato, SymbolType.None);

            //teamAcurve = mypane.AddCurve("Line A", listA, Color.Red, SymbolType.Circle);
            //teamBcurve = mypane.AddCurve("Line B", listB, Color.Aqua, SymbolType.Diamond);

            //teamAcurve.AddPoint(101,Math.Sin(1));  //adds a point to the curve
            zedGraphControl1.AxisChange();
            zedGraphControl1.IsShowPointValues = true;

        }
        #endregion

        private void updatePlot()
        {

            if (zedGraphControl1.InvokeRequired)
            {
                var d = new CalldrawGraph(updatePlot);
                Invoke(d);
            }
            else
            {
                //listA.Clear();
                //listB.Clear();
                teamAcurve.Clear();
                teamBcurve.Clear();
                //Thread.Sleep(10);
                double[] a = teamA();
                double[] b = teamB();

                for (int i = 0; i < 100; i++)
                {
                    listA.Add(i, a[i]);
                    listB.Add(i, b[i]);
                }

                //adding a single point
                //double v = Math.Sin(2 * time * Math.PI / 90);
                //listA.Add(new PointPair(time, v));
                //double v2 = 2 * Math.Sin(2 * Math.PI * (time / 2) / 45);
                //listB.Add(new PointPair(time / 2, v2));
                mypane.AxisChange();
                zedGraphControl1.Refresh();

            }

        }

        private void CheckBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked)
            {
                teamAcurve.IsVisible = true;
                zedGraphControl1.Refresh();

            }
            else
            {
                teamAcurve.IsVisible = false;
                zedGraphControl1.Refresh();
            }
        }

        private void CheckBox2_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox2.Checked)
            {
                teamBcurve.IsVisible = true;
                zedGraphControl1.Refresh();

            }
            else
            {
                teamBcurve.IsVisible = false;
                zedGraphControl1.Refresh();
            }
        }

       

      

        private void RealtimePlotToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _setting._serial.Write("/");    //sending signal to GUI to do real time plot
        }

        private void StartToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _setting._serial.Write("/");    //sending signal to GUI to do real time plot
        }

        private void EndToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _setting._serial.Write(".");    //sending signal to GUI to do real time plot
        }
    }
}
