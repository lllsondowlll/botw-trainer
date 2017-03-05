namespace BotwTrainer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Documents;
    using System.Windows.Input;

    using BotwTrainer.Properties;

    /// <summary>
    /// Interaction logic for MainWindow
    /// </summary>
    public partial class MainWindow : Window
    {
        private const uint SaveItemStart = 0x3FCE7FF0;

        private const uint ItemEnd = 0x43CA2AEC;

        private const uint ItemStart = ItemEnd - (0x140 * 0x220);

        private TCPGecko tcpGecko;

        private bool connected = false;

        private bool dataLoaded = false;

        private List<Item> items;

        public MainWindow()
        {
            this.InitializeComponent();

            IpAddress.Text = Settings.Default.IpAddress;
        }

        private enum Cheat
        {
            Stamina = 0,
            Health = 1,
            Run = 2,
            Rupees = 3
        }

        private void ConnectClick(object sender, RoutedEventArgs e)
        {
            this.tcpGecko = new TCPGecko(this.IpAddress.Text, 7331);

            try
            {
                this.connected = this.tcpGecko.Connect();

                if (this.connected)
                {
                    Settings.Default.IpAddress = IpAddress.Text;
                    Settings.Default.Save();

                    this.ToggleControls();
                    this.Continue.Visibility = Visibility.Visible;
                }
            }
            catch (ETCPGeckoException ex)
            {
                MessageBox.Show(ex.Message);
            }
            catch (System.Net.Sockets.SocketException)
            {
                MessageBox.Show("Wrong IP");
            }
        }

        private void ToggleControls()
        {
            this.IpAddress.IsEnabled = !this.connected;
            this.Connect.IsEnabled = !this.connected;
            this.Disconnect.IsEnabled = this.connected;
            this.TabControl.IsEnabled = this.connected;
            this.Load.IsEnabled = this.connected;

            if (!this.connected)
            {
                Save.IsEnabled = false;
            }
        }

        private void DisconnectClick(object sender, RoutedEventArgs e)
        {
            try
            {
                this.connected = this.tcpGecko.Disconnect();

                this.ToggleControls();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void LoadTab(TabItem tab, IEnumerable<int> pages, bool clear = false)
        {
            var panel = new WrapPanel { Name = "PanelContent" };

            var x = 1;

            foreach (var page in pages)
            {
                var list = this.items.Where(i => i.Page == page).OrderByDescending(i => i.Address).ToList();

                foreach (var item in list)
                {
                    var label = string.Format("Item {0}:", x);
                    var value = item.Value;
                    if (value > int.MaxValue)
                    {
                        value = 0;
                    }

                    panel.Children.Add(new Label
                                           {
                                               Content = label,
                                               ToolTip = item.Address.ToString("X"),
                                               Width = 55,
                                               Margin = new Thickness(15, 20, 5, 30)
                                           });

                    var tb = new TextBox
                                 {
                                     Text = value.ToString(),
                                     Width = 60,
                                     Height = 22,
                                     Margin = new Thickness(0, 20, 10, 30),
                                     Name = "Item_" + item.AddressHex
                                 };

                    var check = (TextBox)this.FindName("Item_" + item.AddressHex);
                    if (check != null)
                    {
                        this.UnregisterName("Item_" + item.AddressHex);
                    }

                    this.RegisterName("Item_" + item.AddressHex, tb);

                    panel.Children.Add(tb);

                    x++;
                }
            }

            if (tab.Name == "Materials")
            {
                MaterialsContent.Content = panel;
                return;
            }

            tab.Content = panel;
        }

        private void LoadData()
        {
            if (this.dataLoaded)
            {
                // Refresh?
            }

            this.items = new List<Item>();

            uint end = ItemEnd;

            while (end >= ItemStart)
            {
                var page = this.tcpGecko.peek(end);
                if (page > 9)
                {
                    break;
                }

                var item = new Item
                {
                    Address = end,
                    Page = Convert.ToInt32(page),
                    Unknown = Convert.ToInt32(this.tcpGecko.peek(end + 0x4)),
                    Value = this.tcpGecko.peek(end + 0x8),
                    Equipped = this.tcpGecko.peek(end + 0xC),
                    ModAmount = this.tcpGecko.peek(end + 0x5C),
                    ModType = this.tcpGecko.peek(end + 0x64),
                };

                this.items.Add(item);

                end -= 0x220;
            }

            this.DebugData();

            this.LoadTab(this.Weapons, new[] { 0 });
            this.LoadTab(this.BowsArrows, new[] { 1, 2 });
            this.LoadTab(this.Shields, new[] { 3 });
            this.LoadTab(this.Armour, new[] { 4, 5, 6 });
            this.LoadTab(this.Materials, new[] { 7 });
            this.LoadTab(this.Food, new[] { 8 });
            this.LoadTab(this.KeyItems, new[] { 9 });

            CurrentRupees.Text = this.tcpGecko.peek(0x4010AA0C).ToString();

            this.Save.IsEnabled = true;

            this.dataLoaded = true;
        }

        private void DebugData()
        {
            //DebugLog.Document.Blocks.Clear();

            //DebugLog.Document.Blocks.Add(new Paragraph(new Run("Total items: " + this.items.Count)));

            foreach (var item in this.items.OrderByDescending(x => x.Address))
            {
                var paragraph = new Paragraph
                {
                    Margin = new Thickness(0),
                    Padding = new Thickness(0)
                };

                paragraph.Inlines.Add("Address: " + item.Address.ToString("x").ToUpper());
                paragraph.Inlines.Add(" | Page: " + item.Page);
                paragraph.Inlines.Add(" | ?: " + item.Unknown);
                paragraph.Inlines.Add(" | Value: " + item.Value);
                paragraph.Inlines.Add(" | Equipped: " + item.Equipped.ToString("X"));
                paragraph.Inlines.Add(" | Mod Amt.: " + item.ModAmount.ToString("X"));
                paragraph.Inlines.Add(" | Mod Type: " + item.ModType.ToString("X"));

                //DebugLog.Document.Blocks.Add(paragraph);
            }

            DebugGrid.ItemsSource = this.items;
            

            var stamina1 = this.tcpGecko.peek(0x42439594).ToString("X");
            var stamina2 = this.tcpGecko.peek(0x42439598).ToString("X");
            this.StaminaData.Content = string.Format("[0x42439594 = {0}, 0x42439598 = {1}]", stamina1, stamina2);

            var health = this.tcpGecko.peek(0x439B6558);
            this.HealthData.Content = string.Format("0x439B6558 = {0}", health);

            var run = this.tcpGecko.peek(0x43A88CC4).ToString("X");
            this.RunData.Content = string.Format("Wrong address - 0x43A88CC4 = {0}", run);

            var rupee1 = this.tcpGecko.peek(0x3FC92D10);
            var rupee2 = this.tcpGecko.peek(0x4010AA0C);
            var rupee3 = this.tcpGecko.peek(0x40E57E78);
            this.RupeeData.Content = string.Format("[0x3FC92D10 = {0}, 0x4010AA0C = {1}, 0x40E57E78 = {2}]", rupee1, rupee2, rupee3);
        }

        private void LoadClick(object sender, RoutedEventArgs e)
        {
            this.LoadData();
        }

        private void SaveClick(object sender, RoutedEventArgs e)
        {
            var tab = (TabItem)TabControl.SelectedItem;

            if (Equals(tab, this.Weapons) || Equals(tab, this.Shields) || Equals(tab, this.Armour) || Equals(tab, this.Debug))
            {
                var weaponsList = this.items.Where(x => x.Page == 0).ToList();
                var bowList = this.items.Where(x => x.Page == 1).ToList();
                var shieldList = this.items.Where(x => x.Page == 3).ToList();

                var y = 0;
                if (Equals(tab, this.Weapons))
                {
                    foreach (var item in weaponsList)
                    {
                        var foundTextBox = (TextBox)this.FindName("Item_" + item.AddressHex);
                        if (foundTextBox != null)
                        {
                            uint offset = (uint)(SaveItemStart + (y * 0x8));

                            this.tcpGecko.poke32(offset, Convert.ToUInt32(foundTextBox.Text));
                        }
                        y++;
                    }
                }

                if (Equals(tab, this.BowsArrows))
                {
                    foreach (var item in bowList)
                    {
                        var foundTextBox = (TextBox)this.FindName("Item_" + item.AddressHex);
                        if (foundTextBox != null)
                        {
                            uint offset = (uint)(SaveItemStart + (y * 0x8));

                            this.tcpGecko.poke32(offset, Convert.ToUInt32(foundTextBox.Text));
                        }
                        y++;
                    }
                }
            }

            if (Equals(tab, this.BowsArrows) || Equals(tab, this.Materials) || Equals(tab, this.Food) || Equals(tab, this.KeyItems))
            {
                var page = 0;

                if (Equals(tab, this.BowsArrows))
                {
                    page = 2;
                }

                if (Equals(tab, this.Materials))
                {
                    page = 7;
                }

                if (Equals(tab, this.Food))
                {
                    page = 8;
                }

                if (Equals(tab, this.KeyItems))
                {
                    page = 9;
                }

                foreach (var item in this.items.Where(x => x.Page == page))
                {
                    var foundTextBox = (TextBox)this.FindName("Item_" + item.AddressHex);
                    if (foundTextBox != null)
                    {
                        this.tcpGecko.poke32(item.Address + 0x8, Convert.ToUInt32(foundTextBox.Text));
                    }
                }
            }

            if (Equals(tab, this.Other))
            {
                var selected = new List<Cheat>();

                if (Stamina.IsChecked == true)
                {
                    selected.Add(Cheat.Stamina);
                }

                if (Health.IsChecked == true)
                {
                    selected.Add(Cheat.Health);
                }

                if (Run.IsChecked == true)
                {
                    selected.Add(Cheat.Run);
                }

                if (Rupees.IsChecked == true)
                {
                    selected.Add(Cheat.Rupees);
                }

                this.SetCheats(selected);
            }
        }

        private void SetCheats(List<Cheat> cheats)
        {
            /*
            Code List Starting Address = 01133000
            Code List End Address = 01134300
            Code Handler Enabled Address = 10014CFC
            */

            this.tcpGecko.poke32(0x10014CFC, 0x00000000);

            uint start = 0x01133000;
            uint end = 01134300;

            // clear current
            var c = start;
            while (c <= end)
            {
                this.tcpGecko.poke32(start, 0x0);
                c += 0x4;
            }

            var codes = new List<uint>();

            if (cheats.Contains(Cheat.Stamina))
            {
                codes.Add(0x00020000);
                codes.Add(0x42439594);
                codes.Add(0x453B8000);
                codes.Add(0x00000000);

                codes.Add(0x00020000);
                codes.Add(0x42439598);
                codes.Add(0x453B8000);
                codes.Add(0x00000000);
            }

            if (cheats.Contains(Cheat.Health))
            {
                codes.Add(0x00020000);
                codes.Add(0x439B6558);
                codes.Add(0x00000078);
                codes.Add(0x00000000);
            }

            if (cheats.Contains(Cheat.Run))
            {
                codes.Add(0x00020000);
                codes.Add(0x43A88CC4);
                codes.Add(0x3FC00000);
                codes.Add(0x00000000);
            }

            if (cheats.Contains(Cheat.Rupees))
            {
                uint value = Convert.ToUInt32(CurrentRupees.Text);

                codes.Add(0x00020000);
                codes.Add(0x3FC92D10);
                codes.Add(value);
                codes.Add(0x00000000);

                codes.Add(0x00020000);
                codes.Add(0x4010AA0C);
                codes.Add(value);
                codes.Add(0x00000000);

                codes.Add(0x00020000);
                codes.Add(0x40E57E78);
                codes.Add(value);
                codes.Add(0x00000000);
            }

            foreach (var code in codes)
            {
                this.tcpGecko.poke32(start, code);
                start += 0x4;
            }

            this.tcpGecko.poke32(0x10014CFC, 0x00000001);
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }
}
