using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace TNovElectrical
{
    public partial class ElSystemSyncSimpleWPF : Window
    {
        // Входные данные
        public List<string> LinkNames { get; set; }

        // Выходные данные
        public string SelectedLinkName { get; private set; }
        public bool IsPySelected { get; private set; }
        public bool IsCosfSelected { get; private set; }
        public bool IsCableLengthSelected { get; private set; }
        public bool Is3PhaseSelected { get; private set; }
        public bool IsLoadNameSelected { get; private set; }
        public bool IsElemsCountSelected { get; private set; }
        public bool IsCableWaySelected { get; private set; }

        public ElSystemSyncSimpleWPF()
        {
            InitializeComponent();
            // По умолчанию список пуст
            LinkNames = new List<string>();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Заполняем ComboBox
            cmbLinkNames.ItemsSource = LinkNames;
            if (LinkNames.Any())
                cmbLinkNames.SelectedIndex = 0;
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                this.DragMove();
        }

        private void chkSelectAll_Click(object sender, RoutedEventArgs e)
        {
            // Поведение "Выбрать всё / Снять всё"
            bool allChecked = new[] { chkPy, chkCosf, chkCableLength, chk3Phase, chkLoadName, chkElemsCount, chkCableWay }
                                .All(chk => chk.IsChecked == true);

            bool newState = !allChecked; // если все выбраны – снимаем, иначе выбираем все
            chkPy.IsChecked = newState;
            chkCosf.IsChecked = newState;
            chkCableLength.IsChecked = newState;
            chk3Phase.IsChecked = newState;
            chkLoadName.IsChecked = newState;
            chkElemsCount.IsChecked = newState;
            chkCableWay.IsChecked = newState;
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            // Сохраняем выбранные значения
            SelectedLinkName = cmbLinkNames.SelectedItem as string;
            IsPySelected = chkPy.IsChecked == true;
            IsCosfSelected = chkCosf.IsChecked == true;
            IsCableLengthSelected = chkCableLength.IsChecked == true;
            Is3PhaseSelected = chk3Phase.IsChecked == true;
            IsLoadNameSelected = chkLoadName.IsChecked == true;
            IsElemsCountSelected = chkElemsCount.IsChecked == true;
            IsCableWaySelected = chkCableWay.IsChecked == true;

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            string commandText = @"https://portal.talan.group/knowledge/proektirovanie/plaginyiskriptynovatsiya/";
            var proc = new System.Diagnostics.Process();
            proc.StartInfo.FileName = commandText;
            proc.StartInfo.UseShellExecute = true;
            proc.Start();
        }
    }
}