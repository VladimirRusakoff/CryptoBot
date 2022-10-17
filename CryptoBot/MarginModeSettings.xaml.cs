using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace CryptoBot
{
    /// <summary>
    /// Логика взаимодействия для MarginModeSettings.xaml
    /// </summary>
    public partial class MarginModeSettings : Window
    {
        public MarginModeSettings()
        {
            InitializeComponent();

            FillDefaultValue();
        }

        void FillDefaultValue()
        {
            cbMarginType.Items.Add("ISOLATED");
            cbMarginType.Items.Add("CROSSED");

            cbMarginType.SelectedItem = "ISOLATED";
            tbLeverage.Text = "50";
        }
    }
}
