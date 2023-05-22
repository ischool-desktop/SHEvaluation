using System.Collections.Generic;
using System.Linq;
using System.Xml;
using FISCA.DSAUtil;
using SmartSchool.Customization.Data;
using SmartSchool.Customization.Data.StudentExtension;
using SmartSchool.Evaluation.GraduationPlan;

namespace SmartSchool.Evaluation.WearyDogComputerHelper
{
    internal class GraduateEvaluator
    {
        public GraduateEvaluator(AccessHelper accesshelper, List<StudentRecord> students)
        {
            DataSource = accesshelper;
            SourceStudents = students;
        }

        private AccessHelper DataSource { get; set; }

        private List<StudentRecord> SourceStudents { get; set; }

        /// <summary>
        /// 檢查學生是否符合畢業條件，傳回無法做畢業判斷的學生及原因。
        /// </summary>
        /// <returns></returns>
        public Dictionary<StudentRecord, List<string>> Evaluate()
        {
            Dictionary<StudentRecord, List<string>> _ErrorList = new Dictionary<StudentRecord, List<string>>();
            //取得學期科目成績
            DataSource.StudentHelper.FillSemesterSubjectScore(true, SourceStudents);
            //取得學期分項成績，因德行成績判斷已移除，故不需取得此資料
            //DataSource.StudentHelper.FillSemesterEntryScore(true, SourceStudents);
            //取得學年分項成績
            DataSource.StudentHelper.FillSchoolYearEntryScore(true, SourceStudents);

            #region 取得獎勵懲戒資料 & 功過換算表

            List<string> StudentIDList = new List<string>();
            foreach (StudentRecord item in SourceStudents)
            {
                if (!StudentIDList.Contains(item.StudentID))
                    StudentIDList.Add(item.StudentID);
            }
            List<K12.Data.DisciplineRecord> DisciplineList = K12.Data.Discipline.SelectByStudentIDs(StudentIDList);
            Dictionary<string, List<K12.Data.DisciplineRecord>> DisciplineDic = new Dictionary<string, List<K12.Data.DisciplineRecord>>();
            foreach (var item in DisciplineList)
            {
                if (!DisciplineDic.ContainsKey(item.RefStudentID))
                    DisciplineDic.Add(item.RefStudentID, new List<K12.Data.DisciplineRecord>());

                DisciplineDic[item.RefStudentID].Add(item);
            }

            //取得功過換算表 MeritDemeritReduce.Select()
            K12.Data.MeritDemeritReduceRecord mreducerecord = K12.Data.MeritDemeritReduce.Select();

            #endregion

            #region 針對每位學生判斷是否符合畢業條件
            foreach (StudentRecord student in SourceStudents)
            {
                //可以計算(表示需要的資料都有)
                bool isDataReasonable = true;
                //計算資料採用課程規劃(預設採用課程規劃)
                bool useGPlan = true;
                //修滿所有必修課程
                bool attendAllRequiredSubjects = false;
                //及格標準<年及,及格標準>
                Dictionary<int, decimal> applyLimit = new Dictionary<int, decimal>();
                //學分規則相關資訊。
                GraduateRule crule = new GraduateRule();
                //同一科目級別不重複採計
                bool filterSameSubject = true;

                XmlDocument docCreditReport = new XmlDocument();
                XmlElement evalReport = docCreditReport.CreateElement("畢業預警及資格審查報告");
                docCreditReport.AppendChild(evalReport);


                XmlDocument docGradCheck = new XmlDocument();
                XmlElement evalResult = docGradCheck.CreateElement("GradCheck");

                List<CreditCheckConfig> creditCheckConfigList = new List<CreditCheckConfig>();


                XmlElement rule = ScoreCalcRule.ScoreCalcRule.Instance.GetStudentScoreCalcRuleInfo(student.StudentID) == null ? null : ScoreCalcRule.ScoreCalcRule.Instance.GetStudentScoreCalcRuleInfo(student.StudentID).ScoreCalcRuleElement;
                if (rule == null)
                {
                    if (!_ErrorList.ContainsKey(student))
                        _ErrorList.Add(student, new List<string>());
                    _ErrorList[student].Add("沒有設定成績計算規則。");
                    isDataReasonable &= false;
                }
                else
                {
                    evalReport.SetAttribute("科別", student.Department);
                    evalReport.SetAttribute("班級", student.RefClass == null ? "" : student.RefClass.ClassName);
                    evalReport.SetAttribute("座號", student.SeatNo);
                    evalReport.SetAttribute("學號", student.StudentNumber);
                    evalReport.SetAttribute("姓名", student.StudentName);
                    evalReport.SetAttribute("成績計算規則", ScoreCalcRule.ScoreCalcRule.Instance.GetStudentScoreCalcRuleInfo(student.StudentID).Name);
                    evalReport.SetAttribute("課程規劃表",
                        GraduationPlan.GraduationPlan.Instance.GetStudentGraduationPlan(student.StudentID) == null
                        ? ""
                        : GraduationPlan.GraduationPlan.Instance.GetStudentGraduationPlan(student.StudentID).Name
                        );

                    DSXmlHelper helper = new DSXmlHelper(rule);
                    crule.RefStudentID = student.StudentID;
                    #region 學期科目成績屬性採計方式
                    //判斷學期科目成績屬性採計方式以課程規劃為主，還是以學生實際修課為主
                    if (rule.SelectSingleNode("學期科目成績屬性採計方式") != null)
                    {
                        if (rule.SelectSingleNode("學期科目成績屬性採計方式").InnerText == "以實際學期科目成績內容為準")
                        {
                            useGPlan = false;
                        }
                        else
                            useGPlan = true;
                    }
                    //判斷若以課程規為主，但是學生身上卻沒有課程規劃
                    if (useGPlan && GraduationPlan.GraduationPlan.Instance.GetStudentGraduationPlan(student.StudentID) == null)
                    {
                        if (!_ErrorList.ContainsKey(student))
                            _ErrorList.Add(student, new List<string>());
                        _ErrorList[student].Add("沒有設定課程規劃表，當\"學期科目成績屬性採計方式\"設定使用\"以課程規劃表內容為準\"時，學生必須要有課程規劃表以做參考。");
                        isDataReasonable &= false;
                    }
                    #endregion


                    //應修總學分數
                    #region 應修總學分數
                    {
                        string ruleName = "應修總學分數";
                        string ruleType = "修課學分數統計";
                        string rulePath = "畢業學分數/應修總學分數";

                        XmlElement reportEle = docCreditReport.CreateElement("畢業規則");
                        evalReport.AppendChild(reportEle);
                        reportEle.SetAttribute("規則", ruleName);
                        reportEle.SetAttribute("類型", ruleType);
                        reportEle.SetAttribute("啟用", "否");
                        reportEle.SetAttribute("設定值", "");
                        reportEle.SetAttribute("課規總學分數", "0");
                        reportEle.SetAttribute("通過標準", "0");
                        reportEle.SetAttribute("累計學分", "0");

                        creditCheckConfigList.Add(new CreditCheckConfig()
                        {
                            Name = ruleName,
                            Type = ruleType,
                            Active = rule.SelectSingleNode(rulePath) != null && rule.SelectSingleNode(rulePath).InnerText.Trim() != "",
                            AcceptOtherSource = true,
                            SetupValue = rule.SelectSingleNode(rulePath) != null ? rule.SelectSingleNode(rulePath).InnerText.Trim() : "",
                            XmlElement = reportEle,
                            DoCheck = delegate (string domain, decimal credit, string entry, string required, string requiredBy, string subjectName, string subjectLevel)
                            {
                                return true;
                            }
                        });
                    }
                    #endregion

                    //修滿所有必修課程
                    #region 修滿所有必修課程
                    {
                        string ruleName = "應修所有必修課程";
                        string ruleType = "修課學分數統計";
                        string rulePath = "修滿所有必修課程";

                        XmlElement reportEle = docCreditReport.CreateElement("畢業規則");
                        evalReport.AppendChild(reportEle);
                        reportEle.SetAttribute("規則", ruleName);
                        reportEle.SetAttribute("類型", ruleType);
                        reportEle.SetAttribute("啟用", "否");
                        reportEle.SetAttribute("設定值", "");
                        reportEle.SetAttribute("課規總學分數", "0");
                        reportEle.SetAttribute("通過標準", "0");
                        reportEle.SetAttribute("累計學分", "0");

                        creditCheckConfigList.Add(new CreditCheckConfig()
                        {
                            Name = ruleName,
                            Type = ruleType,
                            Active = rule.SelectSingleNode(rulePath) != null && rule.SelectSingleNode(rulePath).InnerText.Trim() == "True",
                            AcceptOtherSource = false,// 不採計課規外的必修學分數
                            SetupValue = rule.SelectSingleNode(rulePath) != null && rule.SelectSingleNode(rulePath).InnerText.Trim() == "True" ? "100%" : "",
                            XmlElement = reportEle,
                            DoCheck = delegate (string domain, decimal credit, string entry, string required, string requiredBy, string subjectName, string subjectLevel)
                            {
                                return required == "必修";
                            }
                        });
                    }
                    #endregion

                    //應修專業及實習總學分數
                    #region 應修總學分數
                    {
                        string ruleName = "應修專業及實習總學分數";
                        string ruleType = "修課學分數統計";
                        string rulePath = "畢業學分數/應修專業及實習總學分數";

                        XmlElement reportEle = docCreditReport.CreateElement("畢業規則");
                        evalReport.AppendChild(reportEle);
                        reportEle.SetAttribute("規則", ruleName);
                        reportEle.SetAttribute("類型", ruleType);
                        reportEle.SetAttribute("啟用", "否");
                        reportEle.SetAttribute("設定值", "");
                        reportEle.SetAttribute("課規總學分數", "0");
                        reportEle.SetAttribute("通過標準", "0");
                        reportEle.SetAttribute("累計學分", "0");

                        creditCheckConfigList.Add(new CreditCheckConfig()
                        {
                            Name = ruleName,
                            Type = ruleType,
                            Active = rule.SelectSingleNode(rulePath) != null && rule.SelectSingleNode(rulePath).InnerText.Trim() != "",
                            AcceptOtherSource = true,
                            SetupValue = rule.SelectSingleNode(rulePath) != null ? rule.SelectSingleNode(rulePath).InnerText.Trim() : "",
                            XmlElement = reportEle,
                            DoCheck = delegate (string domain, decimal credit, string entry, string required, string requiredBy, string subjectName, string subjectLevel)
                            {
                                return entry == "實習科目" || entry == "專業科目";
                            }
                        });
                    }
                    #endregion

                    //總學分數
                    #region 總學分數
                    {
                        string ruleName = "總學分數";
                        string ruleType = "取得學分數統計";
                        string rulePath = "畢業學分數/學科累計總學分數";

                        XmlElement reportEle = docCreditReport.CreateElement("畢業規則");
                        evalReport.AppendChild(reportEle);
                        reportEle.SetAttribute("規則", ruleName);
                        reportEle.SetAttribute("類型", ruleType);
                        reportEle.SetAttribute("啟用", "否");
                        reportEle.SetAttribute("設定值", "");
                        reportEle.SetAttribute("課規總學分數", "0");
                        reportEle.SetAttribute("通過標準", "0");
                        reportEle.SetAttribute("累計學分", "0");

                        creditCheckConfigList.Add(new CreditCheckConfig()
                        {
                            Name = ruleName,
                            Type = ruleType,
                            Active = rule.SelectSingleNode(rulePath) != null && rule.SelectSingleNode(rulePath).InnerText.Trim() != "",
                            AcceptOtherSource = true,
                            SetupValue = rule.SelectSingleNode(rulePath) != null ? rule.SelectSingleNode(rulePath).InnerText.Trim() : "",
                            XmlElement = reportEle,
                            DoCheck = delegate (string domain, decimal credit, string entry, string required, string requiredBy, string subjectName, string subjectLevel)
                            {
                                return true;
                            }
                        });
                    }
                    #endregion

                    //必修學分數
                    #region 必修學分數
                    {
                        string ruleName = "必修學分數";
                        string ruleType = "取得學分數統計";
                        string rulePath = "畢業學分數/必修學分數";

                        XmlElement reportEle = docCreditReport.CreateElement("畢業規則");
                        evalReport.AppendChild(reportEle);
                        reportEle.SetAttribute("規則", ruleName);
                        reportEle.SetAttribute("類型", ruleType);
                        reportEle.SetAttribute("啟用", "否");
                        reportEle.SetAttribute("設定值", "");
                        reportEle.SetAttribute("課規總學分數", "0");
                        reportEle.SetAttribute("通過標準", "0");
                        reportEle.SetAttribute("累計學分", "0");

                        creditCheckConfigList.Add(new CreditCheckConfig()
                        {
                            Name = ruleName,
                            Type = ruleType,
                            Active = rule.SelectSingleNode(rulePath) != null && rule.SelectSingleNode(rulePath).InnerText.Trim() != "",
                            AcceptOtherSource = true,
                            SetupValue = rule.SelectSingleNode(rulePath) != null ? rule.SelectSingleNode(rulePath).InnerText.Trim() : "",
                            XmlElement = reportEle,
                            DoCheck = delegate (string domain, decimal credit, string entry, string required, string requiredBy, string subjectName, string subjectLevel)
                            {
                                return required == "必修";
                            }
                        });
                    }
                    #endregion

                    //部訂必修學分數
                    #region 部訂必修學分數
                    {
                        string ruleName = "部訂必修學分數";
                        string ruleType = "取得學分數統計";
                        string rulePath = "畢業學分數/部訂必修學分數";

                        XmlElement reportEle = docCreditReport.CreateElement("畢業規則");
                        evalReport.AppendChild(reportEle);
                        reportEle.SetAttribute("規則", ruleName);
                        reportEle.SetAttribute("類型", ruleType);
                        reportEle.SetAttribute("啟用", "否");
                        reportEle.SetAttribute("設定值", "");
                        reportEle.SetAttribute("課規總學分數", "0");
                        reportEle.SetAttribute("通過標準", "0");
                        reportEle.SetAttribute("累計學分", "0");

                        creditCheckConfigList.Add(new CreditCheckConfig()
                        {
                            Name = ruleName,
                            Type = ruleType,
                            Active = rule.SelectSingleNode(rulePath) != null && rule.SelectSingleNode(rulePath).InnerText.Trim() != "",
                            AcceptOtherSource = true,
                            SetupValue = rule.SelectSingleNode(rulePath) != null ? rule.SelectSingleNode(rulePath).InnerText.Trim() : "",
                            XmlElement = reportEle,
                            DoCheck = delegate (string domain, decimal credit, string entry, string required, string requiredBy, string subjectName, string subjectLevel)
                            {
                                return required == "必修" && requiredBy == "部訂";
                            }
                        });
                    }
                    #endregion

                    //校訂必修學分數
                    #region 校訂必修學分數
                    {
                        string ruleName = "校訂必修學分數";
                        string ruleType = "取得學分數統計";
                        string rulePath = "畢業學分數/校訂必修學分數";

                        XmlElement reportEle = docCreditReport.CreateElement("畢業規則");
                        evalReport.AppendChild(reportEle);
                        reportEle.SetAttribute("規則", ruleName);
                        reportEle.SetAttribute("類型", ruleType);
                        reportEle.SetAttribute("啟用", "否");
                        reportEle.SetAttribute("設定值", "");
                        reportEle.SetAttribute("課規總學分數", "0");
                        reportEle.SetAttribute("通過標準", "0");
                        reportEle.SetAttribute("累計學分", "0");

                        creditCheckConfigList.Add(new CreditCheckConfig()
                        {
                            Name = ruleName,
                            Type = ruleType,
                            Active = rule.SelectSingleNode(rulePath) != null && rule.SelectSingleNode(rulePath).InnerText.Trim() != "",
                            AcceptOtherSource = true,
                            SetupValue = rule.SelectSingleNode(rulePath) != null ? rule.SelectSingleNode(rulePath).InnerText.Trim() : "",
                            XmlElement = reportEle,
                            DoCheck = delegate (string domain, decimal credit, string entry, string required, string requiredBy, string subjectName, string subjectLevel)
                            {
                                return required == "必修" && requiredBy == "部訂";
                            }
                        });
                    }
                    #endregion

                    //選修學分數
                    #region 選修學分數
                    {
                        string ruleName = "選修學分數";
                        string ruleType = "取得學分數統計";
                        string rulePath = "畢業學分數/選修學分數";

                        XmlElement reportEle = docCreditReport.CreateElement("畢業規則");
                        evalReport.AppendChild(reportEle);
                        reportEle.SetAttribute("規則", ruleName);
                        reportEle.SetAttribute("類型", ruleType);
                        reportEle.SetAttribute("啟用", "否");
                        reportEle.SetAttribute("設定值", "");
                        reportEle.SetAttribute("課規總學分數", "0");
                        reportEle.SetAttribute("通過標準", "0");
                        reportEle.SetAttribute("累計學分", "0");

                        creditCheckConfigList.Add(new CreditCheckConfig()
                        {
                            Name = ruleName,
                            Type = ruleType,
                            Active = rule.SelectSingleNode(rulePath) != null && rule.SelectSingleNode(rulePath).InnerText.Trim() != "",
                            AcceptOtherSource = true,
                            SetupValue = rule.SelectSingleNode(rulePath) != null ? rule.SelectSingleNode(rulePath).InnerText.Trim() : "",
                            XmlElement = reportEle,
                            DoCheck = delegate (string domain, decimal credit, string entry, string required, string requiredBy, string subjectName, string subjectLevel)
                            {
                                return required == "選修";
                            }
                        });
                    }
                    #endregion

                    //專業及實習總學分數
                    #region 專業及實習總學分數
                    {
                        string ruleName = "專業及實習總學分數";
                        string ruleType = "取得學分數統計";
                        string rulePath = "畢業學分數/專業及實習總學分數";

                        XmlElement reportEle = docCreditReport.CreateElement("畢業規則");
                        evalReport.AppendChild(reportEle);
                        reportEle.SetAttribute("規則", ruleName);
                        reportEle.SetAttribute("類型", ruleType);
                        reportEle.SetAttribute("啟用", "否");
                        reportEle.SetAttribute("設定值", "");
                        reportEle.SetAttribute("課規總學分數", "0");
                        reportEle.SetAttribute("通過標準", "0");
                        reportEle.SetAttribute("累計學分", "0");

                        creditCheckConfigList.Add(new CreditCheckConfig()
                        {
                            Name = ruleName,
                            Type = ruleType,
                            Active = rule.SelectSingleNode(rulePath) != null && rule.SelectSingleNode(rulePath).InnerText.Trim() != "",
                            AcceptOtherSource = true,
                            SetupValue = rule.SelectSingleNode(rulePath) != null ? rule.SelectSingleNode(rulePath).InnerText.Trim() : "",
                            XmlElement = reportEle,
                            DoCheck = delegate (string domain, decimal credit, string entry, string required, string requiredBy, string subjectName, string subjectLevel)
                            {
                                return required == "選修";
                            }
                        });
                    }
                    #endregion

                    //實習學分數
                    #region 實習學分數
                    {
                        string ruleName = "實習學分數";
                        string ruleType = "取得學分數統計";
                        string rulePath = "畢業學分數/實習學分數";

                        XmlElement reportEle = docCreditReport.CreateElement("畢業規則");
                        evalReport.AppendChild(reportEle);
                        reportEle.SetAttribute("規則", ruleName);
                        reportEle.SetAttribute("類型", ruleType);
                        reportEle.SetAttribute("啟用", "否");
                        reportEle.SetAttribute("設定值", "");
                        reportEle.SetAttribute("課規總學分數", "0");
                        reportEle.SetAttribute("通過標準", "0");
                        reportEle.SetAttribute("累計學分", "0");

                        creditCheckConfigList.Add(new CreditCheckConfig()
                        {
                            Name = ruleName,
                            Type = ruleType,
                            Active = rule.SelectSingleNode(rulePath) != null && rule.SelectSingleNode(rulePath).InnerText.Trim() != "",
                            AcceptOtherSource = true,
                            SetupValue = rule.SelectSingleNode(rulePath) != null ? rule.SelectSingleNode(rulePath).InnerText.Trim() : "",
                            XmlElement = reportEle,
                            DoCheck = delegate (string domain, decimal credit, string entry, string required, string requiredBy, string subjectName, string subjectLevel)
                            {
                                return required == "選修";
                            }
                        });
                    }
                    #endregion

                    //判斷核心科目表
                    #region 判斷核心科目表
                    {
                        List<string> checkedList = new List<string>();
                        #region 整理要選用的科目表
                        foreach (XmlNode st in rule.SelectNodes("核心科目表/科目表"))
                        {
                            checkedList.Add(st.InnerText);
                        }
                        #endregion
                        foreach (SubjectTableItem subjectTable in SubjectTable.Items["核心科目表"])
                        {
                            #region 是要選用的科目表就進行判斷
                            if (checkedList.Contains(subjectTable.Name) && subjectTable.Content.SelectSingleNode("SubjectTableContent") != null)
                            {
                                XmlElement contentElement = (XmlElement)subjectTable.Content.SelectSingleNode("SubjectTableContent");

                                decimal passLimit; //必須取得的學分數
                                decimal attendLimit; //必須修習的學分數
                                decimal.TryParse(contentElement.GetAttribute("CreditCount"), out passLimit);
                                decimal.TryParse(contentElement.GetAttribute("AttendCount"), out attendLimit);


                                CreditCheckConfig passConfig, attendConfig;
                                // 應修學分數
                                {
                                    string ruleName = subjectTable.Name + "應修學分數";
                                    string ruleType = "修課學分數統計";

                                    XmlElement reportEle = docCreditReport.CreateElement("畢業規則");
                                    evalReport.AppendChild(reportEle);
                                    reportEle.SetAttribute("規則", ruleName);
                                    reportEle.SetAttribute("類型", ruleType);
                                    reportEle.SetAttribute("啟用", "否");
                                    reportEle.SetAttribute("設定值", "");
                                    reportEle.SetAttribute("課規總學分數", "0");
                                    reportEle.SetAttribute("通過標準", "0");
                                    reportEle.SetAttribute("累計學分", "0");
                                    attendConfig = new CreditCheckConfig()
                                    {
                                        Name = ruleName,
                                        Type = ruleType,
                                        Active = attendLimit > 0,
                                        AcceptOtherSource = false,
                                        SetupValue = "" + attendLimit,
                                        XmlElement = reportEle,
                                        DoCheck = delegate (string domain, decimal credit, string entry, string required, string requiredBy, string subjectName, string subjectLevel)
                                        {
                                            return reportEle.SelectSingleNode("科目[@科目名稱=\"" + subjectName + "\" and @科目級別=\"" + subjectLevel + "\"]") != null;
                                        }
                                    };
                                    creditCheckConfigList.Add(attendConfig);
                                }
                                // 取得學分數
                                {
                                    string ruleName = subjectTable.Name + "取得學分數";
                                    string ruleType = "取得學分數統計";

                                    XmlElement reportEle = docCreditReport.CreateElement("畢業規則");
                                    evalReport.AppendChild(reportEle);
                                    reportEle.SetAttribute("規則", ruleName);
                                    reportEle.SetAttribute("類型", ruleType);
                                    reportEle.SetAttribute("啟用", "否");
                                    reportEle.SetAttribute("設定值", "");
                                    reportEle.SetAttribute("課規總學分數", "0");
                                    reportEle.SetAttribute("通過標準", "0");
                                    reportEle.SetAttribute("累計學分", "0");

                                    passConfig = new CreditCheckConfig()
                                    {
                                        Name = ruleName,
                                        Type = ruleType,
                                        Active = attendLimit > 0,
                                        AcceptOtherSource = false,
                                        SetupValue = "" + attendLimit,
                                        XmlElement = reportEle,
                                        DoCheck = delegate (string domain, decimal credit, string entry, string required, string requiredBy, string subjectName, string subjectLevel)
                                        {
                                            return reportEle.SelectSingleNode("科目[@科目名稱=\"" + subjectName + "\" and @科目級別=\"" + subjectLevel + "\"]") != null;
                                        }
                                    };
                                    creditCheckConfigList.Add(passConfig);
                                }

                                #region 整理在科目表中所有的科目級別
                                foreach (XmlNode snode in contentElement.SelectNodes("Subject"))
                                {
                                    string name = ((XmlElement)snode).GetAttribute("Name");
                                    if (snode.SelectNodes("Level").Count == 0)
                                    {
                                        foreach (CreditCheckConfig check in new List<CreditCheckConfig>() { passConfig, attendConfig })
                                        {
                                            XmlElement subjectElement = check.XmlElement.OwnerDocument.CreateElement("科目");
                                            check.XmlElement.AppendChild(subjectElement);
                                            subjectElement.SetAttribute("科目名稱", name.Trim());
                                            subjectElement.SetAttribute("科目級別", "");
                                            subjectElement.SetAttribute("學分數", "0");
                                            subjectElement.SetAttribute("取得學分數", "0");
                                            subjectElement.SetAttribute("修課學年度", "");
                                            subjectElement.SetAttribute("修課學期", "");
                                            subjectElement.SetAttribute("狀態", "未修習");
                                        }
                                    }
                                    else
                                    {
                                        foreach (XmlNode lnode in snode.SelectNodes("Level"))
                                        {
                                            foreach (CreditCheckConfig check in new List<CreditCheckConfig>() { passConfig, attendConfig })
                                            {
                                                XmlElement subjectElement = check.XmlElement.OwnerDocument.CreateElement("科目");
                                                check.XmlElement.AppendChild(subjectElement);
                                                subjectElement.SetAttribute("科目名稱", name.Trim());
                                                subjectElement.SetAttribute("科目級別", lnode.InnerText);
                                                subjectElement.SetAttribute("學分數", "0");
                                                subjectElement.SetAttribute("取得學分數", "0");
                                                subjectElement.SetAttribute("修課學年度", "");
                                                subjectElement.SetAttribute("修課學期", "");
                                                subjectElement.SetAttribute("狀態", "未修習");
                                            }
                                        }
                                    }
                                }
                                #endregion
                            }
                            #endregion
                        }
                    }
                    #endregion

                    // 掃描課程規劃表寫入採計科目
                    foreach (GraduationPlan.GraduationPlanSubject gplanSubject in GraduationPlan.GraduationPlan.Instance.GetStudentGraduationPlan(student.StudentID).Subjects)
                    {
                        // 略過特殊需求領域
                        if (gplanSubject.Domain.StartsWith("特殊需求領域")) continue;

                        decimal credit = 0;
                        decimal.TryParse(gplanSubject.Credit, out credit);
                        foreach (CreditCheckConfig check in creditCheckConfigList)
                        {
                            // 如果這個科目是這項規則有採計的
                            if (check.DoCheck(gplanSubject.Domain, credit, gplanSubject.Entry, gplanSubject.Required, gplanSubject.RequiredBy, gplanSubject.SubjectName, gplanSubject.Level))
                            {
                                // 將科目寫入報告
                                XmlElement subjectElement = check.XmlElement.SelectSingleNode("科目[@科目名稱=\"" + gplanSubject.SubjectName.Trim() + "\" and @科目級別=\"" + gplanSubject.Level.Trim() + "\"]") as XmlElement;
                                if (subjectElement == null)
                                {
                                    subjectElement = check.XmlElement.OwnerDocument.CreateElement("科目");
                                    check.XmlElement.AppendChild(subjectElement);
                                    subjectElement.SetAttribute("科目名稱", gplanSubject.SubjectName.Trim());
                                    subjectElement.SetAttribute("科目級別", gplanSubject.Level.Trim());
                                    subjectElement.SetAttribute("學分數", gplanSubject.Credit);
                                    subjectElement.SetAttribute("取得學分數", "0");
                                    subjectElement.SetAttribute("修課學年度", "");
                                    subjectElement.SetAttribute("修課年級", "");
                                    subjectElement.SetAttribute("修課學期", "");
                                    subjectElement.SetAttribute("狀態", "未修習");
                                }
                                else
                                    subjectElement.SetAttribute("學分數", gplanSubject.Credit);
                                // 建立開課學期
                                XmlElement semesterElement = docCreditReport.CreateElement("開課設定");
                                subjectElement.AppendChild(semesterElement);
                                semesterElement.SetAttribute("開課年級", gplanSubject.SubjectElement.GetAttribute("GradeYear"));
                                semesterElement.SetAttribute("開課學期", gplanSubject.SubjectElement.GetAttribute("Semester"));
                                semesterElement.SetAttribute("分組名稱", gplanSubject.SubjectElement.GetAttribute("分組名稱"));
                                semesterElement.SetAttribute("分組修課學分數", gplanSubject.SubjectElement.GetAttribute("分組修課學分數"));

                                // 累計課程規劃表學分數
                                if (gplanSubject.SubjectElement.GetAttribute("分組名稱") == "")
                                {
                                    check.GPlanCount += credit;
                                }
                                // 用開課年級+開課學期+分組名稱判斷此分組名稱有沒有採計過
                                else if (check.XmlElement.SelectNodes("科目/開課設定[@開課年級=\"" + gplanSubject.SubjectElement.GetAttribute("GradeYear")
                                    + "\" and @開課學期=\"" + gplanSubject.SubjectElement.GetAttribute("Semester")
                                    + "\" and @分組名稱=\"" + gplanSubject.SubjectElement.GetAttribute("分組名稱") + "\"]").Count == 1)
                                {
                                    decimal.TryParse(gplanSubject.SubjectElement.GetAttribute("分組修課學分數"), out credit);
                                    check.GPlanCount += credit;
                                }
                            }
                        }
                    }

                    // 計算學分相關標準值
                    foreach (CreditCheckConfig check in creditCheckConfigList)
                    {
                        if (check.Active)
                        {
                            if (check.SetupValue.Contains("%"))
                            {
                                // 用百分比計算
                                check.PassLimit = check.GPlanCount * decimal.Parse(check.SetupValue.Replace("%", "")) / 100m;
                            }
                            else
                            {
                                // 指定標準
                                check.PassLimit = decimal.Parse(check.SetupValue);
                            }
                        }
                    }

                    //同一科目級別不重複採計
                    #region 同一科目級別不重複採計
                    if (rule.SelectSingleNode("畢業學分數/同一科目級別不重複採計") != null)
                    {
                        if (rule.SelectSingleNode("畢業學分數/同一科目級別不重複採計").InnerText.Trim() == "FALSE")
                            filterSameSubject = false;
                    }
                    #endregion
                    #region 功過相抵未滿三大過
                    //<德行成績畢業判斷規則 功過相抵未滿三大過="true">每學年德行成績均及格</德行成績畢業判斷規則>
                    if (rule.SelectSingleNode("德行成績畢業判斷規則") != null)
                    {
                        XmlElement Element = rule.SelectSingleNode("德行成績畢業判斷規則") as XmlElement;

                        if (Element.GetAttribute("功過相抵未滿三大過").ToUpper().Equals("TRUE"))
                        {
                            crule.IsDemeritNotExceedMaximum = true;



                        }
                    }
                    #endregion
                    #region 學年學業成績及格
                    //<畢業成績計算規則 學年學業成績及格="true">學期分項成績平均</畢業成績計算規則>
                    if (rule.SelectSingleNode("畢業成績計算規則") != null)
                    {
                        XmlElement Element = rule.SelectSingleNode("畢業成績計算規則") as XmlElement;

                        if (Element.GetAttribute("學年學業成績及格").ToUpper().Equals("TRUE"))
                        {
                            crule.IsEverySchoolYearEntryStudiesPass = true;
                            #region 及格標準
                            foreach (XmlElement element in helper.GetElements("及格標準/學生類別"))
                            {
                                string cat = element.GetAttribute("類別");
                                bool useful = false;
                                //掃描學生的類別作比對
                                foreach (CategoryInfo catinfo in student.StudentCategorys)
                                {
                                    if (catinfo.Name == cat || catinfo.FullName == cat)
                                        useful = true;
                                }
                                //學生是指定的類別或類別為"預設"
                                if (cat == "預設" || useful)
                                {
                                    decimal tryParseDecimal;
                                    for (int gyear = 1; gyear <= 4; gyear++)
                                    {
                                        switch (gyear)
                                        {
                                            case 1:
                                                if (decimal.TryParse(element.GetAttribute("一年級及格標準"), out tryParseDecimal))
                                                {
                                                    if (!applyLimit.ContainsKey(gyear))
                                                        applyLimit.Add(gyear, tryParseDecimal);
                                                    if (applyLimit[gyear] > tryParseDecimal)
                                                        applyLimit[gyear] = tryParseDecimal;
                                                }
                                                break;
                                            case 2:
                                                if (decimal.TryParse(element.GetAttribute("二年級及格標準"), out tryParseDecimal))
                                                {
                                                    if (!applyLimit.ContainsKey(gyear))
                                                        applyLimit.Add(gyear, tryParseDecimal);
                                                    if (applyLimit[gyear] > tryParseDecimal)
                                                        applyLimit[gyear] = tryParseDecimal;
                                                }
                                                break;
                                            case 3:
                                                if (decimal.TryParse(element.GetAttribute("三年級及格標準"), out tryParseDecimal))
                                                {
                                                    if (!applyLimit.ContainsKey(gyear))
                                                        applyLimit.Add(gyear, tryParseDecimal);
                                                    if (applyLimit[gyear] > tryParseDecimal)
                                                        applyLimit[gyear] = tryParseDecimal;
                                                }
                                                break;
                                            case 4:
                                                if (decimal.TryParse(element.GetAttribute("四年級及格標準"), out tryParseDecimal))
                                                {
                                                    if (!applyLimit.ContainsKey(gyear))
                                                        applyLimit.Add(gyear, tryParseDecimal);
                                                    if (applyLimit[gyear] > tryParseDecimal)
                                                        applyLimit[gyear] = tryParseDecimal;
                                                }
                                                break;
                                            default:
                                                break;
                                        }
                                    }
                                }
                            }
                            #endregion
                        }
                    }
                    #endregion
                }


                if (isDataReasonable)
                {
                    //判斷畢業學分
                    #region 判斷畢業學分

                    #region 整理學期科目成績
                    //按學年度學期排好。
                    List<SemesterSubjectScoreInfo> subjectsByStudent = student.SemesterSubjectScoreList;
                    subjectsByStudent.Sort((x, y) =>
                    {
                        SemesterData xx = new SemesterData(0, x.SchoolYear, x.Semester);
                        SemesterData yy = new SemesterData(0, y.SchoolYear, y.Semester);
                        return xx.Order.CompareTo(yy.Order);
                    });
                    #endregion

                    int maxGradeYear = 0, maxSemester = 0;
                    foreach (SemesterSubjectScoreInfo subjectScore in subjectsByStudent)
                    {
                        #region 依據設定設定畢業採計各項屬性
                        if (useGPlan)
                        {
                            #region 從課程規劃表取得這個科目的相關屬性
                            GraduationPlan.GraduationPlanSubject gPlanSubject = GraduationPlan.GraduationPlan.Instance.GetStudentGraduationPlan(student.StudentID).GetSubjectInfo(subjectScore.Subject, subjectScore.Level);
                            decimal credit = 0;
                            decimal.TryParse(gPlanSubject.Credit, out credit);

                            subjectScore.Detail.SetAttribute("畢業採計-領域", gPlanSubject.Domain);
                            subjectScore.Detail.SetAttribute("畢業採計-學分數", "" + credit);
                            subjectScore.Detail.SetAttribute("畢業採計-分項類別", gPlanSubject.Entry);
                            subjectScore.Detail.SetAttribute("畢業採計-必選修", gPlanSubject.Required);
                            subjectScore.Detail.SetAttribute("畢業採計-校部訂", gPlanSubject.RequiredBy);
                            subjectScore.Detail.SetAttribute("畢業採計-不計學分", gPlanSubject.NotIncludedInCredit ? "是" : "否");
                            #endregion
                        }
                        else
                        {
                            #region 直接使用科目成績上的屬性
                            subjectScore.Detail.SetAttribute("畢業採計-領域", subjectScore.Detail.GetAttribute("Domain"));
                            subjectScore.Detail.SetAttribute("畢業採計-學分數", "" + subjectScore.CreditDec());
                            subjectScore.Detail.SetAttribute("畢業採計-分項類別", subjectScore.Detail.GetAttribute("開課分項類別"));
                            subjectScore.Detail.SetAttribute("畢業採計-必選修", subjectScore.Require ? "必修" : "選修");
                            subjectScore.Detail.SetAttribute("畢業採計-校部訂", subjectScore.Detail.GetAttribute("修課校部訂"));
                            subjectScore.Detail.SetAttribute("畢業採計-不計學分", subjectScore.Detail.GetAttribute("不計學分"));
                            #endregion
                        }
                        #endregion

                        // 略過特殊需求領域
                        if (subjectScore.Detail.GetAttribute("畢業採計-領域").StartsWith("特殊需求領域")) continue;

                        //不計學分不用算
                        if (subjectScore.Detail.GetAttribute("畢業採計-不計學分") == "是")
                        {
                            subjectScore.Detail.SetAttribute("畢業採計-學分數", "0");
                            subjectScore.Detail.SetAttribute("畢業採計-說明", "不計學分");
                            continue;
                        }

                        #region 統計累計學分
                        {
                            foreach (CreditCheckConfig check in creditCheckConfigList)
                            {
                                SubjectSet attendSubjects = new SubjectSet();
                                SubjectSet passedSubjects = new SubjectSet();
                                decimal credit = 0;
                                decimal.TryParse(subjectScore.Detail.GetAttribute("畢業採計-學分數"), out credit);
                                if (check.DoCheck(
                                    subjectScore.Detail.GetAttribute("畢業採計-領域"),
                                    credit,
                                    subjectScore.Detail.GetAttribute("畢業採計-分項類別"),
                                    subjectScore.Detail.GetAttribute("畢業採計-必選修"),
                                    subjectScore.Detail.GetAttribute("畢業採計-校部訂"),
                                    subjectScore.Subject.Trim(),
                                    subjectScore.Level.Trim()
                                    ))
                                {

                                    switch (check.Type)
                                    {
                                        case "修課學分數統計":
                                            {
                                                bool isCount = true;
                                                if (filterSameSubject)
                                                {
                                                    SubjectName sn = new SubjectName(subjectScore.Subject.Trim(), subjectScore.Level.Trim());
                                                    if (attendSubjects.Contains(sn))
                                                    {
                                                        isCount = false;
                                                    }
                                                    else
                                                        attendSubjects.Add(sn);
                                                }
                                                if (isCount)
                                                {
                                                    XmlElement subjectElement = check.XmlElement.SelectSingleNode("科目[@科目名稱=\"" + subjectScore.Subject.Trim() + "\" and @科目級別=\"" + subjectScore.Level.Trim() + "\"]") as XmlElement;
                                                    // 非課程規劃表課程 且 允許採計時採計
                                                    if (subjectElement == null && check.AcceptOtherSource)
                                                    {
                                                        subjectElement = check.XmlElement.OwnerDocument.CreateElement("科目");
                                                        check.XmlElement.AppendChild(subjectElement);
                                                        subjectElement.SetAttribute("科目名稱", subjectScore.Subject.Trim());
                                                        subjectElement.SetAttribute("科目級別", subjectScore.Level.Trim());
                                                        subjectElement.SetAttribute("學分數", subjectScore.Detail.GetAttribute("畢業採計-學分數"));
                                                        subjectElement.SetAttribute("取得學分數", "0");
                                                        subjectElement.SetAttribute("修課學年度", "");
                                                        subjectElement.SetAttribute("修課年級", "");
                                                        subjectElement.SetAttribute("修課學期", "");
                                                        subjectElement.SetAttribute("狀態", "未修習");
                                                        subjectElement.SetAttribute("非課程規劃表課程", "非課程規劃表課程");
                                                    }

                                                    if (subjectElement != null)
                                                    {
                                                        subjectElement.SetAttribute("修課學年度", "" + subjectScore.SchoolYear);
                                                        subjectElement.SetAttribute("修課年級", "" + subjectScore.GradeYear);
                                                        subjectElement.SetAttribute("修課學期", "" + subjectScore.Semester);
                                                        if (subjectScore.Detail.GetAttribute("是否補修成績") == "是" && subjectScore.Detail.GetAttribute("補修學年度") == "" && subjectScore.Detail.GetAttribute("補修學期") == "")
                                                        {
                                                            subjectElement.SetAttribute("狀態", "尚未補修");
                                                            check.MakeupCount += credit;
                                                        }
                                                        else
                                                        {
                                                            subjectElement.SetAttribute("狀態", "已修");
                                                            subjectElement.SetAttribute("取得學分數", "" + (decimal.Parse(subjectElement.GetAttribute("取得學分數")) + decimal.Parse(subjectScore.Detail.GetAttribute("畢業採計-學分數"))));
                                                            check.AttendCount += credit;
                                                            check.PassCount += credit;
                                                        }
                                                    }
                                                }
                                            }
                                            break;
                                        case "取得學分數統計":
                                            {
                                                XmlElement subjectElement = check.XmlElement.SelectSingleNode("科目[@科目名稱=\"" + subjectScore.Subject.Trim() + "\" and @科目級別=\"" + subjectScore.Level.Trim() + "\"]") as XmlElement;
                                                // 非課程規劃表課程 且 允許採計時採計
                                                if (subjectElement == null && check.AcceptOtherSource)
                                                {
                                                    subjectElement = check.XmlElement.OwnerDocument.CreateElement("科目");
                                                    check.XmlElement.AppendChild(subjectElement);
                                                    subjectElement.SetAttribute("科目名稱", subjectScore.Subject.Trim());
                                                    subjectElement.SetAttribute("科目級別", subjectScore.Level.Trim());
                                                    subjectElement.SetAttribute("學分數", subjectScore.Detail.GetAttribute("畢業採計-學分數"));
                                                    subjectElement.SetAttribute("取得學分數", "0");
                                                    subjectElement.SetAttribute("修課學年度", "");
                                                    subjectElement.SetAttribute("修課年級", "");
                                                    subjectElement.SetAttribute("修課學期", "");
                                                    subjectElement.SetAttribute("狀態", "未修習");
                                                    subjectElement.SetAttribute("非課程規劃表課程", "非課程規劃表課程");
                                                }
                                                if (subjectElement != null)
                                                {
                                                    if (subjectScore.Pass)
                                                    {
                                                        bool isCount = true;
                                                        if (filterSameSubject)
                                                        {
                                                            SubjectName sn = new SubjectName(subjectScore.Subject.Trim(), subjectScore.Level.Trim());
                                                            if (passedSubjects.Contains(sn))
                                                            {
                                                                subjectScore.Detail.SetAttribute("畢業採計-學分數", "0");
                                                                subjectScore.Detail.SetAttribute("畢業採計-說明", "同科目級別重複取得學分數");
                                                                isCount = false;
                                                            }
                                                            else
                                                                passedSubjects.Add(sn);
                                                        }
                                                        if (isCount)
                                                        {
                                                            subjectElement.SetAttribute("修課學年度", "" + subjectScore.SchoolYear);
                                                            subjectElement.SetAttribute("修課年級", "" + subjectScore.GradeYear);
                                                            subjectElement.SetAttribute("修課學期", "" + subjectScore.Semester);
                                                            subjectElement.SetAttribute("狀態", "已取得");
                                                            subjectElement.SetAttribute("取得學分數", "" + (decimal.Parse(subjectElement.GetAttribute("取得學分數")) + credit));
                                                            check.AttendCount += credit;
                                                            check.PassCount += credit;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        if (subjectElement.GetAttribute("狀態") != "已取得")// 理論上都是不等於啦
                                                        {
                                                            subjectElement.SetAttribute("修課學年度", "" + subjectScore.SchoolYear);
                                                            subjectElement.SetAttribute("修課年級", "" + subjectScore.GradeYear);
                                                            subjectElement.SetAttribute("修課學期", "" + subjectScore.Semester);
                                                            if (subjectScore.Detail.GetAttribute("是否補修成績") == "是" && subjectScore.Detail.GetAttribute("補修學年度") == "" && subjectScore.Detail.GetAttribute("補修學期") == "")
                                                            {
                                                                subjectElement.SetAttribute("狀態", "尚未補修");
                                                                check.MakeupCount += credit;
                                                            }
                                                            else
                                                            {
                                                                subjectElement.SetAttribute("狀態", "可重修");
                                                                check.AttendCount += credit;
                                                                check.RetakeCount += credit;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                            break;
                                    }
                                }
                            }
                        }
                        #endregion

                        #region 計算有成績的最大年級
                        if (subjectScore.GradeYear > maxGradeYear)
                        {
                            maxGradeYear = subjectScore.GradeYear;
                            maxSemester = subjectScore.Semester;
                        }
                        else if (subjectScore.GradeYear == maxGradeYear && subjectScore.Semester > maxSemester)
                        {
                            maxSemester = subjectScore.Semester;
                        }
                        #endregion
                        // 畢業採計-說明 增列科目屬性來源
                        if (useGPlan)
                            subjectScore.Detail.SetAttribute("畢業採計-說明", (subjectScore.Detail.GetAttribute("畢業採計-說明") == "" ? "" : (subjectScore.Detail.GetAttribute("畢業採計-說明") + "\n")) + "以課程規劃表(" + GraduationPlan.GraduationPlan.Instance.GetStudentGraduationPlan(student.StudentID).Name + ")為主");
                        else
                            subjectScore.Detail.SetAttribute("畢業採計-說明", (subjectScore.Detail.GetAttribute("畢業採計-說明") == "" ? "" : (subjectScore.Detail.GetAttribute("畢業採計-說明") + "\n")) + "直接使用成績內容");
                    }

                    #region 結算各項畢業學分數檢查報告
                    foreach (CreditCheckConfig check in creditCheckConfigList)
                    {
                        // 標註尚未開課
                        List<string> checkList = new List<string>();
                        foreach (XmlElement subjectElement in check.XmlElement.SelectNodes("科目"))
                        {
                            if (subjectElement.GetAttribute("狀態") == "未修習")
                            {
                                bool comeAfter = false;
                                foreach (XmlElement semesterElemest in subjectElement.SelectNodes("開課設定"))
                                {
                                    int tryParseGradeYear = 0, tryParseSemester = 0;
                                    int.TryParse(semesterElemest.GetAttribute("開課年級"), out tryParseGradeYear);
                                    int.TryParse(semesterElemest.GetAttribute("開課學期"), out tryParseSemester);
                                    if (
                                        tryParseGradeYear > maxGradeYear
                                        || (tryParseGradeYear == maxGradeYear && tryParseSemester > maxSemester)
                                        )
                                    {
                                        comeAfter = true;
                                        if (semesterElemest.GetAttribute("分組名稱") == "")
                                            check.ComeAfterCount += decimal.Parse(subjectElement.GetAttribute("學分數"));
                                        else
                                        {
                                            string checkKey = "" + semesterElemest.GetAttribute("分組名稱") + "_" + (tryParseGradeYear * 2 + tryParseSemester);
                                            if (!checkList.Contains(checkKey))
                                            {
                                                checkList.Add(checkKey);
                                                check.ComeAfterCount += decimal.Parse(semesterElemest.GetAttribute("分組修課學分數"));
                                            }
                                        }
                                    }
                                }
                                if (comeAfter)
                                    subjectElement.SetAttribute("狀態", "尚未開課");
                            }
                        }
                        // 標註各項統計值
                        check.XmlElement.SetAttribute("設定值", "" + check.SetupValue);
                        check.XmlElement.SetAttribute("課規總學分數", "" + check.GPlanCount);
                        check.XmlElement.SetAttribute("累計學分", "" + check.PassCount);

                        XmlElement summaryElement = check.XmlElement.OwnerDocument.CreateElement("預警統計");
                        check.XmlElement.PrependChild(summaryElement);
                        summaryElement.SetAttribute("畢業差額", "" + (check.PassCount + check.ComeAfterCount - check.PassLimit));
                        summaryElement.SetAttribute("已修習", "" + check.AttendCount);
                        summaryElement.SetAttribute("尚未開課", "" + check.ComeAfterCount);
                        summaryElement.SetAttribute("可重修", "" + check.RetakeCount);
                        summaryElement.SetAttribute("可補修", "" + check.MakeupCount);


                        if (check.Active)
                        {
                            check.XmlElement.SetAttribute("啟用", "是");
                            check.XmlElement.SetAttribute("通過標準", "" + check.PassLimit);
                            // 畢業審查不通過
                            if (check.PassLimit > check.PassCount)
                            {
                                XmlElement unPasselement = docGradCheck.CreateElement("UnPassReson");
                                unPasselement.InnerText = "未達" + check.Name + "取得學分標準";
                                evalResult.AppendChild(unPasselement);
                            }
                        }
                    }
                    #endregion
                    #endregion
                    //判斷學年學業分項成績及格
                    #region 判斷學年學業分項成績及格
                    if (crule.IsEverySchoolYearEntryStudiesPass)
                    {
                        Dictionary<int, bool?> passGrades = new Dictionary<int, bool?>();
                        passGrades.Add(1, null);
                        passGrades.Add(2, null);
                        passGrades.Add(3, null);
                        foreach (var item in student.SchoolYearEntryScoreList)
                        {
                            if (item.Entry != "學業") continue;
                            decimal applylimit = 60;
                            if (applyLimit.ContainsKey(item.GradeYear))
                                applylimit = applyLimit[item.GradeYear];
                            if (!passGrades.ContainsKey(item.GradeYear))
                                passGrades.Add(item.GradeYear, item.Score >= applylimit);
                            else
                                passGrades[item.GradeYear] = item.Score >= applylimit;
                        }
                        foreach (var gradeYear in passGrades.Keys)
                        {
                            if (passGrades[gradeYear] == null)
                            {
                                XmlElement unPasselement = docGradCheck.CreateElement("UnPassReson");
                                unPasselement.InnerText = "缺少" + gradeYear + "年級學年學業分項成績";
                                evalResult.AppendChild(unPasselement);
                            }
                            else if (!passGrades[gradeYear].Value)
                            {
                                XmlElement unPasselement = docGradCheck.CreateElement("UnPassReson");
                                unPasselement.InnerText = "" + gradeYear + "年級學年學業分項成績不及格";
                                evalResult.AppendChild(unPasselement);
                            }
                        }
                    }
                    #endregion
                    //判斷功過相抵未滿三大過
                    #region 判斷功過相抵未滿三大過
                    if (crule.IsDemeritNotExceedMaximum)
                    {
                        //判斷使用自訂功過換算表或系統預設
                        if (mreducerecord != null)
                        {
                            //將大過數轉為警告數
                            int MaxDemeritC = mreducerecord.DemeritAToDemeritB.HasValue && mreducerecord.DemeritBToDemeritC.HasValue ? mreducerecord.DemeritAToDemeritB.Value * mreducerecord.DemeritBToDemeritC.Value * 3 : 27; //3個大過

                            //學生功過相減統計，以最小單位警告來做計算
                            int StudentDemeritCount = 0;

                            if (DisciplineDic.ContainsKey(student.StudentID))
                            {
                                #region 針對每位學生獎懲資料進行判斷
                                foreach (K12.Data.DisciplineRecord record in DisciplineDic[student.StudentID])
                                {
                                    if (record.MeritFlag == "2") //留察資料予以跳過
                                        continue;

                                    if (record.MeritFlag == "0")
                                        if (record.Cleared == "是")
                                            continue;


                                    //是否進行功過相抵，若是的話才加總獎勵

                                    //(獎勵部份)
                                    //大功
                                    if (mreducerecord.MeritAToMeritB.HasValue && mreducerecord.DemeritBToDemeritC.HasValue && record.MeritA.HasValue)
                                        StudentDemeritCount += record.MeritA.Value * mreducerecord.MeritAToMeritB.Value * mreducerecord.MeritBToMeritC.Value;

                                    //小功
                                    if (mreducerecord.MeritAToMeritB.HasValue && record.MeritB.HasValue)
                                        StudentDemeritCount += record.MeritB.Value * mreducerecord.MeritBToMeritC.Value;

                                    //嘉獎
                                    if (record.MeritC.HasValue)
                                        StudentDemeritCount += record.MeritC.Value;

                                    //(懲戒部份)
                                    if (mreducerecord.DemeritAToDemeritB.HasValue && mreducerecord.DemeritBToDemeritC.HasValue && record.DemeritA.HasValue)
                                        StudentDemeritCount -= record.DemeritA.Value * mreducerecord.DemeritAToDemeritB.Value * mreducerecord.DemeritBToDemeritC.Value;

                                    if (mreducerecord.DemeritBToDemeritC.HasValue && record.DemeritB.HasValue)
                                        StudentDemeritCount -= record.DemeritB.Value * mreducerecord.DemeritBToDemeritC.Value;

                                    if (record.DemeritC.HasValue)
                                        StudentDemeritCount -= record.DemeritC.Value;

                                }
                                #endregion
                            }
                            //如果滿三大過
                            if (StudentDemeritCount <= -MaxDemeritC)
                            {
                                XmlElement unPasselement = docGradCheck.CreateElement("UnPassReson");
                                unPasselement.InnerText = "功過相抵滿三大過";
                                evalResult.AppendChild(unPasselement);
                            }
                        }
                        else
                        {
                            XmlElement unPasselement = docGradCheck.CreateElement("UnPassReson");
                            unPasselement.InnerText = "沒有設定功過換算表";
                            evalResult.AppendChild(unPasselement);
                        }
                    }
                    #endregion
                }
                else
                {
                    string reson = "";
                    foreach (var item in _ErrorList[student])
                    {
                        reson += (reson == "" ? "" : "\n") + item;
                    }
                    foreach (var item in student.SemesterSubjectScoreList)
                    {
                        item.Detail.SetAttribute("畢業採計-說明", reson);
                    }
                }
                //將畢業判斷結果加到學生身上
                if (student.Fields.ContainsKey("GradCheck"))
                    student.Fields.Remove("GradCheck");
                student.Fields.Add("GradCheck", evalResult);

                //將畢業判斷報告加到學生身上
                if (student.Fields.ContainsKey("GrandCheckReport"))
                    student.Fields.Remove("GrandCheckReport");
                student.Fields.Add("GrandCheckReport", evalReport);

            }
            #endregion
            return _ErrorList;
        }
    }

    class CreditCheckConfig
    {
        // 名稱
        public string Name { get; set; }
        // 類型
        public string Type { get; set; }
        // 啟用
        public bool Active { get; set; }
        // 接受課規以外的成績
        public bool AcceptOtherSource { get; set; }
        // 設定值
        public string SetupValue { get; set; }
        // 報告Element
        public XmlElement XmlElement { get; set; }
        // 課規總學分數
        public decimal GPlanCount { get; set; }
        // 通過標準
        public decimal PassLimit { get; set; }
        // 已修習學分數
        public decimal AttendCount { get; set; }
        // 已通過學分數
        public decimal PassCount { get; set; }
        // 可補修學分數
        public decimal MakeupCount { get; set; }
        // 可重修學分數
        public decimal RetakeCount { get; set; }
        // 尚未開課學分數
        public decimal ComeAfterCount { get; set; }
        // 核心判斷邏輯
        public System.Func<string, decimal, string, string, string, string, string, bool> DoCheck { get; set; }
        public CreditCheckConfig()
        {
            //this.DoCheck = delegate (string domain, decimal credit, string entry, string required, string requiredBy, subjectName, subjectLevel)
            //{
            //    return false;
            //};
        }
    }
}