using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Linq;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraBars.Docking2010.Views;
using DevExpress.XtraBars;
using DevExpress.XtraBars.Navigation;
using System.Threading;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml;
using System.Globalization;
using System.IO;

namespace XD_DBDW_Server
{
    public partial class Form1 : DevExpress.XtraBars.Ribbon.RibbonForm
    {
        XtraUserControl FrequencySpectrumUserControl;
        XtraUserControl IQWaveformUserControl;
        XtraUserControl WaterfallPlotUserControl;

        private XmlProcessing m_XmlProcessing = new XmlProcessing();
        private WindowApp WindowApp = new WindowApp();
        private Transform Transform = new Transform();
        private UI_FrequencySpectrum m_UI_FrequencySpectrum;
        private UI_IQWaveform m_UI_IQWaveform;
        private UI_WaterfallPlot m_UI_WaterfallPlot;
        private DataProcessing m_DataProcessing;
        private FileProcessing m_FileProcessing;
        private string m_NetLocalIP;
        private int m_NetLocalPort;
        public string m_NetGroupIP;
        public int band;
        public string[] UdpMulticastGroup = { "239.1.1.200", "239.1.1.201", "239.1.1.202", "239.1.1.203", "239.1.1.204" };

        #region 初始化
        public Form1()
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false;
            WriteXml();
            DataAlgorithm.Filter.LoadFilterPara();

            #region 网络初始化
            try
            {
                m_DataProcessing = new DataProcessing();//初始化数据处理类
                m_FileProcessing = new FileProcessing(m_DataProcessing);
                m_FileProcessing.udpRecvInit(m_NetLocalIP, m_NetLocalPort);//进数据处理类
            }
            catch (Exception ex)
            {

            }
            #endregion

            #region 委托传递数据
            m_FileProcessing.udpRecv.passTime += new udpRecv.PassTime(udpRecv_passTime);//时标信息的委托
            m_DataProcessing.DataFrequencySpectrum.passPowerAndFreq += new DataFrequencySpectrum.PassPowerAndFreq(DataFrequencySpectrum_passPowerAndFreq);//频域数据的委托
            m_DataProcessing.DataIQWaveform.passIQData += new DataIQWaveform.PassIQData(DataIQWaveform_passIQData);//IQ数据的委托
            #endregion

            #region UI
            FrequencySpectrumUserControl = CreateUserControl("FrequencySpectrum");
            IQWaveformUserControl = CreateUserControl("IQWaveform");
            WaterfallPlotUserControl = CreateUserControl("WaterfallPlot");

            #region UI_FrequencySpectrum
            m_UI_FrequencySpectrum = new UI_FrequencySpectrum();
            this.FrequencySpectrumUserControl.Controls.Clear();
            this.FrequencySpectrumUserControl.Controls.Add(m_UI_FrequencySpectrum);
            this.m_UI_FrequencySpectrum.Dock = DockStyle.Fill;
            #endregion

            //accordionControl.SelectedElement = FrequencySpectrumAccordionControlElement;

            #endregion
        }

        UInt16 datatimeIndex = 0;

