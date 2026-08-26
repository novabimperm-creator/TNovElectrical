using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB.Electrical;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using System.IO;
using System.Collections;
using TNovCommon;

namespace TNovElectrical
{
    

    [Transaction(TransactionMode.Manual)]
    public class CableTrays : IExternalCommand
    {
        
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            #region Исходные
            DateTime dateTime = DateTime.Now;
            string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string DBCommandName = "Лотки";
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
            BuiltInParameter mark = BuiltInParameter.DOOR_NUMBER; //параметр Марка
            BuiltInParameter height = BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM; //параметр Высота (лотка)
            BuiltInParameter width = BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM; //параметр Ширина (лотка)
            #endregion

            bool ss = false;
            if (docName.Contains("СС") || docName.Contains("-СС") || docName.Contains("_СС")) ss = true;

            #region Сбор элементов

            Logger.Log("Сбор элементов", 1);

            List<CableTray> CTList = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_CableTray)   //фильтр по категории Каб лотки
                                                             .WhereElementIsNotElementType()
                                                             .Cast<CableTray>()
                                                             .ToList();

            //проверка наличия крышек в проекте
            List<FamilyInstance> GMs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel)   //фильтр по категории Об модели
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(FamilyInstance))
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();

            List<FamilySymbol> familytypes = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .ToList();


            if (CTList.Count < 1) 
            { 
                Logger.Log("Отсутствуют лотки в модели. Завершение работы", 3);
                new InfoWindow280("В модели отсутствуют лотки.").ShowDialog();
                return Result.Cancelled; 
            }

            //отсеиваем лотки "не специфицировать"

            List<CableTray> CTList1 = new List<CableTray>();

            foreach (CableTray ct in CTList)
            {
                Element elem = doc.GetElement(ct.Id);
                int parval = elem.LookupParameter("N_ЭЛ.Не специфицировать").AsInteger();
                if (parval != 1) { CTList1.Add(ct); }
            }

            #endregion

            #region Десериализация

            Logger.Log("Элементы собраны. Диалоговое окно", 1);

            var viewModel = new CableTraysViewModel();
            // Десериализация
            bool forProject = true;
            json js = new json(in DBCommandName, in forProject, out bool canserialize, out string jsonpath);
            if (canserialize)
            {
                viewModel = JsonConvert.DeserializeObject<CableTraysViewModel>(File.ReadAllText(jsonpath));
                Logger.Log("Десериализация прошла успешно",1);
            }

            string types = viewModel.types; string filter1 = viewModel.filter1; string filter2 = viewModel.filter2; bool replace = viewModel.replace; bool remove = viewModel.remove;
            string capname = viewModel.capname; string ptname = viewModel.ptname;
            double maxLengthM = viewModel.maxLength > 0 ? viewModel.maxLength : 3.0;
            Logger.Log(types,1);

            #endregion

            #region Деление лотков

            Logger.Log("Деление длинных лотков", 1);
            double maxLengthInternal = UnitUtils.ConvertToInternalUnits(maxLengthM, UnitTypeId.Meters);
            Logger.Log("Макс. длина лотка: " + maxLengthM.ToString() + " м", 1);

            int splitCreated = 0;
            using (Transaction transactionSplit = new Transaction(doc))
            {
                try
                {
                    transactionSplit.Start("TNov - Лотки.Деление");
                    TransactionHandler.SetWarningResolver(transactionSplit);

                    foreach (CableTray ct in CTList1.ToList())
                    {
                        splitCreated += SplitCableTrayIfNeeded(doc, ct, maxLengthInternal);
                    }

                    transactionSplit.Commit();
                    Logger.Log("Деление завершено. Создано дополнительных сегментов: " + splitCreated.ToString(), 1);
                }
                catch (Exception ex)
                {
                    Logger.Log("Ошибка при делении лотков: " + ex.Message, 4);
                    new InfoWindow280("Ошибка при делении лотков: " + ex.Message).ShowDialog();
                }
            }

            // Обновляем списки лотков после деления
            CTList = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_CableTray)
                                                     .WhereElementIsNotElementType()
                                                     .Cast<CableTray>()
                                                     .ToList();
            CTList1 = new List<CableTray>();
            foreach (CableTray ct in CTList)
            {
                Element elem = doc.GetElement(ct.Id);
                Parameter notSpec = elem.LookupParameter("N_ЭЛ.Не специфицировать");
                int parval = notSpec != null ? notSpec.AsInteger() : 0;
                if (parval != 1) { CTList1.Add(ct); }
            }

            #endregion

            #region Подготовка

            //заполняем списки

            List<FamilyInstance> caps = new List<FamilyInstance>(); //список крышек в проекте
            List<FamilyInstance> partitions = new List<FamilyInstance>(); //список перегородок в проекте
            foreach (FamilyInstance g in GMs)
            {
                if (g.Symbol.FamilyName.Contains(capname))
                {
                    caps.Add(g);
                }
                else if (g.Symbol.FamilyName.Contains(ptname))
                {
                    partitions.Add(g);
                }
            }
            List<FamilySymbol> captypes = new List<FamilySymbol>(); //список типов крышек в проекте
            Logger.Log("Типы крышек:", 1);
            foreach (FamilySymbol t in familytypes)
            {

                if (t.FamilyName.Contains(capname))
                {
                    captypes.Add(t); Logger.Log("   " + t.Name,1);
                }
            }
            List<FamilySymbol> partitiontypes = new List<FamilySymbol>(); //список типов перегородок в проекте
            Logger.Log("Типы перегородок:", 1);
            foreach (FamilySymbol t in familytypes)
            {

                if (t.FamilyName.Contains(ptname))
                {
                    partitiontypes.Add(t); Logger.Log("   " + t.Name, 1);
                }
            }


            Logger.Log("Проверяем наличие крышек в проекте", 1);
            if (captypes.Count == 0) 
            { 
                new InfoWindow280("Ошибка! В проекте отсутствуют загруженные семейства крышек.").ShowDialog();
                Logger.Log("Отсутствуют семейства крышек. Завершение работы.", 3);
                return Result.Failed; }
            Logger.Log("Проверяем наличие перегородок в проекте", 1);
            if (partitiontypes.Count == 0) 
            { 
                new InfoWindow280("Ошибка! В проекте отсутствуют загруженные семейства перегородок лотков.").ShowDialog();
                Logger.Log("Отсутствуют семейства перегородок. Завершение работы.", 3);
                return Result.Failed; 
            }


            //получаем типовые сочетания в именах типов лотков, удаляем возможные пробелы в начале и конце имен
            string[] types1 = types.Split(','); for (int i = 0; i < types1.Length; i++) { types1[i] = types1[i].Trim(); }

            List<CableTray> CTListCaps = new List<CableTray>(); //список лотков для добавления крышек
            List<CableTray> CTListPts = new List<CableTray>(); //список лотков для добавления перегородок

            Logger.Log("Заполняем списки лотков для добавления/замены крышек/перегородок",1);
            
            foreach (CableTray ct in CTList1)
            {
                bool addtolist1 = false; bool addtolist2 = false; //не добавлять по умолчанию
                Element elem1 = doc.GetElement(ct.Id);
#if R2022
                long ctId = ct.Id.IntegerValue;
#else
                long ctId = ct.Id.Value;
#endif
                string type = doc.GetElement(elem1.GetTypeId()).Name;
                Logger.Log("Лотки для крышек:",1);
                if (type.Contains(filter1)) //исключаем лотки, которым не нужны крышки
                {
                    addtolist1 = true; //добавлять по умолчанию если в имени типа лотка есть "с крышкой"
                    if (replace == false && caps.Count > 0) //если галочка Пересоздать выключена и в модели есть крышки - проверяем наличие крышек у лотков и исключаем лотки с крышками
                    {
                        foreach (FamilyInstance cap in caps)
                        {
                            Element elem = doc.GetElement(cap.Id);
                            int mrkint = 0;
                            string mrkstr = elem.get_Parameter(mark).AsString();
                            if (mrkstr != null)
                            {
                                int.TryParse(mrkstr, out mrkint);
                                if (mrkint != 0)
                                {
                                    if (ctId == mrkint ) { addtolist1 = false; break; }
                                }
                            }
                        }
                    }
                    if(addtolist1) { CTListCaps.Add(ct); Logger.Log(ct.Id.ToString(),1); }
                }
                if (type.Contains(filter2)) //исключаем лотки, которым не нужны перегородки
                {
                    addtolist2 = true; //добавлять по умолчанию если в имени типа лотка есть "перегородкой"
                    if (replace == false && partitions.Count > 0) //если галочка Пересоздать выключена и в модели есть перегородки - проверяем наличие перегородок у лотков и исключаем лотки с перегородками
                    {
                        foreach (FamilyInstance partition in partitions)
                        {
                            Element elem = doc.GetElement(partition.Id);
                            int mrkint = 0;
                            string mrkstr = elem.get_Parameter(mark).AsString();
                            if (mrkstr != null)
                            {
                                int.TryParse(mrkstr, out mrkint);
                                if (mrkint != 0)
                                {
                                    if (ctId == mrkint) { addtolist2 = false; break; }
                                }
                            }
                        }
                    }
                    if (addtolist2) { CTListPts.Add(ct); Logger.Log(ct.Id.ToString(), 1); }
                }
            }

            #endregion

            int count1 = 0; int count2 = 0;

            bool unhandledError = false;
            //транзакции

            #region Основной код. Крышки

            using (Transaction transaction1 = new Transaction(doc))
            {
                try { 
                transaction1.Start("TNov - Лотки.Крышки");
                TransactionHandler.SetWarningResolver(transaction1);
                Logger.Log("Открываем транзакцию (крышки)", 1);

                if (remove)
                {
                    //удаление крышек без марки
                    Logger.Log("Ищем крышки без марки", 1);

                    ICollection<ElementId> capswithoutmark = new List<ElementId>();
                    int cwm = 0;
                    foreach (FamilyInstance cap in caps)
                    {
                        string capmark = cap.get_Parameter(mark).AsString();
                        if (capmark != null)
                        {
                            bool cableTrayexists = false;
                            foreach (CableTray ct in CTListCaps)
                            {
                                string ctIdstr = ct.Id.ToString();
                                if (capmark.Contains(ctIdstr))
                                {
                                    cableTrayexists = true; break;
                                }
                            }
                            if (!cableTrayexists) { capswithoutmark.Add(cap.Id); cwm++; }
                        }
                        else { capswithoutmark.Add(cap.Id); cwm++; }
                         
                    }
                    if (cwm > 0)
                    {
                        doc.Delete(capswithoutmark.ToArray());
                        Logger.Log("Удалено " + cwm.ToString() + " элементов",2);
                    }
                    else { Logger.Log("крышки без марки отсутствуют",1); }
                }
                
                
                                

                //обновленные списки

                List<FamilyInstance> GMs1 = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel)   //фильтр по категории Об модели
                                                             .WhereElementIsNotElementType()
                                                             .OfClass(typeof(FamilyInstance))
                                                             .Cast<FamilyInstance>()
                                                             .ToList();
                List<FamilyInstance> caps1 = new List<FamilyInstance>();
                foreach (FamilyInstance g in GMs1)
                {
                    if (g.Symbol.FamilyName.Contains("Крышка"))
                    {
                        caps1.Add(g);
                    }
                }

                //удаляем крышки, которые будут пересозданы
                Logger.Log("Удаляем крышки, которые будут пересозданы", 1);
                ICollection<ElementId> capstoremove = new List<ElementId>();
                int ctr = 0;
                if (replace)
                {
                    foreach (FamilyInstance cap in caps1)
                    {
                        Element elem = doc.GetElement(cap.Id);
                        int mrkint = 0;
                        string mrkstr = elem.get_Parameter(mark).AsString();
                        if (mrkstr != null)
                        {
                            int.TryParse(mrkstr, out mrkint);
                            if (mrkint != 0)
                            {
                                foreach (CableTray ct in CTList1)
                                {
                                    Element elem1 = doc.GetElement(ct.Id);
#if R2022
                long elem1Id = elem1.Id.IntegerValue;
#else
                                        long elem1Id = elem1.Id.Value;
#endif

                                        
                                    if (elem1Id == mrkint) { capstoremove.Add(cap.Id); ctr++; break; }
                                }
                            }
                        }
                    }
                    if (capstoremove.Count > 0) 
                    { doc.Delete(capstoremove.ToArray()); Logger.Log("Удалено " + ctr.ToString() + " элементов",2); }
                    else { Logger.Log("лишние крышки отсутствуют", 1); }
                }

                //создание крышек
                Logger.Log("Создаем крышки для лотков", 1);
                foreach (CableTray ct in CTListCaps)
                {
                    Element element1 = doc.GetElement(ct.Id);

                    double num2 = element1.get_Parameter(height).AsDouble();
                    double num3 = element1.get_Parameter(width).AsDouble();
                    XYZ endPoint1 = (element1.Location as LocationCurve).Curve.GetEndPoint(0);
                    XYZ endPoint2 = (element1.Location as LocationCurve).Curve.GetEndPoint(1);
                    Element element2 = (Element)null;
                    foreach (Connector connector in ((IEnumerable)((MEPCurve)(element1 as CableTray)).ConnectorManager.Connectors).Cast<Connector>())
                    {
                        foreach (Connector allRef in connector.AllRefs)
                        {
                            if (allRef.Owner is FamilyInstance)
                                element2 = allRef.Owner;
                        }
                    }

                    string ctType = ct.Name; //тип лотка
                    Logger.Log("Лоток " + ct.Id.ToString() + " : тип " + ctType,2);
                    bool run = false;
                    foreach (string t in types1)
                    {
                        if (ctType.Contains(t)) { ctType = t; run = true; } //принципиальный тип лотка
                    }
                    FamilySymbol familySymbol = null;
                    foreach (FamilySymbol captype in captypes)
                    {
                        if (captype.Name.Contains(ctType)) { familySymbol = captype; break; }
                    }
                    if (run && familySymbol != null)
                    {
                        Logger.Log("   тип крышки: " + familySymbol.Name,2);
                        //создание крышки
                        FamilyInstance componentInstance = AdaptiveComponentInstanceUtils.CreateAdaptiveComponentInstance(doc, familySymbol);
                        IList<ElementId> pointElementRefIds = AdaptiveComponentInstanceUtils.GetInstancePlacementPointElementRefIds(componentInstance);
                        ReferencePoint element3 = doc.GetElement(pointElementRefIds.First<ElementId>()) as ReferencePoint;
                        ReferencePoint element4 = doc.GetElement(pointElementRefIds.Last<ElementId>()) as ReferencePoint;
                        if (Math.Round(endPoint1.X, 3) == Math.Round(endPoint2.X, 3) && Math.Round(endPoint1.Y, 3) == Math.Round(endPoint2.Y, 3))
                        {
                            element3.SetCoordinateSystem((element2 as Instance).GetTransform());
                            element3.SetCoordinateSystem((element2 as Instance).GetTransform());
                            element3.Position = endPoint1;
                            element4.Position = endPoint2;
                            ((Element)componentInstance).LookupParameter("Высота лотка").Set(num2);
                            ((Element)componentInstance).LookupParameter("Ширина лотка").Set(num3);
                            ((Location)(((Element)componentInstance).Location as LocationPoint)).Rotate(Line.CreateBound(element3.Position, element4.Position), -1.0 * Math.PI / 2.0);
                        }
                        else
                        {
                            element3.Position = endPoint1;
                            element4.Position = endPoint2;
                            ((Element)componentInstance).LookupParameter("Высота лотка").Set(num2);
                            ((Element)componentInstance).LookupParameter("Ширина лотка").Set(num3);
                        }
                        Element cap = (Element)componentInstance;
                        Parameter elmrk = cap.get_Parameter(mark);
                        elmrk.Set(ct.Id.ToString()); //запись id лотка в параметр Марка у крышки
                        count1++;
                    }
                }

                transaction1.Commit();
                Logger.Log("Закрываем транзакцию 1", 1);
                }
                catch (Exception ex)
                {
                    Logger.Log("Ошибка: " + ex.Message, 4);
                    new InfoWindow280("Ошибка: " + ex.Message).ShowDialog();
                    unhandledError = true;
                }
            }
            #endregion

            #region Основной код. Перегородки
            using (Transaction transaction2 = new Transaction(doc))
            {
                try
                {
                    transaction2.Start("TNov - Лотки.Перегородки");
                    TransactionHandler.SetWarningResolver(transaction2);
                    Logger.Log("Открываем транзакцию 2 (перегородки)", 1);

                    if (remove)
                    {
                        //удаление перегородок без марки
                        Logger.Log("Ищем перегородки без марки", 1);

                        ICollection<ElementId> partitionswithoutmark = new List<ElementId>();
                        int pwm = 0;
                        foreach (FamilyInstance pt in partitions)
                        {
                            string partitionmark = pt.get_Parameter(mark).AsString();
                            if (partitionmark != null)
                            {
                                bool cableTrayexists = false;
                                foreach (CableTray ct in CTListPts)
                                {
                                    string ctIdstr = ct.Id.ToString();
                                    if (partitionmark.Contains(ctIdstr))
                                    {
                                        cableTrayexists = true; break;
                                    }
                                }
                                if (!cableTrayexists) { partitionswithoutmark.Add(pt.Id); pwm++; }
                            }
                            else { partitionswithoutmark.Add(pt.Id); pwm++; }
                        }
                        if (pwm > 0)
                        {
                            doc.Delete(partitionswithoutmark.ToArray());
                            Logger.Log("Удалено " + pwm.ToString() + " элементов", 2);
                        }
                        else { Logger.Log("перегородки без марки отсутствуют", 1); }
                    }


                    //обновленные списки

                    List<FamilyInstance> GMs2 = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel)   //фильтр по категории Об модели
                                                                 .WhereElementIsNotElementType()
                                                                 .OfClass(typeof(FamilyInstance))
                                                                 .Cast<FamilyInstance>()
                                                                 .ToList();
                    List<FamilyInstance> partitions1 = new List<FamilyInstance>();
                    foreach (FamilyInstance g in GMs2)
                    {
                        if (g.Symbol.FamilyName.Contains("Перегородка лотка"))
                        {
                            partitions1.Add(g);
                        }
                    }

                    //удаляем перегородки, которые будут пересозданы
                    Logger.Log("Удаляем перегородки, которые будут пересозданы", 1);
                    ICollection<ElementId> partitionstoremove = new List<ElementId>();
                    int ptr = 0;
                    if (replace)
                    {
                        foreach (FamilyInstance pt in partitions1)
                        {
                            Element elem = doc.GetElement(pt.Id);
                            int mrkint = 0;
                            string mrkstr = elem.get_Parameter(mark).AsString();
                            if (mrkstr != null)
                            {
                                int.TryParse(mrkstr, out mrkint);
                                if (mrkint != 0)
                                {
                                    foreach (CableTray ct in CTList1)
                                    {
                                        Element elem1 = doc.GetElement(ct.Id);
#if R2022
                long elem1Id = elem1.Id.IntegerValue;
#else
                                        long elem1Id = elem1.Id.Value;
#endif
                                        
                                        if (elem1Id == mrkint) { partitionstoremove.Add(pt.Id); ptr++; break; }
                                    }
                                }
                            }
                        }
                        if (partitionstoremove.Count > 0)
                        { doc.Delete(partitionstoremove.ToArray()); Logger.Log("Удалено " + ptr.ToString() + " элементов", 2); }
                        else { Logger.Log("лишние перегородки отсутствуют", 1); }
                    }

                    //создание перегородок
                    Logger.Log("Создаем перегородки для лотков", 1);
                    foreach (CableTray ct in CTListPts)
                    {
                        Element element1 = doc.GetElement(ct.Id);

                        double num = element1.get_Parameter(height).AsDouble();
                        XYZ endPoint1 = (element1.Location as LocationCurve).Curve.GetEndPoint(0);
                        XYZ endPoint2 = (element1.Location as LocationCurve).Curve.GetEndPoint(1);
                        Element element2 = (Element)null;
                        foreach (Connector connector in ((IEnumerable)((MEPCurve)(element1 as CableTray)).ConnectorManager.Connectors).Cast<Connector>())
                        {
                            foreach (Connector allRef in connector.AllRefs)
                            {
                                if (allRef.Owner is FamilyInstance)
                                    element2 = allRef.Owner;
                            }
                        }

                        string ctType = ct.Name; //тип лотка
                        Logger.Log("Лоток " + ct.Id.ToString() + " : тип " + ctType, 2);
                        bool run = false;
                        foreach (string t in types1)
                        {
                            if (ctType.Contains(t)) { ctType = t; run = true; } //принципиальный тип лотка
                        }
                        FamilySymbol familySymbol = null;
                        foreach (FamilySymbol partitiontype in partitiontypes)
                        {
                            if (partitiontype.Name.Contains(ctType)) { familySymbol = partitiontype; break; }
                        }
                        if (run && familySymbol != null)
                        {
                            Logger.Log("   тип перегородки: " + familySymbol.Name, 2);
                            //создание перегородки
                            FamilyInstance componentInstance = AdaptiveComponentInstanceUtils.CreateAdaptiveComponentInstance(doc, familySymbol);
                            IList<ElementId> pointElementRefIds = AdaptiveComponentInstanceUtils.GetInstancePlacementPointElementRefIds(componentInstance);
                            ReferencePoint element3 = doc.GetElement(pointElementRefIds.First<ElementId>()) as ReferencePoint;
                            ReferencePoint element4 = doc.GetElement(pointElementRefIds.Last<ElementId>()) as ReferencePoint;
                            if (Math.Round(endPoint1.X, 3) == Math.Round(endPoint2.X, 3) && Math.Round(endPoint1.Y, 3) == Math.Round(endPoint2.Y, 3))
                            {
                                element3.SetCoordinateSystem((element2 as Instance).GetTransform());
                                element3.SetCoordinateSystem((element2 as Instance).GetTransform());
                                element3.Position = endPoint1;
                                element4.Position = endPoint2;
                                ((Element)componentInstance).LookupParameter("Высота лотка").Set(num);
                                ((Location)(((Element)componentInstance).Location as LocationPoint)).Rotate(Line.CreateBound(element3.Position, element4.Position), -1.0 * Math.PI / 2.0);
                            }
                            else
                            {
                                element3.Position = endPoint1;
                                element4.Position = endPoint2;
                                ((Element)componentInstance).LookupParameter("Высота лотка").Set(num);
                            }
                            Element partition = (Element)componentInstance;
                            Parameter elmrk = partition.get_Parameter(mark);
                            elmrk.Set(ct.Id.ToString()); //запись id лотка в параметр Марка у перегородки
                            count2++;
                        }
                    }

                    transaction2.Commit();
                    Logger.Log("Закрываем транзакцию 2", 1);
                }
                catch (Exception ex)
                {
                    Logger.Log("Ошибка: " + ex.Message, 4);
                }
            }
            #endregion


            #region Рабочий набор

            Logger.Log("Помещение крышек в набор Лотки", 1);

            bool dws = doc.IsWorkshared;
            if (dws&&ss==false) 
            { 
                //ищем рабочий набор "Лотки"
                List<Workset> worksets = new FilteredWorksetCollector(doc)  //рабочие наборы документа
                                         .Cast<Workset>()                   //элементы категории Рабочие наборы
                                         .ToList();                         //формируем список
                Logger.Log("Ищем набор Лотки", 1);
                List<Workset> worksetsL = new List<Workset>();
                foreach (Workset ws in worksets) //ищем наличие набора лотков, добавляем его в список РН лотков
                {
                    string wname = ws.Name;
                    if (wname == "Лотки") worksetsL.Add(ws);
                }

                using (Transaction transaction3 = new Transaction(doc))
                {
                    try
                    {
                        Logger.Log("Открываем транзакцию 3 (рабочий набор)", 1);
                        transaction3.Start("TNov - Лотки.Рабочий набор");
                        TransactionHandler.SetWarningResolver(transaction3);
                        if (worksetsL.Count < 1)
                        {
                            //создаем рабочий набор "Лотки"
                            Logger.Log("Набор Лотки отсутствует, создаем его", 1);
                            Workset ws = Workset.Create(doc, "Лотки");
                        }

                        //обновленные списки

                        List<Workset> worksets1 = new FilteredWorksetCollector(doc)  //рабочие наборы документа
                                                 .Cast<Workset>()                   //элементы категории Рабочие наборы
                                                 .ToList();                         //формируем список
                        List<FamilyInstance> GMs3 = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel)   //фильтр по категории Об модели
                                                                     .WhereElementIsNotElementType()
                                                                     .OfClass(typeof(FamilyInstance))
                                                                     .Cast<FamilyInstance>()
                                                                     .ToList();
                        List<CableTray> CTs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_CableTray)   //фильтр по категории Каб лотки
                                                                     .WhereElementIsNotElementType()
                                                                     .Cast<CableTray>()
                                                                     .ToList();
                        List<FamilyInstance> CTFs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_CableTrayFitting)   //фильтр по категории Соед лотков
                                                                     .WhereElementIsNotElementType()
                                                                     .Cast<FamilyInstance>()
                                                                     .ToList();
                        List<FamilyInstance> FIs = new List<FamilyInstance>();
                        foreach (FamilyInstance g in GMs3)
                        {
                            if (g.Symbol.FamilyName.Contains("Крышка"))
                            {
                                FIs.Add(g);
                            }
                            if (g.Symbol.FamilyName.Contains("Перегородка лотка"))
                            {
                                FIs.Add(g);
                            }
                        }
                        foreach (FamilyInstance CTF in CTFs) { FIs.Add(CTF); }


                        //Назначаем набор крышкам
                        Logger.Log("Ищем набор Лотки", 1);
                        List<Workset> worksetsL1 = new List<Workset>();
                        foreach (Workset ws in worksets1) //ищем наличие набора лотков, добавляем его в список РН лотков
                        {
                            string wname = ws.Name;
                            if (wname == "Лотки") worksetsL1.Add(ws);
                        }

                        List<int> widsL = new List<int>(); //пустой список номеров РН лотков

                        foreach (Workset wsL in worksetsL1) //заполняем список номеров РН лотков
                        {
                            int widL = wsL.Id.IntegerValue;
                            widsL.Add(widL);
                        }

                        Logger.Log("Назначаем набор соед деталям, крышкам и перегородкам", 1);
                        foreach (var elem in FIs)
                        {
                            Element capelement = doc.GetElement(elem.Id);
                            Autodesk.Revit.DB.Parameter param = capelement.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);//получаем параметр "РН"
                            Logger.Log("   Элемент " + elem.Id, 2);
                            try
                            {
                                param.Set(widsL[0]); //берем первое значение из списка номеров РН лотков
                            }
                            catch (Exception ex) { Logger.Log("   Элемент " + elem.Id + " ошибка: " + ex.Message, 4); continue; }
                        }
                        Logger.Log("Назначаем набор лоткам", 1);
                        foreach (var elem in CTs)
                        {
                            Element capelement = doc.GetElement(elem.Id);
                            Autodesk.Revit.DB.Parameter param = capelement.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);//получаем параметр "РН"
                            Logger.Log("   Элемент " + elem.Id, 2);
                            try
                            {
                                param.Set(widsL[0]); //берем первое значение из списка номеров РН лотков
                            }
                            catch (Exception ex) { Logger.Log("   Элемент " + elem.Id + " ошибка: " + ex.Message, 4); continue; }
                        }
                        transaction3.Commit();
                        Logger.Log("Закрываем транзакцию 3", 1);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("Ошибка: " + ex.Message, 4);
                    }
                }

            }
            else
            {
                if (!ss)
                {
                    Logger.Log("Модель НЕ является файлом хранилища. Завершение работы.", 3);
                    string info1txt = "Ошибка!\nТекущий документ не является файлом хранилища. Наборы не созданы.";
                    var info1 = new InfoWindow280(info1txt); info1.ShowDialog();

                }
                
            }
            #endregion

            if (unhandledError)
            {
                Logger.Log("Завершение работы с ошибками.", 4);
                return Result.Succeeded;
            }

            //сообщение об успехе

            if (count1 > 0 && count2 > 0)
            {
                var info = new InfoWindow280("Успешно!\nСозданы крышки в количестве " + count1.ToString() + " шт." +
                    "\nи перегородки в количестве " + count2.ToString() + " шт."); info.ShowDialog();
            }
            else
            {
                if (count1 > 0)
                {
                    if (count1 == 1) { var info1 = new InfoWindow280("Успешно!\nКрышка для лотка создана."); info1.ShowDialog(); }
                    else { var info1 = new InfoWindow280("Успешно!\nСозданы крышки в количестве " + count1.ToString() + " шт."); info1.ShowDialog(); }
                }
                if (count2 > 0)
                {
                    if (count2 == 1) { var info1 = new InfoWindow280("Успешно!\nПерегородка для лотка создана."); info1.ShowDialog(); }
                    else { var info2 = new InfoWindow280("Успешно!\nСозданы перегородки в количестве " + count2.ToString() + " шт."); info2.ShowDialog(); }
                }
            }
            Logger.Log("Завершение работы.", 5);
            return Result.Succeeded;
        }

        // Делит лоток при длине > maxLength: при длине < 2×maxLength — пополам, иначе — части по maxLength (с остатком).
        // В местах стыка вставляет «Соединение» по настройке типоразмера лотка (NewUnionFitting).
        // Возвращает число созданных дополнительных сегментов.
        private static int SplitCableTrayIfNeeded(Document doc, CableTray tray, double maxLength)
        {
            if (tray == null || !tray.IsValidObject) return 0;

            LocationCurve loc = tray.Location as LocationCurve;
            if (loc?.Curve == null) return 0;

            double length = loc.Curve.Length;
            const double tol = 1.0 / 304.8; // 1 мм
            if (length <= maxLength + tol) return 0;

            List<double> segmentLengths = new List<double>();
            if (length < 2.0 * maxLength)
            {
                double half = length / 2.0;
                segmentLengths.Add(half);
                segmentLengths.Add(half);
            }
            else
            {
                int fullParts = (int)Math.Floor(length / maxLength);
                double remainder = length - fullParts * maxLength;
                for (int i = 0; i < fullParts; i++)
                    segmentLengths.Add(maxLength);
                if (remainder > tol)
                    segmentLengths.Add(remainder);
                else if (remainder > 0 && segmentLengths.Count > 0)
                    segmentLengths[segmentLengths.Count - 1] += remainder;
            }

            if (segmentLengths.Count < 2) return 0;

            int created = 0;
            CableTray current = tray;

            for (int i = 0; i < segmentLengths.Count - 1; i++)
            {
                if (current == null || !current.IsValidObject) break;

                LocationCurve currentLoc = current.Location as LocationCurve;
                if (currentLoc?.Curve == null) break;

                Curve curve = currentLoc.Curve;
                double curLen = curve.Length;
                if (curLen <= tol * 2) break;

                XYZ actualStart = curve.GetEndPoint(0);
                XYZ currentEnd = curve.GetEndPoint(1);

                double cutLen = segmentLengths[i];
                if (cutLen >= curLen - tol * 2)
                    cutLen = curLen / 2.0;

                XYZ breakPt = curve.Evaluate(cutLen / curLen, true);

                List<Connector> endRefs = GetConnectedRefsAtPoint(current, currentEnd);

                ICollection<ElementId> copiedIds = ElementTransformUtils.CopyElement(doc, current.Id, XYZ.Zero);
                CableTray newTray = doc.GetElement(copiedIds.First()) as CableTray;
                if (newTray == null) break;

                DisconnectAtPoint(current, currentEnd);

                // Старт текущего сегмента не трогаем (может быть уже стык с предыдущим соединением)
                currentLoc.Curve = Line.CreateBound(actualStart, breakPt);
                (newTray.Location as LocationCurve).Curve = Line.CreateBound(breakPt, currentEnd);

                doc.Regenerate();

                PlaceUnionFittingAtPoint(doc, current, newTray, breakPt);
                ReconnectRefsToTray(newTray, currentEnd, endRefs);

                Logger.Log("   Лоток разделён: " + current.Id.ToString() + " + " + newTray.Id.ToString(), 2);

                created++;
                current = newTray;
            }

            return created;
        }

        private static List<Connector> GetConnectedRefsAtPoint(CableTray tray, XYZ point)
        {
            var result = new List<Connector>();
            foreach (Connector c in tray.ConnectorManager.Connectors)
            {
                if (c.ConnectorType != ConnectorType.End) continue;
                if (!c.Origin.IsAlmostEqualTo(point, 1e-4)) continue;
                foreach (Connector r in c.AllRefs)
                {
                    if (r.Owner.Id != tray.Id)
                        result.Add(r);
                }
            }
            return result;
        }

        private static void DisconnectAtPoint(CableTray tray, XYZ point)
        {
            foreach (Connector c in tray.ConnectorManager.Connectors)
            {
                if (c.ConnectorType != ConnectorType.End) continue;
                if (!c.Origin.IsAlmostEqualTo(point, 1e-4)) continue;

                List<Connector> refs = new List<Connector>();
                foreach (Connector r in c.AllRefs)
                {
                    if (r.Owner.Id != tray.Id)
                        refs.Add(r);
                }
                foreach (Connector r in refs)
                {
                    if (c.IsConnectedTo(r))
                        c.DisconnectFrom(r);
                }
            }
        }

        private static void ReconnectRefsToTray(CableTray tray, XYZ point, List<Connector> refs)
        {
            Connector trayConn = FindEndConnectorAt(tray, point);
            if (trayConn == null) return;

            foreach (Connector r in refs)
            {
                try
                {
                    if (!trayConn.IsConnectedTo(r))
                        trayConn.ConnectTo(r);
                }
                catch { /* соединение может быть недоступно после копирования */ }
            }
        }

        // Вставляет соединительную деталь по настройке типоразмера лотка (RBS_CURVETYPE_DEFAULT_UNION_PARAM).
        private static void PlaceUnionFittingAtPoint(Document doc, CableTray a, CableTray b, XYZ point)
        {
            Connector ca = FindEndConnectorAt(a, point);
            Connector cb = FindEndConnectorAt(b, point);
            if (ca == null || cb == null)
            {
                Logger.Log("   Не найдены коннекторы для соединения в точке " + point.ToString(), 3);
                return;
            }

            try
            {
                if (ca.IsConnectedTo(cb))
                    ca.DisconnectFrom(cb);
            }
            catch { }

            try
            {
                FamilyInstance union = doc.Create.NewUnionFitting(ca, cb);
                if (union != null)
                    Logger.Log("   Установлено соединение Id=" + union.Id.ToString(), 2);
                else
                    Logger.Log("   NewUnionFitting вернул null", 3);
            }
            catch (Exception ex)
            {
                Logger.Log("   Не удалось создать соединение: " + ex.Message, 3);
                try
                {
                    if (!ca.IsConnected)
                        ca.ConnectTo(cb);
                }
                catch { }
            }
        }

        private static Connector FindEndConnectorAt(CableTray tray, XYZ point)
        {
            const double connTol = 0.01; // ~3 мм — после regenerate координаты могут чуть съехать
            Connector best = null;
            double bestDist = double.MaxValue;
            foreach (Connector c in tray.ConnectorManager.Connectors)
            {
                if (c.ConnectorType != ConnectorType.End) continue;
                double d = c.Origin.DistanceTo(point);
                if (d < bestDist && d <= connTol)
                {
                    bestDist = d;
                    best = c;
                }
            }
            return best;
        }
        
    }
    
}
