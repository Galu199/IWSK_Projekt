using System.Collections.Generic;
using System.Text;

namespace IWSK_RS232
{
    public class Command
    {
        public byte addres { get; set; }
        public byte function { get; set; }
        public string data { get; }
        public byte crc { get; set; }

        public Command(byte addres, byte function, string data)
        {
            this.addres = addres;
            this.function = function;
            this.data = data;
            this.crc = calculateCRC();
        }

        public Command(byte addres, byte function, string data, byte crc)
        {
            this.addres = addres;
            this.function = function;
            this.data = data;
            this.crc = crc;
        }

        public string toSend()
        {
            return ":" + this.addres.ToString("x2") + this.function.ToString("x2") + this.data + this.crc.ToString("x2") + "\r\n";
        }

        public string toString()
        {
            return toSend().Replace("\n", "\\n").Replace("\r", "\\r");
        }

        private byte calculateCRC()
        {
            List<byte> toCheck = new List<byte>();

            toCheck.Add(this.addres);
            toCheck.Add(this.function);
            if (this.data != null) toCheck.AddRange(Encoding.ASCII.GetBytes(this.data));
            return Crc8.ComputeChecksum(toCheck.ToArray());
        }

        public bool validate()
        {
            List<byte> toCheck = new List<byte>();

            toCheck.Add(this.addres);
            toCheck.Add(this.function);
            if (this.data != null) toCheck.AddRange(Encoding.ASCII.GetBytes(this.data));

            return this.crc == Crc8.ComputeChecksum(toCheck.ToArray()) ? true : false;
        }
    }
}
