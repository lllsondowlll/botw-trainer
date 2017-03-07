namespace BotwTrainer
{
    using System;
    using System.Linq;

    public class Item
    {
        public uint NameStart { get; set; }

        public string Name { get; set; }

        public uint Address { get; set; }

        public string AddressHex
        {
            get
            {
                return this.Address.ToString("X");
            }
        }

        public uint Value { get; set; }

        public string ValueHex
        {
            get
            {
                return this.Value.ToString("X");
            }
        }

        public uint Equipped { get; set; }

        public bool EquippedBool
        {
            get
            {
                try
                {
                    var val = BitConverter.GetBytes(this.Equipped).Reverse().First().ToString("X");
                    return val != "0";
                }
                catch
                {
                    return false;
                }
                
            }
        }

        public uint ModType { get; set; }

        public string ModTypeHex
        {
            get
            {
                return this.ModType.ToString("X");
            }
        }

        public uint ModAmount { get; set; }

        public string ModAmountHex
        {
            get
            {
                return this.ModAmount.ToString("X");
            }
        }

        public int Page { get; set; }

        public int Unknown { get; set; }
    }
}
