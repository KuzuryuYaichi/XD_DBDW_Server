using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Common.ComponentCs.Log;
using Common.ComponentCs;
using XD_DBDW_Server.DataAlgorithm;

namespace XD_DBDW_Server
{
    public class FileProcessing
    {
        public class FileInfo
        {
            public string fileName;
            public bool IsFilterFlag;
            public int kxFreOff;
            public int kxFltNo;

            public FileInfo(string fileName, bool IsFilterFlag)
            {
                this.fileName = fileName;
                this.IsFilterFlag = IsFilterFlag;
            }

            public FileInfo(string fileName, bool IsFilterFlag, int kxFreOff, int kxFltNo)
            {
                this.fileName = fileName;
                this.IsFilterFlag = IsFilterFlag;
                this.kxFreOff = kxFreOff;
                this.kxFltNo = kxFltNo;
            }
        }

        private AsyncDataQueue<FileInfo> m_queue;
        public delegate void IQ_Process(short[] ar, short[] ai);
        private DataProcessing m_DataProcessing;

        IQ_Process p_IQ_Process;

        public FileProcessing(DataProcessing m_DataProcessing)
        {
            this.m_DataProcessing = m_DataProcessing;
            p_IQ_Process += m_DataProcessing.IQ_Process;
            m_queue = new AsyncDataQueue<FileInfo>();//初始化队列
            m_queue.TEvent += new THandler<FileInfo>(m_queue_TEvent);
        }

        #region UDP底层数据传输

        public udpRecv udpRecv;
        public int udpRecvInit(string LocalIP, int LocalPort)//窄带宽带数据接收与处理
        {
            udpRecv = new udpRecv(LocalIP, LocalPort);//建立udp连接
            udpRecv.passParameter += new XD_DBDW_Server.udpRecv.PassInformationr(udpRecv_passParameter);//委托
            udpRecv.Start();//开始接收数据
            return 0;
        }

        #endregion

        #region 解析数据

        void udpRecv_passParameter(udpRecv sender, byte[] CltRBuf)//委托事件，udpRecv类传递的数据
        {
            if (CltRBuf[0] == 0x0A && CltRBuf[1] == 0x0B && CltRBuf[2] == 0x0C && CltRBuf[3] == 0x0D) //前4字节是ID 屏蔽网络上的非命令包
            {
                byte comno = CltRBuf[4]; // 第5字节是指令ID，可以是处理指令，回放指令等
                string fileName = null;
                switch (comno)
                {
                    case 0x10:  // 指令为0x10时表示下达日期，任务号等
                        //cmdIP.Text = iep_Svr.ToString() + "     " + BitConverter.ToInt32(CltRBuf, 13) + "   " + datat;
                        if ((fileName = FindOriDataFromHD(CltRBuf, 0)) != null)  // 判断如果找到这个文件，则
                        {
                            m_queue.Enqueue(new FileInfo(fileName, false));
                        }
                    break;

                    case 0x11:  // 接收 滤波带宽
                        try
                        {
                            if ((fileName = FindOriDataFromHD(CltRBuf, 0)) != null)  // 判断如果找到这个文件，则
                            {
                                m_queue.Enqueue(new FileInfo(fileName, true, BitConverter.ToInt32(CltRBuf, 5), BitConverter.ToInt32(CltRBuf, 9) / 100));
                            }
                            //bool LoadStartFlag = false;
                            //int LoadPacNum = 0;
                            //LoadStartFlag = true;
                            //bool timerswitch = true;
                            //bool LoadTmDif = true;
                            //int SendCnt = 0;
                        }
                        catch
                        {
                        }
                        break;
                }
            }
        }

        //回放计算
        int FilterSnpNo = 0;

