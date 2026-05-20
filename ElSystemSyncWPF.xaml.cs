using System;
using System.Collections.Generic;
using System.Globalization;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TNovElectrical
{
    /// <summary>
    /// Логика взаимодействия для ElSystemSyncWPF.xaml
    /// </summary>
    public partial class ElSystemSyncWPF : Window
    {
        public ElSystemSyncWPF(IEnumerable<ElSystem> systems)
        {
            InitializeComponent();
            var viewModel = new ElSystemSyncViewModel(systems);
            viewModel.RequestClose += (s, e) =>
            {
                DialogResult = e;
                Close();
            };
            DataContext = viewModel;
        }

        public ElSystem SelectedSystem => (DataContext as ElSystemSyncViewModel)?.CurrentSystem;

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {

        }
    }
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isDifferent && isDifferent)
                return new SolidColorBrush(Colors.LightSalmon);
            return new SolidColorBrush(Colors.Transparent); // или любой цвет по умолчанию
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
