using System;
using System.Collections.Generic;
using System.Text;
using SmartSchool.Customization.PlugIn;
using SmartSchool.Customization.PlugIn.Report;
using System.ComponentModel;
using Aspose.Words;
using System.IO;
using SmartSchool.Customization.Data;
using SmartSchool.Customization.Data.StudentExtension;
using System.Windows.Forms;
using System.Xml;
using FISCA.DSAUtil;
using SmartSchool.Common;
using SHSchool.Data;
using Aspose.Cells;
using System.Data;
using System.Linq;

namespace SemesterScoreReportNewEpost
{
    class SemesterScoreReportNew
    {
        public static void RegistryFeature()
        {
            SemesterScoreReportNew semsScoreReport = new SemesterScoreReportNew();

            string reportName = "學期成績單(新制)epost";
            string path = "成績相關報表";
            
            semsScoreReport.button = new SecureButtonAdapter("Report0055");
            semsScoreReport.button.Text = reportName;
            semsScoreReport.button.Path = path;
            semsScoreReport.button.OnClick += new EventHandler(semsScoreReport.button_OnClick);
            StudentReport.AddReport(semsScoreReport.button);

            semsScoreReport.button2 = new SecureButtonAdapter("Report0155");
            semsScoreReport.button2.Text = reportName;
            semsScoreReport.button2.Path = path;
            semsScoreReport.button2.OnClick += new EventHandler(semsScoreReport.button2_OnClick);
            ClassReport.AddReport(semsScoreReport.button2);
        }

        private ButtonAdapter button, button2;
        private BackgroundWorker _BGWSemesterScoreReport;
        private Dictionary<string, decimal> _degreeList = null; //等第List

        private enum Entity { Student, Class }

        public SemesterScoreReportNew()
        {
        }

        #region Common Function

        public int SortBySemesterSubjectScoreInfo(SemesterSubjectScoreInfo a, SemesterSubjectScoreInfo b)
        {
            return SortBySubjectName(a.Subject, b.Subject);
        }

        private int SortBySubjectName(string a, string b)
        {
            string a1 = a.Length > 0 ? a.Substring(0, 1) : "";
            string b1 = b.Length > 0 ? b.Substring(0, 1) : "";
            #region 第一個字一樣的時候
            if (a1 == b1)
            {
                if (a.Length > 1 && b.Length > 1)
                    return SortBySubjectName(a.Substring(1), b.Substring(1));
                else
                    return a.Length.CompareTo(b.Length);
            }
            #endregion
            #region 第一個字不同，分別取得在設定順序中的數字，如果都不在設定順序中就用單純字串比較
            int ai = getIntForSubject(a1), bi = getIntForSubject(b1);
            if (ai > 0 || bi > 0)
                return ai.CompareTo(bi);
            else
                return a1.CompareTo(b1);
            #endregion
        }

        private int getIntForSubject(string a1)
        {
            List<string> list = new List<string>();
            list.AddRange(new string[] { "國", "英", "數", "物", "化", "生", "基", "歷", "地", "公", "文", "礎", "世" });

            int x = list.IndexOf(a1);
            if (x < 0)
                return list.Count;
            else
                return x;
        }

        private int SortByEntryName(string a, string b)
        {
            int ai = getIntForEntry(a), bi = getIntForEntry(b);
            if (ai > 0 || bi > 0)
                return ai.CompareTo(bi);
            else
                return a.CompareTo(b);
        }

        private int getIntForEntry(string a1)
        {
            List<string> list = new List<string>();
            list.AddRange(new string[] { "學業", "學業成績名次", "實習科目", "體育", "國防通識", "健康與護理", "德行", "學年德行成績" });           
            int x = list.IndexOf(a1);
            if (x < 0)
                return list.Count;
            else
                return x;
        }

        //科目級別轉換
        private string GetNumber(int p)
        {
            List<string> list = new List<string>(new string[] { "", "Ⅰ", "Ⅱ", "Ⅲ", "Ⅳ", "Ⅴ", "Ⅵ", "Ⅶ", "Ⅷ", "Ⅸ", "Ⅹ" });

            if (p < list.Count)
                return list[p];
            else
                return "" + p;
        }

        //德行成績 -> 等第
        private string ParseLevel(decimal score)
        {
            if (_degreeList == null)
            {
                _degreeList = new Dictionary<string, decimal>();
                DSResponse dsrsp = SmartSchool.Feature.Basic.Config.GetDegreeList();
                DSXmlHelper helper = dsrsp.GetContent();
                foreach (XmlElement element in helper.GetElements("Degree"))
                {
                    decimal low = decimal.MinValue;
                    if (!decimal.TryParse(element.GetAttribute("Low"), out low))
                        low = decimal.MinValue;
                    _degreeList.Add(element.GetAttribute("Name"), low);
                }
            }

            foreach (string var in _degreeList.Keys)
            {
                if (_degreeList[var] <= score)
                    return var;
            }
            return "";
        }

