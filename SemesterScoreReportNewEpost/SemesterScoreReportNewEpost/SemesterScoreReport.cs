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

            string reportName = "�Ǵ����Z��(�s��)epost";
            string path = "���Z��������";
            
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
        private Dictionary<string, decimal> _degreeList = null; //����List

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
            #region �Ĥ@�Ӧr�@�˪��ɭ�
            if (a1 == b1)
            {
                if (a.Length > 1 && b.Length > 1)
                    return SortBySubjectName(a.Substring(1), b.Substring(1));
                else
                    return a.Length.CompareTo(b.Length);
            }
            #endregion
            #region �Ĥ@�Ӧr���P�A���O���o�b�]�w���Ǥ����Ʀr�A�p�G�����b�]�w���Ǥ��N�γ�¦r����
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
            list.AddRange(new string[] { "��", "�^", "��", "��", "��", "��", "��", "��", "�a", "��", "��", "¦", "�@" });

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
            list.AddRange(new string[] { "�Ƿ~", "�Ƿ~���Z�W��", "��߬��", "��|", "�꨾�q��", "���d�P�@�z", "�w��", "�Ǧ~�w�榨�Z" });           
            int x = list.IndexOf(a1);
            if (x < 0)
                return list.Count;
            else
                return x;
        }

        //��دŧO�ഫ
        private string GetNumber(int p)
        {
            List<string> list = new List<string>(new string[] { "", "��", "��", "��", "��", "��", "��", "��", "��", "��", "��" });

            if (p < list.Count)
                return list[p];
            else
                return "" + p;
        }

        //�w�榨�Z -> ����
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

        //�������ͧ�����A�x�s�åB�}��
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
                sd.Title = "�t�s�s��";
                sd.FileName = reportName + ".doc";
                sd.Filter = "Word�ɮ� (*.doc)|*.doc|�Ҧ��ɮ� (*.*)|*.*";
                if (sd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        doc.Save(sd.FileName, Aspose.Words.SaveFormat.Doc);

                    }
                    catch
                    {
                        MsgBox.Show("���w���|�L�k�s���C", "�إ��ɮץ���", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                sd.Title = "�t�s�s��";
                sd.FileName = reportName + ".csv";
                sd.Filter = "CSV�ɮ� (*.csv)|*.xls|�Ҧ��ɮ� (*.*)|*.*";
                if (sd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        wb.Save(sd.FileName, Aspose.Cells.FileFormatType.CSV);

                    }
                    catch
                    {
                        MsgBox.Show("���w���|�L�k�s���C", "�إ��ɮץ���", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
            }
        }


        //��J�ǥ͸��
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
            helper.StudentHelper.FillField("SemesterEntryClassRating", students); //�Ǵ������Z�ƦW�C
            helper.StudentHelper.FillField("�ɦҼз�", students);
            helper.StudentHelper.FillSchoolYearEntryScore(true, students);
            helper.StudentHelper.FillSemesterMoralScore(true, students);
        }

        #endregion

        private void button_OnClick(object sender, EventArgs e)
        {
            SemesterScoreReportFormNew form = new SemesterScoreReportFormNew();
            
            if (form.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                // �ǥ�
                
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
                // �Z��

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
            // �ˬd�O�_���� Excel
            if (Global._CheckExportEpost)
            {
                Utility.CompletedXlsCsv("�Ǵ����Z��epost", Global.dt);
                //Workbook wb = new Workbook();
                //wb.Worksheets[0].Cells.ImportDataTable(Global.dt, true, "A1");
                //CompletedXls("�Ǵ����Z��epost", wb);
            }

            button.SetBarMessage("�Ǵ����Z�沣�ͧ���");
            Completed("�Ǵ����Z��", (Document)e.Result);
        }

        private void _BGWSemesterScoreReport_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            button.SetBarMessage("�Ǵ����Z�沣�ͤ�...", e.ProgressPercentage);
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

            #region ���o���

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

            // �Ƨ�
            try
            {
                allStudent = (from data in selStudent orderby data.RefClass.ClassName, int.Parse(data.SeatNo) ascending select data).ToList();
            }
            catch (Exception ex)
            {
                allStudent = selStudent;
            }


            //���o��r���q��Ӫ�
            SmartSchool.Customization.Data.SystemInformation.getField("��r���q��Ӫ�");

            int currentStudent = 1;
            int totalStudent = allStudent.Count;

            //WearyDogComputer computer = new WearyDogComputer();
            //computer.FillSemesterSubjectScoreInfoWithResit(helper, true, allStudent);

            #endregion

            // �ϥ� Data Table �覡�ө��ơA���ͦ� Excel
            Global.dt.Clear();
            Global.dt.Columns.Clear();
            // ��J���W��
            Global.dt.Columns.Add("CN");
            Global.dt.Columns.Add("POSTALCODE");
            Global.dt.Columns.Add("POSTALADDRESS");
            Global.dt.Columns.Add("�Ǧ~��");
            Global.dt.Columns.Add("�Ǵ�");
            Global.dt.Columns.Add("��O");
            //Global.dt.Columns.Add("�ǮզW��");

            Global.dt.Columns.Add("�Z��");
            Global.dt.Columns.Add("�y��");
            Global.dt.Columns.Add("�Ǹ�");
            Global.dt.Columns.Add("�ǥͩm�W");
            //Global.dt.Columns.Add("�l���ϸ�");
            //Global.dt.Columns.Add("�a�}");
            //Global.dt.Columns.Add("����H");

            //int subjMax = 0,commentMax=0;
            //foreach (StudentRecord eachStudent in allStudent)
            //{
            //    int count = 0;
            //    foreach (SemesterSubjectScoreInfo info in eachStudent.SemesterSubjectScoreList)
            //    {
            //        string invalidCredit = info.Detail.GetAttribute("���p�Ǥ�");
            //        string noScoreString = info.Detail.GetAttribute("���ݵ���");
            //        bool noScore = (noScoreString != "�O");

            //        if (invalidCredit == "�O")
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

            //                //�p�G�ǥͨ��W��face���s�b��Ӫ��W�A�N���L�X��
            //                if ((SmartSchool.Customization.Data.SystemInformation.Fields["��r���q��Ӫ�"] as XmlElement).SelectSingleNode("Content/Morality[@Face='" + face + "']") == null) continue;
                            

            //                commIdx++;
            //            }
            //            if (commIdx > commentMax)
            //                commentMax = commIdx;
            //        }
            //    }

            //}

            // ��ئW�١B��ؾǤ��ơB��ئ��Z..(�ʺA)
            for (int i = 1; i <= 22 ; i++)
            {
                Global.dt.Columns.Add("��ئW��" + i);
                Global.dt.Columns.Add("��ؾǤ���" + i);
                Global.dt.Columns.Add("��ئ��Z" + i);
                Global.dt.Columns.Add("��سƵ�" + i);
            }

            //// ��X���{ (�ʺA)
            //for (int i = 1; i <= commentMax; i++)
            //{
            //    Global.dt.Columns.Add("�ǥͦb�պ�X���{����" + i);
            //    Global.dt.Columns.Add("�ǥͦb�պ�X���{��r���q" + i);                
            //}


            //// ���m���O�B���m���O�B���m���O�έp .. (�ʺA)
            //int abs1 = 1;
            //foreach (KeyValuePair<string,List<string>> data in userType)
            //{
            //    Global.dt.Columns.Add("���m���O" + abs1);
            //    for (int i = 1; i <= data.Value.Count; i++)
            //    {
            //        Global.dt.Columns.Add("���m���O"+abs1+"���O" + i);
            //        Global.dt.Columns.Add("���m���O"+abs1+"���O�έp" + i);
            //    }
            //    abs1++;
            //}
                       

            Global.dt.Columns.Add("�Ǵ����Z");
            Global.dt.Columns.Add("��|");
            Global.dt.Columns.Add("�꨾�q��");
            Global.dt.Columns.Add("���Z�W��");
            Global.dt.Columns.Add("���o�Ǥ���");
            Global.dt.Columns.Add("�֭p�Ǥ���");
            Global.dt.Columns.Add("�ż�");
            Global.dt.Columns.Add("�p�\");
            Global.dt.Columns.Add("�j�\");
            Global.dt.Columns.Add("ĵ�i");
            Global.dt.Columns.Add("�p�L");
            Global.dt.Columns.Add("�j�L");            
            Global.dt.Columns.Add("�d�չ��");

            Global.dt.Columns.Add("�m��");
            Global.dt.Columns.Add("��즭�h");
            Global.dt.Columns.Add("�ư�");
            Global.dt.Columns.Add("�f��");
            Global.dt.Columns.Add("�ల");
            Global.dt.Columns.Add("����");
            //Global.dt.Columns.Add("��X���{");
            //Global.dt.Columns.Add("�����ĳ");
            //Global.dt.Columns.Add("�����v�ɪA�Ⱦǲ�");
            //Global.dt.Columns.Add("���y�]�t���ΡB�v�ɡB�A�ȾǲߡB§�`����X���{�Ϋ�ĳ�^");

            //Global.dt.Columns.Add("�ɮv");
            Global.dt.Columns.Add("���y");
            Global.dt.Columns.Add("�Ƶ�");


            #region ���ͳ����ö�J���

            Document doc = new Document();
            doc.Sections.Clear();

            foreach (StudentRecord eachStudent in allStudent)
            {
                DataRow row = Global.dt.NewRow();
                Document eachStudentDoc = new Document(template, "", LoadFormat.Doc, "");

                Dictionary<string, object> mergeKeyValue = new Dictionary<string, object>();

                #region �Ǯհ򥻸��
                mergeKeyValue.Add("�ǮզW��", SmartSchool.Customization.Data.SystemInformation.SchoolChineseName);
                mergeKeyValue.Add("�Ǯզa�}", SmartSchool.Customization.Data.SystemInformation.Address);
                mergeKeyValue.Add("�Ǯչq��", SmartSchool.Customization.Data.SystemInformation.Telephone);
                #endregion

                #region ����H�m�W�P�a�}
                if (receiver == 0)
                {
                    mergeKeyValue.Add("����H", eachStudent.ParentInfo.CustodianName);
                    row["CN"] = eachStudent.ParentInfo.CustodianName;
                }
                else if (receiver == 1)
                {
                    mergeKeyValue.Add("����H", eachStudent.ParentInfo.FatherName);
                    row["CN"] = eachStudent.ParentInfo.FatherName;
                }
                else if (receiver == 2)
                {
                    mergeKeyValue.Add("����H", eachStudent.ParentInfo.MotherName);
                    row["CN"] = eachStudent.ParentInfo.MotherName;
                }
                else if (receiver == 3)
                {
                    mergeKeyValue.Add("����H", eachStudent.StudentName);
                    row["CN"] = eachStudent.StudentName;
                }

                if (address == 0)
                {
                    mergeKeyValue.Add("����H�a�}", eachStudent.ContactInfo.PermanentAddress.FullAddress);
                    row["POSTALCODE"] = eachStudent.ContactInfo.PermanentAddress.ZipCode;
                    row["POSTALADDRESS"] = eachStudent.ContactInfo.PermanentAddress.County + eachStudent.ContactInfo.PermanentAddress.Town + eachStudent.ContactInfo.PermanentAddress.DetailAddress;
                }
                else if (address == 1)
                {
                    mergeKeyValue.Add("����H�a�}", eachStudent.ContactInfo.MailingAddress.FullAddress);
                    row["POSTALCODE"] = eachStudent.ContactInfo.MailingAddress.ZipCode;
                    row["POSTALADDRESS"] = eachStudent.ContactInfo.MailingAddress.County + eachStudent.ContactInfo.MailingAddress.Town + eachStudent.ContactInfo.MailingAddress.DetailAddress;
                }
                #endregion

                #region �ǥͰ򥻸��
                mergeKeyValue.Add("�Ǧ~��", schoolyear.ToString());
                mergeKeyValue.Add("�Ǵ�", semester.ToString());
                mergeKeyValue.Add("�Z�Ŭ�O�W��", (eachStudent.RefClass != null) ? eachStudent.RefClass.Department : "");
                mergeKeyValue.Add("�Z��", (eachStudent.RefClass != null) ? eachStudent.RefClass.ClassName : "");
                mergeKeyValue.Add("�Ǹ�", eachStudent.StudentNumber);
                mergeKeyValue.Add("�m�W", eachStudent.StudentName);
                mergeKeyValue.Add("�y��", eachStudent.SeatNo);
                #endregion

                row["�Ǧ~��"] = schoolyear.ToString();
                row["�Ǵ�"] = semester.ToString();
                //row["�ǮզW��"] = SmartSchool.Customization.Data.SystemInformation.SchoolChineseName;
                row["�Ǹ�"] = eachStudent.StudentNumber;
                row["��O"] = eachStudent.Department;
                row["�Z��"] = (eachStudent.RefClass != null) ? eachStudent.RefClass.ClassName : "";
                row["�y��"] = eachStudent.SeatNo;
                row["�ǥͩm�W"] = eachStudent.StudentName;


                #region �ɮv�P���y
                if (eachStudent.RefClass != null && eachStudent.RefClass.RefTeacher != null)
                {
                    mergeKeyValue.Add("�Z�ɮv", eachStudent.RefClass.RefTeacher.TeacherName);
                    //row["�ɮv"] = eachStudent.RefClass.RefTeacher.TeacherName;
                }
                //mergeKeyValue.Add("���y", "");
                //if (eachStudent.SemesterMoralScoreList.Count > 0)
                //{
                //    foreach (SemesterMoralScoreInfo info in eachStudent.SemesterMoralScoreList)
                //    {
                //        if (info.SchoolYear == schoolyear && info.Semester == semester)
                //        {
                //            mergeKeyValue["���y"] = info.SupervisedByComment;
                //            row["���y"] = @"""" + info.SupervisedByComment + @"""";
                //        }
                //    }
                //}
                #endregion

                #region ���g����
                int awardA = 0;
                int awardB = 0;
                int awardC = 0;
                int faultA = 0;
                int faultB = 0;
                int faultC = 0;
                bool ua = false; //�d�չ��
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
                mergeKeyValue.Add("�j�\", awardA.ToString());
                mergeKeyValue.Add("�p�\", awardB.ToString());
                mergeKeyValue.Add("�ż�", awardC.ToString());
                mergeKeyValue.Add("�j�L", faultA.ToString());
                mergeKeyValue.Add("�p�L", faultB.ToString());
                mergeKeyValue.Add("ĵ�i", faultC.ToString());
                row["�j�\"] = awardA.ToString();
                row["�p�\"] = awardB.ToString();
                row["�ż�"] = awardC.ToString();
                row["�j�L"] = faultA.ToString();
                row["�p�L"] = faultB.ToString();
                row["ĵ�i"] = faultC.ToString();
                
                if (ua)
                {
                    mergeKeyValue.Add("�d�չ��", "��");
                    row["�d�չ��"] = "��";
                }
                else
                    mergeKeyValue.Add("�d�չ��", "");

                #endregion

                #region ��ئ��Z

                Dictionary<SemesterSubjectScoreInfo, Dictionary<string, string>> subjectScore = new Dictionary<SemesterSubjectScoreInfo, Dictionary<string, string>>();
                int thisSemesterTotalCredit = 0;
                int thisSchoolYearTotalCredit = 0;
                int beforeSemesterTotalCredit = 0;

                Dictionary<int, decimal> resitStandard = eachStudent.Fields["�ɦҼз�"] as Dictionary<int, decimal>;

                
                foreach (SemesterSubjectScoreInfo info in eachStudent.SemesterSubjectScoreList)
                {
                    string invalidCredit = info.Detail.GetAttribute("���p�Ǥ�");
                    string noScoreString = info.Detail.GetAttribute("���ݵ���");
                    bool noScore = (noScoreString != "�O");

                    if (invalidCredit == "�O")
                        continue;

                    if (info.SchoolYear == schoolyear && info.Semester == semester)
                    {
                        if (!subjectScore.ContainsKey(info))
                            subjectScore.Add(info, new Dictionary<string, string>());

                        subjectScore[info].Add("���", info.Subject);
                        subjectScore[info].Add("�ŧO", (string.IsNullOrEmpty(info.Level) ? "" : GetNumber(int.Parse(info.Level))));
                        subjectScore[info].Add("�Ǥ�", info.Credit.ToString());
                        subjectScore[info].Add("����", noScore ? info.Score.ToString() : "");
                        subjectScore[info].Add("����", ((info.Require) ? "��" : "��"));

                        

                        //�P�_�ɦҩέ��� 
                        if (!info.Pass)
                        {
                            if (info.Score >= resitStandard[info.GradeYear])
                                subjectScore[info].Add("�ɦ�", "�O");
                            else
                                subjectScore[info].Add("�ɦ�", "�_");
                        }
                    }

                    if (info.Pass)
                    {
                        if (info.SchoolYear == schoolyear && info.Semester == semester)
                            thisSemesterTotalCredit += info.Credit;

                        if (info.SchoolYear < schoolyear)
                            beforeSemesterTotalCredit += info.Credit;
                        else if (info.SchoolYear == schoolyear && info.Semester <= semester)
                            beforeSemesterTotalCredit += info.Credit;

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
                //                if (subjectScore[key]["���"] == subject && !subjectScore[key].ContainsKey("�Ǧ~���Z"))
                //                    subjectScore[key].Add("�Ǧ~���Z", info.Score.ToString());

                //            }
                //        }
                //    }
                //}

                mergeKeyValue.Add("��ئ��Z�_�l��m", new object[] { subjectScore, resitSign, repeatSign });
                //mergeKeyValue.Add("���o�Ǥ���", "�Ǵ�" + (schoolyearscore ? "/�Ǧ~" : "") + "���o�Ǥ���");
                //mergeKeyValue.Add("�W��", "");
                //mergeKeyValue.Add("�Ǥ���", thisSemesterTotalCredit.ToString());
                //mergeKeyValue.Add("�֭p�Ǥ���", beforeSemesterTotalCredit.ToString());


                List<SemesterSubjectScoreInfo> sortList = new List<SemesterSubjectScoreInfo>();
                sortList.AddRange(subjectScore.Keys);
                sortList.Sort(SortBySemesterSubjectScoreInfo);

                int subjIdx = 1;
                foreach (SemesterSubjectScoreInfo info in sortList)
                {
                    row["��ئW��"+subjIdx] = subjectScore[info]["���"] + ((string.IsNullOrEmpty(subjectScore[info]["�ŧO"])) ? "" : (" (" + subjectScore[info]["�ŧO"] + ")"));
                    row["��ؾǤ���" + subjIdx] = subjectScore[info]["�Ǥ�"];
                    row["��ئ��Z" + subjIdx] = subjectScore[info]["����"];
                    if (subjectScore[info].ContainsKey("�ɦ�"))
                    {
                        if (subjectScore[info]["�ɦ�"] == "�O")
                            row["��سƵ�" + subjIdx] = resitSign;
                        else if (subjectScore[info]["�ɦ�"] == "�_")
                            row["��سƵ�" + subjIdx] = repeatSign;
                    }
                    subjIdx++;
                }


                #endregion

                #region �������Z

                Dictionary<string, Dictionary<string, string>> entryScore = new Dictionary<string, Dictionary<string, string>>();

                foreach (SemesterEntryScoreInfo info in eachStudent.SemesterEntryScoreList)
                {
                    if (info.SchoolYear == schoolyear && info.Semester == semester)
                    {
                        string entry = info.Entry;
                        if (!entryScore.ContainsKey(entry))
                            entryScore.Add(entry, new Dictionary<string, string>());
                        entryScore[entry].Add("����", info.Score.ToString());
                    }
                }

                if(entryScore.ContainsKey("�Ƿ~"))
                    row["�Ǵ����Z"] = entryScore["�Ƿ~"]["����"];

                if (entryScore.ContainsKey("��|"))
                    row["��|"] = entryScore["��|"]["����"];

                if (entryScore.ContainsKey("�꨾�q��"))
                    row["�꨾�q��"] = entryScore["�꨾�q��"]["����"];

                //�p�G�O�U�Ǵ��A�N�h�C�L�Ǧ~�w�榨�Z�C
                if (semester == 2)
                {
                    foreach (SchoolYearEntryScoreInfo info in eachStudent.SchoolYearEntryScoreList)
                    {
                        if (info.SchoolYear == schoolyear)
                        {
                            string entry = info.Entry;

                            if (entry == "�w��")
                            {
                                entryScore.Add("�Ǧ~�w�榨�Z", new Dictionary<string, string>());
                                entryScore["�Ǧ~�w�榨�Z"].Add("����", info.Score.ToString());
                            }
                        }
                    }
                }

                SemesterEntryRating rating = new SemesterEntryRating(eachStudent);

                Dictionary<string, string> totalCredit = new Dictionary<string, string>();
                totalCredit.Add("�Ƿ~���Z�W��", rating.GetPlace(schoolyear, semester));
                row["���Z�W��"] = rating.GetPlace(schoolyear, semester);
                totalCredit.Add("�Ǵ����o�Ǥ���", thisSemesterTotalCredit.ToString());
                row["���o�Ǥ���"] = thisSemesterTotalCredit.ToString();
                totalCredit.Add("�֭p���o�Ǥ���", beforeSemesterTotalCredit.ToString());
                row["�֭p�Ǥ���"] = beforeSemesterTotalCredit.ToString();
                mergeKeyValue.Add("�������Z�_�l��m", new object[] { entryScore, totalCredit, over100 });

                #endregion

                #region �w���r���q
                foreach (SemesterMoralScoreInfo info in eachStudent.SemesterMoralScoreList)
                {
                    if (info.SchoolYear == schoolyear && info.Semester == semester)
                    {
                        mergeKeyValue.Add("��X���{", info.Detail);

                        // �B�z����{��Jrow ���
                        XmlElement objValue = (XmlElement)info.Detail;
                        foreach (XmlElement each in objValue.SelectNodes("TextScore/Morality"))
                        {
                            string face = each.GetAttribute("Face");

                            //�p�G�ǥͨ��W��face���s�b��Ӫ��W�A�N���L�X��
                            if ((SmartSchool.Customization.Data.SystemInformation.Fields["��r���q��Ӫ�"] as XmlElement).SelectSingleNode("Content/Morality[@Face='" + face + "']") == null) continue;

                            //string comment = each.InnerText.Replace(",", "�B");

                            string comment = @"""" + each.InnerText + @"""";

                            // ���y�]�t���ΡB�v�ɡB�A�ȾǲߡB§�`����X���{�Ϋ�ĳ�^
                            if (face.Contains("���y�]�t���ΡB�v�ɡB�A�ȾǲߡB§�`����X���{�Ϋ�ĳ�^"))
                                row["���y"] = comment;
                            //    row["���y�]�t���ΡB�v�ɡB�A�ȾǲߡB§�`����X���{�Ϋ�ĳ�^"] = comment;

                            //if (face.Contains("��X���{"))
                            //    row["��X���{"] = comment;

                            //if (face.Contains("�����ĳ"))
                            //    row["�����ĳ"] = comment;

                            //if (face.Contains("����"))
                            //    row["�����v�ɪA�Ⱦǲ�"] = comment;
                        }
                    }
                }
                #endregion

                #region ���m����

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


                // �m��	��즭�h	�ư�	�f��	�ల	����
                if (absenceInfo.ContainsKey("�@��_�m��"))
                    row["�m��"] = absenceInfo["�@��_�m��"];

                if (absenceInfo.ContainsKey("�@��_���/���h"))
                    row["��즭�h"] = absenceInfo["�@��_���/���h"];

                if(absenceInfo.ContainsKey("�@��_�ư�"))
                    row["�ư�"] = absenceInfo["�@��_�ư�"];

                if (absenceInfo.ContainsKey("�@��_�f��"))
                    row["�f��"] = absenceInfo["�@��_�f��"];

                if (absenceInfo.ContainsKey("�@��_�ల"))
                    row["�ల"] = absenceInfo["�@��_�ల"];

                if (absenceInfo.ContainsKey("�@��_����"))
                    row["����"] = absenceInfo["�@��_����"];

             
                mergeKeyValue.Add("���m����", new object[] { userType, absenceInfo });

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

                //�^���i��
                _BGWSemesterScoreReport.ReportProgress((int)(currentStudent++ * 100.0 / totalStudent));
            }

            #endregion

            e.Result = doc;
        }

        private void MailMerge_MergeField(object sender, Aspose.Words.Reporting.MergeFieldEventArgs e)
        {
            #region ��ئ��Z
            
            if (e.FieldName == "��ئ��Z�_�l��m")
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
                        throw new ArgumentException("��ئ��Z���椣���e�U�Ҧ���ئ��Z�C");
                    }

                    Runs runs = SSTable.Rows[SSTableRowIndex].Cells[SSTableColIndex].Paragraphs[0].Runs;
                    runs.Add(new Run(e.Document));
                    runs[runs.Count - 1].Text = subjectScore[info]["���"] + ((string.IsNullOrEmpty(subjectScore[info]["�ŧO"])) ? "" : (" (" + subjectScore[info]["�ŧO"] + ")"));
                    runs[runs.Count - 1].Font.Size = 10;
                    runs[runs.Count - 1].Font.Name = "�s�ө���";

                    SSTable.Rows[SSTableRowIndex].Cells[SSTableColIndex + 1].Paragraphs[0].Runs.Add(new Run(e.Document, subjectScore[info]["����"] + subjectScore[info]["�Ǥ�"]));
                    SSTable.Rows[SSTableRowIndex].Cells[SSTableColIndex + 1].Paragraphs[0].Runs[0].Font.Size = 10;
                    SSTable.Rows[SSTableRowIndex].Cells[SSTableColIndex + 2].Paragraphs[0].Runs.Add(new Run(e.Document, subjectScore[info]["����"]));
                    SSTable.Rows[SSTableRowIndex].Cells[SSTableColIndex + 2].Paragraphs[0].Runs[0].Font.Size = 10;

                    int colshift = 0;
                    string re = "";
                    if (subjectScore[info].ContainsKey("�ɦ�"))
                    {
                        if (subjectScore[info]["�ɦ�"] == "�O")
                            re = resitSign;
                        else if (subjectScore[info]["�ɦ�"] == "�_")
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

            #region �������Z

            if (e.FieldName == "�������Z�_�l��m")
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
                    // ���N(��l)�L�o
                    if (entry.Contains("(��l)"))
                        continue;

                    string semesterDegree = "";
                    if (entry == "�w��" || entry == "�Ǧ~�w�榨�Z")
                    {
                        continue;

                        //decimal moralScore = decimal.Parse(entryScore[entry]["����"]);
                        //if (!over100 && moralScore > 100)
                        //    entryScore[entry]["����"] = "100";
                        //semesterDegree = " / " + ParseLevel(moralScore);
                    }

                    Runs runs = ESTable.Rows[ESTableRowIndex].Cells[ESTableColIndex].Paragraphs[0].Runs;

                    runs.Add(new Run(e.Document, ToDisplayName(entry)));
                    runs[runs.Count - 1].Font.Size = 10;
                    ESTable.Rows[ESTableRowIndex].Cells[ESTableColIndex + 1].Paragraphs[0].Runs.Add(new Run(e.Document, entryScore[entry]["����"] + semesterDegree));
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

            #region ���m����

            if (e.FieldName == "���m����")
            {
                object[] objectValue = (object[])e.FieldValue;

                if ((Dictionary<string, List<string>>)objectValue[0] == null || ((Dictionary<string, List<string>>)objectValue[0]).Count == 0)
                {
                    e.Text = string.Empty;
                    return;
                }

                Dictionary<string, List<string>> userType = (Dictionary<string, List<string>>)objectValue[0];
                Dictionary<string, int> absenceInfo = (Dictionary<string, int>)objectValue[1];

                #region ���ͯ��m��������

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

                //�h������|�䪺�u
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

                #region ��J���m�������
                #endregion

                e.Text = string.Empty;
            }

            #endregion

            #region ��X���{(��r���q)

            if (e.FieldName == "��X���{")
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
                        
                        //�p�G�ǥͨ��W��face���s�b��Ӫ��W�A�N���L�X��
                        if ((SmartSchool.Customization.Data.SystemInformation.Fields["��r���q��Ӫ�"] as XmlElement).SelectSingleNode("Content/Morality[@Face='" + face + "']") == null) continue;
                        
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
                case "�Ƿ~":
                    return "�Ǵ��Ƿ~���Z";
                case "�w��":
                    return "�Ǵ��w�榨�Z";
                default:
                    return entry;
            }
        }

        /// <summary>
        /// ���m�`��������ӲM��(By dylan)
        /// </summary>
        private Dictionary<string, string> PeriodTypeDic = new Dictionary<string, string>();

        /// <summary>
        /// ���o�`���W�ٹ�Ӹ`������(by dylan)
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
    /// �u�B�z�Ƿ~���Z���ƦW�C
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

            string path = string.Format("SemesterEntryScore[SchoolYear='{0}' and Semester='{1}']/ClassRating/Rating/Item[@����='�Ƿ~']/@�ƦW", schoolYear, semester);
            XmlNode result = _sems_ratings.SelectSingleNode(path);

            if (result != null)
                return result.InnerText;
            else
                return string.Empty;
        }
    }

}