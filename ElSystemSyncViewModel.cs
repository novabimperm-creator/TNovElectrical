using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using TNovCommon;

namespace TNovElectrical
{
    public class ElSystemSyncViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<ElSystem> _allSystems;
        private List<string> _linkNames;
        private string _selectedLinkName;
        private List<string> _systemNumbers;
        private string _selectedSystemNumber;
        private ElSystem _currentSystem;
        private bool _selectAll;

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public ElSystemSyncViewModel(IEnumerable<ElSystem> systems)
        {
            AllSystems = new ObservableCollection<ElSystem>(systems);
            LinkNames = AllSystems.Select(s => s.LinkName).Distinct().ToList();
            if (LinkNames.Any())
                SelectedLinkName = LinkNames.First();

            ApplyCommand = new RelayCommand2(_ => Apply(), _ => CurrentSystem != null);
            ExitCommand = new RelayCommand2(_ => Exit());
        }

        public ObservableCollection<ElSystem> AllSystems
        {
            get => _allSystems;
            set { _allSystems = value; OnPropertyChanged(); }
        }

        public List<string> LinkNames
        {
            get => _linkNames;
            set { _linkNames = value; OnPropertyChanged(); }
        }

        public string SelectedLinkName
        {
            get => _selectedLinkName;
            set
            {
                if (_selectedLinkName != value)
                {
                    _selectedLinkName = value;
                    OnPropertyChanged();
                    UpdateSystemNumbers();
                }
            }
        }

        public List<string> SystemNumbers
        {
            get => _systemNumbers;
            set { _systemNumbers = value; OnPropertyChanged(); }
        }

        public string SelectedSystemNumber
        {
            get => _selectedSystemNumber;
            set
            {
                if (_selectedSystemNumber != value)
                {
                    _selectedSystemNumber = value;
                    OnPropertyChanged();
                    UpdateCurrentSystem();
                }
            }
        }

