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
        static string sendBuffer = "";
        string ownTerminator;
        string dataTermintaorBuffer;
        bool dsrCstThreadContinue = false;
        bool rtsCtsHandshake = false;
        bool dtrDsrHandshake = false;
        bool XonXoffHandshake = false;
        bool pingMode = false;
        bool pingReady = true;

        enum SelectTerminator { None, CR, LF, CRLF, OWN };
        SelectTerminator terminator;

        SerialPort serialPort;

        public MainWindow()
        {
            InitializeComponent();
            serialPort = (SerialPort)Resources["serialPort"];
            masterBox.IsEnabled = false;
            slaveBox.IsEnabled = false;
        }

        private void ComboBoxPort_DropDownOpened(object sender, EventArgs e)
        {
            ComboBoxPort.Items.Clear();
            Array.ForEach(SerialPort.GetPortNames(), s => ComboBoxPort.Items.Add(s));
        }

        private void ButtonOpen_Click(object sender, RoutedEventArgs e)
        {
            if (serialPort.IsOpen)
            {
                MessageBox.Show($"Port {serialPort.PortName} is already open");
                return;
            }
            try
            {
                string portName = Convert.ToString(ComboBoxPort.SelectedItem);
                serialPort = new SerialPort();
                serialPort.PortName = portName;
                serialPort.WriteTimeout = 1000;
                serialPort.ReadTimeout = 1000;
                serialPort.BaudRate = Convert.ToInt32(ComboBoxSpeed.Text);
                serialPort.DataBits = Convert.ToInt32(ComboBoxBitsCount.SelectedValue.ToString());
                serialPort.Parity = (Parity)ComboBoxParityBit.SelectedIndex;
                serialPort.StopBits = (StopBits)(ComboBoxStopBit.SelectedIndex + 1);
                switch (ComboBoxFlowControl.SelectedIndex)
                {
                    case 3:
                        dtrDsrHandshake = true;
                        rtsCtsHandshake = false;
                        XonXoffHandshake = false;
                        serialPort.PinChanged += new SerialPinChangedEventHandler(portPinChanged);
                        serialPort.DtrEnable = true;
                        LampDTR.Background = Brushes.Green;

                        break;
                    case 2:
                        dtrDsrHandshake = false;
                        rtsCtsHandshake = true;
                        XonXoffHandshake = false;
                        serialPort.PinChanged += new SerialPinChangedEventHandler(portPinChanged);
                        serialPort.RtsEnable = true;
                        LampRTS.Background = Brushes.Green;
                        serialPort.Handshake = (Handshake)Enum.ToObject(typeof(Handshake), ComboBoxFlowControl.SelectedIndex);
                        break;
                    case 1:
                        dtrDsrHandshake = false;
                        rtsCtsHandshake = false;
                        XonXoffHandshake = true;
                        serialPort.Handshake = (Handshake)Enum.ToObject(typeof(Handshake), ComboBoxFlowControl.SelectedIndex);
                        break;
                    case 0:
                    default:
                        dtrDsrHandshake = false;
                        rtsCtsHandshake = false;
                        XonXoffHandshake = false;
                        serialPort.Handshake = (Handshake)Enum.ToObject(typeof(Handshake), ComboBoxFlowControl.SelectedIndex);
                        break;
                }

                terminator = (SelectTerminator)Enum.ToObject(typeof(SelectTerminator), ComboBoxTerminator.SelectedIndex);
                if (terminator == SelectTerminator.OWN)
                {
                    ownTerminator = TextBoxTerminatorCustom.Text.Length < 2 ? "\r\n" : TextBoxTerminatorCustom.Text;
                }

                serialPort.ReadBufferSize = 4096;
                serialPort.Open();

                dsrCstThreadContinue = true;

                serialPort.DataReceived += DataReceivedHandler;
                serialPort.PinChanged += SerialPort1_PinChanged;

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
                    serialPort.Write(((char)17).ToString());
                    LampX.Background = Brushes.Green;
                }
                if (ComboBoxFlowControl.SelectedIndex == 0)
                {
                    ButtonRTS.IsEnabled = true;
                    ButtonDTR.IsEnabled = true;
                    ButtonXON.IsEnabled = true;
                    ButtonXOFF.IsEnabled = true;
                }
                ButtonClose.IsEnabled = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                MessageBox.Show(ex.Message);
            }
        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            if (serialPort.IsOpen)
            {
                dsrCstThreadContinue = false;
                serialPort.Close();
                portSelectBox.IsEnabled = true;
                ButtonPingMode.IsEnabled = false;

                ButtonDTR.IsEnabled = false;
                ButtonRTS.IsEnabled = false;
                ButtonXON.IsEnabled = false;
                ButtonXOFF.IsEnabled = false;

                ButtonClose.IsEnabled = false;

                LampDTR.Background = Brushes.Red;
                LampDSR.Background = Brushes.Red;
                LampCTS.Background = Brushes.Red;
                LampRTS.Background = Brushes.Red;
                LampX.Background = Brushes.Red;
            }
        }

        private void ButtonSend_Click(object sender, RoutedEventArgs e)
        {
            string dataToSend = TextBoxInput.Text;

            if (serialPort.IsOpen && dataToSend != "")
            {
                if (terminator == SelectTerminator.CR)
                    dataToSend += '\r';
                else if (terminator == SelectTerminator.LF)
                    dataToSend += '\n';
                else if (terminator == SelectTerminator.CRLF)
                    dataToSend += "\r\n";
                else if (terminator == SelectTerminator.OWN)
                    dataToSend += ownTerminator;
                if (dtrDsrHandshake)
                {
                    if (serialPort.CDHolding)
                    {
                        serialPort.Write(dataToSend);
                    }
                    else
                    {
                        sendBuffer += dataToSend;
                    }
                }
                else if (rtsCtsHandshake)
                {
                    if (serialPort.CtsHolding)
                    {
                        serialPort.Write(dataToSend);
                    }
                    else
                    {
                        sendBuffer += dataToSend;
                    }
                }
                else
                {
                    serialPort.Write(dataToSend);
                }
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
            try
            {
                Stopwatch sw = new Stopwatch();
                pingReady = false;
                sw.Start();
                serialPort.Write("\r");
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
            catch (Exception ex)
            {
                MessageBox.Show($"Exception: {ex.Message}");
                Console.WriteLine(ex.Message);
            }
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
            try
            {
                serialPort.DtrEnable = !serialPort.DtrEnable;
                if (serialPort.DtrEnable) LampDTR.Background = Brushes.Green;
                else LampDTR.Background = Brushes.Red;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Exception: {ex.Message}");
                Console.WriteLine(ex.Message);
            }
        }

        private void ButtonRTS_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                serialPort.RtsEnable = !serialPort.RtsEnable;
                if (serialPort.RtsEnable) LampRTS.Background = Brushes.Green;
                else LampRTS.Background = Brushes.Red;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Exception: {ex.Message}");
                Console.WriteLine(ex.Message);
            }
        }

        private void ButtonXON_Click(object sender, RoutedEventArgs e)
        {
            serialPort.Write(((char)17).ToString());
            LampX.Background = Brushes.Green;
        }

        private void ButtonXOFF_Click(object sender, RoutedEventArgs e)
        {
            serialPort.Write(((char)19).ToString());
            LampX.Background = Brushes.Red;
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (serialPort.IsOpen)
            {
                serialPort.Close();
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
                            TextBoxOutput.Dispatcher.Invoke(() => TextBoxOutput.AppendText(dataTermintaorBuffer + '\n'));
                            dataTermintaorBuffer = "";
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
                            TextBoxOutput.Dispatcher.Invoke(() => TextBoxOutput.AppendText(dataTermintaorBuffer + '\n'));
                            dataTermintaorBuffer = "";
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
                            TextBoxOutput.Dispatcher.Invoke(() => TextBoxOutput.AppendText(dataTermintaorBuffer + '\n'));
                            dataTermintaorBuffer = "";
                        }
                    }
                    else
                    {
                        dataTermintaorBuffer += (char)sp.ReadChar();
                        if (dataTermintaorBuffer.Contains(ownTerminator))
                        {
                            TextBoxOutput.Dispatcher.Invoke(() =>
                                TextBoxOutput.AppendText(dataTermintaorBuffer.Substring(0, dataTermintaorBuffer.Length - 2) + '\n')
                            );
                            dataTermintaorBuffer = "";
                        }
                    }
                }
            }
            else
            {
                SerialPort sp = (SerialPort)sender;
                TextBoxOutput.Dispatcher.Invoke(() => TextBoxOutput.AppendText(sp.ReadExisting()));
            }
        }

        private void SerialPort1_PinChanged(object sender, SerialPinChangedEventArgs e)
        {
            dsrCstListenerHandler();
        }

        private void dsrCstListenerHandler()
        {
            if (serialPort.CDHolding)
                LampDSR.Dispatcher.Invoke(() => LampDSR.Background = Brushes.Green);
            else
                LampDSR.Dispatcher.Invoke(()=>LampDSR.Background = Brushes.Red);
            if (serialPort.CtsHolding)
                LampCTS.Dispatcher.Invoke(() => LampCTS.Background = Brushes.Green);
            else
                LampCTS.Dispatcher.Invoke(() => LampCTS.Background = Brushes.Red);
        }

        // MOD BUS
        ModbusSlave modbusSlave;
        ModbusMaster modbusMaster;
        const int BrushDeley = 100;

        private void ModbusOpen_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (serialPort.IsOpen)
                {
                    MessageBox.Show($"Port {serialPort.PortName} is already open");
                    return;
                }

                serialPort = new SerialPort();
                serialPort.PortName = Convert.ToString(portNameCombo.SelectedItem);
                serialPort.BaudRate = int.Parse(baudCombo.Text);

                if (RadioMaster.IsChecked == false)
                {
                    modbusSlave = new ModbusSlave(serialPort);
                    modbusSlave.Function1Event += SlaveFunction1Handler;
                    modbusSlave.RequestDebugHandler += SlaveRequestDebugHandler;
                    modbusSlave.ResponseDebugHandler += SlaveResponseDebugHandler;
                    modbusSlave.BadCrcHandler += BadCrc;
                    modbusSlave.TimeoutHandler += ModbusMaster_TimeoutHandler;
                }
                else
                {
                    modbusMaster = new ModbusMaster(serialPort);
                    modbusMaster.BadCrcHandler += BadCrc;
                    modbusMaster.RequestHandler += MasterRequestDebugHandler;
                    modbusMaster.ResponseHandler += MasterResponseDebugHandler;
                    modbusMaster.Function2Hanlder += MasterFunction2;
                    modbusMaster.TimeoutHandler += ModbusMaster_TimeoutHandler;
                    serialPort.Open();
                }

                if (RadioMaster.IsChecked == true)
                {
                    masterBox.IsEnabled = true;
                    slaveBox.IsEnabled = false;
                }
                else
                {
                    masterBox.IsEnabled = false;
                    slaveBox.IsEnabled = true;
                }

                AllMessageBox.Text = "";
                ModbusOpen.IsEnabled = false;
                RadioMaster.IsEnabled = false;
                RadioSlave.IsEnabled = false;
                portNameCombo.IsEnabled = false;
                baudCombo.IsEnabled = false;
                ModbusClose.IsEnabled = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                MessageBox.Show(ex.Message);
            }
        }

        private void ModbusClose_Click(object sender, RoutedEventArgs e)
        {
            if (serialPort.IsOpen)
            {
                serialPort.Close();
            }
            masterBox.IsEnabled = false;
            slaveBox.IsEnabled = false;
            ModbusOpen.IsEnabled = true;
            RadioMaster.IsEnabled = true;
            RadioSlave.IsEnabled = true;
            ModbusClose.IsEnabled = false;
            portNameCombo.IsEnabled = true;
            baudCombo.IsEnabled = true;
            //startListenCheck.Checked = false;
        }

        private async void SlaveFunction1Handler(object sender, ModbusEventArgs e)
        {
            label13.Dispatcher.Invoke(() => label13.Background = Brushes.Green);
            await Task.Run(async () => await Task.Delay(BrushDeley));
            slaveRecivedTexbox.Dispatcher.Invoke(() =>
                slaveRecivedTexbox.AppendText(e.message)
            );
        }

        private async void SlaveRequestDebugHandler(object sender, ModbusEventArgs e)
        {
            label13.Dispatcher.Invoke(() => label13.Background = Brushes.Cyan);
            await Task.Run(async () => await Task.Delay(BrushDeley));
            AllMessageBox.Dispatcher.Invoke(() =>
                AllMessageBox.AppendText($"Przychodzące: {e.message}{Environment.NewLine}")
            );
        }

        private async void SlaveResponseDebugHandler(object sender, ModbusEventArgs e)
        {
            label13.Dispatcher.Invoke(() => label13.Background = Brushes.Blue);
            await Task.Run(async () => await Task.Delay(BrushDeley));
            AllMessageBox.Dispatcher.Invoke(() =>
                AllMessageBox.AppendText($"Wychodzące: {e.message}{Environment.NewLine}")
            );
        }

        private async void BadCrc(object sender, ModbusEventArgs eventArgs)
        {
            label13.Dispatcher.Invoke(() => label13.Background = Brushes.Red);
            await Task.Run(async () => await Task.Delay(BrushDeley));
            MessageBox.Show($"Zła suma kontrolna: {eventArgs.message}", "CRC error");
        }

        private async void ModbusMaster_TimeoutHandler(object sender, ModbusEventArgs e)
        {
            label13.Dispatcher.Invoke(() => label13.Background = Brushes.Red);
            await Task.Run(async () => await Task.Delay(BrushDeley));
            AllMessageBox.Dispatcher.Invoke(() =>
                AllMessageBox.AppendText($"{e.message} {Environment.NewLine}")
            );
        }

        private async void MasterRequestDebugHandler(object sender, ModbusEventArgs e)
        {
            label13.Dispatcher.Invoke(() => label13.Background = Brushes.Cyan);
            await Task.Run(async () => await Task.Delay(BrushDeley));
            AllMessageBox.Dispatcher.Invoke(() =>
                AllMessageBox.AppendText($"Wychodzące: {e.message}{Environment.NewLine}")
            );
        }

        private async void MasterResponseDebugHandler(object sender, ModbusEventArgs e)
        {
            label13.Dispatcher.Invoke(() => label13.Background = Brushes.Blue);
            await Task.Run(async () => await Task.Delay(BrushDeley));
            AllMessageBox.Dispatcher.Invoke(() =>
                AllMessageBox.AppendText($"Przychodzące: {e.message}{Environment.NewLine}")
            );
        }

        private async void MasterFunction2(object sender, ModbusEventArgs e)
        {
            label13.Dispatcher.Invoke(() => label13.Background = Brushes.Purple);
            await Task.Run(async () => await Task.Delay(BrushDeley));
            masterRecivedDataTexbox.Dispatcher.Invoke(() => 
                masterRecivedDataTexbox.AppendText($"{e.message} {Environment.NewLine}")
            );
        }

        private async void ModbusOpenSlave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var adres = int.Parse(slaveAddresTexbox.Text);
                modbusSlave.setCharacterTimeout((int)(double.Parse(slaveCharacterTimeUpDown.Text) * 1000));
                modbusSlave.function2Message = slaveSendTextbox.Text;
                modbusSlave.open(adres);
                slaveAddresTexbox.IsEnabled = false;
                slaveCharacterTimeUpDown.IsEnabled = false;
                ModbusClose.IsEnabled = false;
                ModbusOpenSlave.IsEnabled = false;
                ModbusColeSlave.IsEnabled = true;
            }
            catch (FormatException)
            {
                MessageBox.Show($"Number format is required (X,XX)");
            }
            catch (Exception ex)
            {
                label13.Dispatcher.Invoke(() => label13.Background = Brushes.Red);
                await Task.Run(async () => await Task.Delay(BrushDeley));
                Console.WriteLine(ex.Message);
                MessageBox.Show($"{ex.Message}");
            }
        }

        private void ModbusColeSlave_Click(object sender, RoutedEventArgs e)
        {
            modbusSlave.close();
            slaveAddresTexbox.IsEnabled = true;
            slaveCharacterTimeUpDown.IsEnabled = true;
            ModbusClose.IsEnabled = true;
            ModbusOpenSlave.IsEnabled = true;
            ModbusColeSlave.IsEnabled = false;
        }

        private async void ModbusFunction1_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                label13.Dispatcher.Invoke(() => label13.Background = Brushes.YellowGreen);
                await Task.Run(async () => await Task.Delay(BrushDeley));
                int addres = int.Parse(addresTextBox.Text);
                string data = argTextBox.Text;
                int retransmit = int.Parse(retransmissionUpDown.Text);
                int charTimeout = (int)(double.Parse(masterCharacterTimeUpDown.Text) * 1000);
                int transmisionTimeout = (int)(double.Parse(masterTransactionTimeUpDown.Text) * 1000);
                masterRecivedDataTexbox.Text = "";
                modbusMaster.setTransactionTimeout(transmisionTimeout, charTimeout);
                modbusMaster.prepareFunction1(addres, data);
                retransmitController(retransmit);
                label13.Dispatcher.Invoke(() => label13.Background = Brushes.Green);
                await Task.Run(async () => await Task.Delay(BrushDeley));
            }
            catch (FormatException)
            {
                MessageBox.Show($"Number format is required (X,XX)");
            }
            catch (Exception ex)
            {
                label13.Dispatcher.Invoke(() => label13.Background = Brushes.Red);
                await Task.Run(async () => await Task.Delay(BrushDeley));
                Console.WriteLine(ex.Message);
                MessageBox.Show($"{ex.Message}");
            }
        }

        private async void ModbusFunction2_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                label13.Dispatcher.Invoke(() => label13.Background = Brushes.YellowGreen);
                await Task.Run(async () => await Task.Delay(BrushDeley));
                int addres = int.Parse(addresTextBox.Text);
                string data = argTextBox.Text;
                int retransmit = int.Parse(retransmissionUpDown.Text);
                int charTimeout = (int)(double.Parse(masterCharacterTimeUpDown.Text) * 1000);
                int transmisionTimeout = (int)(double.Parse(masterTransactionTimeUpDown.Text) * 1000);
                masterRecivedDataTexbox.Text = "";
                modbusMaster.setTransactionTimeout(transmisionTimeout, charTimeout);
                if (addres == 0)
                {
                    MessageBox.Show("funkcja adresowa nie może być używana dla adresu rozgłoszniowego");
                    masterRecivedDataTexbox.Text = "funkcja adresowa";
                }
                else
                {
                    modbusMaster.prepareFunction2(addres, data);
                    retransmitController(retransmit);
                }
                label13.Dispatcher.Invoke(() => label13.Background = Brushes.Green);
                await Task.Run(async () => await Task.Delay(BrushDeley));
            }
            catch (FormatException)
            {
                MessageBox.Show($"Number format is required (X,XX)");
            }
            catch (Exception ex)
            {
                label13.Dispatcher.Invoke(() => label13.Background = Brushes.Red);
                await Task.Run(async () => await Task.Delay(BrushDeley));
                Console.WriteLine(ex.Message);
                MessageBox.Show(ex.Message);
            }
        }

        private async void retransmitController(int retransmit)
        {
            for (int i = 1; i <= retransmit + 1; i++)
            {
                try
                {
                    label13.Dispatcher.Invoke(() => label13.Background = Brushes.YellowGreen);
                    await Task.Run(async () => await Task.Delay(BrushDeley));
                    modbusMaster.Transaction(i);
                    break;
                }
                catch (TimeoutException)
                {
                    label13.Dispatcher.Invoke(() => label13.Background = Brushes.Red);
                    await Task.Run(async () => await Task.Delay(BrushDeley));
                }
            }
        }

        private void slaveSendTextbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            modbusSlave.function2Message = slaveSendTextbox.Text;
        }

        private void portNameCombo_DropDownOpened(object sender, EventArgs e)
        {
            portNameCombo.Items.Clear();
            Array.ForEach(SerialPort.GetPortNames(), i => portNameCombo.Items.Add(i));
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            AllMessageBox.Clear();
        }

        private void MenuItem_Click2(object sender, RoutedEventArgs e)
        {
            TextBoxOutput.Clear();
        }

        private void MenuItem_Click3(object sender, RoutedEventArgs e)
        {
            masterRecivedDataTexbox.Clear();
        }

        private void MenuItem_Click4(object sender, RoutedEventArgs e)
        {
            slaveRecivedTexbox.Clear();
        }
    }
}