        void m_queue_TEvent(FileInfo fileInfo)
        {
            byte[] headTmp = new byte[64];
            const int HeadLen = 136;
            int dataHeadlen = 38;

            FileStream fsOri = new FileStream(fileInfo.fileName, FileMode.Open, FileAccess.Read, FileShare.Read);//缓冲区大小？
            BinaryReader brdOri = new BinaryReader(fsOri);
            fsOri.Seek(0, SeekOrigin.Begin);
            byte[] FileHead = brdOri.ReadBytes(HeadLen);//先把头读取出来
            //DataSend[0].Send(FileHead, FileHead.Length);  //将文件头发送出去

            int CHCT = FileHead[52];//数据通道数量
            int iDFl = BitConverter.ToInt32(FileHead, 4);//每帧测向数据点数
            int framLen = BitConverter.ToInt32(FileHead, 0);//已存盘数据帧数
            int signlPacLen = 2196 + CHCT * 1024;//每一帧数据的长度（字节）

            byte[] LoadSigBuffer = new byte[signlPacLen];
            float[] A_rCal = new float[CHCT * iDFl];
            float[] A_iCal = new float[CHCT * iDFl];

            //滤波器参数设置 窄带滤波器
            double[, ,] rfilter_one = new double[CHCT, 2, 3];//连续滤波记忆
            double[, ,] rfilter_two = new double[CHCT, 2, 3];
            double[, ,] rfilter_three = new double[CHCT, 2, 3];

            byte[] Line_Array = new byte[2 * 2 * iDFl];
            byte[] DataPagSnd = new byte[64 + Line_Array.Length + CHCT * CHCT * 4 * 2];
            byte[] DataSndBuffer = new byte[DataPagSnd.Length * framLen];
            int LoadPacNum = 0;
            int SendCnt = 0;
            int channelNum = 0;
            fsOri.Seek(HeadLen, SeekOrigin.Begin);
            //try
            //{
                while (LoadPacNum < framLen)  // 文件读取完毕之前
                {
                    Int16[] A_r = new Int16[CHCT * iDFl];
                    Int16[] A_i = new Int16[CHCT * iDFl];
                    LoadSigBuffer = brdOri.ReadBytes(signlPacLen); // 读取数据长度
                    Buffer.BlockCopy(LoadSigBuffer, 0, headTmp, 0, 40); // 获取数据包头
                    Buffer.BlockCopy(LoadSigBuffer, dataHeadlen, A_r, 0, A_r.Length * 2);
                    Buffer.BlockCopy(LoadSigBuffer, dataHeadlen + A_r.Length * 2, A_i, 0, A_i.Length * 2);
                    Buffer.BlockCopy(headTmp, 0, DataPagSnd, 0, 64);
                    byte[] cnttem = BitConverter.GetBytes(SendCnt);
                    Buffer.BlockCopy(cnttem, 0, DataPagSnd, 40, 4);
                    if (fileInfo.IsFilterFlag)
                    {
                        try
                        {
                            int kxFreOff = fileInfo.kxFreOff;
                            int kxFltNo = fileInfo.kxFltNo;
                            for (int ijk = 0; ijk < CHCT * iDFl; ijk++)  // 将数据转为浮点数据
                            {
                                A_rCal[ijk] = A_r[ijk];
                                A_iCal[ijk] = A_i[ijk];
                            }
                            //G10频率搬移
                            Filter.SpectrumShiftDDC(0 - kxFreOff, A_rCal, A_iCal, ref FilterSnpNo, CHCT, iDFl);
                            //G10滤波
                            Filter.FilterCacuDDC(kxFltNo, rfilter_one, rfilter_two, rfilter_three, Filter.filter_ae, Filter.filter_be, A_rCal, A_iCal, CHCT, iDFl);
                            //G10频率搬移
                            Filter.SpectrumShiftDDC(kxFreOff, A_rCal, A_iCal, ref FilterSnpNo, CHCT, iDFl);
                            for (int ijk = 0; ijk < CHCT * iDFl; ijk++)  // 将数据转为浮点数据
                            {
                                A_r[ijk] = (short)(A_rCal[ijk] * 10);
                                A_i[ijk] = (short)(A_iCal[ijk] * 10);
                            }
                            // 获取 //// 获取 单通道数据，滤波以后
                            Buffer.BlockCopy(A_r, channelNum * iDFl * 2, Line_Array, 0, 2 * iDFl);  // I
                            Buffer.BlockCopy(A_i, channelNum * iDFl * 2, Line_Array, 2 * iDFl, 2 * iDFl); //Q
                            //Buffer.BlockCopy(RocMartix.ComputeRx(A_rCal, A_iCal, CHCT, iDFl), 0, DataPagSnd, 64, CHCT * CHCT * 4 * 2);
                            Buffer.BlockCopy(Line_Array, 0, DataPagSnd, 64 + CHCT * CHCT * 4 * 2, Line_Array.Length);
                        }
                        catch
                        {
                        }
                    }
                    else
                    {
                        //获取 单通道数据，滤波以后
                        //Buffer.BlockCopy(A_r, channelNum * iDFl * 2, Line_Array, 0, 2 * iDFl);  // I
                        //Buffer.BlockCopy(A_i, channelNum * iDFl * 2, Line_Array, 2 * iDFl, 2 * iDFl); //Q
                        //Buffer.BlockCopy(Line_Array, 0, DataPagSnd, 64 + CHCT * CHCT * 4 * 2, Line_Array.Length);
                        Buffer.BlockCopy(RocMartix.ComputeRxShort(A_r, A_i, CHCT, iDFl), 0, DataPagSnd, 64, CHCT * CHCT * 4 * 2);
                        Buffer.BlockCopy(A_r, channelNum * iDFl * 2, DataPagSnd, 64 + CHCT * CHCT * 4 * 2 , 2 * iDFl);  // I
                        Buffer.BlockCopy(A_i, channelNum * iDFl * 2, DataPagSnd, 64 + CHCT * CHCT * 4 * 2 + 2 * iDFl, 2 * iDFl); //Q
                    }
                    LoadPacNum++;
                    //至此数据打包完毕，开始通过网络传送回去
                    fsOri.Seek(HeadLen + signlPacLen * LoadPacNum, SeekOrigin.Begin);

                    if (SendCnt < 3000)
                    {
                        Buffer.BlockCopy(DataPagSnd, 0, DataSndBuffer, SendCnt * DataPagSnd.Length, DataPagSnd.Length);
                        SendCnt++;
                    }
                    p_IQ_Process(A_r, A_i);
                }
            //}
            //catch
            //{
            //}
        }

