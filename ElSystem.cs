using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TNovElectrical
{
    using System.ComponentModel;
    using System.Runtime.CompilerServices;

    public class ElSystem : INotifyPropertyChanged
    {
        private string _linkName;
        private string _systemNumber;
        private string _annoSystemNumber;
        private double _systemPy;
        private double _annoPy;
        private double _systemCosf;
        private double _annoCosf;
        private double _systemConductorL1;
        private double _annoConductorL1;
        private double _systemConductorL2;
        private double _annoConductorL2;
        private double _systemConductorL3;
        private double _annoConductorL3;
        private string _systemVoltage;
        private string _threePhase;
        private string _anno3PhaseDevice;
        private string _systemLoadName;
        private string _annoDeviceName;
        private int _systemElemsCount;
        private int _annoElemsCount;
        private string _systemCableWay;
        private string _annoCableWay;

        // Флаги выбора параметров для обновления
        private bool _isSelectedPy;
        private bool _isSelectedCosf;
        private bool _isSelectedCableLength;
        private bool _isSelected3Phase;
        private bool _isSelectedLoadName;
        private bool _isSelectedElemsCount;
        private bool _isSelectedCableWay;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Основные свойства
        public int Order;
        public int AnnoId;
        public string LinkName
        {
            get => _linkName;
            set { _linkName = value; OnPropertyChanged(); }
        }

        public string SystemNumber
        {
            get => _systemNumber;
            set { _systemNumber = value; OnPropertyChanged(); }
        }

        public string AnnoSystemNumber
        {
            get => _annoSystemNumber;
            set { _annoSystemNumber = value; OnPropertyChanged(); }
        }

        public double SystemPy
        {
            get => _systemPy;
            set { _systemPy = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsPyDifferent)); }
        }

        public double AnnoPy
        {
            get => _annoPy;
            set { _annoPy = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsPyDifferent)); }
        }

        public double SystemCosf
        {
            get => _systemCosf;
            set { _systemCosf = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsCosfDifferent)); }
        }

        public double AnnoCosf
        {
            get => _annoCosf;
            set { _annoCosf = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsCosfDifferent)); }
        }

        public double SystemConductorL1
        {
            get => _systemConductorL1;
            set { _systemConductorL1 = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsConductorL1Different)); }
        }

        public double AnnoConductorL1
        {
            get => _annoConductorL1;
            set { _annoConductorL1 = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsConductorL1Different)); }
        }

        public double SystemConductorL2
        {
            get => _systemConductorL2;
            set { _systemConductorL2 = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsConductorL2Different)); }
        }

        public double AnnoConductorL2
        {
            get => _annoConductorL2;
            set { _annoConductorL2 = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsConductorL2Different)); }
        }

        public double SystemConductorL3
        {
            get => _systemConductorL3;
            set { _systemConductorL3 = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsConductorL3Different)); }
        }

        public double AnnoConductorL3
        {
            get => _annoConductorL3;
            set { _annoConductorL3 = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsConductorL3Different)); }
        }

        public string SystemVoltage
        {
            get => _systemVoltage;
            set { _systemVoltage = value; OnPropertyChanged(); OnPropertyChanged(nameof(Is3PhaseDifferent)); }
        }

        public string ThreePhase
        {
            get => _threePhase;
            set { _threePhase = value; OnPropertyChanged(); OnPropertyChanged(nameof(Is3PhaseDifferent)); }
        }
        public int ThreePhaseInt;

        public string Anno3PhaseDevice
        {
            get => _anno3PhaseDevice;
            set { _anno3PhaseDevice = value; OnPropertyChanged(); OnPropertyChanged(nameof(Is3PhaseDifferent)); }
        }

        public string SystemLoadName
        {
            get => _systemLoadName;
            set { _systemLoadName = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsLoadNameDifferent)); }
        }

        public string AnnoDeviceName
        {
            get => _annoDeviceName;
            set { _annoDeviceName = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsLoadNameDifferent)); }
        }

        public int SystemElemsCount
        {
            get => _systemElemsCount;
            set { _systemElemsCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsElemsCountDifferent)); }
        }

        public int AnnoElemsCount
        {
            get => _annoElemsCount;
            set { _annoElemsCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsElemsCountDifferent)); }
        }

        public string SystemCableWay
        {
            get => _systemCableWay;
            set { _systemCableWay = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsCableWayDifferent)); }
        }

        public string AnnoCableWay
        {
            get => _annoCableWay;
            set { _annoCableWay = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsCableWayDifferent)); }
        }

        // Флаги выбора
        public bool IsSelectedPy
        {
            get => _isSelectedPy;
            set { _isSelectedPy = value; OnPropertyChanged(); }
        }

        public bool IsSelectedCosf
        {
            get => _isSelectedCosf;
            set { _isSelectedCosf = value; OnPropertyChanged(); }
        }

        public bool IsSelectedCableLength
        {
            get => _isSelectedCableLength;
            set { _isSelectedCableLength = value; OnPropertyChanged(); }
        }

        public bool IsSelected3Phase
        {
            get => _isSelected3Phase;
            set { _isSelected3Phase = value; OnPropertyChanged(); }
        }

        public bool IsSelectedLoadName
        {
            get => _isSelectedLoadName;
            set { _isSelectedLoadName = value; OnPropertyChanged(); }
        }

        public bool IsSelectedElemsCount
        {
            get => _isSelectedElemsCount;
            set { _isSelectedElemsCount = value; OnPropertyChanged(); }
        }

        public bool IsSelectedCableWay
        {
            get => _isSelectedCableWay;
            set { _isSelectedCableWay = value; OnPropertyChanged(); }
        }

        // Вспомогательные свойства для подсветки различий
        public bool IsPyDifferent => SystemPy != AnnoPy;
        public bool IsCosfDifferent => SystemCosf != AnnoCosf;
        public bool IsConductorL1Different => SystemConductorL1 != AnnoConductorL1;
        public bool IsConductorL2Different => SystemConductorL2 != AnnoConductorL2;
        public bool IsConductorL3Different => SystemConductorL3 != AnnoConductorL3;
        public bool Is3PhaseDifferent => ThreePhase != Anno3PhaseDevice; 
        public bool IsLoadNameDifferent => SystemLoadName != AnnoDeviceName;
        public bool IsElemsCountDifferent => SystemElemsCount != AnnoElemsCount;
        public bool IsCableWayDifferent => SystemCableWay != AnnoCableWay;
    }
}
