namespace BotwTrainer
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;

    using BotwTrainer.Properties;

    /// <summary>
    /// Interaction logic for MainWindow
    /// </summary>
    public partial class MainWindow
    {
        // The original list of values that take effect when you save / load
        private const uint SaveItemStart = 0x3FCE7FF0;

        // Technically your first item as they are stored in reverse so we work backwards
        private const uint ItemEnd = 0x43CA2AEC;

        private const uint ItemStart = 0x43C6B2AC;

        private const uint CodeHandlerStart = 0x01133000;

        private const uint CodeHandlerEnd = 0x01134300;

        private const uint CodeHandlerEnabled = 0x10014CFC;

        private readonly List<Item> items;

        private readonly string version;

        private int itemsFound;

        private TCPGecko tcpGecko;

        private bool connected;

        public MainWindow()
        {
            this.InitializeComponent();

            IpAddress.Text = Settings.Default.IpAddress;
            this.version = Settings.Default.CurrentVersion;

            this.Title = string.Format("{0} v{1}", this.Title, this.version);

            var client = new WebClient { BaseAddress = Settings.Default.VersionUrl, Encoding = Encoding.UTF8 };
            client.DownloadStringCompleted += this.ClientDownloadStringCompleted;
            client.DownloadStringAsync(new Uri(string.Format("{0}{1}", client.BaseAddress, "version.txt")));

            this.items = new List<Item>();
        }

        private enum Cheat
        {
            Stamina = 0,
            Health = 1,
            Run = 2,
            Rupees = 3,
            MoonJump = 4,
            WeaponInv = 5,
            BowInv = 6,
            ShieldInv = 7,
            Speed = 8
        }

        private bool LoadDataAsync()
        {
            try
            {
                var x = 0;

                var currentItem = ItemEnd;

                while (currentItem >= ItemStart)
                {
                    // Skip FFFFFFFF invalild items. Usuauly end of the list
                    var page = this.tcpGecko.peek(currentItem);
                    if (page > 9)
                    {
                        var percent = (100m / 418m) * x;
                        Dispatcher.Invoke(
                            () =>
                                {
                                    ProgressText.Text = string.Format("{0}/{1}", x, 418);
                                    this.UpdateProgress(Convert.ToInt32(percent));
                                });

                        currentItem -= 0x220;
                        x++;

                        continue;
                    }

                    // TODO: Implement the following
                    /*
                    var stream = new MemoryStream();
                    this.tcpGecko.Dump(currentItem, currentItem + 0x70, stream);
                    stream.Seek(0, SeekOrigin.Begin);

                    var buffer = new byte[4];
                    stream.Read(buffer, 0, 4);
                    var baseAddress = ByteSwap.Swap(BitConverter.ToUInt32(buffer, 0));
                    */

                    var item = new Item
                    {
                        BaseAddress = currentItem,
                        Page = Convert.ToInt32(page),
                        Unknown = Convert.ToInt32(this.tcpGecko.peek(currentItem + 0x4)),
                        Value = this.tcpGecko.peek(currentItem + 0x8),
                        Equipped = this.tcpGecko.peek(currentItem + 0xC),
                        NameStart = this.tcpGecko.peek(currentItem + 0x1C),
                        Name = this.ReadString(currentItem + 0x1C),
                        Modifier1Value = this.tcpGecko.peek(currentItem + 0x5C).ToString("x8").ToUpper(),
                        Modifier2Value = this.tcpGecko.peek(currentItem + 0x60).ToString("x8").ToUpper(),
                        Modifier3Value = this.tcpGecko.peek(currentItem + 0x64).ToString("x8").ToUpper(),
                        Modifier4Value = this.tcpGecko.peek(currentItem + 0x68).ToString("x8").ToUpper(),
                        Modifier5Value = this.tcpGecko.peek(currentItem + 0x6C).ToString("x8").ToUpper()
                    };

                    this.items.Add(item);

                    var currentPercent = (100m / 418m) * x;
                    Dispatcher.Invoke(
                        () =>
                            {
                                ProgressText.Text = string.Format("{0}/{1}", x, 418);
                                this.UpdateProgress(Convert.ToInt32(currentPercent));
                            });

                    currentItem -= 0x220;
                    x++;
                }

                this.itemsFound = this.items.Count;

                return true;
            }
            catch (Exception)
            {
                Dispatcher.Invoke(() => this.ToggleControls("LoadError"));
                return false;
            }
        }

        private async void LoadClick(object sender, RoutedEventArgs e)
        {
            this.ToggleControls("Load");

            this.items.Clear();

            var result = await Task.Run(() => this.LoadDataAsync());

            if (result)
            {
                this.DebugData();

                this.LoadTab(this.Weapons, 0);
                this.LoadTab(this.Bows, 1);
                this.LoadTab(this.Arrows, 2);
                this.LoadTab(this.Shields, 3);
                this.LoadTab(this.Armour, 4);
                this.LoadTab(this.Materials, 7);
                this.LoadTab(this.Food, 8);
                this.LoadTab(this.KeyItems, 9);

                // Code Tab Values
                CurrentStamina.Text = this.tcpGecko.peek(0x42439598).ToString("X");
                CurrentSpeed.Text = this.tcpGecko.peek(0x439BF514).ToString("X");
                CurrentHealth.Text = this.tcpGecko.peek(0x439B6558).ToString(CultureInfo.InvariantCulture);
                CurrentRupees.Text = this.tcpGecko.peek(0x4010AA0C).ToString(CultureInfo.InvariantCulture);

                CurrentWeaponSlots.Text = this.tcpGecko.peek(0x3FCFB498).ToString(CultureInfo.InvariantCulture);
                CurrentBowSlots.Text = this.tcpGecko.peek(0x3FD4BB50).ToString(CultureInfo.InvariantCulture);
                CurrentShieldSlots.Text = this.tcpGecko.peek(0x3FCC0B40).ToString(CultureInfo.InvariantCulture);

                this.Notification.Content = string.Format("Items found: {0}", this.itemsFound);

                this.ToggleControls("DataLoaded");
            }
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

                    this.ToggleControls("Connected");
                }
            }
            catch (ETCPGeckoException ex)
            {
                MessageBox.Show(ex.Message);

                this.connected = false;
            }
            catch (System.Net.Sockets.SocketException)
            {
                MessageBox.Show("Wrong IP");

                this.connected = false;
            }
        }

        private void DisconnectClick(object sender, RoutedEventArgs e)
        {
            try
            {
                this.connected = this.tcpGecko.Disconnect();

                this.ToggleControls("Disconnected");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void SaveClick(object sender, RoutedEventArgs e)
        {
            // Grab the values from the relevant tab and poke them back to memory
            var tab = (TabItem)TabControl.SelectedItem;

            // For these we amend the 0x3FCE7FF0 area which requires save/load
            if (Equals(tab, this.Weapons) || Equals(tab, this.Bows) || Equals(tab, this.Shields) || Equals(tab, this.Armour))
            {
                var weaponList = this.items.Where(x => x.Page == 0).ToList();
                var bowList = this.items.Where(x => x.Page == 1).ToList();
                var arrowList = this.items.Where(x => x.Page == 2).ToList();
                var shieldList = this.items.Where(x => x.Page == 3).ToList();
                var armourList = this.items.Where(x => x.Page == 4 || x.Page == 5 || x.Page == 6).ToList();

                var y = 0;
                if (Equals(tab, this.Weapons))
                {
                    foreach (var item in weaponList)
                    {
                        var foundTextBox = (TextBox)this.FindName("Item_" + item.BaseAddressHex);
                        if (foundTextBox != null)
                        {
                            var offset = (uint)(SaveItemStart + (y * 0x8));
                            this.tcpGecko.poke32(offset, Convert.ToUInt32(foundTextBox.Text));
                        }

                        y++;
                    }
                }

                if (Equals(tab, this.Bows))
                {
                    // jump past weapons before we start
                    y += weaponList.Count;

                    foreach (var item in bowList)
                    {
                        var foundTextBox = (TextBox)this.FindName("Item_" + item.BaseAddressHex);
                        if (foundTextBox != null)
                        {
                            var offset = (uint)(SaveItemStart + (y * 0x8));

                            this.tcpGecko.poke32(offset, Convert.ToUInt32(foundTextBox.Text));
                        }

                        y++;
                    }
                }

                if (Equals(tab, this.Shields))
                {
                    // jump past weapons/bows/arrows before we start
                    y += weaponList.Count + bowList.Count + arrowList.Count;

                    foreach (var item in shieldList)
                    {
                        var foundTextBox = (TextBox)this.FindName("Item_" + item.BaseAddressHex);
                        if (foundTextBox != null)
                        {
                            var offset = (uint)(SaveItemStart + (y * 0x8));

                            this.tcpGecko.poke32(offset, Convert.ToUInt32(foundTextBox.Text));
                        }

                        y++;
                    }
                }

                if (Equals(tab, this.Armour))
                {
                    // jump past weapons/bows/arrows/shields before we start
                    y += weaponList.Count + bowList.Count + arrowList.Count + shieldList.Count;

                    foreach (var item in armourList)
                    {
                        var offset = (uint)(SaveItemStart + (y * 0x8));

                        var foundTextBox = (TextBox)this.FindName("Item_" + item.BaseAddressHex);
                        if (foundTextBox != null)
                        {
                            this.tcpGecko.poke32(offset, Convert.ToUInt32(foundTextBox.Text));
                        }

                        y++;
                    }
                }

                MessageBox.Show("Data sent. Please save/load the game if you changed the Item Value.");
            }

            // Here we can poke the values as it has and immediate effect
            var page = 0;
            switch (tab.Name)
            {
                case "Weapons":
                    page = 0;
                    break;
                case "Bows":
                    page = 1;
                    break;
                case "Arrows":
                    page = 2;
                    break;
                case "Shields":
                    page = 3;
                    break;
                case "Armour":
                    page = 4;
                    break;
                case "Materials":
                    page = 7;
                    break;
                case "Food":
                    page = 8;
                    break;
                case "KeyItems":
                    page = 9;
                    break;
            }

            var collection = this.items.Where(x => x.Page == page);
            if (page == 4)
            {
                collection = this.items.Where(i => i.Page == 4 || i.Page == 5 || i.Page == 6);
            }

            foreach (var item in collection)
            {
                var foundTextBox = (TextBox)this.FindName("Item_" + item.BaseAddressHex);
                if (foundTextBox != null)
                {
                    this.tcpGecko.poke32(item.BaseAddress + 0x8, Convert.ToUInt32(foundTextBox.Text));
                }

                this.FindAndPoke(item.Modifier1Address, item.BaseAddress + 0x5c);
                this.FindAndPoke(item.Modifier2Address, item.BaseAddress + 0x60);
                this.FindAndPoke(item.Modifier3Address, item.BaseAddress + 0x64);
                this.FindAndPoke(item.Modifier4Address, item.BaseAddress + 0x68);
                this.FindAndPoke(item.Modifier5Address, item.BaseAddress + 0x6C);
            }

            // For the 'Codes' tab we mimic JGecko and send cheats to codehandler
            if (Equals(tab, this.Codes))
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

                if (Rupees.IsChecked == true)
                {
                    selected.Add(Cheat.Rupees);
                }

                if (Run.IsChecked == true)
                {
                    selected.Add(Cheat.Run);
                }

                if (Speed.IsChecked == true)
                {
                    selected.Add(Cheat.Speed);
                }

                if (MoonJump.IsChecked == true)
                {
                    selected.Add(Cheat.MoonJump);
                }

                if (WeaponSlots.IsChecked == true)
                {
                    selected.Add(Cheat.WeaponInv);
                }

                if (BowSlots.IsChecked == true)
                {
                    selected.Add(Cheat.BowInv);
                }

                if (ShieldSlots.IsChecked == true)
                {
                    selected.Add(Cheat.ShieldInv);
                }

                this.SetCheats(selected);
            }
        }

        private void ExportClick(object sender, RoutedEventArgs e)
        {
            this.ExportToExcel();
        }

        private void LoadTab(ContentControl tab, int page)
        {
            var scroll = new ScrollViewer { Name = "ScrollContent", Margin = new Thickness(10), VerticalAlignment = VerticalAlignment.Top };

            var holder = new WrapPanel { Margin = new Thickness(0), VerticalAlignment = VerticalAlignment.Top};

            // setup grid
            var grid = this.GenerateTabGrid();

            var x = 1;
            var list = this.items.Where(i => i.Page == page).OrderByDescending(i => i.BaseAddress);

            if (page == 4)
            {
                list = this.items.Where(i => i.Page == 4 || i.Page == 5 || i.Page == 6).OrderByDescending(i => i.BaseAddress);
            }

            foreach (var item in list)
            {
                grid.RowDefinitions.Add(new RowDefinition());

                var value = item.Value;
                if (value > int.MaxValue)
                {
                    value = 0;
                }

                // Name
                var name = new TextBox
                {
                    Text = item.Name,
                    ToolTip = BitConverter.ToString(Encoding.Default.GetBytes(item.Name)).Replace("-", string.Empty),
                    //ToolTip = item.Address.ToString("x8").ToUpper(), 
                    Margin = new Thickness(0,0,10,0),
                    Height = 22,
                    Width = 250
                };

                Grid.SetRow(name, x);
                Grid.SetColumn(name, 0);
                grid.Children.Add(name);

                // Value
                var val = this.GenerateGridTextBox(value.ToString(), item.Modifier5Address, x, 1);
                val.PreviewTextInput += this.NumberValidationTextBox;
                grid.Children.Add(val);

                // Mod1
                var mtb1 = this.GenerateGridTextBox(item.Modifier1Value, item.Modifier1Address, x, 2);
                grid.Children.Add(mtb1);

                // Mod2
                var mtb2 = this.GenerateGridTextBox(item.Modifier2Value, item.Modifier2Address, x, 3);
                grid.Children.Add(mtb2);

                // Mod3
                var mtb3 = this.GenerateGridTextBox(item.Modifier3Value, item.Modifier3Address, x, 4);
                grid.Children.Add(mtb3);

                // Mod4
                var mtb4 = this.GenerateGridTextBox(item.Modifier4Value, item.Modifier4Address, x, 5);
                grid.Children.Add(mtb4);

                // Mod5
                var mtb5 = this.GenerateGridTextBox(item.Modifier5Value, item.Modifier5Address, x, 6);
                grid.Children.Add(mtb5);

                x++;
            }

            grid.Height = x * 35;

            if (tab.Name == "Food")
            {
                holder.Children.Add(new TextBox 
                {
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Margin = new Thickness(10, 10, 0, 0),
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    Text = "See post: https://gbatemp.net/threads/post-your-wiiu-cheat-codes-here.395443/page-303#post-7156278"
                });
            }

            holder.Children.Add(grid);

            scroll.Content = holder;

            tab.Content = scroll;
        }

        private void DebugData()
        {
            // Debug Grid data
            DebugGrid.ItemsSource = this.items;

            // Show extra info in 'Codes' tab to see if our cheats are looking in the correct place
            var stamina1 = this.tcpGecko.peek(0x42439594).ToString("X");
            var stamina2 = this.tcpGecko.peek(0x42439598).ToString("X");
            this.StaminaData.Content = string.Format("[0x42439594 = {0}, 0x42439598 = {1}]", stamina1, stamina2);

            var health = this.tcpGecko.peek(0x439B6558);
            this.HealthData.Content = string.Format("0x439B6558 = {0}", health);

            var run = this.tcpGecko.peek(0x43A88CC4).ToString("X");
            this.RunData.Content = string.Format("0x43A88CC4 = {0}", run);

            var speed = this.tcpGecko.peek(0x439BF514).ToString("X");
            this.SpeedData.Content = string.Format("0x439BF514 = {0}", speed);

            var rupee1 = this.tcpGecko.peek(0x3FC92D10);
            var rupee2 = this.tcpGecko.peek(0x4010AA0C);
            this.RupeeData.Content = string.Format("[0x3FC92D10 = {0}, 0x4010AA0C = {1}]", rupee1, rupee2);

            var weapon1 = this.tcpGecko.peek(0x3FCFB498);
            var weapon2 = this.tcpGecko.peek(0x4010B34C);
            this.WeaponSlotsData.Content = string.Format("[0x3FCFB498 = {0}, 0x4010B34C = {1}]", weapon1, weapon2);

            var bow1 = this.tcpGecko.peek(0x3FD4BB50);
            var bow2 = this.tcpGecko.peek(0x4011126C);
            this.BowSlotsData.Content = string.Format("[0x3FD4BB50 = {0}, 0x4011126C = {1}]", bow1, bow2);

            var shield1 = this.tcpGecko.peek(0x3FCC0B40);
            var shield2 = this.tcpGecko.peek(0x4011128C);
            this.ShieldSlotsData.Content = string.Format("[0x3FCC0B40 = {0}, 0x4011128C = {1}]", shield1, shield2);
        }

        private void SetCheats(ICollection<Cheat> cheats)
        {
            // Disable codehandler before we modify
            this.tcpGecko.poke32(CodeHandlerEnabled, 0x00000000);

            // clear current codes
            var clear = CodeHandlerStart;
            while (clear <= CodeHandlerEnd)
            {
                this.tcpGecko.poke32(clear, 0x0);
                clear += 0x4;
            }

            var codes = new List<uint>();

            // TODO: Consider moving first and last line of each to loop at the end to avoid duplicating them
            // Most are 32 bit writes
            if (cheats.Contains(Cheat.Stamina))
            {
                // Max 453B8000
                var value = uint.Parse(CurrentStamina.Text, NumberStyles.HexNumber);

                codes.Add(0x00020000);
                codes.Add(0x42439594);
                codes.Add(value);
                codes.Add(0x00000000);

                codes.Add(0x00020000);
                codes.Add(0x42439598);
                codes.Add(value);
                codes.Add(0x00000000);
            }

            if (cheats.Contains(Cheat.Health))
            {
                var value = Convert.ToUInt32(CurrentHealth.Text);

                codes.Add(0x00020000);
                codes.Add(0x439B6558);
                codes.Add(value);
                codes.Add(0x00000000);
            }

            if (cheats.Contains(Cheat.Run))
            {
                codes.Add(0x00020000);
                codes.Add(0x43A88CC4);
                codes.Add(0x3FC00000);
                codes.Add(0x00000000);
            }

            if (cheats.Contains(Cheat.Speed))
            {
                var value = uint.Parse(CurrentSpeed.Text, NumberStyles.HexNumber);

                codes.Add(0x00020000);
                codes.Add(0x439BF514);
                codes.Add(value);
                codes.Add(0x00000000);
            }

            if (cheats.Contains(Cheat.Rupees))
            {
                var value = Convert.ToUInt32(CurrentRupees.Text);

                codes.Add(0x00020000);
                codes.Add(0x3FC92D10);
                codes.Add(value);
                codes.Add(0x00000000);

                codes.Add(0x00020000);
                codes.Add(0x4010AA0C);
                codes.Add(value);
                codes.Add(0x00000000);
            }

            if (cheats.Contains(Cheat.MoonJump))
            {
                codes.Add(0x03020000);
                codes.Add(0x102F48A8);
                codes.Add(0x00002000);
                codes.Add(0x00000000);
                codes.Add(0x00020000);
                codes.Add(0x439BF528);
                codes.Add(0xBF400000);
                codes.Add(0x00000000);
                codes.Add(0xD0000000);
                codes.Add(0xDEADCAFE);

                codes.Add(0x04020000);
                codes.Add(0x102F48A8);
                codes.Add(0x00002000);
                codes.Add(0x00000000);
                codes.Add(0x00020000);
                codes.Add(0x439BF528);
                codes.Add(0x3F800000);
                codes.Add(0x00000000);
                codes.Add(0xD0000000);
                codes.Add(0xDEADCAFE);
            }

            if (cheats.Contains(Cheat.WeaponInv))
            {
                var value = Convert.ToUInt32(CurrentWeaponSlots.Text);

                codes.Add(0x00020000);
                codes.Add(0x3FCFB498);
                codes.Add(value);
                codes.Add(0x00000000);

                codes.Add(0x00020000);
                codes.Add(0x4010B34C);
                codes.Add(value);
                codes.Add(0x00000000);
            }

            if (cheats.Contains(Cheat.BowInv))
            {
                var value = Convert.ToUInt32(CurrentBowSlots.Text);

                codes.Add(0x00020000);
                codes.Add(0x3FD4BB50);
                codes.Add(value);
                codes.Add(0x00000000);

                codes.Add(0x00020000);
                codes.Add(0x4011126C);
                codes.Add(value);
                codes.Add(0x00000000);
            }

            if (cheats.Contains(Cheat.ShieldInv))
            {
                var value = Convert.ToUInt32(CurrentShieldSlots.Text);

                codes.Add(0x00020000);
                codes.Add(0x3FCC0B40);
                codes.Add(value);
                codes.Add(0x00000000);

                codes.Add(0x00020000);
                codes.Add(0x4011128C);
                codes.Add(value);
                codes.Add(0x00000000);
            }

            // Write our selected codes
            var address = CodeHandlerStart;
            foreach (var code in codes)
            {
                this.tcpGecko.poke32(address, code);
                address += 0x4;
            }

            // Re-enable codehandler
            this.tcpGecko.poke32(CodeHandlerEnabled, 0x00000001);
        }

        private void ToggleControls(string state)
        {
            if (state == "Connected")
            {
                Load.IsEnabled = this.connected;
                this.Connect.IsEnabled = !this.connected;
                this.Connect.Visibility = Visibility.Hidden;

                this.Disconnect.IsEnabled = this.connected;
                this.Disconnect.Visibility = Visibility.Visible;

                this.IpAddress.IsEnabled = !this.connected;

                if (this.Load.Visibility == Visibility.Hidden)
                {
                    this.Refresh.IsEnabled = true;
                }
            }

            if (state == "Disconnected")
            {
                Load.IsEnabled = !this.connected;
                this.Connect.IsEnabled = true;
                this.Connect.Visibility = Visibility.Visible;
                this.Disconnect.IsEnabled = false;
                this.Disconnect.Visibility = Visibility.Hidden;
                this.IpAddress.IsEnabled = this.connected;

                //TabControl.IsEnabled = false;
                this.Save.IsEnabled = false;
                this.Refresh.IsEnabled = false;
            }

            if (state == "Load")
            {
                TabControl.IsEnabled = false;
                this.Load.IsEnabled = false;
                this.Load.Visibility = Visibility.Hidden;

                this.Save.IsEnabled = false;
                this.Refresh.IsEnabled = false;
            }

            if (state == "DataLoaded")
            {
                TabControl.IsEnabled = true;
                this.Refresh.IsEnabled = true;
                this.Save.IsEnabled = true;
            }

            if (state == "LoadError")
            {
                
            }
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            var regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void TabControlSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.Save == null)
            {
                return;
            }

            if (Debug.IsSelected || Help.IsSelected || Credits.IsSelected)
            {
                this.Save.IsEnabled = false;
            }
            else
            {
                this.Save.IsEnabled = true;
            }
        }

        private void ClientDownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            try
            {
                var result = e.Result;
                if (result != this.version)
                {
                    MessageBox.Show(string.Format("An update is available: {0}", result));
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Error checking for new version");
            }
        }

        private void UpdateProgress(int percent)
        {
            Progress.Value = percent;
        }

        private void ExportToExcel()
        {
            try
            {
                DebugGrid.SelectAllCells();
                DebugGrid.ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader;
                ApplicationCommands.Copy.Execute(null, DebugGrid);
                var result = (string)Clipboard.GetData(DataFormats.CommaSeparatedValue);
                DebugGrid.UnselectAllCells();

                var path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var excelFile = new StreamWriter(path + @"\debug.csv");
                excelFile.WriteLine(result);
                excelFile.Close();

                MessageBox.Show("File exported to " + path);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void FindAndPoke(string itemAddress, uint address)
        {
            var foundTextBox = (TextBox)this.FindName("Item_" + itemAddress);
            if (foundTextBox != null)
            {
                uint val;
                bool parsed = uint.TryParse(foundTextBox.Text, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out val);
                if (parsed)
                {
                    this.tcpGecko.poke32(address, val);
                }
            }
        }

        private string ReadString(uint addr)
        {
            //string result = Encoding.UTF8.GetString(bytearray);

            var dump = new MemoryStream();
            this.tcpGecko.Dump(addr, addr + 0x24, dump);
            dump.Position = 0;

            var builder = new StringBuilder();

            for (var i = 0; i < dump.Length; i++)
            {
                var data = dump.ReadByte();
                if (data == 0)
                {
                    break;
                }

                builder.Append((char)data);
            }

            var name = builder.ToString();

            return name;
        }

        private TextBox GenerateGridTextBox(string value, string field, int x, int col)
        {
            var tb = new TextBox
            {
                Text = value,
                ToolTip = field,
                Width = 70,
                Height = 22,
                Margin = new Thickness(10,0,10,0),
                Name = "Item_" + field,
                IsEnabled = true,
                CharacterCasing = CharacterCasing.Upper,
                MaxLength = 8
            };

            var check = (TextBox)this.FindName("Item_" + field);
            if (check != null)
            {
                this.UnregisterName("Item_" + field);
            }

            this.RegisterName("Item_" + field, tb);

            Grid.SetRow(tb, x);
            Grid.SetColumn(tb, col);

            return tb;
        }

        private Grid GenerateTabGrid()
        {
            var grid = new Grid
            {
                Name = "TabGrid",
                Margin = new Thickness(10),
                ShowGridLines = false,
                VerticalAlignment = VerticalAlignment.Top
            };

            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());

            grid.RowDefinitions.Add(new RowDefinition());

            // Headers
            var itemHeader = new TextBlock
            {
                Text = "Item Name",
                FontSize = 14,
                FontWeight = FontWeights.Bold
            };
            Grid.SetRow(itemHeader, 0);
            Grid.SetColumn(itemHeader, 0);
            grid.Children.Add(itemHeader);

            var valueHeader = new TextBlock
            {
                Text = "Item Value",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetRow(valueHeader, 0);
            Grid.SetColumn(valueHeader, 1);
            grid.Children.Add(valueHeader);

            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());

            for (int y = 1; y < 6; y++)
            {
                var header = new TextBlock
                {
                    Text = "Modifier " + y,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Grid.SetRow(header, 0);
                Grid.SetColumn(header, y + 1);
                grid.Children.Add(header);
            }

            return grid;
        }
    }
}