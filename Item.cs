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
                return this.Address.ToString("x8").ToUpper();
            }
        }

        public uint Value { get; set; }

        public string ValueHex
        {
            get
            {
                return this.Value.ToString("x8").ToUpper();
            }
        }

        public uint Equipped { get; set; }

        public bool EquippedBool
        {
            get
            {
                try
                {
                    var val = BitConverter.GetBytes(this.Equipped).Reverse().First().ToString("x8");
                    return val != "0";
                }
                catch
                {
                    return false;
                }
                
            }
        }

        public uint Modifier1 { get; set; }

        public string Modifier1Hex
        {
            get
            {
                return this.Modifier1.ToString("x8").ToUpper();
            }
        }

        public uint Modifier2 { get; set; }

        public string Modifier2Hex
        {
            get
            {
                return this.Modifier2.ToString("x8").ToUpper();
            }
        }

        public uint Modifier3 { get; set; }

        public string Modifier3Hex
        {
            get
            {
                return this.Modifier3.ToString("x8").ToUpper();
            }
        }

        public uint Modifier4 { get; set; }

        public string Modifier4Hex
        {
            get
            {
                return this.Modifier4.ToString("x8").ToUpper();
            }
        }

        public uint Modifier5 { get; set; }

        public string Modifier5Hex
        {
            get
            {
                return this.Modifier5.ToString("x8").ToUpper();
            }
        }

        public int Page { get; set; }

        public int Unknown { get; set; }
    }
}

/*X00000YY
X = Modifier Level (0 = Level 1, 8 = Level 2)
YY = Modifier Type
--
01 = Attack Up
02 = Durability Up
04 = Critical Hit
08 = Long Throw
10 = Five-Shot Burst
20 = Zoom x3
40 = Quick Shot
80 = Shield Surf Up*/