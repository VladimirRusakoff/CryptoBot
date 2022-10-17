using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace CryptoBot
{
    class ViewModelMM : INotifyPropertyChanged
    {
        public ICommand buttonChange { get; }

        public ICommand buttonSelectAll { get; }

        public ICommand buttonFill { get; }

        public string formMarginType { get; set; }

        public string formLeverage { get; set; } = "50";

        public ViewModelMM()
        {
            buttonFill = new RelayCommand(fillTable);
            buttonSelectAll = new RelayCommand(selectAll);
            buttonChange = new RelayCommand(changeTable);
        }

        public ObservableCollection<SymbolsLeverage> symbLeverage { get; } = new ObservableCollection<SymbolsLeverage>();

        public event PropertyChangedEventHandler PropertyChanged;

        private BinanceFutures bf;
        private List<SymbolsLeverage> sLev = new List<SymbolsLeverage>();

        void onPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        async void fillTable(object ob)
        {
            await Task.Run(() =>
            {
                dispatcher.Invoke(() =>
                {
                    bf = new BinanceFutures(false);
                    sLev = bf.GetMarginModeLeverage().OrderBy(u=>u.symbol).ToList();
                    foreach (var e in sLev)
                        symbLeverage.Add(e);
                });
            });
        }

        async void selectAll(object ob)
        {
            await Task.Run(() =>
            {
                dispatcher.Invoke(() =>
                {
                    symbLeverage.Clear();

                    foreach (var el in sLev)
                    {
                        el.isSelect = true;
                        symbLeverage.Add(el);
                    }

                    //var sl = symbLeverage[0];
                    //sl.isSelect = true;
                    //symbLeverage[0] = sl;
                    //symbLeverage.Add(new SymbolsLeverage() { isSelect = true, Leverage = 40, marMode = MarginMode.Cross, symbol = "111" });
                });
            });
        }

        async void changeTable(object ob)
        {
            await Task.Run(() =>
            {
                dispatcher.Invoke(() =>
                {
                    MarginMode mt = ConvertToMarginMode(formMarginType);
                    int lev = ConvertToInt(formLeverage);
                    foreach (var el in symbLeverage)
                    {
                        if (el.isSelect == false)
                            continue;

                        if (el.marMode != mt) // нужно поменять тип маржи
                            bf.ChangeMarginType(el.symbol, mt);

                        if (lev != 0 && el.Leverage != lev) // нужно поменять плечо
                            bf.ChangeLeverage(el.symbol, lev);
                    }

                    symbLeverage.Clear();
                    // заполняем заново
                    sLev = bf.GetMarginModeLeverage().OrderBy(u => u.symbol).ToList();
                    foreach (var e in sLev)
                        symbLeverage.Add(e);
                });
            });
        }

        private int ConvertToInt(string strLeverage)
        {
            int retVal = 0;

            try
            {
                retVal = Convert.ToInt32(strLeverage);
                if (retVal > 0 && retVal <= 120)
                    return retVal;
            }
            catch (Exception ex)
            {

            }

            MessageBox.Show("Can't convert leverage to integer!!! This value should be from 1 to 120!!!");
            return retVal;
        }

        private MarginMode ConvertToMarginMode(string marType)
        {
            return marType == "CROSSED" ? MarginMode.Cross : MarginMode.Isolate;
        }

        private readonly System.Windows.Threading.Dispatcher dispatcher = System.Windows.Application.Current.Dispatcher;
    }

    public class SymbolsLeverage
    {
        public bool isSelect { get; set; }
        public string symbol { get; set; }
        public MarginMode marMode { get; set; }
        public int Leverage { get; set; }
    }

    public enum MarginMode
    {
        Cross,
        Isolate
    }

    class RelayCommand : ICommand
    {
        #region Fields 
        /// <summary>
        /// Делегат непосредственно выполняющий действие
        /// </summary>
        readonly Action<object> _execute;
        /// <summary>
        /// Делегат осуществляющий проверку на возможность выполнения действия
        /// </summary>
        readonly Predicate<object> _canExecute;
        #endregion // Fields

        /// <summary>
        /// Конструктор
        /// </summary>
        /// <param name="execute">Метод передаваемый по делегату - который является коллбеком</param>
        public RelayCommand(Action<object> execute) : this(execute, null) { }
        /// <summary>
        /// Конструктор
        /// </summary>
        /// <param name="execute">
        /// Метод передаваемый по делегату - который является коллбеком
        /// </param>
        /// <param name="canExecute">
        /// Метод передаваемый по делегату - проверяющий возможность выполнения действия
        /// </param>
        public RelayCommand(Action<object> execute, Predicate<object> canExecute)
        {
            if (execute == null)
                throw new ArgumentNullException("execute");
            _execute = execute; _canExecute = canExecute;
        }

        /// <summary>
        /// Проверка на возможность выполнения действия
        /// </summary>
        /// <param name="parameter">передаваемый из View параметр</param>
        /// <returns></returns>
        public bool CanExecute(object parameter)
        {
            return _canExecute == null ? true : _canExecute(parameter);
        }
        /// <summary>
        /// Событие - вызываемое всякий раз когда меняется возможность исполнения коллбека.
        /// При срабатывании данного события, форма вновь вызывает метод "CanExecute"
        /// Событие запускается из ViewModel по мере необходимости
        /// </summary>
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
        /// <summary>
        /// Метод вызывающий делегат который в свою очередь выполняет действие
        /// </summary>
        /// <param name="parameter">передаваемый из View параметр</param>
        public void Execute(object parameter) { _execute(parameter); }
    }
}