        #endregion

        //按时间从磁盘找到一帧（40ms）的原始数据并提取
        public string FindOriDataFromHD(byte[] Data, int FramNo)
        {
            //参数 StartDt：包含起始的日期和时间
            try
            {
                // 数据读取 
                int Year = (Data[5] - 0x30) * 1000 + (Data[6] - 0x30) * 100 + (Data[7] - 0x30) * 10 + (Data[8] - 0x30);
                int Month = (Data[9] - 0x30) * 10 + (Data[10] - 0x30);
                int Day = (Data[11] - 0x30) * 10 + (Data[12] - 0x30);
                int Hour = 0, Minute = 0, Second = 0, Millisecond = 0;
                // 真实只是用了 年、月、日的信息，
                string datat = Month.ToString() + "/" + Day.ToString() + "/" + Year.ToString() + " " + Hour.ToString() + ":" + Minute.ToString() + ":" + Second.ToString() + "." + Millisecond.ToString();
                DateTime vPlayDt = DateTime.Parse(datat);  // 下达的任务的日期
                vPlayDt = vPlayDt.AddMilliseconds(40 * FramNo);//跨日时日期自动更改
                var TargNum = BitConverter.ToInt32(Data, 13);  // int32 任务号

                // 优先寻找 100k文件 154_指测任务_部二局_1_2018_12_16.dat
                string monthS, DayS;
                if (Month < 10 || Day < 10)
                {
                    if (Month < 10)
                    {
                        monthS = "0" + Month.ToString();
                    }
                    else
                    {
                        monthS = Month.ToString();
                    }
                    if (Day < 10)
                    {
                        DayS = "0" + Day.ToString();
                    }
                    else
                    {
                        DayS = Day.ToString();
                    }
                }
                else
                {
                    monthS = Month.ToString();
                    DayS = Day.ToString();
                }
                string dt = Year.ToString() + Month.ToString("D2") + Day.ToString("D2");
                //var tpFNStr = @":\ftp\play\" + filePathflag[2] + @"\" + Year.ToString() + "-" + Month.ToString() + "-" + Day.ToString() + @"\" + TargNum.ToString() + "_" + "指测任务" + "_" + "部二局" + "_" + "1" + "_" + Year.ToString() + "-" + monthS + "-" + DayS + ".dat";
                var tpFNStr = @":\Share\XD\" + Year.ToString() + "-" + Month.ToString() + "-" + Day.ToString() + @"\CY\" + TargNum.ToString() + "_" + "指测任务" + "_" + "部二局" + "_" + "1" + "_" + Year.ToString() + "_" + monthS + "_" + DayS + ".dat";
                //[2]找到对应的文件并打开、定位指针、读一帧
                var cHDStr = "D";
                var vOpenFileName = cHDStr + tpFNStr;
                //cmdIP.Text = vOpenFileName;

                //优先使用当前驱动器号(大多数情况)
                if (File.Exists(vOpenFileName))
                {
                    return vOpenFileName;
                }
                //else//在所有驱动器下遍历找文件
                //{
                //    for (int i = 0; i < 1; i++)
                //    {
                //        vOpenFileName = filePathflag[0] + tpFNStr;  // 使用字符串数组之后  GDStoreDrv[i] 
                //        if (File.Exists(vOpenFileName))
                //        {
                //            //打开对应文件
                //            fsOri = new FileStream(vOpenFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);//缓冲区大小？
                //            brdOri = new BinaryReader(fsOri);
                //            cHDStr = filePathflag[0];     // 使用字符串数组之后  GDStoreDrvWZD[i] 
                //            cFileOpen = true;
                //            break;
                //        }
                //    }
                //    if (!cFileOpen)//遍历后未找到，宣告失败
                //    {
                //        // 如果失败 到 增加 @1 filePathflag[1],需要事先配置，此处参数需要和57所沟通  154_指测任务_部二局_1_2018_12_16@1.dat

                //        tpFNStr = ":\\ftp" + "\\" + "play" + "\\" + filePathflag[2] + "\\" + Year.ToString() + "-" + Month.ToString() + "-" + Day.ToString() + "\\" + TargNum.ToString() + "_" + "指测任务" + "_" + "部二局" + "_" + "1" + "_" + Year.ToString() + "-" + monthS + "-" + DayS + filePathflag[1] + ".dat";

                //        //[2]找到对应的文件并打开、定位指针、读一帧
                //        //判断文件名是否改变  由此组合出一个文件名字  
                //        vOpenFileName = cHDStr + tpFNStr;
                //        // /---默认上次已经在读取一个包则 首先对 文件指针进行初始化 
                //        if (cFileOpen)
                //        {
                //            brdOri.Close();
                //            fsOri.Close();
                //            cFileOpen = false;
                //        }
                //        //优先使用当前驱动器号(大多数情况)
                //        if (File.Exists(vOpenFileName))
                //        {
                //            //打开对应文件
                //            fsOri = new FileStream(vOpenFileName, FileMode.Open, FileAccess.Read, FileShare.Read);//缓冲区大小？
                //            brdOri = new BinaryReader(fsOri);
                //            cFileOpen = true;
                //            cOpenFileName = vOpenFileName;
                //        }
                //        else//在所有驱动器下遍历找文件
                //        {
                //            for (int i = 0; i < 1; i++)
                //            {
                //                vOpenFileName = filePathflag[0] + tpFNStr;  // 使用字符串数组之后  GDStoreDrv[i] 
                //                if (File.Exists(vOpenFileName))
                //                {
                //                    //打开对应文件
                //                    fsOri = new FileStream(vOpenFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);//缓冲区大小？
                //                    brdOri = new BinaryReader(fsOri);
                //                    cHDStr = filePathflag[0];     // 使用字符串数组之后  GDStoreDrvWZD[i] 
                //                    cFileOpen = true;
                //                    break;
                //                }
                //            }
                //            if (!cFileOpen)//遍历后未找到，宣告失败
                //            {
                //                cOpenFileName = "";
                //                //flag_IsFileExist = false;
                //                return false;
                //            }
                //        }
                //    }
                //}
                return null;//已读到对应帧，立即返回
            }
            catch (Exception ex)
            {
                string exs;
                exs = ex.Message;
                return null;//宣告失败
            }
        }
    }
}
