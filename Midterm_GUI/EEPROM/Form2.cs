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

namespace EEPROM
{
    public partial class Setting : Form
    {
        public SerialPort _serial = new SerialPort();
        public Setting()
        {
            InitializeComponent();
            _serial.BaudRate = 115200;
            foreach (string s in SerialPort.GetPortNames())
            {
                comboBox1.Items.Add(s);
            }
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            try
            {
                try
                {
                    _serial.PortName = comboBox1.SelectedItem.ToString();
                    _serial.BaudRate = Convert.ToInt32(comboBox2.SelectedItem);
                    _serial.Open();
                    this.Close();
                    EEPROM _main = new EEPROM();
                    foreach (EEPROM tmpform in Application.OpenForms)
                    {
                        if (tmpform.Name == "EEPROM")
                        {
                            _main = tmpform;
                            break;
                        }
                    }


                    _main.toolStripStatusLabel1.Text = " Connected: " + _serial.PortName.ToString();
                    _main.toolStripStatusLabel1.ForeColor = Color.Green;
                    _main.toolStripProgressBar1.Value = 100;
                }
                catch
                {
                    MessageBox.Show("Please select proper COM Port/Baud Rate");
                }
            }
            catch (InvalidOperationException err)
            {
                MessageBox.Show(err.ToString());
            }
        }
    }
}
