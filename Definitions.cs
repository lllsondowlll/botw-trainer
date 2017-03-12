namespace BotwTrainer
{
    using System;
    using System.Linq;

    public class Definitions
    {
        public string Inventory
        {
            get
            {
                var Main = new MainWindow();
                var a = Main.Name;
                var b = "-";
                if (a == "BombArrow A")
                {
                    b = "Bomb Arrow";
                }
                else if (a == "ElectricArrow")
                {
                    b = "Shock Arrow";
                }
                else if (a == "FireArrow")
                {
                    b = "Fire Arrow";
                }
                else if (a == "NormalArrow")
                {
                    b = "Arrow";
                }
                else if (a == "Weapon Lsword 060")
                {
                    b = "Fierce Deity Sword";
                }
                else if (a == "Armor 229 Upper")
                {
                    b = "Fierce Deity Armor";
                }
                else if (a == "Armor 229 Lower")
                {
                    b = "Fierce Deity Boots";
                }
                else if (a == "Armor 229 Head")
                {
                    b = "Fierce Deity Mask";
                }
                else b = a;
                return b.ToString();
            }
        }
    }
}