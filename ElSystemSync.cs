using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using TNovCommon;
using static TNovElectrical.CableWays;

namespace TNovElectrical
{
    
    [Transaction(TransactionMode.Manual)]
    public class ElSystemSync : IExternalCommand
    {
        private TNovProgressBar Class1ProgressBar;
        private void ThreadStartingPoint()
        {
            this.Class1ProgressBar = new TNovProgressBar();
            this.Class1ProgressBar.Show();
            Dispatcher.Run();
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            #region Исходные
            DateTime dateTime = DateTime.Now;
            string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string DBCommandName = "Синхронизатор";
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;
            string docName = doc.Title.ToString(); docName = docName.Replace(",", " ");
            string userName = rvtApp.Username; userName = userName.Replace(",", "");
            string docNameUserName = "_" + userName; docName = docName.Replace(docNameUserName, "");
            docName = docName.Replace(",", "");
            #endregion

            TNovConfig config = TNovConfigLoad.LoadConfig(DBCommandName, TNovVersion);

            #region Настройки логов
            // создание log - файла
            Logger.Initialize(DBCommandName, dateTime, TNovVersion);

            var viewModel0 = new AppVersionViewModel();

            string jsonpath0 = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TNovClient/TNovSettings.json");
            viewModel0 = JsonConvert.DeserializeObject<AppVersionViewModel>(File.ReadAllText(jsonpath0));
            if (viewModel0.extendedLogs)

            {
                var qViewModel = new QuestionWindowViewModel();
                qViewModel.headtxt = "Включены расширенные логи. " +
                    "Плагин будет работать медленнее, но соберет больше данных. " +
                    "Выключить расширенные логи для ускорения работы?";
                var qwpfview = new QuestionWindow280(qViewModel);
                qViewModel.CloseRequest += (s, e) => qwpfview.Close();
                bool? qok = qwpfview.ShowDialog();
                if (qok != null && qok == true) { Logger.TurnOffExtendedLogs(); } else Logger.Log("Расширенные логи вкл", 2);
            }
            #endregion

            #region Параметры
            Guid systemConductorL1ParamGuid = new Guid("814b4466-eac4-4976-8c8a-1df5ef27269c"); //Длина проводника
            Guid systemConductorL2ParamGuid = new Guid("ee24b017-3f41-4e20-aa8f-9baf85aa5a5d"); //Длина проводника до дальнего устройства
            Guid systemConductorL3ParamGuid = new Guid("605255f5-4fce-481b-a9a9-bd551a510f52"); //Длина проводника приведённая 
            Guid systemCableWayParamGuid = new Guid("f630bce9-293c-48c0-8fb4-cceff61c4068"); //Способ прокладки 
            #endregion

            #region Сбор элементов

            List<RevitLinkInstance> links = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_RvtLinks)
            .WhereElementIsNotElementType()
            .Cast<RevitLinkInstance>()
            .ToList();
            

            //анализ текущей выборки
            Autodesk.Revit.UI.Selection.Selection selection = commandData.Application.ActiveUIDocument.Selection;
            List<AnnotationSymbol> annoList1 = new List<AnnotationSymbol>();


            Logger.Log("Анализ текущей выборки", 1);
            annoList1 = CableWays.GetAnnoFromCurrentSelection(doc, selection); //получаем автоматы из текущей выборки
            if (annoList1.Count == 0) //запускаем выбор элементов если ничего не выбрано
            {
                AnnoSelectionFilter annoSelectionFilter = new AnnoSelectionFilter();
                IList<Reference> referenceList;
                try
                {
                    referenceList = selection.PickObjects((ObjectType)1, (ISelectionFilter)annoSelectionFilter, "Выберите автоматы");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException ex)
                {
                    Logger.Log("Отменено: " + ex.Message + ". Завершение работы.", 3); return Result.Cancelled;
                }
                foreach (Reference reference in (IEnumerable<Reference>)referenceList)
                    annoList1.Add(doc.GetElement(reference) as AnnotationSymbol);
            }
            if (annoList1 == null || annoList1.Count < 1)
            {
                Logger.Log("Выборка пуста. Завершение работы.", 3); return Result.Cancelled;
            }

            List<ElSystem> elSystems = new List<ElSystem>(); //список ElSystem
            List<string> linkNames = new List<string>(); //список связей для окна

            foreach(var link in links)
            {
                //можно добавить логику фильтрации связей по ЭЛ
                string[] titleParts = link.Name.Split(':');
                string linkName = titleParts[0].TrimEnd(' ');
                linkNames.Add(linkName);
            }
            linkNames.Add("Текущий файл");//добавляем возможность работы с цепями в текущем файле