        //B码、GPS/BD时间显示
        void udpRecv_passTime(TimeInfo timeInfo, int type)
        {
            int MAX_TIME;
            switch(type)
            {
                case 1: MAX_TIME = 10; break;
                case 2: MAX_TIME = 500; break;
                case 3: MAX_TIME = 1000; break;
                default: MAX_TIME = 1000; break;
            }
            if (++datatimeIndex > MAX_TIME)
            {
                datatimeIndex = 0;
                if (this.IsHandleCreated)
                    this.BeginInvoke(new Action(() =>
                    {
                        DateTimeFormatInfo dtFormat = new System.Globalization.DateTimeFormatInfo();//时间格式化
                        string nanosecond = timeInfo.millisecond.ToString("d3") + "毫秒" + timeInfo.microsecond.ToString("d3") + "微秒";

                        switch (timeInfo.satelliteInfo.time_state)
                        {
                            //B码=0、GPS/BD=0
                            case 0:
                                barStaticItem3.Caption =
                                "B码：" + System.DateTime.Now.ToString("yyyy年MM月dd日HH时mm分ss秒", dtFormat)
                                + " | BD/GPS时间：" + System.DateTime.Now.ToString("yyyy年MM月dd日HH时mm分ss秒", dtFormat);
                                break;

                            //B码=0、GPS/BD=1
                            case 1:
                                barStaticItem3.Caption =
                                "B码：" + System.DateTime.Now.ToString("yyyy年MM月dd日HH时mm分ss秒", dtFormat) + nanosecond
                                + " | BD/GPS时间：" + timeInfo.satelliteInfo.year.ToString() + "年"
                                + timeInfo.satelliteInfo.month.ToString() + "月"
                                + timeInfo.satelliteInfo.day.ToString() + "日"
                                + timeInfo.satelliteInfo.hour.ToString() + "时"
                                + timeInfo.satelliteInfo.minute.ToString("d2") + "分"
                                + timeInfo.satelliteInfo.second.ToString("d2") + "秒"
                                + nanosecond;
                                break;
                            //B码=1、GPS/BD=0
                            case 2:
                                barStaticItem3.Caption =
                                "B码：" + timeInfo.year.ToString() + "年"
                                + timeInfo.month.ToString() + "月"
                                + timeInfo.day_offset.ToString() + "日"
                                + timeInfo.hour.ToString() + "时"
                                + timeInfo.minute.ToString("d2") + "分"
                                + timeInfo.second.ToString("d2") + "秒"
                                + nanosecond
                                + " | BD/GPS时间：" + System.DateTime.Now.ToString("yyyy年MM月dd日HH时mm分ss秒", dtFormat) + nanosecond;
                                break;

                            //B码=1、GPS/BD=1
                            case 3:
                                barStaticItem3.Caption =
                                "B码：" + timeInfo.year.ToString() + "年"
                                + timeInfo.month.ToString() + "月"
                                + timeInfo.day_offset.ToString() + "日"
                                + timeInfo.hour.ToString() + "时"
                                + timeInfo.minute.ToString("d2") + "分"
                                + timeInfo.second.ToString("d2") + "秒"
                                + nanosecond
                                + " | BD/GPS时间：" + timeInfo.satelliteInfo.year.ToString() + "年"
                                + timeInfo.satelliteInfo.month.ToString() + "月"
                                + timeInfo.satelliteInfo.day.ToString() + "日"
                                + timeInfo.satelliteInfo.hour.ToString() + "时"
                                + timeInfo.satelliteInfo.minute.ToString("d2") + "分"
                                + timeInfo.satelliteInfo.second.ToString("d2") + "秒"
                                + nanosecond;
                                break;
                            default:
                                break;
                        }
                    }));
            }
        }

        #endregion

        #region 创建XtraUserControl
        XtraUserControl CreateUserControl(string text)
        {
            XtraUserControl result = new XtraUserControl();
            result.Name = text.ToLower() + "UserControl";
            result.Text = text;
            LabelControl label = new LabelControl();
            label.Parent = result;
            label.Appearance.Font = new Font("Tahoma", 25.25F);
            label.Appearance.ForeColor = Color.Gray;
            label.Dock = System.Windows.Forms.DockStyle.Fill;
            label.AutoSizeMode = LabelAutoSizeMode.None;
            label.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            label.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            label.Text = text;
            return result;
        }
        #endregion

        #region accordionControl属性更改事件

        void accordionControl_SelectedElementChanged(object sender, SelectedElementChangedEventArgs e)
        {
            if (e.Element == null) return;
            XtraUserControl userControl = new XtraUserControl();
            switch (e.Element.Text)
            {
                case "FrequencySpectrum":
                    userControl = FrequencySpectrumUserControl;
                    break;
                case "IQWaveform":
                    userControl = IQWaveformUserControl;
                    break;
                case "WaterfallPlot":
                    userControl = WaterfallPlotUserControl;
                    break;
                default:
                    break;
            }
            tabbedView.AddDocument(userControl);
            tabbedView.ActivateDocument(userControl);
        }
        #endregion

