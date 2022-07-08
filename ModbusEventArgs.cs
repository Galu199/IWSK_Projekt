namespace IWSK_RS232
{
    public class ModbusEventArgs
    {
        public string message { get; }
        public ModbusEventArgs(string data)
        {
            this.message = data;
        }
    }
}