        //報表產生完成後，儲存並且開啟
        private void Completed(string inputReportName, Document inputDoc)
        {
            string reportName = inputReportName;

            string path = Path.Combine(Application.StartupPath, "Reports");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            path = Path.Combine(path, reportName + ".doc");

            Aspose.Words.Document doc = inputDoc;

            if (File.Exists(path))
            {
                int i = 1;
                while (true)
                {
                    string newPath = Path.GetDirectoryName(path) + "\\" + Path.GetFileNameWithoutExtension(path) + (i++) + Path.GetExtension(path);
                    if (!File.Exists(newPath))
                    {
                        path = newPath;
                        break;
                    }
                }
            }

            try
            {
                doc.Save(path, Aspose.Words.SaveFormat.Doc);
                System.Diagnostics.Process.Start(path);
            }
            catch
            {
                SaveFileDialog sd = new SaveFileDialog();
                sd.Title = "另存新檔";
                sd.FileName = reportName + ".doc";
                sd.Filter = "Word檔案 (*.doc)|*.doc|所有檔案 (*.*)|*.*";
                if (sd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        doc.Save(sd.FileName, Aspose.Words.SaveFormat.Doc);

                    }
                    catch
                    {
                        MsgBox.Show("指定路徑無法存取。", "建立檔案失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
            }
        }

        private void CompletedXls(string inputReportName, Workbook inputXls)
        {
            string reportName = inputReportName;

            string path = Path.Combine(Application.StartupPath, "Reports");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            path = Path.Combine(path, reportName + ".csv");

            Workbook wb = inputXls;

            if (File.Exists(path))
            {
                int i = 1;
                while (true)
                {
                    string newPath = Path.GetDirectoryName(path) + "\\" + Path.GetFileNameWithoutExtension(path) + (i++) + Path.GetExtension(path);
                    if (!File.Exists(newPath))
                    {
                        path = newPath;
                        break;
                    }
                }
            }

            try
            {
                wb.Save(path, Aspose.Cells.FileFormatType.CSV);
                System.Diagnostics.Process.Start(path);
            }
            catch
            {
                SaveFileDialog sd = new SaveFileDialog();
                sd.Title = "另存新檔";
                sd.FileName = reportName + ".csv";
                sd.Filter = "CSV檔案 (*.csv)|*.xls|所有檔案 (*.*)|*.*";
                if (sd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        wb.Save(sd.FileName, Aspose.Cells.FileFormatType.CSV);

                    }
                    catch
                    {
                        MsgBox.Show("指定路徑無法存取。", "建立檔案失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
            }
        }


        //填入學生資料
        private void FillStudentData(AccessHelper helper, List<StudentRecord> students)
        {
            helper.StudentHelper.FillAttendance(students);
            helper.StudentHelper.FillReward(students);
            helper.StudentHelper.FillParentInfo(students);
            helper.StudentHelper.FillContactInfo(students);
            //helper.StudentHelper.FillSchoolYearEntryScore(true, students);
            //helper.StudentHelper.FillSchoolYearSubjectScore(true, students);
            helper.StudentHelper.FillSemesterSubjectScore(true, students);
            helper.StudentHelper.FillSemesterEntryScore(true, students);
            helper.StudentHelper.FillField("SemesterEntryClassRating", students); //學期分項班排名。
            helper.StudentHelper.FillField("補考標準", students);
            helper.StudentHelper.FillSchoolYearEntryScore(true, students);
            helper.StudentHelper.FillSemesterMoralScore(true, students);
        }

        #endregion

        private void button_OnClick(object sender, EventArgs e)
        {
            SemesterScoreReportFormNew form = new SemesterScoreReportFormNew();
            
            if (form.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                // 學生
                
                _BGWSemesterScoreReport = new BackgroundWorker();
                _BGWSemesterScoreReport.WorkerReportsProgress = true;
                _BGWSemesterScoreReport.DoWork += new DoWorkEventHandler(_BGWSemesterScoreReport_DoWork);
                _BGWSemesterScoreReport.ProgressChanged += new ProgressChangedEventHandler(_BGWSemesterScoreReport_ProgressChanged);
                _BGWSemesterScoreReport.RunWorkerCompleted += new RunWorkerCompletedEventHandler(_BGWSemesterScoreReport_RunWorkerCompleted);
                _BGWSemesterScoreReport.RunWorkerAsync(new object[] { form.SchoolYear, form.Semester, form.UserDefinedType, form.Template, form.Receiver, form.Address, form.ResitSign, form.RepeatSign, Entity.Student, form.AllowMoralScoreOver100 });
            }
        }

        private void button2_OnClick(object sender, EventArgs e)
        {
            SemesterScoreReportFormNew form = new SemesterScoreReportFormNew();
            if (form.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                // 班級

                _BGWSemesterScoreReport = new BackgroundWorker();
                _BGWSemesterScoreReport.WorkerReportsProgress = true;
                _BGWSemesterScoreReport.DoWork += new DoWorkEventHandler(_BGWSemesterScoreReport_DoWork);
                _BGWSemesterScoreReport.ProgressChanged += new ProgressChangedEventHandler(_BGWSemesterScoreReport_ProgressChanged);
                _BGWSemesterScoreReport.RunWorkerCompleted += new RunWorkerCompletedEventHandler(_BGWSemesterScoreReport_RunWorkerCompleted);
                _BGWSemesterScoreReport.RunWorkerAsync(new object[] { form.SchoolYear, form.Semester, form.UserDefinedType, form.Template, form.Receiver, form.Address, form.ResitSign, form.RepeatSign, Entity.Class, form.AllowMoralScoreOver100 });
            }
        }

        private void _BGWSemesterScoreReport_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // 檢查是否產生 Excel
            if (Global._CheckExportEpost)
            {
                Utility.CompletedXlsCsv("學期成績單epost", Global.dt);
                //Workbook wb = new Workbook();
                //wb.Worksheets[0].Cells.ImportDataTable(Global.dt, true, "A1");
                //CompletedXls("學期成績單epost", wb);
            }

            button.SetBarMessage("學期成績單產生完成");
            Completed("學期成績單", (Document)e.Result);
        }

        private void _BGWSemesterScoreReport_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            button.SetBarMessage("學期成績單產生中...", e.ProgressPercentage);
        }

        private void _BGWSemesterScoreReport_DoWork(object sender, DoWorkEventArgs e)
        {
            object[] objectValue = (object[])e.Argument;

            int schoolyear = (int)objectValue[0];
            int semester = (int)objectValue[1];
            Dictionary<string, List<string>> userType = (Dictionary<string, List<string>>)objectValue[2];
            MemoryStream template = (MemoryStream)objectValue[3];
            int receiver = (int)objectValue[4];
            int address = (int)objectValue[5];
            string resitSign = (string)objectValue[6];
            string repeatSign = (string)objectValue[7];
            Entity entity = (Entity)objectValue[8];
            bool over100 = (bool)objectValue[9];

            _BGWSemesterScoreReport.ReportProgress(0);

            #region 取得資料

            GetPeriodType();

            AccessHelper helper = new AccessHelper();

            List<StudentRecord> allStudent = new List<StudentRecord>();
            List<StudentRecord> selStudent = new List<StudentRecord>();
            if (entity == Entity.Student)
            {
                selStudent = helper.StudentHelper.GetSelectedStudent();
                FillStudentData(helper, selStudent);
            }
            else if (entity == Entity.Class)
            {
                foreach (ClassRecord aClass in helper.ClassHelper.GetSelectedClass())
                {
                    FillStudentData(helper, aClass.Students);
                    selStudent.AddRange(aClass.Students);
                }
            }

            // 排序
            try
            {
                allStudent = (from data in selStudent orderby data.RefClass.ClassName, int.Parse(data.SeatNo) ascending select data).ToList();
            }
            catch (Exception ex)
            {
                allStudent = selStudent;
            }


            //取得文字評量對照表
            SmartSchool.Customization.Data.SystemInformation.getField("文字評量對照表");

            int currentStudent = 1;
            int totalStudent = allStudent.Count;

            //WearyDogComputer computer = new WearyDogComputer();
            //computer.FillSemesterSubjectScoreInfoWithResit(helper, true, allStudent);

            #endregion

            // 使用 Data Table 方式來放資料，產生至 Excel
            Global.dt.Clear();
            Global.dt.Columns.Clear();
            // 放入欄位名稱
            Global.dt.Columns.Add("CN");
            Global.dt.Columns.Add("POSTALCODE");
            Global.dt.Columns.Add("POSTALADDRESS");
            Global.dt.Columns.Add("學年度");
            Global.dt.Columns.Add("學期");
            Global.dt.Columns.Add("科別");
            //Global.dt.Columns.Add("學校名稱");

            Global.dt.Columns.Add("班級");
            Global.dt.Columns.Add("座號");
            Global.dt.Columns.Add("學號");
            Global.dt.Columns.Add("學生姓名");
            //Global.dt.Columns.Add("郵遞區號");
            //Global.dt.Columns.Add("地址");
            //Global.dt.Columns.Add("收件人");

            //int subjMax = 0,commentMax=0;
            //foreach (StudentRecord eachStudent in allStudent)
            //{
            //    int count = 0;
            //    foreach (SemesterSubjectScoreInfo info in eachStudent.SemesterSubjectScoreList)
            //    {
            //        string invalidCredit = info.Detail.GetAttribute("不計學分");
            //        string noScoreString = info.Detail.GetAttribute("不需評分");
            //        bool noScore = (noScoreString != "是");

            //        if (invalidCredit == "是")
            //            continue;
                    
            //        if (info.SchoolYear == schoolyear && info.Semester == semester)
            //        {
            //            count++;
            //        }
            //        if (count > subjMax)
            //            subjMax = count;                   
            //    }

            //    foreach (SemesterMoralScoreInfo info in eachStudent.SemesterMoralScoreList)
            //    {
            //        if (info.SchoolYear == schoolyear && info.Semester == semester)
            //        {
            //            int commIdx = 0;
            //            XmlElement objValue = (XmlElement)info.Detail;
            //            foreach (XmlElement each in objValue.SelectNodes("TextScore/Morality"))
            //            {
            //                string face = each.GetAttribute("Face");

            //                //如果學生身上的face不存在對照表上，就不印出來
            //                if ((SmartSchool.Customization.Data.SystemInformation.Fields["文字評量對照表"] as XmlElement).SelectSingleNode("Content/Morality[@Face='" + face + "']") == null) continue;
                            

            //                commIdx++;
            //            }
            //            if (commIdx > commentMax)
            //                commentMax = commIdx;
            //        }
            //    }

            //}

            // 科目名稱、科目學分數、科目成績..(動態)
            for (int i = 1; i <= 22 ; i++)
            {
                Global.dt.Columns.Add("科目名稱" + i);
                Global.dt.Columns.Add("科目學分數" + i);
                Global.dt.Columns.Add("科目成績" + i);
                Global.dt.Columns.Add("科目備註" + i);
            }

            //// 綜合表現 (動態)
            //for (int i = 1; i <= commentMax; i++)
            //{
            //    Global.dt.Columns.Add("學生在校綜合表現項目" + i);
            //    Global.dt.Columns.Add("學生在校綜合表現文字評量" + i);                
            //}


            //// 缺曠類別、缺曠假別、缺曠假別統計 .. (動態)
            //int abs1 = 1;
            //foreach (KeyValuePair<string,List<string>> data in userType)
            //{
            //    Global.dt.Columns.Add("缺曠類別" + abs1);
            //    for (int i = 1; i <= data.Value.Count; i++)
            //    {
            //        Global.dt.Columns.Add("缺曠類別"+abs1+"假別" + i);
            //        Global.dt.Columns.Add("缺曠類別"+abs1+"假別統計" + i);
            //    }
            //    abs1++;
            //}
                       

            Global.dt.Columns.Add("學期成績");
            Global.dt.Columns.Add("體育");
            Global.dt.Columns.Add("國防通識");
            Global.dt.Columns.Add("成績名次");
            Global.dt.Columns.Add("學期取得學分數");
            //Global.dt.Columns.Add("累計學分數");
            Global.dt.Columns.Add("累計取得必修學分");
            Global.dt.Columns.Add("累計取得選修學分");
            Global.dt.Columns.Add("嘉獎");
            Global.dt.Columns.Add("小功");
            Global.dt.Columns.Add("大功");
            Global.dt.Columns.Add("警告");
            Global.dt.Columns.Add("小過");
            Global.dt.Columns.Add("大過");            
            Global.dt.Columns.Add("留校察看");

            Global.dt.Columns.Add("曠課");
            Global.dt.Columns.Add("遲到早退");
            Global.dt.Columns.Add("事假");
            Global.dt.Columns.Add("病假");
            Global.dt.Columns.Add("喪假");
            Global.dt.Columns.Add("公假");
            //Global.dt.Columns.Add("綜合表現");
            //Global.dt.Columns.Add("具體建議");
            //Global.dt.Columns.Add("社團競賽服務學習");
            //Global.dt.Columns.Add("評語（含社團、競賽、服務學習、禮節等綜合表現及建議）");

            //Global.dt.Columns.Add("導師");
            Global.dt.Columns.Add("評語");
            Global.dt.Columns.Add("備註");


            #region 產生報表並填入資料

            Document doc = new Document();
            doc.Sections.Clear();

            foreach (StudentRecord eachStudent in allStudent)
            {
                DataRow row = Global.dt.NewRow();
                Document eachStudentDoc = new Document(template, "", LoadFormat.Doc, "");

                Dictionary<string, object> mergeKeyValue = new Dictionary<string, object>();

                #region 學校基本資料
                mergeKeyValue.Add("學校名稱", SmartSchool.Customization.Data.SystemInformation.SchoolChineseName);
                mergeKeyValue.Add("學校地址", SmartSchool.Customization.Data.SystemInformation.Address);
                mergeKeyValue.Add("學校電話", SmartSchool.Customization.Data.SystemInformation.Telephone);
                #endregion

                #region 收件人姓名與地址
                if (receiver == 0)
                {
                    mergeKeyValue.Add("收件人", eachStudent.ParentInfo.CustodianName);
                    row["CN"] = eachStudent.ParentInfo.CustodianName;
                }
                else if (receiver == 1)
                {
                    mergeKeyValue.Add("收件人", eachStudent.ParentInfo.FatherName);
                    row["CN"] = eachStudent.ParentInfo.FatherName;
                }
                else if (receiver == 2)
                {
                    mergeKeyValue.Add("收件人", eachStudent.ParentInfo.MotherName);
                    row["CN"] = eachStudent.ParentInfo.MotherName;
                }
                else if (receiver == 3)
                {
                    mergeKeyValue.Add("收件人", eachStudent.StudentName);
                    row["CN"] = eachStudent.StudentName;
                }

                if (address == 0)
                {
                    mergeKeyValue.Add("收件人地址", eachStudent.ContactInfo.PermanentAddress.FullAddress);
                    row["POSTALCODE"] = eachStudent.ContactInfo.PermanentAddress.ZipCode;
                    row["POSTALADDRESS"] = eachStudent.ContactInfo.PermanentAddress.County + eachStudent.ContactInfo.PermanentAddress.Town + eachStudent.ContactInfo.PermanentAddress.DetailAddress;
                }
                else if (address == 1)
                {
                    mergeKeyValue.Add("收件人地址", eachStudent.ContactInfo.MailingAddress.FullAddress);
                    row["POSTALCODE"] = eachStudent.ContactInfo.MailingAddress.ZipCode;
                    row["POSTALADDRESS"] = eachStudent.ContactInfo.MailingAddress.County + eachStudent.ContactInfo.MailingAddress.Town + eachStudent.ContactInfo.MailingAddress.DetailAddress;
                }
                #endregion

                #region 學生基本資料
                mergeKeyValue.Add("學年度", schoolyear.ToString());
                mergeKeyValue.Add("學期", semester.ToString());
                mergeKeyValue.Add("班級科別名稱", (eachStudent.RefClass != null) ? eachStudent.RefClass.Department : "");
                mergeKeyValue.Add("班級", (eachStudent.RefClass != null) ? eachStudent.RefClass.ClassName : "");
                mergeKeyValue.Add("學號", eachStudent.StudentNumber);
                mergeKeyValue.Add("姓名", eachStudent.StudentName);
                mergeKeyValue.Add("座號", eachStudent.SeatNo);
                #endregion

                row["學年度"] = schoolyear.ToString();
                row["學期"] = semester.ToString();
                //row["學校名稱"] = SmartSchool.Customization.Data.SystemInformation.SchoolChineseName;
                row["學號"] = eachStudent.StudentNumber;
                row["科別"] = eachStudent.Department;
                row["班級"] = (eachStudent.RefClass != null) ? eachStudent.RefClass.ClassName : "";
                row["座號"] = eachStudent.SeatNo;
                row["學生姓名"] = eachStudent.StudentName;


                #region 導師與評語
                if (eachStudent.RefClass != null && eachStudent.RefClass.RefTeacher != null)
                {
                    mergeKeyValue.Add("班導師", eachStudent.RefClass.RefTeacher.TeacherName);
                    //row["導師"] = eachStudent.RefClass.RefTeacher.TeacherName;
                }
                //mergeKeyValue.Add("評語", "");
                //if (eachStudent.SemesterMoralScoreList.Count > 0)
                //{
                //    foreach (SemesterMoralScoreInfo info in eachStudent.SemesterMoralScoreList)
                //    {
                //        if (info.SchoolYear == schoolyear && info.Semester == semester)
                //        {
                //            mergeKeyValue["評語"] = info.SupervisedByComment;
                //            row["評語"] = @"""" + info.SupervisedByComment + @"""";
                //        }
                //    }
                //}
                #endregion

                #region 獎懲紀錄
                int awardA = 0;
                int awardB = 0;
                int awardC = 0;
                int faultA = 0;
                int faultB = 0;
                int faultC = 0;
                bool ua = false; //留校察看
                foreach (RewardInfo info in eachStudent.RewardList)
                {
                    if (info.SchoolYear == schoolyear && info.Semester == semester)
                    {
                        awardA += info.AwardA;
                        awardB += info.AwardB;
                        awardC += info.AwardC;

                        if (!info.Cleared)
                        {
                            faultA += info.FaultA;
                            faultB += info.FaultB;
                            faultC += info.FaultC;
                        }

                        if (info.UltimateAdmonition)
                            ua = true;
                    }
                }
                mergeKeyValue.Add("大功", awardA.ToString());
                mergeKeyValue.Add("小功", awardB.ToString());
                mergeKeyValue.Add("嘉獎", awardC.ToString());
                mergeKeyValue.Add("大過", faultA.ToString());
                mergeKeyValue.Add("小過", faultB.ToString());
                mergeKeyValue.Add("警告", faultC.ToString());
                row["大功"] = awardA.ToString();
                row["小功"] = awardB.ToString();
                row["嘉獎"] = awardC.ToString();
                row["大過"] = faultA.ToString();
                row["小過"] = faultB.ToString();
                row["警告"] = faultC.ToString();
                
                if (ua)
                {
                    mergeKeyValue.Add("留校察看", "ˇ");
                    row["留校察看"] = "＊";
                }
                else
                    mergeKeyValue.Add("留校察看", "");

                #endregion

                #region 科目成績

                Dictionary<SemesterSubjectScoreInfo, Dictionary<string, string>> subjectScore = new Dictionary<SemesterSubjectScoreInfo, Dictionary<string, string>>();
                int thisSemesterTotalCredit = 0;
                int thisSchoolYearTotalCredit = 0;
                int beforeSemesterTotalCredit = 0;
                // 必修累計
                int beforeSemesterTotalCreditR1 = 0;
                // 選修累計
                int beforeSemesterTotalCreditR2 = 0;

                Dictionary<int, decimal> resitStandard = eachStudent.Fields["補考標準"] as Dictionary<int, decimal>;

                
                foreach (SemesterSubjectScoreInfo info in eachStudent.SemesterSubjectScoreList)
                {
                    string invalidCredit = info.Detail.GetAttribute("不計學分");
                    string noScoreString = info.Detail.GetAttribute("不需評分");
                    bool noScore = (noScoreString != "是");

                    if (invalidCredit == "是")
                        continue;

                    if (info.SchoolYear == schoolyear && info.Semester == semester)
                    {
                        if (!subjectScore.ContainsKey(info))
                            subjectScore.Add(info, new Dictionary<string, string>());

                        subjectScore[info].Add("科目", info.Subject);
                        subjectScore[info].Add("級別", (string.IsNullOrEmpty(info.Level) ? "" : GetNumber(int.Parse(info.Level))));
                        subjectScore[info].Add("學分", info.Credit.ToString());
                        subjectScore[info].Add("分數", noScore ? info.Score.ToString() : "");
                        subjectScore[info].Add("必修", ((info.Require) ? "必" : "選"));

                        

                        //判斷補考或重修 
                        if (!info.Pass)
                        {
                            if (info.Score >= resitStandard[info.GradeYear])
                                subjectScore[info].Add("補考", "是");
                            else
                                subjectScore[info].Add("補考", "否");
                        }
                    }

                    if (info.Pass)
                    {
                        if (info.SchoolYear == schoolyear && info.Semester == semester)
                            thisSemesterTotalCredit += info.Credit;

                        if (info.SchoolYear < schoolyear)
                        {
                            beforeSemesterTotalCredit += info.Credit;
                            // 累計必選修學分數
                            if (info.Require)
                                beforeSemesterTotalCreditR1 += info.Credit;
                            else
                                beforeSemesterTotalCreditR2 += info.Credit;
                        }
                        else if (info.SchoolYear == schoolyear && info.Semester <= semester)
                        {
                            beforeSemesterTotalCredit += info.Credit;

                            // 累計必選修學分數
                            if (info.Require)
                                beforeSemesterTotalCreditR1 += info.Credit;
                            else
                                beforeSemesterTotalCreditR2 += info.Credit;

                        }
                        if (info.SchoolYear == schoolyear)
                            thisSchoolYearTotalCredit += info.Credit;
                    }
                }

                //if (schoolyearscore)
                //{
                //    foreach (SchoolYearSubjectScoreInfo info in var.SchoolYearSubjectScoreList)
                //    {
                //        if (info.SchoolYear == schoolyear)
                //        {
                //            string subject = info.Subject;
                //            foreach (SemesterSubjectScoreInfo key in subjectScore.Keys)
                //            {
                //                if (subjectScore[key]["科目"] == subject && !subjectScore[key].ContainsKey("學年成績"))
                //                    subjectScore[key].Add("學年成績", info.Score.ToString());

                //            }
                //        }
                //    }
                //}

                mergeKeyValue.Add("科目成績起始位置", new object[] { subjectScore, resitSign, repeatSign });
                //mergeKeyValue.Add("取得學分數", "學期" + (schoolyearscore ? "/學年" : "") + "取得學分數");
                //mergeKeyValue.Add("名次", "");
                //mergeKeyValue.Add("學分數", thisSemesterTotalCredit.ToString());
                //mergeKeyValue.Add("累計學分數", beforeSemesterTotalCredit.ToString());


                List<SemesterSubjectScoreInfo> sortList = new List<SemesterSubjectScoreInfo>();
                sortList.AddRange(subjectScore.Keys);
                sortList.Sort(SortBySemesterSubjectScoreInfo);

                int subjIdx = 1;
                foreach (SemesterSubjectScoreInfo info in sortList)
                {
                    row["科目名稱"+subjIdx] = subjectScore[info]["科目"] + ((string.IsNullOrEmpty(subjectScore[info]["級別"])) ? "" : (" (" + subjectScore[info]["級別"] + ")"));
                    row["科目學分數" + subjIdx] = subjectScore[info]["學分"];
                    row["科目成績" + subjIdx] = subjectScore[info]["分數"];
                    if (subjectScore[info].ContainsKey("補考"))
                    {
                        if (subjectScore[info]["補考"] == "是")
                            row["科目備註" + subjIdx] = resitSign;
                        else if (subjectScore[info]["補考"] == "否")
                            row["科目備註" + subjIdx] = repeatSign;
                    }
                    subjIdx++;
                }


                #endregion

                #region 分項成績

                Dictionary<string, Dictionary<string, string>> entryScore = new Dictionary<string, Dictionary<string, string>>();

                foreach (SemesterEntryScoreInfo info in eachStudent.SemesterEntryScoreList)
                {
                    if (info.SchoolYear == schoolyear && info.Semester == semester)
                    {
                        string entry = info.Entry;
                        if (!entryScore.ContainsKey(entry))
                            entryScore.Add(entry, new Dictionary<string, string>());
                        entryScore[entry].Add("分數", info.Score.ToString());
                    }
                }

                if(entryScore.ContainsKey("學業"))
                    row["學期成績"] = entryScore["學業"]["分數"];

                if (entryScore.ContainsKey("體育"))
                    row["體育"] = entryScore["體育"]["分數"];

                if (entryScore.ContainsKey("國防通識"))
                    row["國防通識"] = entryScore["國防通識"]["分數"];

                //如果是下學期，就多列印學年德行成績。
                if (semester == 2)
                {
                    foreach (SchoolYearEntryScoreInfo info in eachStudent.SchoolYearEntryScoreList)
                    {
                        if (info.SchoolYear == schoolyear)
                        {
                            string entry = info.Entry;

                            if (entry == "德行")
                            {
                                entryScore.Add("學年德行成績", new Dictionary<string, string>());
                                entryScore["學年德行成績"].Add("分數", info.Score.ToString());
                            }
                        }
                    }
                }

                SemesterEntryRating rating = new SemesterEntryRating(eachStudent);

                Dictionary<string, string> totalCredit = new Dictionary<string, string>();
                totalCredit.Add("學業成績名次", rating.GetPlace(schoolyear, semester));
                row["成績名次"] = rating.GetPlace(schoolyear, semester);
                totalCredit.Add("學期取得學分數", thisSemesterTotalCredit.ToString());
                row["學期取得學分數"] = thisSemesterTotalCredit.ToString();
                totalCredit.Add("累計取得必修學分", beforeSemesterTotalCreditR1.ToString());
                row["累計取得必修學分"] = beforeSemesterTotalCreditR1.ToString();
                totalCredit.Add("累計取得選修學分", beforeSemesterTotalCreditR2.ToString());
                row["累計取得選修學分"] = beforeSemesterTotalCreditR2.ToString();

                //totalCredit.Add("累計取得學分數", beforeSemesterTotalCredit.ToString());
                //row["累計學分數"] = beforeSemesterTotalCredit.ToString();

                if (!mergeKeyValue.ContainsKey("學期取得學分數"))
                    mergeKeyValue.Add("學期取得學分數", thisSemesterTotalCredit.ToString());

                if (!mergeKeyValue.ContainsKey("累計取得必修學分"))
                    mergeKeyValue.Add("累計取得必修學分", beforeSemesterTotalCreditR1.ToString());

                if (!mergeKeyValue.ContainsKey("累計取得選修學分"))
                    mergeKeyValue.Add("累計取得選修學分", beforeSemesterTotalCreditR2.ToString());



                mergeKeyValue.Add("分項成績起始位置", new object[] { entryScore, totalCredit, over100 });

                #endregion

                #region 德行文字評量
                foreach (SemesterMoralScoreInfo info in eachStudent.SemesterMoralScoreList)
                {
                    if (info.SchoolYear == schoolyear && info.Semester == semester)
                    {
                        mergeKeyValue.Add("綜合表現", info.Detail);

                        // 處理綜表現放入row 資料
                        XmlElement objValue = (XmlElement)info.Detail;
                        foreach (XmlElement each in objValue.SelectNodes("TextScore/Morality"))
                        {
                            string face = each.GetAttribute("Face");

                            //如果學生身上的face不存在對照表上，就不印出來
                            if ((SmartSchool.Customization.Data.SystemInformation.Fields["文字評量對照表"] as XmlElement).SelectSingleNode("Content/Morality[@Face='" + face + "']") == null) continue;

                            //string comment = each.InnerText.Replace(",", "、");

                            string comment = @"""" + each.InnerText + @"""";

                            // 評語（含社團、競賽、服務學習、禮節等綜合表現及建議）
                            if (face.Contains("評語（含社團、競賽、服務學習、禮節等綜合表現及建議）"))
                                row["評語"] = comment;
                            //    row["評語（含社團、競賽、服務學習、禮節等綜合表現及建議）"] = comment;

                            //if (face.Contains("綜合表現"))
                            //    row["綜合表現"] = comment;

                            //if (face.Contains("具體建議"))
                            //    row["具體建議"] = comment;

                            //if (face.Contains("社團"))
                            //    row["社團競賽服務學習"] = comment;
                        }
                    }
                }
                #endregion

                #region 缺曠紀錄

                Dictionary<string, int> absenceInfo = new Dictionary<string, int>();

                foreach (string periodType in userType.Keys)
                {
                    foreach (string absence in userType[periodType])
                    {
                        if (!absenceInfo.ContainsKey(periodType + "_" + absence))
                            absenceInfo.Add(periodType + "_" + absence, 0);
                    }
                }

                

                foreach (AttendanceInfo info in eachStudent.AttendanceList)
                {
                    if (info.SchoolYear == schoolyear && info.Semester == semester)
                    {
                        if (PeriodTypeDic.ContainsKey(info.Period)) //2011/1/25 by dylan
                        {
                            if (absenceInfo.ContainsKey(PeriodTypeDic[info.Period] + "_" + info.Absence))
                                absenceInfo[PeriodTypeDic[info.Period] + "_" + info.Absence]++;
                        }
                    }
                }


                // 曠課	遲到早退	事假	病假	喪假	公假
                if (absenceInfo.ContainsKey("一般_曠課"))
                    row["曠課"] = absenceInfo["一般_曠課"];

                if (absenceInfo.ContainsKey("一般_遲到/早退"))
                    row["遲到早退"] = absenceInfo["一般_遲到/早退"];

                if(absenceInfo.ContainsKey("一般_事假"))
                    row["事假"] = absenceInfo["一般_事假"];

                if (absenceInfo.ContainsKey("一般_病假"))
                    row["病假"] = absenceInfo["一般_病假"];

                if (absenceInfo.ContainsKey("一般_喪假"))
                    row["喪假"] = absenceInfo["一般_喪假"];

                if (absenceInfo.ContainsKey("一般_公假"))
                    row["公假"] = absenceInfo["一般_公假"];

             
                mergeKeyValue.Add("缺曠紀錄", new object[] { userType, absenceInfo });

                #endregion

                eachStudentDoc.MailMerge.MergeField += new Aspose.Words.Reporting.MergeFieldEventHandler(MailMerge_MergeField);
                eachStudentDoc.MailMerge.RemoveEmptyParagraphs = true;

                List<string> keys = new List<string>();
                List<object> values = new List<object>();

                foreach (string key in mergeKeyValue.Keys)
                {
                    keys.Add(key);
                    values.Add(mergeKeyValue[key]);
                }
                eachStudentDoc.MailMerge.Execute(keys.ToArray(), values.ToArray());

                doc.Sections.Add(doc.ImportNode(eachStudentDoc.Sections[0], true));

                Global.dt.Rows.Add(row);

                //回報進度
                _BGWSemesterScoreReport.ReportProgress((int)(currentStudent++ * 100.0 / totalStudent));
            }

            #endregion

            e.Result = doc;
        }

        private void MailMerge_MergeField(object sender, Aspose.Words.Reporting.MergeFieldEventArgs e)
        {
            #region 科目成績
            
            if (e.FieldName == "科目成績起始位置")
            {
                object[] objectValue = (object[])e.FieldValue;
                Dictionary<SemesterSubjectScoreInfo, Dictionary<string, string>> subjectScore = (Dictionary<SemesterSubjectScoreInfo, Dictionary<string, string>>)objectValue[0];
                string resitSign = (string)objectValue[1];
                string repeatSign = (string)objectValue[2];

                DocumentBuilder builder = new DocumentBuilder(e.Document);
                builder.MoveToField(e.Field, false);

                Table SSTable = ((Aspose.Words.Row)((Aspose.Words.Cell)builder.CurrentParagraph.ParentNode).ParentRow).ParentTable;

                int SSRowNumber = SSTable.Rows.Count - 1;
                int SSTableRowIndex = 1;
                int SSTableColIndex = 0;

                List<SemesterSubjectScoreInfo> sortList = new List<SemesterSubjectScoreInfo>();
                sortList.AddRange(subjectScore.Keys);
                sortList.Sort(SortBySemesterSubjectScoreInfo);

                foreach (SemesterSubjectScoreInfo info in sortList)
                {
                    if (SSTable.Rows[SSTableRowIndex].Cells[SSTableColIndex] == null)
                    {
                        throw new ArgumentException("科目成績表格不足容下所有科目成績。");
                    }

                    Runs runs = SSTable.Rows[SSTableRowIndex].Cells[SSTableColIndex].Paragraphs[0].Runs;
                    runs.Add(new Run(e.Document));
                    runs[runs.Count - 1].Text = subjectScore[info]["科目"] + ((string.IsNullOrEmpty(subjectScore[info]["級別"])) ? "" : (" (" + subjectScore[info]["級別"] + ")"));
                    runs[runs.Count - 1].Font.Size = 10;
                    runs[runs.Count - 1].Font.Name = "新細明體";

                    SSTable.Rows[SSTableRowIndex].Cells[SSTableColIndex + 1].Paragraphs[0].Runs.Add(new Run(e.Document, subjectScore[info]["必修"] + subjectScore[info]["學分"]));
                    SSTable.Rows[SSTableRowIndex].Cells[SSTableColIndex + 1].Paragraphs[0].Runs[0].Font.Size = 10;
                    SSTable.Rows[SSTableRowIndex].Cells[SSTableColIndex + 2].Paragraphs[0].Runs.Add(new Run(e.Document, subjectScore[info]["分數"]));
                    SSTable.Rows[SSTableRowIndex].Cells[SSTableColIndex + 2].Paragraphs[0].Runs[0].Font.Size = 10;

                    int colshift = 0;
                    string re = "";
                    if (subjectScore[info].ContainsKey("補考"))
                    {
                        if (subjectScore[info]["補考"] == "是")
                            re = resitSign;
                        else if (subjectScore[info]["補考"] == "否")
                            re = repeatSign;
                    }

                    SSTable.Rows[SSTableRowIndex].Cells[SSTableColIndex + 3 + colshift].Paragraphs[0].Runs.Add(new Run(e.Document, re));
                    SSTable.Rows[SSTableRowIndex].Cells[SSTableColIndex + 3 + colshift].Paragraphs[0].Runs[0].Font.Size = 10;

                    SSTableRowIndex++;
                    if (SSTableRowIndex > SSRowNumber)
                    {
                        SSTableRowIndex = 1;
                        SSTableColIndex += 4;
                    }
                }

                e.Text = string.Empty;
            }

            #endregion

            #region 分項成績

            if (e.FieldName == "分項成績起始位置")
            {
                object[] objectValue = (object[])e.FieldValue;
                Dictionary<string, Dictionary<string, string>> entryScore = (Dictionary<string, Dictionary<string, string>>)objectValue[0];
                Dictionary<string, string> totalCredit = (Dictionary<string, string>)objectValue[1];
                bool over100 = (bool)objectValue[2];

                DocumentBuilder builder = new DocumentBuilder(e.Document);
                builder.MoveToField(e.Field, false);

                Table ESTable = ((Aspose.Words.Row)((Aspose.Words.Cell)builder.CurrentParagraph.ParentNode).ParentRow).ParentTable;

                int ESRowNumber = ESTable.Rows.Count - 1;
                int ESTableRowIndex = 1;
                int ESTableColIndex = 0;

                List<string> sortList = new List<string>();
                sortList.AddRange(entryScore.Keys);
                sortList.Sort(SortByEntryName);

                foreach (string entry in sortList)
                {
                    // 先將(原始)過濾
                    if (entry.Contains("(原始)"))
                        continue;

                    string semesterDegree = "";
                    if (entry == "德行" || entry == "學年德行成績")
                    {
                        continue;

                        //decimal moralScore = decimal.Parse(entryScore[entry]["分數"]);
                        //if (!over100 && moralScore > 100)
                        //    entryScore[entry]["分數"] = "100";
                        //semesterDegree = " / " + ParseLevel(moralScore);
                    }

                    Runs runs = ESTable.Rows[ESTableRowIndex].Cells[ESTableColIndex].Paragraphs[0].Runs;

                    runs.Add(new Run(e.Document, ToDisplayName(entry)));
                    runs[runs.Count - 1].Font.Size = 10;
                    ESTable.Rows[ESTableRowIndex].Cells[ESTableColIndex + 1].Paragraphs[0].Runs.Add(new Run(e.Document, entryScore[entry]["分數"] + semesterDegree));
                    ESTable.Rows[ESTableRowIndex].Cells[ESTableColIndex + 1].Paragraphs[0].Runs[0].Font.Size = 10;

                    ESTableRowIndex++;
                    if (ESTableRowIndex > ESRowNumber)
                    {
                        ESTableRowIndex = 1;
                        ESTableColIndex += 2;
                    }
                }

                foreach (string key in totalCredit.Keys)
                {
                    Runs runs = ESTable.Rows[ESTableRowIndex].Cells[ESTableColIndex].Paragraphs[0].Runs;

                    runs.Add(new Run(e.Document, key));
                    runs[runs.Count - 1].Font.Size = 10;
                    ESTable.Rows[ESTableRowIndex].Cells[ESTableColIndex + 1].Paragraphs[0].Runs.Add(new Run(e.Document, totalCredit[key]));
                    ESTable.Rows[ESTableRowIndex].Cells[ESTableColIndex + 1].Paragraphs[0].Runs[0].Font.Size = 10;

                    ESTableRowIndex++;
                    if (ESTableRowIndex > ESRowNumber)
                    {
                        ESTableRowIndex = 1;
                        ESTableColIndex += 2;
                    }
                }

                e.Text = string.Empty;
            }

            #endregion

            #region 缺曠紀錄

            if (e.FieldName == "缺曠紀錄")
            {
                object[] objectValue = (object[])e.FieldValue;

                if ((Dictionary<string, List<string>>)objectValue[0] == null || ((Dictionary<string, List<string>>)objectValue[0]).Count == 0)
                {
                    e.Text = string.Empty;
                    return;
                }

                Dictionary<string, List<string>> userType = (Dictionary<string, List<string>>)objectValue[0];
                Dictionary<string, int> absenceInfo = (Dictionary<string, int>)objectValue[1];

                #region 產生缺曠紀錄表格

                DocumentBuilder builder = new DocumentBuilder(e.Document);
                builder.MoveToField(e.Field, false);

                int ARowNumber = 3;
                double AWidth = 0;
                double AHeight = 0;
                double ARowHeight = 0;

                int AColumn = 0;
                double AMicroColumn = 0;

                foreach (string periodType in userType.Keys)
                {
                    AColumn += userType[periodType].Count;
                }

                Aspose.Words.Cell ACell = (Aspose.Words.Cell)builder.CurrentParagraph.ParentNode;

                AWidth = ACell.CellFormat.Width;
                AHeight = (ACell.ParentNode as Aspose.Words.Row).RowFormat.Height;
                ARowHeight = (AHeight) / (ARowNumber);
                AMicroColumn = AWidth / (double)AColumn;

                builder.StartTable();
                builder.CellFormat.ClearFormatting();
                builder.RowFormat.HeightRule = HeightRule.Exactly;
                builder.RowFormat.Height = ARowHeight;
                builder.RowFormat.Alignment = RowAlignment.Center;
                builder.CellFormat.VerticalAlignment = CellVerticalAlignment.Center;
                builder.CellFormat.LeftPadding = 0.0;
                builder.CellFormat.RightPadding = 0.0;
                builder.ParagraphFormat.Alignment = ParagraphAlignment.Center;
                builder.ParagraphFormat.LineSpacingRule = LineSpacingRule.Exactly;
                builder.ParagraphFormat.LineSpacing = 10;
                builder.Font.Size = 10;

                foreach (string periodType in userType.Keys)
                {
                    builder.InsertCell().CellFormat.Width = AMicroColumn * userType[periodType].Count;
                    builder.Write(periodType);
                }

                builder.EndRow();

                foreach (string periodType in userType.Keys)
                {
                    foreach (string absence in userType[periodType])
                    {
                        builder.InsertCell().CellFormat.Width = AMicroColumn;
                        builder.Write(absence);
                    }
                }

                builder.EndRow();

                foreach (string periodType in userType.Keys)
                {
                    foreach (string absence in userType[periodType])
                    {
                        builder.InsertCell().CellFormat.Width = AMicroColumn;
                        builder.Write(absenceInfo[periodType + "_" + absence].ToString());
                    }
                }

                builder.EndRow();

                Table ATable = builder.EndTable();

                //去除表格四邊的線
                foreach (Aspose.Words.Cell cell in ATable.FirstRow.Cells)
                    cell.CellFormat.Borders.Top.LineStyle = LineStyle.None;

                foreach (Aspose.Words.Cell cell in ATable.LastRow.Cells)
                    cell.CellFormat.Borders.Bottom.LineStyle = LineStyle.None;

                foreach (Aspose.Words.Row row in ATable.Rows)
                {
                    row.FirstCell.CellFormat.Borders.Left.LineStyle = LineStyle.None;
                    row.LastCell.CellFormat.Borders.Right.LineStyle = LineStyle.None;
                }

                #endregion

                #region 填入缺曠紀錄資料
                #endregion

                e.Text = string.Empty;
            }

            #endregion

            #region 綜合表現(文字評量)

            if (e.FieldName == "綜合表現")
            {
                XmlElement objectValue = (XmlElement)e.FieldValue;

                if (objectValue != null)
                {
                    DocumentBuilder builder = new DocumentBuilder(e.Document);
                    builder.MoveToField(e.Field, false);
                    
                    Aspose.Words.Cell temp;

                    double width = (builder.CurrentParagraph.ParentNode as Aspose.Words.Cell).CellFormat.Width;

                    builder.StartTable();
                    //builder.CellFormat.ClearFormatting();
                    foreach (XmlElement each in objectValue.SelectNodes("TextScore/Morality"))
                    {
                        string face = each.GetAttribute("Face");
                        
                        //如果學生身上的face不存在對照表上，就不印出來
                        if ((SmartSchool.Customization.Data.SystemInformation.Fields["文字評量對照表"] as XmlElement).SelectSingleNode("Content/Morality[@Face='" + face + "']") == null) continue;
                        
                        string comment = each.InnerText;

                        temp = builder.InsertCell();
                        temp.CellFormat.LeftPadding = 5;
                        temp.CellFormat.Width = 120;
                        temp.ParentRow.RowFormat.Alignment = RowAlignment.Left;
                        temp.CellFormat.VerticalAlignment = CellVerticalAlignment.Center;
                        builder.Write(face);

                        temp = builder.InsertCell();
                        temp.CellFormat.LeftPadding = 5;
                        temp.CellFormat.Width = width - 120;
                        temp.ParentRow.RowFormat.Alignment = RowAlignment.Left;
                        temp.CellFormat.VerticalAlignment = CellVerticalAlignment.Center;
                        builder.Write(comment);
                        builder.EndRow();
                    }
                    Table table = builder.EndTable();

                    if (table.Rows.Count > 0)
                    {
                        foreach (Aspose.Words.Cell each in table.FirstRow.Cells)
                            each.CellFormat.Borders.Top.LineStyle = LineStyle.None;

                        foreach (Aspose.Words.Cell each in table.LastRow.Cells)
                            each.CellFormat.Borders.Bottom.LineStyle = LineStyle.None;

                        foreach (Aspose.Words.Row each in table.Rows)
                        {
                            each.FirstCell.CellFormat.Borders.Left.LineStyle = LineStyle.None;
                            each.LastCell.CellFormat.Borders.Right.LineStyle = LineStyle.None;
                        }
                    }

                    e.Field.Remove();
                }
            }
            #endregion
        }

        private static string ToDisplayName(string entry)
        {
            switch (entry)
            {
                case "學業":
                    return "學期學業成績";
                case "德行":
                    return "學期德行成績";
                default:
                    return entry;
            }
        }

        /// <summary>
        /// 缺曠節次類型對照清單(By dylan)
        /// </summary>
        private Dictionary<string, string> PeriodTypeDic = new Dictionary<string, string>();

        /// <summary>
        /// 取得節次名稱對照節次類型(by dylan)
        /// </summary>
        /// <returns></returns>
        private void GetPeriodType()
        {
            PeriodTypeDic.Clear();
            foreach (SHPeriodMappingInfo period in SHSchool.Data.SHPeriodMapping.SelectAll())
            {
                if (!PeriodTypeDic.ContainsKey(period.Name))
                {
                    PeriodTypeDic.Add(period.Name, period.Type);
                }

            }
        }
    }

    /// <summary>
    /// 只處理學業成績的排名。
    /// </summary>
    class SemesterEntryRating
    {
        private XmlElement _sems_ratings = null;

        public SemesterEntryRating(StudentRecord student)
        {
            if (student.Fields.ContainsKey("SemesterEntryClassRating"))
                _sems_ratings = student.Fields["SemesterEntryClassRating"] as XmlElement;
        }

        public string GetPlace(int schoolYear, int semester)
        {
            if (_sems_ratings == null) return string.Empty;

            string path = string.Format("SemesterEntryScore[SchoolYear='{0}' and Semester='{1}']/ClassRating/Rating/Item[@分項='學業']/@排名", schoolYear, semester);
            XmlNode result = _sems_ratings.SelectSingleNode(path);

            if (result != null)
                return result.InnerText;
            else
                return string.Empty;
        }
    }

}