            #endregion

            #region Диалог
            Logger.Log("Стартовое окно", 1);
            

            var dialog = new ElSystemSyncSimpleWPF
            {
                LinkNames = linkNames
            };

            if (dialog.ShowDialog() == true) { }
            else { Logger.Log("Отменено пользователем. Завершение работы.", 3); return Result.Cancelled; }

            #endregion

            #region Файл для работы
            Document linkDoc = doc;
            foreach (var link in links)
            {
                string[] titleParts = link.Name.Split(':');
                string linkName = titleParts[0].TrimEnd(' ');
                if (dialog.SelectedLinkName.Contains(linkName))
                {
                    try
                    {
                        linkDoc = link.GetLinkDocument();
                    }
                    catch (Exception ex) 
                    { 
                        Logger.Log($"Связь {linkName} ошибка: {ex.Message}. Завершение работы.", 3);
                        new InfoWindow280($"Ошибка со связанным файлом {linkName}: {ex.Message}. Обновите связь RVT.").ShowDialog();
                        return Result.Cancelled;
                    }
                }
            }
            

            Logger.Log($"Выбран файл для работы: {linkDoc.Title}",1);

            List<ElectricalSystem> ElectricalSystems = new FilteredElementCollector(linkDoc)
                        .OfCategory(BuiltInCategory.OST_ElectricalCircuit)
                        .WhereElementIsNotElementType()
                        .Cast<ElectricalSystem>()
                        .ToList();


            if (ElectricalSystems.Count() > 0) { }
            else
            {
                Logger.Log("В выбранном файле отсутствуют цепи. Завершение работы.", 3);
                new InfoWindow280("В выбранном файле отсутствуют цепи!").ShowDialog();
                return Result.Cancelled;
            }
            #endregion

