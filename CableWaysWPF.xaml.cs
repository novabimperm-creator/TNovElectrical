using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using TNovCommon;

namespace TNovElectrical
{
    /// <summary>
    /// Логика взаимодействия для CableWaysWPF.xaml
    /// </summary>
    public partial class CableWaysWPF : Window
    {
        private readonly CableWaysViewModel viewModel;

        public CableWaysWPF(CableWaysViewModel viewModel)
        {
            InitializeComponent();
            this.viewModel = viewModel;
            DataContext = viewModel;
            this.SizeToContent = SizeToContent.Height;
        }

        private void addPipeTypeButton_Click(object sender, RoutedEventArgs e)
        {
            viewModel.isAddingPipeType = true;
            FocusNewPipeTypeBox();
        }

        private void acceptPipeTypeButton_Click(object sender, RoutedEventArgs e)
        {
            viewModel.AddPipeType();
            FocusNewPipeTypeBox();
        }

        private void newPipeTypeBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) //чтобы Enter не сработал как "Применить"
            {
                viewModel.AddPipeType();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape) //чтобы Escape не закрыл окно
            {
                viewModel.newPipeType = "";
                viewModel.isAddingPipeType = false;
                e.Handled = true;
            }
        }

        private void FocusNewPipeTypeBox()
        {
            //поле ввода появляется по биндингу, поэтому фокус ставим после отрисовки
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new System.Action(() =>
            {
                newPipeTypeBox.Focus();
                newPipeTypeBox.CaretIndex = newPipeTypeBox.Text.Length;
            }));
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
