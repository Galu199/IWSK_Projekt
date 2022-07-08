using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace IWSK_RS232
{
    /// <summary>
    /// Logika interakcji dla klasy MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static string sendBuffer = "";

        public bool dsrCstThreadContinue = false;
        public bool rtsCtsHandshake = false;
        public bool dtrDsrHandshake = false;
        public bool XonXoffHandshake = false;
        public bool pingMode = false;
        public bool pingReady = true;

        public string ownTerminator;
        public string dataTermintaorBuffer;

        public enum SelectTerminator { None, CR, LF, CRLF, OWN };
        public SelectTerminator terminator;

        public SerialPort serialPort1;

        public MainWindow()
        {
            InitializeComponent();
            serialPort1 = (SerialPort)Resources["serialPort"];
        }

        private void ComboBoxPort_DropDownOpened(object sender, EventArgs e)
        {
            ComboBoxPort.Items.Clear();
            Array.ForEach(SerialPort.GetPortNames(), s => ComboBoxPort.Items.Add(s));
        }

        private void ButtonOpen_Click(object sender, RoutedEventArgs e)
        {
            if (serialPort1.IsOpen) return;
            try
            {
                string portName = Convert.ToString(ComboBoxPort.SelectedItem);
                serialPort1 = new SerialPort();
                serialPort1.PortName = portName;
                serialPort1.BaudRate = Convert.ToInt32(ComboBoxSpeed.Text);
                serialPort1.DataBits = Convert.ToInt32(ComboBoxBitsCount.SelectedValue.ToString());
                serialPort1.Parity = (Parity)ComboBoxParityBit.SelectedIndex;
                serialPort1.StopBits = (StopBits)(ComboBoxStopBit.SelectedIndex + 1);
                switch (ComboBoxFlowControl.SelectedIndex)
                {
                    case 3:
                        dtrDsrHandshake = true;
                        rtsCtsHandshake = false;
                        XonXoffHandshake = false;
                        serialPort1.PinChanged += new SerialPinChangedEventHandler(portPinChanged);
                        serialPort1.DtrEnable = true;
                        break;
                    case 2:
                        dtrDsrHandshake = false;
                        rtsCtsHandshake = true;
                        XonXoffHandshake = false;
                        serialPort1.PinChanged += new SerialPinChangedEventHandler(portPinChanged);
                        serialPort1.RtsEnable = true;
                        //serialPort1.Handshake = (Handshake)2;
                        break;
                    case 1:
                        dtrDsrHandshake = false;
                        rtsCtsHandshake = false;
                        XonXoffHandshake = true;
                        serialPort1.Handshake = (Handshake)Enum.ToObject(typeof(Handshake), ComboBoxFlowControl.SelectedIndex);
                        break;
                    case 0:
                    default:
                        dtrDsrHandshake = true;
                        rtsCtsHandshake = true;
                        XonXoffHandshake = true;
                        serialPort1.Handshake = (Handshake)Enum.ToObject(typeof(Handshake), ComboBoxFlowControl.SelectedIndex);
                        break;
                }

                terminator = (SelectTerminator)Enum.ToObject(typeof(SelectTerminator), ComboBoxTerminator.SelectedIndex);
                if (terminator == SelectTerminator.OWN)
                {
                    ownTerminator = TextBoxTerminatorCustom.Text.Length < 2 ? "\r\n" : TextBoxTerminatorCustom.Text;
                }

                serialPort1.ReadBufferSize = 4096;
                serialPort1.Open();

                dsrCstThreadContinue = true;

                serialPort1.DataReceived += DataReceivedHandler;
                serialPort1.PinChanged += SerialPort1_PinChanged;

                dsrCstListenerHandler();

                portSelectBox.IsEnabled = false;
                ButtonPingMode.IsEnabled = true;

                //if (serialPort1.Handshake != Handshake.RequestToSend) ButtonRTS.IsEnabled = true;
                if (rtsCtsHandshake) ButtonRTS.IsEnabled = true;
                else ButtonRTS.IsEnabled = false;
                if (dtrDsrHandshake) ButtonDTR.IsEnabled = true;
                else ButtonDTR.IsEnabled = false;
                if (XonXoffHandshake)
                {
                    ButtonXON.IsEnabled = true;
                    ButtonXOFF.IsEnabled = true;
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            if (serialPort1.IsOpen)
            {
                dsrCstThreadContinue = false;
                serialPort1.Close();
                portSelectBox.IsEnabled = true;
                ButtonPingMode.IsEnabled = false;

                ButtonDTR.IsEnabled = false;
                ButtonRTS.IsEnabled = false;
                ButtonXON.IsEnabled = false;
                ButtonXOFF.IsEnabled = false;
            }
        }

        private void ButtonPingMode_Click(object sender, RoutedEventArgs e)
        {
            if (!pingMode)
            {
                ButtonClose.IsEnabled = false;
                TextBoxInput.IsEnabled = false;
                ButtonSend.IsEnabled = false;
                ButtonPingSend.IsEnabled = true;
                pingMode = true;
            }
            else
            {
                ButtonClose.IsEnabled = true;
                TextBoxInput.IsEnabled = true;
                ButtonSend.IsEnabled = true;
                ButtonPingSend.IsEnabled = false;
                pingMode = false;
                TextBoxOutput.Text = "";
            }
        }

        private void ButtonPingSend_Click(object sender, RoutedEventArgs e)
        {
            serialPort1.Write("\r");
            pingReady = false;
            Stopwatch sw = new Stopwatch();
            sw.Start();
            while (!pingReady)
            {
                if (sw.ElapsedMilliseconds > 1000)
                {
                    sw.Stop();
                    TextBoxOutput.Clear();
                    TextBoxOutput.AppendText("Error timeout");
                    return;
                }
            }
            sw.Stop();
            TextBoxOutput.Clear();
            TextBoxOutput.AppendText($"Czas pingu = {sw.ElapsedMilliseconds}ms");
        }

        private void ComboBoxTerminator_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboBoxTerminator.SelectedIndex == 4)
            {
                TextBoxTerminatorCustom.IsEnabled = true;
            }
            else
            {
                if (TextBoxTerminatorCustom == null) return;
                TextBoxTerminatorCustom.IsEnabled = false;
                TextBoxTerminatorCustom.Text = "";
            }
        }

        private void ButtonDTR_Click(object sender, RoutedEventArgs e)
        {
            serialPort1.DtrEnable = !serialPort1.DtrEnable;
            if (serialPort1.DtrEnable) LampDTR.Background = Brushes.Green;
            else LampDTR.Background = Brushes.Red;
        }

        private void ButtonRTS_Click(object sender, RoutedEventArgs e)
        {
            serialPort1.RtsEnable = !serialPort1.RtsEnable;
            if (serialPort1.RtsEnable) LampRTS.Background = Brushes.Green;
            else LampRTS.Background = Brushes.Red;
        }

        private void ButtonXON_Click(object sender, RoutedEventArgs e)
        {
            serialPort1.Write(((char)17).ToString());
            LampX.Background = Brushes.Green;
        }

        private void ButtonXOFF_Click(object sender, RoutedEventArgs e)
        {
            serialPort1.Write(((char)19).ToString());
            LampX.Background = Brushes.Red;
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (serialPort1.IsOpen)
            {
                serialPort1.Close();
            }
        }

        private static void portPinChanged(object sender, SerialPinChangedEventArgs e)
        {
            if (e.EventType == SerialPinChange.CDChanged)
            {
                SerialPort sp = (SerialPort)sender;
                if (sp.CDHolding)
                {
                    if (sendBuffer.Length != 0)
                    {
                        sp.Write(sendBuffer);
                        sendBuffer = "";
                    }
                }
            }
            if (e.EventType == SerialPinChange.CtsChanged)
            {
                SerialPort sp = (SerialPort)sender;
                if (sp.CtsHolding)
                {
                    if (sendBuffer.Length != 0)
                    {
                        sp.Write(sendBuffer);
                        sendBuffer = "";
                    }
                }
            }
        }

        private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            if (pingMode)
            {
                SerialPort sp = (SerialPort)sender;
                while (sp.BytesToRead >= 1)
                {
                    int read = sp.ReadChar();
                    if (read == (int)'\n')
                    {
                        pingReady = true;
                    }
                    else if (read == (int)'\r') // odpowiedz na ping
                    {
                        sp.Write("\n");
                    }
                }
            }
            else if (terminator != SelectTerminator.None)
            {
                SerialPort sp = (SerialPort)sender;
                while (sp.BytesToRead >= 1)
                {
                    if (terminator == SelectTerminator.CR)
                    {
                        char term = (char)sp.ReadChar();
                        if (term == '\r')
                        {
                            TextBoxOutput.AppendText(dataTermintaorBuffer + '\n');
                            //recivedTextBox.Invoke(new Action(delegate ()
                            //{
                            //    recivedTextBox.AppendText(dataTermintaorBuffer + '\n');
                            //}));
                            //dataTermintaorBuffer = "";
                        }
                        else
                        {
                            dataTermintaorBuffer += term;
                        }
                    }
                    else if (terminator == SelectTerminator.LF)
                    {
                        char term = (char)sp.ReadChar();
                        if (term == '\n')
                        {
                            TextBoxOutput.AppendText(dataTermintaorBuffer + '\n');
                            //recivedTextBox.Invoke(new Action(delegate ()
                            //{
                            //    recivedTextBox.AppendText(dataTermintaorBuffer + '\n');
                            //}));
                            //dataTermintaorBuffer = "";
                        }
                        else
                        {
                            dataTermintaorBuffer += term;
                        }
                    }
                    else if (terminator == SelectTerminator.CRLF)
                    {
                        dataTermintaorBuffer += (char)sp.ReadChar();
                        if (dataTermintaorBuffer.Contains("\r\n"))
                        {
                            TextBoxOutput.AppendText(dataTermintaorBuffer + '\n');
                            //recivedTextBox.Invoke(new Action(delegate ()
                            //{
                            //    recivedTextBox.AppendText(dataTermintaorBuffer + '\n');
                            //}));
                            //dataTermintaorBuffer = "";
                        }
                    }
                    else
                    {
                        dataTermintaorBuffer += (char)sp.ReadChar();
                        if (dataTermintaorBuffer.Contains(ownTerminator))
                        {
                            TextBoxOutput.AppendText(dataTermintaorBuffer.Substring(0, dataTermintaorBuffer.Length - 2) + '\n');
                            //recivedTextBox.Invoke(new Action(delegate ()
                            //{
                            //    recivedTextBox.AppendText(dataTermintaorBuffer.Substring(0, dataTermintaorBuffer.Length - 2) + '\n');
                            //}));
                            //dataTermintaorBuffer = "";
                        }
                    }
                }
            }
            else
            {
                SerialPort sp = (SerialPort)sender;
                TextBoxOutput.AppendText(sp.ReadExisting());
                //recivedTextBox.Invoke(new Action(delegate () {
                //    recivedTextBox.AppendText(sp.ReadExisting());
                //}));
            }
        }

        private void SerialPort1_PinChanged(object sender, SerialPinChangedEventArgs e)
        {
            dsrCstListenerHandler();
        }

        private void dsrCstListenerHandler()
        {
            if (serialPort1.CDHolding)
                LampDSR.Background = Brushes.Green;
            else
                LampDSR.Background = Brushes.Red;
            if (serialPort1.CtsHolding)
                LampCTS.Background = Brushes.Green;
            else
                LampCTS.Background = Brushes.Red;
        }

    }
}
