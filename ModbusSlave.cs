using System;
using System.Diagnostics;
using System.IO.Ports;

namespace IWSK_RS232
{
    partial class ModbusSlave
    {
        SerialPort serialPort;

        private int addres;

        private string messageBuffer;

        public string function2Message { get; set; }

        Stopwatch characterTimeoutTimer = new Stopwatch();
        int characterTimeout;
        enum state { idle, reciver, resolve }
        state slaveState = state.idle;

        bool badCRC = false;

        public event EventHandler<ModbusEventArgs> Function1Event;
        public event EventHandler<ModbusEventArgs> RequestDebugHandler;
        public event EventHandler<ModbusEventArgs> ResponseDebugHandler;
        public event EventHandler<ModbusEventArgs> TimeoutHandler;
        public event EventHandler<ModbusEventArgs> BadCrcHandler;

        public ModbusSlave(SerialPort serialPort)
        {
            this.serialPort = serialPort;
            this.serialPort.DataReceived += new SerialDataReceivedEventHandler(reciver);
        }

        public void open(int addres)
        {
            this.addres = addres;
            if (!serialPort.IsOpen)
                this.serialPort.Open();
        }

        public void close()
        {
            if (serialPort.IsOpen)
                this.serialPort.Close();
        }

        void reciver(object sender, SerialDataReceivedEventArgs e)
        {
            while (serialPort.BytesToRead > 0)
            {
                reciveChar((char)serialPort.ReadByte());
            }
        }

        public void reciveChar(char letter)
        {

            // stan idle i pierwszy znak 
            if (slaveState == state.idle & letter == ':')
            {
                slaveState = state.reciver;
                characterTimeoutTimer = new Stopwatch();
                characterTimeoutTimer.Start();
            }
            // reciver i jesli crlf to koniec i czas na przetwarzanie danych 
            else if (slaveState == state.reciver)
            {
                //sprawdzic timeout
                System.Threading.Thread.Sleep(20);
                if (characterTimeoutTimer.ElapsedMilliseconds > characterTimeout)
                {
                    //timeout
                    characterTimeoutTimer.Stop();
                    slaveState = state.idle;
                    messageBuffer = "";
                    serialPort.ReadExisting();
                    TimeoutHandler(this, new ModbusEventArgs("Brak ciągłości ramki"));
                    return;
                }
                //sprawdzic czy jest crfl
                messageBuffer += letter;
                if (messageBuffer.Contains("\r\n"))
                {
                    messageBuffer = messageBuffer.Remove(messageBuffer.Length - 2);
                    slaveState = state.resolve;
                    resolveRequest();
                    messageBuffer = "";
                    slaveState = state.idle;
                    characterTimeoutTimer.Stop();
                }
            }
            characterTimeoutTimer.Restart();
        }

        private void resolveRequest()
        {
            if (messageBuffer.Length < 6)
                return;
            string addresASCII = messageBuffer.Substring(0, 2);
            string functionASCII = messageBuffer.Substring(2, 2);

            string dataASCII = messageBuffer.Substring(4, messageBuffer.Length - 2 - 4);
            string crcASCII = messageBuffer.Substring(messageBuffer.Length - 2, 2);

            // sprawdzenie summy kontrolnej
            Command command = new Command(
                Convert.ToByte(addresASCII, 16),
                Convert.ToByte(functionASCII, 16),
                dataASCII,
                Convert.ToByte(crcASCII, 16));
            RequestDebugHandler(this, new ModbusEventArgs(command.toString()));
            if (!command.validate())
            {
                //event bad CRC
                BadCrcHandler(this, new ModbusEventArgs(command.toString()));
                return;
            }
            // czy to adres rozgłoszeniowy lub nasz adres
            if (command.addres == 0 | command.addres == addres)
            {
                resolveCommand(command);
            }
        }

        internal void setCharacterTimeout(int timeout)
        {
            this.characterTimeout = timeout;
        }

        private void resolveCommand(Command command)
        {
            switch (command.function)
            {
                case 1:
                    function01(command);
                    slaveState = state.idle;
                    break;
                case 2:
                    function02(command);
                    slaveState = state.idle;
                    break;
                default:
                    break;
            }
        }

        private void function02(Command cmd)
        {
            //odczytuje z tekst boxa i odsyła
            Command response = new Command(cmd.addres, cmd.function, function2Message);
            if (badCRC) response.crc = 0;
            serialPort.Write(response.toSend());
            ResponseDebugHandler(this, new ModbusEventArgs(response.toString()));
        }

        protected virtual void function01(Command cmd)
        {
            Function1Event(this, new ModbusEventArgs(cmd.data));
            if (cmd.addres != 0)
            {
                if (badCRC) cmd.crc = 0;
                serialPort.Write(cmd.toSend());
                ResponseDebugHandler(this, new ModbusEventArgs(cmd.toString()));
            }
        }

        internal void setBadCRC(bool Checked)
        {
            this.badCRC = Checked;
        }
    }
}