        #region barSubItemNavigation按键效果
        void barButtonNavigation_ItemClick(object sender, ItemClickEventArgs e)
        {
            int barItemIndex = barSubItemNavigation.ItemLinks.IndexOf(e.Link);
            accordionControl.SelectedElement = mainAccordionGroup.Elements[barItemIndex];
        }

        #endregion

        #region tabbedView关闭事件
        void tabbedView_DocumentClosed(object sender, DocumentEventArgs e)
        {
            RecreateUserControls(e);
            SetAccordionSelectedElement(e);
        }
        void SetAccordionSelectedElement(DocumentEventArgs e)
        {
            if (tabbedView.Documents.Count != 0)
            {
                //if (e.Document.Caption == "FrequencySpectrum") accordionControl.SelectedElement = IQWaveformAccordionControlElement;
                //else accordionControl.SelectedElement = FrequencySpectrumAccordionControlElement;
            }
            else
            {
                accordionControl.SelectedElement = null;
            }
        }
        void RecreateUserControls(DocumentEventArgs e)
        {
            if (e.Document.Caption == "FrequencySpectrum") FrequencySpectrumUserControl = CreateUserControl("FrequencySpectrum");
            else IQWaveformUserControl = CreateUserControl("IQWaveform");
        }
        #endregion

        #region dockPane视图按钮

        private void barButtonItem9_ItemClick(object sender, ItemClickEventArgs e)
        {
            dockPanel.Visibility = DevExpress.XtraBars.Docking.DockVisibility.Visible;
        }

        private void barButtonItem10_ItemClick(object sender, ItemClickEventArgs e)
        {
            Export.Visibility = DevExpress.XtraBars.Docking.DockVisibility.Visible;
        }
        #endregion

        #region accordionControl视图按键
        private void FrequencySpectrumAccordionControlElement_Click(object sender, EventArgs e)
        {
            m_UI_FrequencySpectrum = new UI_FrequencySpectrum();
            this.FrequencySpectrumUserControl.Controls.Clear();
            this.FrequencySpectrumUserControl.Controls.Add(m_UI_FrequencySpectrum);
            this.m_UI_FrequencySpectrum.Dock = DockStyle.Fill;
        }

        private void IQWaveformAccordionControlElement_Click(object sender, EventArgs e)
        {
            m_UI_IQWaveform = new UI_IQWaveform();
            this.IQWaveformUserControl.Controls.Clear();
            this.IQWaveformUserControl.Controls.Add(m_UI_IQWaveform);
            this.m_UI_IQWaveform.Dock = DockStyle.Fill;
        }
        private void accordionControlElement1_Click(object sender, EventArgs e)
        {
            m_UI_WaterfallPlot = new UI_WaterfallPlot();
            this.WaterfallPlotUserControl.Controls.Clear();
            this.WaterfallPlotUserControl.Controls.Add(m_UI_WaterfallPlot);
            this.m_UI_WaterfallPlot.Dock = DockStyle.Fill;
        }

        #endregion

        #region 功能按键

        private void barButtonItem14_ItemClick(object sender, ItemClickEventArgs e)
        {
            WindowApp.RestartApplication();
        }

        private void barButtonItem7_ItemClick(object sender, ItemClickEventArgs e)
        {
            barHeaderItem1.Caption = string.Format("RecvPackets：{0}", m_FileProcessing.udpRecv.RevCount);
            barHeaderItem2.Caption = string.Format("LossPackets：{0}", m_FileProcessing.udpRecv.LostCount);
        }

        private void barButtonItem19_ItemClick(object sender, ItemClickEventArgs e)
        {
            m_DataProcessing.DataFrequencySpectrum.StartSave();
        }

