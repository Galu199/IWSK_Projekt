using System;
using System.Linq;
using System.IO.Ports;
using System.Diagnostics;

namespace IWSK_RS232
{
    class ModbusMaster
    {
        SerialPort serialPort;

        private int transactionTimeout;

        private string messageBuffer;

        enum stateMaster { idle, litener, recivingCommand, recivedCommand, errorCommand }
        stateMaster masterState = stateMaster.idle;

        public Command commandToSend { get; set; }
        public Command recivedCommad { get; set; }

        bool badCRC = false;

        Stopwatch characterTimeoutTimer = new Stopwatch();
        int characterTimeout;

        public event EventHandler<ModbusEventArgs> RequestHandler;
        public event EventHandler<ModbusEventArgs> ResponseHandler;
        public event EventHandler<ModbusEventArgs> BadCrcHandler;
        public event EventHandler<ModbusEventArgs> TimeoutHandler;
        public event EventHandler<ModbusEventArgs> Function2Hanlder;

        public ModbusMaster(SerialPort serialPort)
        {
            this.serialPort = serialPort;
            this.serialPort.DataReceived += new SerialDataReceivedEventHandler(reciver);
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
            if (masterState == stateMaster.litener & letter == ':')
            {
                masterState = stateMaster.recivingCommand;
                characterTimeoutTimer = new Stopwatch();
                characterTimeoutTimer.Start();
            }
            // reciver i jesli crlf to koniec i czas na przetwarzanie danych 
            else if (masterState == stateMaster.recivingCommand)
            {
                //sprawdzic timeout
                System.Threading.Thread.Sleep(20);
                if (characterTimeoutTimer.ElapsedMilliseconds > characterTimeout)
                {
                    //timeout
                    characterTimeoutTimer.Stop();
                    masterState = stateMaster.litener;
                    messageBuffer = "";
                    serialPort.ReadExisting();
                    TimeoutHandler(this, new ModbusEventArgs("Brak ciągłości ramki"));
                    return;
                }

                messageBuffer += letter;
                if (messageBuffer.Contains("\r\n"))
                {
                    messageBuffer = messageBuffer.Remove(messageBuffer.Length - 2);
                    if (!resolveComand())
                        masterState = stateMaster.errorCommand;
                    else
                        masterState = stateMaster.recivedCommand;
                    characterTimeoutTimer.Stop();
                }
            }
            characterTimeoutTimer.Restart();
        }

        private bool resolveComand()
        {
            string addresASCII = messageBuffer.Substring(0, 2);
            string functionASCII = messageBuffer.Substring(2, 2);

            string dataASCII = messageBuffer.Substring(4, messageBuffer.Length - 2 - 4);
            string crcASCII = messageBuffer.Substring(messageBuffer.Length - 2, 2);
            messageBuffer = "";
            // sprawdzenie summy kontrolnej
            recivedCommad = new Command(
                Convert.ToByte(addresASCII, 16),
                Convert.ToByte(functionASCII, 16),
                dataASCII,
                Convert.ToByte(crcASCII, 16));
            return recivedCommad.validate();
        }

        internal void setTransactionTimeout(int transmisionTimeout, int charTimeout)
        {
            this.transactionTimeout = transmisionTimeout;
            this.characterTimeout = charTimeout;
        }

        internal void prepareFunction1(int addres, string data)
        {
            commandToSend = new Command((byte)addres, 1, data);
        }

        internal void prepareFunction2(int addres, string data)
        {
            commandToSend = new Command((byte)addres, 2, data);
        }

        internal void Transaction(int retransmission)
        {
            // zrobienie transakkcji
            if (serialPort.IsOpen)
            {
                masterState = stateMaster.litener;
                if (badCRC)
                    commandToSend.crc = 0;
                serialPort.Write(commandToSend.toSend());
                RequestHandler(this, new ModbusEventArgs(requestDebug()));
                if (commandToSend.addres == 0)
                {
                    return;
                }
                Stopwatch sw = new Stopwatch();
                sw.Start();
                while (masterState != stateMaster.recivedCommand)
                {
                    if (masterState == stateMaster.errorCommand)
                    {
                        BadCrcHandler(this, new ModbusEventArgs(responseDebug()));
                        break;
                    }
                    //sprawdzenie timeout
                    if (sw.ElapsedMilliseconds > this.transactionTimeout)
                    {
                        masterState = stateMaster.idle;
                        TimeoutHandler(this, new ModbusEventArgs("Transaction Timeout " + retransmission.ToString()));
                        messageBuffer = "";
                        throw new TimeoutException();
                    }
                }
                if (masterState == stateMaster.recivedCommand)
                {
                    ResponseHandler(this, new ModbusEventArgs(responseDebug()));
                    if (commandToSend.function == 2)
                    {
                        Function2Hanlder(this, new ModbusEventArgs(recivedCommad.data));
                    }
                }
                masterState = stateMaster.idle;
                sw.Stop();

            }
        }

        public string requestDebug()
        {
            if (commandToSend != null)
                return commandToSend.toString();
            else
                return "";
        }

        public string responseDebug()
        {
            if (recivedCommad != null)
                return recivedCommad.toString();
            else
                return "";
        }

        internal void setBadCRC(bool Checked)
        {
            this.badCRC = Checked;
        }
    }
}