            #region Сбор данных
            //параметры выбранных автоматов
            foreach (AnnotationSymbol annoSymbol in annoList1)
            {
                Logger.Log($"   {annoSymbol.Id.IntegerValue.ToString()}", 1);
                string annoSystemNumber = "";
                if (Param.ParamExist("Номер цепи", annoSymbol) && annoSymbol.LookupParameter("Номер цепи").HasValue)
                {
                    annoSystemNumber = annoSymbol.LookupParameter("Номер цепи").AsString();
                    Logger.Log($"      Номер цепи: {annoSystemNumber.ToString()}", 1);
                }
                //проверяем наличие цепи в выбранном файле с цепями

                if (ElectricalSystems.Count() > 0)
                {
                    foreach (var eSystem in ElectricalSystems)
                    {
                        Parameter systemNumberParam = eSystem.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_NUMBER);
                        string systemNumber = "";
                        if (systemNumberParam != null && systemNumberParam.HasValue) systemNumber = systemNumberParam.AsString();

                        if (systemNumber == annoSystemNumber)
                        {
                            Parameter systemPyParam = eSystem.get_Parameter(BuiltInParameter.RBS_ELEC_TRUE_LOAD);
                            double systemPy = 0;
                            if (systemPyParam != null && systemPyParam.HasValue) systemPy = systemPyParam.AsDouble() / 10763.910417;
                            systemPy = Math.Round(systemPy, 2);//добавлено в 1.7
                            Logger.Log($"   Активная нагрузка: {systemPy.ToString()}", 2); //делить на 10763,910417

                            Parameter systemCosfParam = eSystem.get_Parameter(BuiltInParameter.RBS_ELEC_POWER_FACTOR);
                            double systemCosf = 0;
                            if (systemCosfParam != null && systemCosfParam.HasValue) systemCosf = systemCosfParam.AsDouble();
                            Logger.Log($"   Коэффициент мощности: {systemCosf.ToString()}", 2);

                            Parameter systemConductorL1Param = eSystem.get_Parameter(systemConductorL1ParamGuid);
                            double systemConductorL1 = 0;
                            if (systemConductorL1Param != null && systemConductorL1Param.HasValue) systemConductorL1 = systemConductorL1Param.AsDouble();
                            Logger.Log($"   Длина проводника: {systemConductorL1.ToString()}", 2);

                            Parameter systemConductorL2Param = eSystem.get_Parameter(systemConductorL2ParamGuid);
                            double systemConductorL2 = 0;
                            if (systemConductorL2Param != null && systemConductorL2Param.HasValue) systemConductorL2 = systemConductorL2Param.AsDouble();
                            Logger.Log($"   Длина проводника до дальнего устройства: {systemConductorL2.ToString()}", 2);

                            Parameter systemConductorL3Param = eSystem.get_Parameter(systemConductorL3ParamGuid);
                            double systemConductorL3 = 0;
                            if (systemConductorL3Param != null && systemConductorL3Param.HasValue) systemConductorL3 = systemConductorL3Param.AsDouble();
                            Logger.Log($"   Длина проводника приведенная: {systemConductorL3.ToString()}", 2);

                            string threePhase = "1-фазный";
                            Parameter systemVoltageParam = eSystem.get_Parameter(BuiltInParameter.RBS_ELEC_VOLTAGE);
                            double systemVoltage = 0;
                            int threePhaseInt = 0;
                            string voltageStr = "";
                            if (systemVoltageParam != null && systemVoltageParam.HasValue)
                            {
                                systemVoltage = systemVoltageParam.AsDouble();
                                voltageStr = systemVoltageParam.AsValueString();
                            }
                            if (systemVoltage > 2500) { threePhase = "3-фазный"; threePhaseInt = 1; }
                            Logger.Log($"   Напряжение: {voltageStr}", 2);

                            Parameter systemLoadNameParam = eSystem.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_NAME);
                            string systemLoadName = "";
                            if (systemLoadNameParam != null && systemLoadNameParam.HasValue) systemLoadName = systemLoadNameParam.AsString();
                            Logger.Log($"   Имя нагрузки: {systemLoadName}", 2);

                            Parameter systemElemsCountParam = eSystem.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_NUMBER_OF_ELEMENTS_PARAM);
                            int systemElemsCount = 0;
                            if (systemElemsCountParam != null && systemElemsCountParam.HasValue) systemElemsCount = systemElemsCountParam.AsInteger(); //исправлено в 1.7
                            Logger.Log($"   Число электроприёмников: {systemElemsCount.ToString()}", 2);

                            Parameter systemCableWayParam = eSystem.get_Parameter(systemCableWayParamGuid);
                            string systemCableWay = "";
                            if (systemCableWayParam != null && systemCableWayParam.HasValue) systemCableWay = systemCableWayParam.AsString();
                            systemCableWay = systemCableWay.Replace(";", "; ");//добавлено в 1.7
                            Logger.Log($"   Способ прокладки: {systemCableWay}", 2);

                            double annoPy = 0;
                            if (Param.ParamExist("Py", annoSymbol) && annoSymbol.LookupParameter("Py").HasValue)
                            {
                                annoPy = annoSymbol.LookupParameter("Py").AsDouble() / 10763.910417;
                                Logger.Log($"      Py: {annoPy.ToString()}", 2);
                            }
                            double annoCosf = 0;
                            if (Param.ParamExist("Cosf", annoSymbol) && annoSymbol.LookupParameter("Cosf").HasValue)
                            {
                                annoCosf = annoSymbol.LookupParameter("Cosf").AsDouble();
                                Logger.Log($"      Cosf: {annoCosf.ToString()}", 2);
                            }
                            double annoConductorL1 = 0;
                            if (Param.ParamExist("Длина проводника", annoSymbol) && annoSymbol.LookupParameter("Длина проводника").HasValue)
                            {
                                annoConductorL1 = annoSymbol.LookupParameter("Длина проводника").AsDouble();
                                Logger.Log($"      Длина проводника: {annoConductorL1.ToString()}", 2);
                            }
                            double annoConductorL2 = 0;
                            if (Param.ParamExist("Длина проводника до дальнего устройства", annoSymbol) && annoSymbol.LookupParameter("Длина проводника до дальнего устройства").HasValue)
                            {
                                annoConductorL2 = annoSymbol.LookupParameter("Длина проводника до дальнего устройства").AsDouble();
                                Logger.Log($"      Длина проводника до дальнего устройства: {annoConductorL2.ToString()}", 2);
                            }
                            double annoConductorL3 = 0;
                            if (Param.ParamExist("Длина проводника приведённая", annoSymbol) && annoSymbol.LookupParameter("Длина проводника приведённая").HasValue)
                            {
                                annoConductorL3 = annoSymbol.LookupParameter("Длина проводника приведённая").AsDouble();
                                Logger.Log($"      аннотация: {annoConductorL3.ToString()}", 2);
                            }
                            string anno3PhaseDevice = "1-фазный";
                            if (Param.ParamExist("3-фазный аппарат", annoSymbol) && annoSymbol.LookupParameter("3-фазный аппарат").HasValue)
                            {
                                if (annoSymbol.LookupParameter("3-фазный аппарат").AsInteger() == 1) anno3PhaseDevice = "3-фазный";
                                Logger.Log($"      аннотация: {anno3PhaseDevice}", 2);
                            }
                            string annoDeviceName = "";
                            if (Param.ParamExist("Наименование электроприёмника", annoSymbol) && annoSymbol.LookupParameter("Наименование электроприёмника").HasValue)
                            {
                                annoDeviceName = annoSymbol.LookupParameter("Наименование электроприёмника").AsString();
                                Logger.Log($"      Наименование электроприёмника: {annoDeviceName}", 2);
                            }
                            int annoElemsCount = 0;
                            if (Param.ParamExist("Число электроприёмников", annoSymbol) && annoSymbol.LookupParameter("Число электроприёмников").HasValue)
                            {
                                annoElemsCount = annoSymbol.LookupParameter("Число электроприёмников").AsInteger();
                                Logger.Log($"      Число электроприёмников: {annoElemsCount.ToString()}", 2);
                            }
                            string annoCableWay = "";
                            if (Param.ParamExist("Способ прокладки", annoSymbol) && annoSymbol.LookupParameter("Способ прокладки").HasValue)
                            {
                                annoCableWay = annoSymbol.LookupParameter("Способ прокладки").AsString();
                                Logger.Log($"      Способ прокладки: {annoCableWay}", 2);
                            }

                            Logger.Log("Собираем данные для вспомогательного класса ElSystem", 1);

                            ElSystem elSystem = new ElSystem()
                            {
                                SystemNumber = systemNumber,
                                AnnoSystemNumber = annoSystemNumber,
                                SystemPy = systemPy,
                                AnnoPy = annoPy,
                                SystemCosf = systemCosf,
                                AnnoCosf = annoCosf,
                                SystemConductorL1 = systemConductorL1,
                                AnnoConductorL1 = annoConductorL1,
                                SystemConductorL2 = systemConductorL2,
                                AnnoConductorL2 = annoConductorL2,
                                SystemConductorL3 = systemConductorL3,
                                AnnoConductorL3 = annoConductorL3,
                                SystemVoltage = voltageStr,
                                ThreePhase = threePhase,
                                ThreePhaseInt = threePhaseInt,
                                Anno3PhaseDevice = anno3PhaseDevice,
                                SystemLoadName = systemLoadName,
                                AnnoDeviceName = annoDeviceName,
                                SystemElemsCount = systemElemsCount,
                                AnnoElemsCount = annoElemsCount,
                                SystemCableWay = systemCableWay,
                                AnnoCableWay = annoCableWay,
                                AnnoId = annoSymbol.Id.IntegerValue,
                            };
                            elSystems.Add(elSystem);
                        }


                    }


                }

                


            }
            #endregion

            Thread thread = new Thread(new ThreadStart(this.ThreadStartingPoint));
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            Thread.Sleep(100);

            int PBCount = 0;
            this.Class1ProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.Class1ProgressBar.TNov_ProgressBar.Minimum = (double)PBCount));
            this.Class1ProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.Class1ProgressBar.value.Text = PBCount.ToString()));
            this.Class1ProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.Class1ProgressBar.TNov_ProgressBar.Maximum = elSystems.Count));
            this.Class1ProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.Class1ProgressBar.maxvalue.Text = elSystems.Count.ToString()));

            bool unhandledError = false;
            #region Основной код
            using (Transaction t = new Transaction(RevitAPI.Document, "TNov - Синхронизатор"))
            {
                try
                {
                    t.Start();
                    Logger.Log("Открываем транзакцию", 1);

                    foreach (var elSystem in elSystems)
                    {

                        Element elem = doc.GetElement(new ElementId(elSystem.AnnoId));
                        Logger.Log(elSystem.AnnoId.ToString(), 1);
                        if (dialog.IsPySelected && Param.ParamExist("Py", elem))
                        {
                            try
                            {
                                elem.LookupParameter("Py").Set(elSystem.SystemPy);
                                Logger.Log($"Назначен параметр Py: {elSystem.SystemPy.ToString()}", 2);
                            }
                            catch (Exception e)
                            {
                                Logger.Log($"Ошибка назначения параметра Py: {e.Message}", 4);
                            }
                        }
                        if (dialog.IsCosfSelected && Param.ParamExist("Cosf", elem))
                        {
                            try
                            {
                                elem.LookupParameter("Cosf").Set(elSystem.SystemCosf);
                                Logger.Log($"Назначен параметр Cosf: {elSystem.SystemCosf.ToString()}", 2);
                            }
                            catch (Exception e)
                            {
                                Logger.Log($"Ошибка назначения параметра Cosf: {e.Message}", 4);
                            }
                        }
                        if (dialog.IsCableLengthSelected)
                        {
                            if (Param.ParamExist("Длина проводника", elem))
                            {
                                try
                                {
                                    elem.LookupParameter("Длина проводника").Set(elSystem.SystemConductorL1);
                                    Logger.Log($"Назначен параметр Длина проводника: {elSystem.SystemConductorL1.ToString()}", 2);
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
                                    elem.LookupParameter("Длина проводника до дальнего устройства").Set(elSystem.SystemConductorL2);
                                    Logger.Log($"Назначен параметр Длина проводника до дальнего устройства: {elSystem.SystemConductorL2.ToString()}", 2);
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
                                    elem.LookupParameter("Длина проводника приведённая").Set(elSystem.SystemConductorL3);
                                    Logger.Log($"Назначен параметр Длина проводника приведённая: {elSystem.SystemConductorL3.ToString()}", 2);
                                }
                                catch (Exception e)
                                {
                                    Logger.Log($"Ошибка назначения параметра Длина проводника приведённая: {e.Message}", 4);
                                }
                            }
                        }
                        if (dialog.Is3PhaseSelected && Param.ParamExist("3-фазный аппарат", elem))
                        {
                            try
                            {
                                elem.LookupParameter("3-фазный аппарат").Set(elSystem.ThreePhaseInt);
                                Logger.Log($"Назначен параметр 3-фазный аппарат: {elSystem.ThreePhaseInt.ToString()} ({elSystem.ThreePhase})", 2);
                            }
                            catch (Exception e)
                            {
                                Logger.Log($"Ошибка назначения параметра 3-фазный аппарат: {e.Message}", 4);
                            }
                        }
                        if (dialog.IsLoadNameSelected && Param.ParamExist("Наименование электроприёмника", elem))
                        {
                            try
                            {
                                elem.LookupParameter("Наименование электроприёмника").Set(elSystem.SystemLoadName);
                                Logger.Log($"Назначен параметр Наименование электроприёмника: {elSystem.SystemLoadName}", 2);
                            }
                            catch (Exception e)
                            {
                                Logger.Log($"Ошибка назначения параметра Наименование электроприёмника: {e.Message}", 4);
                            }
                        }
                        if (dialog.IsElemsCountSelected && Param.ParamExist("Число электроприёмников", elem))
                        {
                            try
                            {
                                elem.LookupParameter("Число электроприёмников").Set(elSystem.SystemElemsCount);
                                Logger.Log($"Назначен параметр Число электроприёмников: {elSystem.SystemElemsCount.ToString()}", 2);
                            }
                            catch (Exception e)
                            {
                                Logger.Log($"Ошибка назначения параметра Число электроприёмников: {e.Message}", 4);
                            }
                        }
                        if (dialog.IsCableWaySelected && Param.ParamExist("Способ прокладки", elem))
                        {
                            try
                            {
                                elem.LookupParameter("Способ прокладки").Set(elSystem.SystemCableWay);
                                Logger.Log($"Назначен параметр Способ прокладки: {elSystem.SystemCableWay}", 2);
                            }
                            catch (Exception e)
                            {
                                Logger.Log($"Ошибка назначения параметра Способ прокладки: {e.Message}", 4);
                            }
                        }

                        PBCount++;
                        this.Class1ProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.Class1ProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                        this.Class1ProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.Class1ProgressBar.value.Text = PBCount.ToString()));

                    }

                    t.Commit();
                    Logger.Log("Закрываем транзакцию", 1);
                }
                catch (Exception ex)
                {
                    Logger.Log("Ошибка: " + ex.Message, 4);
                    new InfoWindow280("Ошибка: " + ex.Message).ShowDialog();
                    unhandledError = true;
                }
                finally
                {
                    CloseProgressBarSafely();
                }
            }
            #endregion

            if (unhandledError)
            {
                Logger.Log("Завершение работы с ошибками.", 4);
                return Result.Succeeded;
            }

            Logger.Log("Завершение работы.", 5);
            return Result.Succeeded;
        }
        private void CloseProgressBarSafely()
        {
            if (Class1ProgressBar != null &&
                Class1ProgressBar.Dispatcher != null &&
                !Class1ProgressBar.Dispatcher.HasShutdownStarted)
            {
                Class1ProgressBar.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (Class1ProgressBar.IsLoaded)
                        Class1ProgressBar.Close();
                    // Завершаем цикл сообщений диспетчера, чтобы поток завершился
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }));
            }
        }
    }
}
