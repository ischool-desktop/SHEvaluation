using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using FISCA.DSAUtil;
using System.Xml;
using Aspose.Cells;
using SmartSchool;
using System.IO;
using System.Diagnostics;
using SmartSchool.Evaluation.Reports;
using FISCA.Presentation;

namespace SmartSchool.Evaluation.Process
{
    public partial class Resit : SmartSchool.Evaluation.Process.RibbonBarBase
    {
        public Resit()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Resit));

            var buttonItem89 = MotherForm.RibbonBarItems["�аȧ@�~", "�妸�@�~/�˵�"]["���Z�@�~"];
            //buttonItem89.Image = ( (System.Drawing.Image)( resources.GetObject("buttonItem89.Image") ) );
            buttonItem89["�ɦҦW��-�̬��"].BeginGroup = true;
            buttonItem89["�ɦҦW��-�̬��"].Click += new System.EventHandler(this.buttonItem2_Click);
            buttonItem89["�ɦҦW��-�̾ǥ�"].Click += new System.EventHandler(this.buttonItem1_Click);
            buttonItem89["�ɦҦ��Z�פJ��"].Click += new System.EventHandler(this.buttonItem3_Click);
            buttonItem89["���F�ɦҼзǦW��"].Click += new System.EventHandler(this.buttonItem4_Click);

            buttonItem89.Enable = CurrentUser.Acl["Button0690"].Executable;
        }

        

        public override string ProcessTabName
        {
            get
            {
                return "���Z�B�z";
            }
        }

        private void buttonItem1_Click(object sender, EventArgs e)
        {
            new ResitListByStudent ();
        }

        private void buttonItem2_Click(object sender, EventArgs e)
        {
            new ResitListBySubject();
        }

        private void buttonItem3_Click(object sender, EventArgs e)
        {
            new ResitScoreImport();
        }

        private void buttonItem4_Click(object sender, EventArgs e)
        {
            new NotReachStandardList();
        }
    }
}

