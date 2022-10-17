using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using CryptoBot.SerializationClasses;
using MessageBox = System.Windows.MessageBox;
using Color = System.Drawing.Color;
using MouseEventArgs = System.Windows.Forms.MouseEventArgs;

namespace CryptoBot
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            connectorBinance = new BinanceFutures();

            FillSettingsTable();
            SubscribeToEvents();
            setEnableForBTCdelta();

            //Thread BalancePositionUpdate = new Thread(balancePositionUpdate);
            //BalancePositionUpdate.CurrentCulture = new System.Globalization.CultureInfo("ru-RU");
            //BalancePositionUpdate.IsBackground = true;
            //BalancePositionUpdate.Start();

            Thread CheckLiqudationPosition = new Thread(checkDisconnect);
            CheckLiqudationPosition.CurrentCulture = new System.Globalization.CultureInfo("ru-RU");
            CheckLiqudationPosition.IsBackground = true;
            CheckLiqudationPosition.Start();

            lbLicense.Content = string.Format("Your license is valid until {0} UTC Time", WorkSettings.LastTime);
        }

        private DataGridView _grid;

        private readonly System.Windows.Threading.Dispatcher dispatcher = System.Windows.Application.Current.Dispatcher;

        private bool isStartBalance = true; // только что запустили бота
        private bool isStartPosition = true;

        private void FillSettingsTable()
        {
            _grid = GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.None);

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _grid.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = "Symbols";
            column0.ReadOnly = false;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = "Direction";
            column1.ReadOnly = false;
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column1);

            DataGridViewColumn column2 = new DataGridViewColumn();
            column2.CellTemplate = cell0;
            column2.HeaderText = "Volume";
            column2.ReadOnly = false;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column2);

            DataGridViewColumn column6 = new DataGridViewColumn();
            column6.CellTemplate = cell0;
            column6.HeaderText = "Distance";
            column6.ReadOnly = false;
            column6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column6);

            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.HeaderText = "Buffer";
            column3.ReadOnly = false;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column3);

            DataGridViewColumn column4 = new DataGridViewColumn();
            column4.CellTemplate = cell0;
            column4.HeaderText = "Stop";
            column4.ReadOnly = false;
            column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column4);

            DataGridViewColumn column5 = new DataGridViewColumn();
            column5.CellTemplate = cell0;
            column5.HeaderText = "Take";
            column5.ReadOnly = false;
            column5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(column5);

            HostTemp.Child = _grid;

            FillSettingsTableData();
        }

        private void FillSettingsTableData()
        {
            foreach (var str in connectorBinance.settings.setSettings)
            {
                DataGridViewRow row = new DataGridViewRow();
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[0].Value = str.symbols;
                row.Cells[1].Value = str.direction;
                row.Cells[2].Value = str.volume;
                row.Cells[3].Value = str.distance;
                row.Cells[4].Value = str.buffer;
                row.Cells[5].Value = str.stop;
                row.Cells[6].Value = str.take;
                //row.Cells[1].Value = "222";
                _grid.Rows.Insert(_grid.Rows.Count, row);
            }
        }

        private void checkDisconnect()
        {
            while (true)
            {
                Thread.Sleep(2000);

                if (connectorBinance.isDisconnect == true)
                    CloseStrategy();
            }
        }

        //int ui = 0;
        //private void balancePositionUpdate()
        //{
        //    while (true)
        //    {
        //        Thread.Sleep(1000);

        //        dispatcher.Invoke(() =>
        //        {
        //            //lvOpenPositions.Items.Add(ui++);
        //            if (connectorBinance.IsOn == false)
        //                return;

        //            // заполнение баланса
        //            List<Balances> bal = connectorBinance.listBalances;
        //            if (bal.Count > 0)
        //            {
        //                lvBalances.Items.Clear();
        //                foreach (var el in bal)
        //                {
        //                    lvBalances.Items.Add(string.Format("{0}     | {1}   | {2}       | {3} ", el.a, el.wb, el.cw, DateTime.Now));
        //                }
        //            }
        //            else
        //            {
        //                if (isStartBalance)
        //                {
        //                    List<BalanceResponce> br = connectorBinance.GetBalance();
        //                    foreach (var el in br)
        //                        lvBalances.Items.Add(string.Format("{0}     | {1}   | {2}       | {3} ", el.asset, el.balance, el.crossWalletBalance, DateTime.Now));
        //                    isStartBalance = false;
        //                }
        //            }

        //            // заполнение позиции
        //            List<PositionUp> pos = connectorBinance.dictPositions.Values.ToList();
        //            if (pos.Count == 0)
        //            {
        //                if (isStartPosition)
        //                {
        //                    List<PositionOneWayResponce> pr = connectorBinance.GetCurrentPositions();
        //                    foreach (var el in pr)
        //                    {
        //                        lvOpenPositions.Items.Add(string.Format("{0}|{1}|{2}|{3}|{4}",
        //                            ConvertToOpenPosition(el.symbol, 0),
        //                            ConvertToOpenPosition(el.positionAmt > 0 ? "BUY" : "SELL", 1),
        //                            ConvertToOpenPosition(el.positionAmt.ToString(), 2),
        //                            ConvertToOpenPosition(el.entryPrice.ToString(), 3),
        //                            ConvertToOpenPosition(el.unRealizedProfit.ToString(), 4)));
        //                        PositionUp newPos = new PositionUp()
        //                        {
        //                            s = el.symbol,
        //                            pa = el.positionAmt,
        //                            up = el.unRealizedProfit,
        //                            mt = el.marginType,
        //                            ep = el.entryPrice,
        //                            ps = el.positionSide
        //                        };
        //                        if (connectorBinance.dictPositions.ContainsKey(el.symbol))
        //                            connectorBinance.dictPositions[el.symbol] = newPos;
        //                        else
        //                            connectorBinance.dictPositions.Add(el.symbol, newPos);

        //                    }
        //                    isStartPosition = false;
        //                    timeOpenPositionsUpForm = DateTime.Now;
        //                }
        //            }
        //            else
        //            {
        //                if (connectorBinance.timePositionsUpdate > timeOpenPositionsUpForm)
        //                {
        //                    lvOpenPositions.Items.Clear();
        //                    foreach (var el in pos)
        //                    {
        //                        if (el.pa == 0)
        //                            continue;

        //                        lvOpenPositions.Items.Add(string.Format("{0}|{1}|{2}|{3}|{4}",
        //                                ConvertToOpenPosition(el.s, 0),
        //                                ConvertToOpenPosition(el.pa > 0 ? "BUY" : "SELL", 1),
        //                                ConvertToOpenPosition(el.pa.ToString(), 2),
        //                                ConvertToOpenPosition(el.ep.ToString(), 3),
        //                                ConvertToOpenPosition(el.up.ToString(), 4)));
        //                    }
        //                    timeOpenPositionsUpForm = DateTime.Now;
        //                }
        //            }
        //        });
        //    }
        //}

        private DateTime timeOpenPositionsUpForm { get; set; }

        private List<int> intForOpenPositions = new List<int>() { 12, 8, 10, 14, 14 };
        private string ConvertToOpenPosition(string elem, int ij)
        {
            if (ij >= intForOpenPositions.Count)
                return elem;

            string emptyString = "";
            int lengthEmpty = intForOpenPositions[ij] - elem.Length;
            if (lengthEmpty <= 0)
                return elem;

            for (int i = 0; i < lengthEmpty; i++)
                emptyString += " ";

            return string.Format("{0}{1}", elem, emptyString);
        }

        private void SubscribeToEvents()
        {
            // load default values
            cbLogIsOn.IsChecked = connectorBinance.settings.LogIsOn;
            cbLogIsOn.Click += (sender, args) =>
            {
                connectorBinance.settings.LogIsOn = cbLogIsOn.IsChecked.Value;
                connectorBinance.SaveSettings();
            };

            cbUseBTCdelta.IsChecked = connectorBinance.settings.isUseBTCdelta;
            cbUseBTCdelta.Click += (sender, args) =>
            {
                connectorBinance.settings.isUseBTCdelta = cbUseBTCdelta.IsChecked.Value;
                connectorBinance.SaveSettings();
                //
                setEnableForBTCdelta();
            };

            tbAPIKey.Text = connectorBinance.settings.apiKey;
            tbAPIKey.TextChanged += (sender, args) =>
            {
                connectorBinance.settings.apiKey = tbAPIKey.Text;
                connectorBinance.SaveSettings();
            };

            pbSecKey.Password = connectorBinance.settings.secKey;
            pbSecKey.PasswordChanged += (sender, args) =>
            {
                connectorBinance.settings.secKey = pbSecKey.Password;
                connectorBinance.SaveSettings();
            };

            tbBTCminutes.Text = connectorBinance.settings.btcMinutes.ToString();
            tbBTCdelta.Text = connectorBinance.settings.btcDelta.ToString();
            tbBTCsecondsOff.Text = connectorBinance.settings.btcSecondsOff.ToString();

            //tbVolume.TextChanged += (sender, args) =>
            //{
            //    if (WorkSettings.maxVolume != 0)
            //    {
            //        try
            //        {
            //            decimal vol = connectorBinance.ConvertStringToDecimal(tbVolume.Text);
            //            if (vol > WorkSettings.maxVolume)
            //            {
            //                tbVolume.Text = connectorBinance.ConvertDecimalToString(WorkSettings.maxVolume);
            //                MessageBox.Show(string.Format("Your max volume is {0}", WorkSettings.maxVolume));
            //            }
            //        }
            //        catch
            //        {

            //        }
            //    }
            //};

            tbNickname.Text = connectorBinance.settings.botNickname;
            tbNickname.TextChanged += (sender, args) =>
            {
                connectorBinance.settings.botNickname = tbNickname.Text;
                connectorBinance.SaveSettings();
            };
        }

        private void setEnableForBTCdelta()
        {
            tbBTCminutes.IsEnabled = cbUseBTCdelta.IsChecked.Value;
            tbBTCdelta.IsEnabled = cbUseBTCdelta.IsChecked.Value;
            tbBTCsecondsOff.IsEnabled = cbUseBTCdelta.IsChecked.Value;
        }

        private BinanceFutures connectorBinance;

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            connectorBinance.IsOn = !connectorBinance.IsOn;

            if (connectorBinance.IsOn)
            {
                if (connectorBinance.LicenseNotOrFinished())
                    MessageBox.Show("You don't have a license!!!");

                if (ConvertValuesAndSaveSettings())
                    connectorBinance.StartStrategy();

                Start.Content = "Close strategy";
            }
            else
            {
                CloseStrategy();
            }
        }

        private void CloseStrategy()
        {
            connectorBinance.IsOn = false;
            connectorBinance.StopStrategy();
            connectorBinance.SaveSettings();
            //connectorBinance.clientWS.Close();
            //
            dispatcher.Invoke(() =>
            {
                Close();
            });
        }

        private bool ConvertValuesAndSaveSettings()
        {
            if (checkConvetToInt32(tbBTCminutes.Text))
            {
                connectorBinance.settings.btcMinutes = Convert.ToInt32(tbBTCminutes.Text);
            }
            else
            {
                MessageBox.Show("ERROR!!! Can't convert to number Minutes BTC delta!!!");
                return false;
            }

            if (checkConvertation(tbBTCdelta.Text))
            {
                connectorBinance.settings.btcDelta = connectorBinance.ConvertStringToDecimal(tbBTCdelta.Text);
            }
            else
            {
                MessageBox.Show("ERROR!!! Can't convert to number BTC seconds for waiting!!!");
                return false;
            }

            if (checkConvetToInt32(tbBTCsecondsOff.Text))
            {
                connectorBinance.settings.btcSecondsOff = Convert.ToInt32(tbBTCsecondsOff.Text);
            }
            else
            {
                MessageBox.Show("ERROR!!! Can't convert to number BTC Delta1!!!");
                return false;
            }

            if (!SaveTableSettings())
            {
                MessageBox.Show("ERROR!!! Problems with data in settings table!!!");
                return false;
            }
                
            connectorBinance.SaveSettings();
            return true;
        }

        private bool SaveTableSettings()
        {
            connectorBinance.settings.setSettings.Clear();

            string val = "";
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                set newSet = new set();
                newSet.symbols = _grid.Rows[i].Cells[0].Value.ToString();
                newSet.direction = _grid.Rows[i].Cells[1].Value.ToString();
                //
                val = _grid.Rows[i].Cells[2].Value.ToString();
                if (checkConvertation(val))
                {
                    decimal volum = connectorBinance.ConvertStringToDecimal(val);
                    if (volum > WorkSettings.maxVolume)
                        return false;
                    else
                        newSet.volume = volum;
                }
                else
                    return false;
                //
                val = _grid.Rows[i].Cells[3].Value.ToString();
                if (checkConvertation(val))
                    newSet.distance = connectorBinance.ConvertStringToDecimal(val);
                else
                    return false;
                //
                val = _grid.Rows[i].Cells[4].Value.ToString();
                if (checkConvertation(val))
                    newSet.buffer = connectorBinance.ConvertStringToDecimal(val);
                else
                    return false;
                //
                val = _grid.Rows[i].Cells[5].Value.ToString();
                if (checkConvertation(val))
                    newSet.stop = connectorBinance.ConvertStringToDecimal(val);
                else
                    return false;
                //
                val = _grid.Rows[i].Cells[6].Value.ToString();
                if (checkConvertation(val))
                    newSet.take = connectorBinance.ConvertStringToDecimal(val);
                else
                    return false;

                connectorBinance.settings.setSettings.Add(newSet);
            }

            return true;
        }

        public bool checkConvertation(string value)
        {
            try
            {
                connectorBinance.ConvertStringToDecimal(value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool checkConvetToInt32(string value)
        {
            try
            {
                Convert.ToInt32(value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (_grid.SelectedRows.Count == 1)
            {
                DataGridViewRow row = new DataGridViewRow();
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[0].Value = _grid.SelectedRows[0].Cells[0].Value;
                row.Cells[1].Value = _grid.SelectedRows[0].Cells[1].Value;
                row.Cells[2].Value = _grid.SelectedRows[0].Cells[2].Value;
                row.Cells[3].Value = _grid.SelectedRows[0].Cells[3].Value;
                row.Cells[4].Value = _grid.SelectedRows[0].Cells[4].Value;
                row.Cells[5].Value = _grid.SelectedRows[0].Cells[5].Value;
                row.Cells[6].Value = _grid.SelectedRows[0].Cells[6].Value;
                _grid.Rows.Insert(_grid.Rows.Count, row);
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_grid.SelectedRows.Count == 1)
                _grid.Rows.Remove(_grid.SelectedRows[0]);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //connectorBinance.GetCandles("BNBUSDT", "1m");
            //connectorBinance.SubscribeCandles1m("BNBUSDT");
            connectorBinance.GetSymbols();
        }

        private void MarginMode_Click_1(object sender, RoutedEventArgs e)
        {
            MarginModeSettings mms = new MarginModeSettings();
            mms.ShowDialog();
            //connectorBinance.GetMarginModeLeverage();
        }

        public static DataGridView GetDataGridView(DataGridViewSelectionMode selectionMode, DataGridViewAutoSizeRowsMode rowsSizeMode)
        {
            DataGridView grid = new DataGridView();

            grid.AllowUserToOrderColumns = true;
            grid.AllowUserToResizeRows = true;
            grid.AutoSizeRowsMode = rowsSizeMode;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToAddRows = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = selectionMode;
            grid.MultiSelect = false;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            grid.ScrollBars = ScrollBars.None;
            grid.BackColor = Color.FromArgb(21, 26, 30);
            grid.BackgroundColor = Color.FromArgb(21, 26, 30);


            grid.GridColor = Color.FromArgb(17, 18, 23);
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.BorderStyle = BorderStyle.None;
            DataGridViewCellStyle style = new DataGridViewCellStyle();
            style.Alignment = DataGridViewContentAlignment.TopLeft;
            style.WrapMode = DataGridViewTriState.True;
            style.BackColor = Color.FromArgb(21, 26, 30);
            style.SelectionBackColor = Color.FromArgb(17, 18, 23);
            style.ForeColor = Color.FromArgb(154, 156, 158);



            grid.DefaultCellStyle = style;
            grid.ColumnHeadersDefaultCellStyle = style;

            grid.MouseHover += delegate (object sender, EventArgs args)
            {
                grid.Focus();
            };

            grid.MouseLeave += delegate (object sender, EventArgs args)
            {
                grid.EndEdit();
            };

            grid.MouseWheel += delegate (object sender, MouseEventArgs args)
            {
                if (grid.SelectedCells.Count == 0)
                {
                    return;
                }
                int rowInd = grid.SelectedCells[0].RowIndex;
                if (args.Delta < 0)
                {
                    rowInd++;
                }
                else if (args.Delta > 0)
                {
                    rowInd--;
                }

                if (rowInd < 0)
                {
                    rowInd = 0;
                }

                if (rowInd >= grid.Rows.Count)
                {
                    rowInd = grid.Rows.Count - 1;
                }

                grid.Rows[rowInd].Selected = true;
                grid.Rows[rowInd].Cells[grid.SelectedCells[0].ColumnIndex].Selected = true;

                if (grid.FirstDisplayedScrollingRowIndex > rowInd)
                {
                    grid.FirstDisplayedScrollingRowIndex = rowInd;
                }


            };

            return grid;
        }
    }
}