        //private void barButtonItem2_ItemClick(object sender, ItemClickEventArgs e)
        //{
        //    m_DataProcessing.DataFunction.StartCheckTime();
        //    m_DataProcessing.DataFunction.passDeleTimeSign += new DataFunction.DeleTimeSign(DataFunction_passDeleTimeSign);
        //}

        //void DataFunction_passDeleTimeSign(DataTime datatime)
        //{
        //    memoEdit1.Text = "";
        //    memoEdit1.Text += "时标检测：" + datatime.year.ToString() + "年" + datatime.month.ToString() + "月"
        //                    + datatime.day.ToString() + "日" + datatime.hour.ToString() + "时" + datatime.minute.ToString() + "分"
        //                    + datatime.second.ToString() + "秒" + datatime.millisecond.ToString() + "毫秒" + datatime.microsecond.ToString() + "微秒";
        //}
        
        #endregion

        static string[] ZHAIDAI_CH = new string[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15", "16", "17", "18", "19", "20" };

        ////路数选择
        //private void repositoryItemComboBox3_SelectedIndexChanged(object sender, EventArgs e)
        //{
        //    string ip = "";
        //    int index = ((DevExpress.XtraEditors.ComboBoxEdit)sender).SelectedIndex;
        //    switch (repositoryItemComboBox4.Items.IndexOf(barEditItem4.EditValue))
        //    {
        //        case 0:
        //            if (index < 0) index = 0;
        //            else if (index > 195) index = 195;
        //            band = index + 1;//获取界面所选择的子带号
        //            ip = string.Format(m_NetGroupIP, band);//根据选择的子带号（路数）更改组播IP
        //            m_DataProcessing.Selected_NBBandWidth_Freq(index, m_UI_RFLocalCtrl.nbddc.NBDDCBandWidth[index], m_UI_RFLocalCtrl.nbddc.NBDDCFreq[index]);
        //                     m_DataProcessing.udpRecvDestroy();//销毁之前的socketLX
        //    m_DataProcessing.udpRecv.ClearQueue();
        //    m_DataProcessing.ClearQueue();
        //    m_DataProcessing.DataIQWaveform.ClearQueue();
        //    m_DataProcessing.DataFunction.ClearQueue();
        //    m_DataProcessing.DataFrequencySpectrum.ClearQueue();
        //    m_DataProcessing.udpRecvInit(m_NetLocalIP, ip, m_NetLocalPort);//根据选择的路数创建socketLX
        //    m_DataProcessing.udpRecv.passTime += new udpRecv.PassTime(udpRecv_passTime);//时标信息的委托LX
        //            break;
        //        case 1:
        //            if (index < 0) index = 0;
        //            else if (index > 59) index = 59;
        //            band = index / 20 + 197;
        //            ip = string.Format(m_NetGroupIP, band);//根据选择的路数更改组播IP
        //            m_DataProcessing.Selected_WBBandWidth_Freq(index, m_UI_RFLocalCtrl.wbddc.WBDDCFreq[index]);//更改宽带频点
        //            m_DataProcessing.SelectedBand(index + 1);//选择宽带数据包中的子带
        //                    m_DataProcessing.udpRecvDestroy();//销毁之前的socketLX
        //    m_DataProcessing.udpRecv.ClearQueue();
        //    m_DataProcessing.ClearQueue();
        //    m_DataProcessing.DataIQWaveform.ClearQueue();
        //    m_DataProcessing.DataFunction.ClearQueue();
        //    m_DataProcessing.DataFrequencySpectrum.ClearQueue();
        //    m_DataProcessing.udpRecvInit(m_NetLocalIP, ip, m_NetLocalPort);//根据选择的路数创建socketLX
        //    m_DataProcessing.udpRecv.passTime += new udpRecv.PassTime(udpRecv_passTime);//时标信息的委托LX
        //            break;
        //        case 2:
        //            if (index < 0) index = 0;
        //            else if (index > 4) index = 4;
        //            band = index + 200;
        //            ip = string.Format(m_NetGroupIP, band);//根据选择的路数更改组播IP
        //            m_DataProcessing.Selected_FFTBandWidth_Freq(band - 200);//更改FFT频点
        //                    m_DataProcessing.udpRecvDestroy();//销毁之前的socketLX
        //    m_DataProcessing.udpRecv.ClearQueue();
        //    m_DataProcessing.ClearQueue();
        //    m_DataProcessing.DataIQWaveform.ClearQueue();
        //    m_DataProcessing.DataFunction.ClearQueue();
        //    m_DataProcessing.DataFrequencySpectrum.ClearQueue();
        //    m_DataProcessing.udpRecvInit(m_NetLocalIP, UdpMulticastGroup, m_NetLocalPort);//根据选择的路数创建socketLX
        //    m_DataProcessing.udpRecv.passTime += new udpRecv.PassTime(udpRecv_passTime);//时标信息的委托LX
        //            break;
        //        default:
        //            break;
        //    }
   
        //}

        //平滑数

        private void repositoryItemComboBox6_SelectedIndexChanged(object sender, EventArgs e)
        {
            m_FileProcessing.udpRecv.ClearQueue();
            m_DataProcessing.ClearQueue();
            m_DataProcessing.DataFrequencySpectrum.ClearQueue();
            if (((DevExpress.XtraEditors.ComboBoxEdit)sender).Text == null)
                return;
            int Num = Convert.ToInt32(((DevExpress.XtraEditors.ComboBoxEdit)sender).Text);
            m_DataProcessing.DataFrequencySpectrum.SelectSmoothNum(Num);
        }

        //分辨率
        private void repositoryItemComboBox5_SelectedIndexChanged(object sender, EventArgs e)
        {
            m_DataProcessing.ClearQueue();
            m_DataProcessing.DataFrequencySpectrum.ClearQueue();
            if (((DevExpress.XtraEditors.ComboBoxEdit)sender).Text == null)
                return;
            int Num = ((DevExpress.XtraEditors.ComboBoxEdit)sender).SelectedIndex;
            int Resolution = (int)Math.Pow(2, Num);
            m_DataProcessing.SelectedResolution(Resolution);
        }

        #region 数据传输至UI显示层
        void DataIQWaveform_passIQData(IQData t)
        {
            if (m_UI_IQWaveform != null && t != null)
            {
                m_UI_IQWaveform.PushDataAttribute(t);
            }
        }

        void DataFrequencySpectrum_passPowerAndFreq(PowerAndFreq t)
        {
            if (m_UI_FrequencySpectrum != null && t != null)
            {
                m_UI_FrequencySpectrum.PushDataAttribute(t);
            }
            if (m_UI_WaterfallPlot != null && t != null)
            {
                m_UI_WaterfallPlot.PushDataAttribute(t);
            }
        }

        #endregion

        #region Xml
        //初始化设备类型、千兆网设置、万兆网设置200117LX
        private void WriteXml()
        {
            m_NetLocalIP = m_XmlProcessing.Read_LocalIP();//初始化万兆网IP
            m_NetLocalPort = Convert.ToInt32(m_XmlProcessing.Read_LocalPort());//初始化万兆网端口

            repositoryItemComboBox3.Items.Clear();
            this.repositoryItemComboBox3.Items.AddRange(new object[] { "1","2","3","4","5","6","7","8","9","10","11","12","13","14", "15","16","17","18","19","20"});
            repositoryItemComboBox4.Items.Clear();
            this.repositoryItemComboBox4.Items.AddRange(new object[] { "0", "1", "2", "3", "4", "5", "6", "7", "8" });
            repositoryItemComboBox5.Items.Clear();
            this.repositoryItemComboBox5.Items.AddRange(new object[] {"2500Hz","1250Hz","625Hz","312.5Hz","156.25Hz","78.125Hz","39.0625Hz","19.53125Hz"});
            this.repositoryItemComboBox5.NullText = "2500Hz";
            this.repositoryItemComboBox11.Items.AddRange(new object[] { "无", "Hanning" });
            this.repositoryItemComboBox11.NullText = "无";
            
            barStaticItem5.Caption = "XD_DBDW测试程序";
        }
        #endregion

        private void FrequencySpectrumBtn_ItemClick(object sender, ItemClickEventArgs e)
        {
            XtraUserControl userControl = FrequencySpectrumUserControl;
            tabbedView.AddDocument(userControl);
            tabbedView.ActivateDocument(userControl);

            m_UI_FrequencySpectrum = new UI_FrequencySpectrum();
            this.FrequencySpectrumUserControl.Controls.Clear();
            this.FrequencySpectrumUserControl.Controls.Add(m_UI_FrequencySpectrum);
            this.m_UI_FrequencySpectrum.Dock = DockStyle.Fill;
        }

        private void IQWaveformBtn_ItemClick(object sender, ItemClickEventArgs e)
        {
            XtraUserControl userControl = IQWaveformUserControl;
            tabbedView.AddDocument(userControl);
            tabbedView.ActivateDocument(userControl);

            m_UI_IQWaveform = new UI_IQWaveform();
            this.IQWaveformUserControl.Controls.Clear();
            this.IQWaveformUserControl.Controls.Add(m_UI_IQWaveform);
            this.m_UI_IQWaveform.Dock = DockStyle.Fill;
        }

        private void WaterFallBtn_ItemClick(object sender, ItemClickEventArgs e)
        {
            XtraUserControl userControl = WaterfallPlotUserControl;
            tabbedView.AddDocument(userControl);
            tabbedView.ActivateDocument(userControl);

            m_UI_WaterfallPlot = new UI_WaterfallPlot();
            this.WaterfallPlotUserControl.Controls.Clear();
            this.WaterfallPlotUserControl.Controls.Add(m_UI_WaterfallPlot);
            this.m_UI_WaterfallPlot.Dock = DockStyle.Fill;
        }

        private void tmp_Click(object sender, EventArgs e)
        {
            byte[] test_data = new byte[4 * 512];
            string path = (string)((AccordionControlElement)sender).Tag;
            FileStream fs = new FileStream(path, FileMode.Open);
            fs.Read(test_data, 0, (int)test_data.Length);
            fs.Close();
            int type = 1;
            //m_DataProcessing.FilePass(type, test_data);
        }

        private void OpenFileBtn_ItemClick(object sender, ItemClickEventArgs e)
        {
            XtraFolderBrowserDialog OpenFolder = new XtraFolderBrowserDialog();
            OpenFolder.ShowNewFolderButton = false;
            OpenFolder.UseParentFormIcon = true;
            OpenFolder.ShowDragDropConfirmation = true;
            //OpenFolder.SelectedPath = @"C:\Users\Administrator\Desktop\JGZC\ZDLP\ZDLP\bin\Release\RecvData";
            if (OpenFolder.ShowDialog() == DialogResult.OK)
            {
                string path = OpenFolder.SelectedPath;
                DirectoryInfo fi = new DirectoryInfo(path);
                if (!fi.Exists)
                    return;
                mainAccordionGroup.Elements.Clear();
                List<AccordionControlElement> ac = new List<AccordionControlElement>();
                foreach (FileInfo file in fi.GetFiles())
                {
                    if (file.Extension == ".dat")
                    {
                        AccordionControlElement tmp = new AccordionControlElement(DevExpress.XtraBars.Navigation.ElementStyle.Item);
                        tmp.Text = file.Name;
                        tmp.Tag = file.FullName;
                        tmp.Click += new System.EventHandler(this.tmp_Click); ;
                        ac.Add(tmp);
                    }
                }
                AccordionControlElement[] acArray = ac.ToArray();
                mainAccordionGroup.Elements.AddRange(ac.ToArray());
            }
        }
    }
}