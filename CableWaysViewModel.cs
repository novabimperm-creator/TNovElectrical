using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TNovCommon;

namespace TNovElectrical
{
    public class CableWaysViewModel : INotifyPropertyChanged
    {
        //параметры записи длины - в окне не показываются, пользователем не меняются
        private string _sPar1 = "К1_Длина_Способ 1"; public string sPar1 { get => _sPar1; set { _sPar1 = value; OnPropertyChanged(); } }
        private string _sPar2 = "К1_Длина_Способ 2"; public string sPar2 { get => _sPar2; set { _sPar2 = value; OnPropertyChanged(); } }
        private string _sPar3 = "К1_Длина_Способ 3"; public string sPar3 { get => _sPar3; set { _sPar3 = value; OnPropertyChanged(); } }
        private string _sPar4 = "К1_Длина_Способ 4"; public string sPar4 { get => _sPar4; set { _sPar4 = value; OnPropertyChanged(); } }
        private string _sPar5 = "К1_Длина_Способ 5"; public string sPar5 { get => _sPar5; set { _sPar5 = value; OnPropertyChanged(); } }
        private string _sTypePar1 = "Настройки_Кабели_Способ прокладки 1"; public string sTypePar1 { get => _sTypePar1; set { _sTypePar1 = value; OnPropertyChanged(); } }
        private string _sTypePar2 = "Настройки_Кабели_Способ прокладки 2"; public string sTypePar2 { get => _sTypePar2; set { _sTypePar2 = value; OnPropertyChanged(); } }
        private string _sTypePar3 = "Настройки_Кабели_Способ прокладки 3"; public string sTypePar3 { get => _sTypePar3; set { _sTypePar3 = value; OnPropertyChanged(); } }
        private string _sTypePar4 = "Настройки_Кабели_Способ прокладки 4"; public string sTypePar4 { get => _sTypePar4; set { _sTypePar4 = value; OnPropertyChanged(); } }
        private string _sTypePar5 = "Настройки_Кабели_Способ прокладки 5"; public string sTypePar5 { get => _sTypePar5; set { _sTypePar5 = value; OnPropertyChanged(); } }

        //типы труб: значение одновременно ищется в исходной строке и пишется в Т_Тип
        private ObservableCollection<string> _pipeTypes = new ObservableCollection<string>
        {
            "гофр. ПВХ", "гофр. ПНД(т)", "МР(г)", "гофр. ПА", "ст."
        };
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public ObservableCollection<string> pipeTypes { get => _pipeTypes; set { _pipeTypes = value; _pipeTypesFromJson = true; OnPropertyChanged(); } }

        //ввод нового типа трубы
        private string _newPipeType = "";
        [JsonIgnore]
        public string newPipeType { get => _newPipeType; set { _newPipeType = value; OnPropertyChanged(); } }

        private bool _isAddingPipeType = false;
        [JsonIgnore]
        public bool isAddingPipeType { get => _isAddingPipeType; set { _isAddingPipeType = value; OnPropertyChanged(); } }

        [JsonIgnore]
        public ICommand AddPipeTypeCommand { get; }
        [JsonIgnore]
        public ICommand RemovePipeTypeCommand { get; }

        public CableWaysViewModel()
        {
            AddPipeTypeCommand = new RelayCommand(o => AddPipeType(), null);
            RemovePipeTypeCommand = new RelayCommand(o => RemovePipeType(o as string), null);
        }

        public bool AddPipeType()
        {
            string value = (newPipeType ?? "").Trim();
            if (value.Length == 0) return false;
            if (pipeTypes == null) pipeTypes = new ObservableCollection<string>();
            if (pipeTypes.Any(p => string.Equals(p, value, StringComparison.CurrentCultureIgnoreCase))) return false;
            pipeTypes.Add(value);
            newPipeType = "";
            return true;
        }

        public void RemovePipeType(string value)
        {
            if (value == null || pipeTypes == null) return;
            pipeTypes.Remove(value);
        }

        #region Совместимость со старыми настройками
        //в старом формате типы труб хранились полями pipeType1..5 (и дублировались в pipeWay1..5)
        [JsonExtensionData]
        private IDictionary<string, JToken> _legacy;
        private bool _pipeTypesFromJson = false;

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (pipeTypes == null) pipeTypes = new ObservableCollection<string>();
            if (!_pipeTypesFromJson && _legacy != null) //настройки старого формата - переносим pipeType1..5 в список
            {
                var migrated = new List<string>();
                for (int i = 1; i <= 5; i++)
                {
                    JToken token;
                    if (!_legacy.TryGetValue("pipeType" + i, out token)) continue;
                    string value = (token.Type == JTokenType.String) ? token.Value<string>() : null;
                    if (string.IsNullOrWhiteSpace(value)) continue;
                    value = value.Trim();
                    if (!migrated.Any(p => string.Equals(p, value, StringComparison.CurrentCultureIgnoreCase))) migrated.Add(value);
                }
                if (migrated.Count > 0) pipeTypes = new ObservableCollection<string>(migrated);
            }
            _legacy = null; //старые поля обратно не пишем
        }
        #endregion

        public event EventHandler CloseRequest;
        private void RaiseCloseRequest()
        {
            CloseRequest?.Invoke(this, EventArgs.Empty);
        }
        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }
    }
}