        public ElSystem CurrentSystem
        {
            get => _currentSystem;
            set
            {
                if (_currentSystem != value)
                {
                    _currentSystem = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasCurrentSystem));
                    // При смене текущего элемента сбросить SelectAll
                    SelectAll = false;
                }
            }
        }

        public bool HasCurrentSystem => CurrentSystem != null;

        public bool SelectAll
        {
            get => _selectAll;
            set
            {
                if (_selectAll != value)
                {
                    _selectAll = value;
                    OnPropertyChanged();
                    if (CurrentSystem != null)
                    {
                        CurrentSystem.IsSelectedPy = value;
                        CurrentSystem.IsSelectedCosf = value;
                        CurrentSystem.IsSelectedCableLength = value;
                        CurrentSystem.IsSelected3Phase = value;
                        CurrentSystem.IsSelectedLoadName = value;
                        CurrentSystem.IsSelectedElemsCount = value;
                        CurrentSystem.IsSelectedCableWay = value;
                    }
                }
            }
        }

        public ICommand ApplyCommand { get; }
        public ICommand ExitCommand { get; }

        private void UpdateSystemNumbers()
        {
            if (string.IsNullOrEmpty(SelectedLinkName))
            {
                SystemNumbers = new List<string>();
                SelectedSystemNumber = null;
                return;
            }

            SystemNumbers = AllSystems
                .Where(s => s.LinkName == SelectedLinkName)
                .Select(s => s.SystemNumber)
                .Distinct()
                .ToList();

            SelectedSystemNumber = SystemNumbers.FirstOrDefault();
        }

        private void UpdateCurrentSystem()
        {
            if (string.IsNullOrEmpty(SelectedLinkName) || string.IsNullOrEmpty(SelectedSystemNumber))
            {
                CurrentSystem = null;
                return;
            }

            CurrentSystem = AllSystems.FirstOrDefault(s =>
                s.LinkName == SelectedLinkName && s.SystemNumber == SelectedSystemNumber);
        }

        private void Apply()
        {
            if (CurrentSystem == null) return;

            using (Transaction t = new Transaction(RevitAPI.Document, "TNov - Синхронизатор"))
            {
                t.Start();
                Logger.Log("Открываем транзакцию", 1);

                Autodesk.Revit.DB.Document doc = RevitAPI.Document;
                if (doc != null) 
                {
                    //назначение параметров аннотации (обновление данных в окне в случае успеха)
                    Element elem = doc.GetElement(new ElementId(CurrentSystem.AnnoId));
                    if(Param.ParamExist("Номер цепи", elem))
                    {
                        try
                        {
                            elem.LookupParameter("Номер цепи").Set(CurrentSystem.SystemNumber);
                            CurrentSystem.AnnoSystemNumber = CurrentSystem.SystemNumber;
                            Logger.Log($"Назначен параметр Номер цепи: {CurrentSystem.SystemNumber}", 2);
                        }
                        catch (Exception e) 
                        {
                            Logger.Log($"Ошибка назначения параметра Номер цепи: {e.Message}", 4);
                        }
                    }
                    if(CurrentSystem.IsSelectedPy&& Param.ParamExist("Py", elem))
                    {
                        try
                        {
                            elem.LookupParameter("Py").Set(CurrentSystem.SystemPy);
                            CurrentSystem.AnnoPy = CurrentSystem.SystemPy;
                            Logger.Log($"Назначен параметр Py: {CurrentSystem.SystemPy}", 2);
                        }
                        catch (Exception e)
                        {
                            Logger.Log($"Ошибка назначения параметра Py: {e.Message}", 4);
                        }
                    }
                    if (CurrentSystem.IsSelectedCosf && Param.ParamExist("Cosf", elem))
                    {
                        try
                        {
                            elem.LookupParameter("Cosf").Set(CurrentSystem.SystemCosf);
                            CurrentSystem.AnnoCosf = CurrentSystem.SystemCosf;
                            Logger.Log($"Назначен параметр Cosf: {CurrentSystem.SystemCosf}", 2);
                        }
                        catch (Exception e)
                        {
                            Logger.Log($"Ошибка назначения параметра Cosf: {e.Message}", 4);
                        }
                    }
                    if (CurrentSystem.IsSelectedCableLength)
                    {
                        if(Param.ParamExist("Длина проводника", elem))
                        {
                            try
                            {
                                elem.LookupParameter("Длина проводника").Set(CurrentSystem.SystemConductorL1);
                                CurrentSystem.AnnoConductorL1 = CurrentSystem.SystemConductorL1;
                                Logger.Log($"Назначен параметр Длина проводника: {CurrentSystem.SystemConductorL1.ToString()}", 2);
                            }
                            catch (Exception e)
                            {
                                Logger.Log($"Ошибка назначения параметра Длина проводника: {e.Message}", 4);
                            }
                        }
                        if (Param.ParamExist("Длина проводника до дальнего устройства", elem))
                        {
                            try
                            {
                                elem.LookupParameter("Длина проводника до дальнего устройства").Set(CurrentSystem.SystemConductorL2);
                                CurrentSystem.AnnoConductorL2 = CurrentSystem.SystemConductorL2;
                                Logger.Log($"Назначен параметр Длина проводника до дальнего устройства: {CurrentSystem.SystemConductorL2.ToString()}", 2);
                            }
                            catch (Exception e)
                            {
                                Logger.Log($"Ошибка назначения параметра Длина проводника до дальнего устройства: {e.Message}", 4);
                            }
                        }
                        if (Param.ParamExist("Длина проводника приведённая", elem))
                        {
                            try
                            {
                                elem.LookupParameter("Длина проводника приведённая").Set(CurrentSystem.SystemConductorL3);
                                CurrentSystem.AnnoConductorL3 = CurrentSystem.SystemConductorL3;
                                Logger.Log($"Назначен параметр Длина проводника приведённая: {CurrentSystem.SystemConductorL3.ToString()}", 2);
                            }
                            catch (Exception e)
                            {
                                Logger.Log($"Ошибка назначения параметра Длина проводника приведённая: {e.Message}", 4);
                            }
                        }
                    }
                    if (CurrentSystem.IsSelected3Phase && Param.ParamExist("3-фазный аппарат", elem))
                    {
                        try
                        {
                            elem.LookupParameter("3-фазный аппарат").Set(CurrentSystem.ThreePhase);
                            CurrentSystem.Anno3PhaseDevice = CurrentSystem.ThreePhase;
                            Logger.Log($"Назначен параметр 3-фазный аппарат: {CurrentSystem.ThreePhase.ToString()}", 2);
                        }
                        catch (Exception e)
                        {
                            Logger.Log($"Ошибка назначения параметра 3-фазный аппарат: {e.Message}", 4);
                        }
                    }
                    if (CurrentSystem.IsSelectedLoadName && Param.ParamExist("Наименование электроприёмника", elem))
                    {
                        try
                        {
                            elem.LookupParameter("Наименование электроприёмника").Set(CurrentSystem.SystemLoadName);
                            CurrentSystem.AnnoDeviceName = CurrentSystem.SystemLoadName;
                            Logger.Log($"Назначен параметр Наименование электроприёмника: {CurrentSystem.SystemLoadName.ToString()}", 2);
                        }
                        catch (Exception e)
                        {
                            Logger.Log($"Ошибка назначения параметра Наименование электроприёмника: {e.Message}", 4);
                        }
                    }
                    if (CurrentSystem.IsSelectedElemsCount && Param.ParamExist("Число электроприёмников", elem))
                    {
                        try
                        {
                            elem.LookupParameter("Число электроприёмников").Set(CurrentSystem.SystemElemsCount);
                            CurrentSystem.AnnoElemsCount = CurrentSystem.SystemElemsCount;
                            Logger.Log($"Назначен параметр Число электроприёмников: {CurrentSystem.SystemElemsCount.ToString()}", 2);
                        }
                        catch (Exception e)
                        {
                            Logger.Log($"Ошибка назначения параметра Число электроприёмников: {e.Message}", 4);
                        }
                    }
                    if (CurrentSystem.IsSelectedCableWay && Param.ParamExist("Способ прокладки", elem))
                    {
                        try
                        {
                            elem.LookupParameter("Способ прокладки").Set(CurrentSystem.SystemCableWay);
                            CurrentSystem.AnnoCableWay = CurrentSystem.SystemCableWay;
                            Logger.Log($"Назначен параметр Способ прокладки: {CurrentSystem.SystemCableWay.ToString()}", 2);
                        }
                        catch (Exception e)
                        {
                            Logger.Log($"Ошибка назначения параметра Способ прокладки: {e.Message}", 4);
                        }
                    }
                    CommandManager.InvalidateRequerySuggested();
                }
                t.Commit();
                Logger.Log("Закрываем транзакцию", 1);
            }

            
        }


        private void Exit()
        {
            RequestClose?.Invoke(this, true);
        }

        public event EventHandler<bool> RequestClose;
    }
}
