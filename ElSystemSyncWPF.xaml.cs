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
using TNovCommon;

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

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            string commandText = HelpLinks.GetHelpLink("-");
            var proc = new System.Diagnostics.Process();
            proc.StartInfo.FileName = commandText;
            proc.StartInfo.UseShellExecute = true;
            proc.Start();
        }
    }
    public class BoolToColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush DiffBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5C4030"));
        private static readonly SolidColorBrush TransparentBrush = new SolidColorBrush(Colors.Transparent);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isDifferent && isDifferent)
                return DiffBrush;
            return TransparentBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
