using System.Windows;
using System.Windows.Input;
using TNovCommon;

namespace TNovElectrical
{
    /// <summary>
    /// Логика взаимодействия для ElElevValuesWPF.xaml
    /// </summary>
    public partial class ElElevValuesWPF : Window
    {
        public ElElevValuesWPF(ElElevValuesViewModel viewModel)
        {
            InitializeComponent();
            textBox1.Focus();
            DataContext = viewModel;
            this.SizeToContent = SizeToContent.Height;
        }
        private void acceptButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            this.Close(); // закрытие окна
        }

        private void escButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            this.Close(); // закрытие окна
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            HelpLinks.ShowHelp("-");
        }
    }
}
