using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common.ComponentCs.Log;
using Common.ComponentCs;
using XD_DBDW_Server;
using System.IO;

namespace XD_DBDW_Server
{
    public class DataProcessing
    {
        public bool DigitialGain_24db = false;
        public bool LowNoise = false;
        private XmlProcessing XmlProcessing = new XmlProcessing();
        private int m_DataPacket = 4096;
        private int m_ChannelCount = 20;
        private int RF_WorkMode = 0;//初始化值，0常规，1低噪声

        #region 通道、分辨率、是否进行性能测试选择、确定当前子带的中心频点和带宽

        /// 选择分辨率
        private int ResolutionIndex = 0;
        private int m_Resolution = 1;
        private double[] IQDataResolution, IQDataResolutionOrg;

        public void SelectedResolution(int Resolution)//上位机界面选择分辨率
        {
            m_Resolution = Resolution;
        }

        private int band = 0;//初始化子带号
        private double nbddcfreq = 1.70;//初始化窄带中心频点
        public double nbddcbandwidth = 0.05;//初始化窄带带宽
        private double wbddcfreq = 1.75;//初始化宽带中心频点

        #endregion

        #region 初始化
        public DataFunction DataFunction;
        public DataIQWaveform DataIQWaveform;
        public DataFrequencySpectrum DataFrequencySpectrum;
        private AsyncDataQueue<DataAndFreq> m_queue;
        private Transform Transform = new Transform();

        public DataProcessing()
        {
            IQDataResolution = new double[m_DataPacket / 4];//初始化分辨率变量
            IQDataResolutionOrg = new double[m_DataPacket / 4];//初始化分辨率变量
            m_queue = new AsyncDataQueue<DataAndFreq>();//初始化队列
            m_queue.TEvent += new THandler<DataAndFreq>(m_queue_TEvent);
            DataFrequencySpectrum = new DataFrequencySpectrum(this);//初始化频域数据处理类
            DataIQWaveform = new DataIQWaveform();//初始化时域数据处理类
            DataFunction = new DataFunction();//存储数据、时标检测类
        }

        public void ClearQueue()
        {
            m_queue.ClearQueue();
        }

        #endregion

        Dictionary<int, FFT_Data> dict = new Dictionary<int, FFT_Data>();

        class FFT_Data
        {
            public int count;
            public byte[] data;
            public byte[] RF_Gain;
            public byte[] Digit_Gain;
            public FFT_Data()
            {
                data = new byte[5 * 38400- 38400/4];//修改
                RF_Gain = new byte[5];
                Digit_Gain = new byte[5 * 12];
                count = 0;
            }
            //FFT拼接
            public FFT_Data Add(byte[] t)
            { 
                int channel = t[2];
                RF_Gain[channel] = t[15];
                if(channel < 4)
                {
                    //数字增益拼接
                    for (int i = 0; i < 12; i++)
                    {
                        Digit_Gain[channel * 12 + i] = t[i + 16];
                    }
                    //数据拼接 
                    Array.Copy(t, 96, data, 38400 * channel, 38400);  
                }
                else if(channel == 4)
                {
                    for (int i = 0; i < 9; i++)
                    {
                        Digit_Gain[channel * 12 + i] = t[i + 16];
                    }

                    Array.Copy(t, 96, data, 38400 * channel, 38400 * 3 / 4);
                }
                ++count;
                return count == 5 ? this : null;
            }
        }

        #region 解析数据

        public void IQ_Process(short[] ar, short[] ai)
        {
            DataAndFreq data = new DataAndFreq();
            data.Ar_Data = ar;
            data.Ai_Data = ai;
            m_queue.Enqueue(data);
        }
        
        #endregion

        double[][] Window = new double[8][]
        {
            Hanning.HanningWindow(256), 
            Hanning.HanningWindow(256 * 2), 
            Hanning.HanningWindow(256 * 2 * 2), 
            Hanning.HanningWindow(256 * 2 * 2 * 2), 
            Hanning.HanningWindow(256 * 2 * 2 * 2 * 2), 
            Hanning.HanningWindow(256 * 2 * 2 * 2 * 2 * 2),
            Hanning.HanningWindow(256 * 2 * 2 * 2 * 2 * 2 * 2),
            Hanning.HanningWindow(256 * 2 * 2 * 2 * 2 * 2 * 2 * 2)
        };

        #region 对队列里的数据进行处理（窄带）
        void m_queue_TEvent(DataAndFreq t)
        {
            IQDataAndFreq nIQDataAndFreq = null;
            
            int dataLength = t.Ai_Data.Length / 20 * 2;
            //(重要)保持多线程数据一致行所做的必要处理
            int mm_Resolution = m_Resolution, Resolution_length = mm_Resolution * 512;

            if (Resolution_length != IQDataResolution.Length)
            {
                ResolutionIndex = 0;
                IQDataResolution = new double[Resolution_length];
                IQDataResolutionOrg = new double[Resolution_length];
            }
                
            int BaseIndex = dataLength * ResolutionIndex;

            for (int i = 0; i < dataLength / 2; ++i)
            {
                IQDataResolutionOrg[BaseIndex + 2 * i] = (double)t.Ar_Data[i];
                IQDataResolutionOrg[BaseIndex + 2 * i + 1] = (double)t.Ai_Data[i];
            }

            if (++ResolutionIndex < mm_Resolution)//凑包个数计数
                return;
            ResolutionIndex = 0;

            //对不同分辨率(长度)的数据进行加窗处理
            int mm_Resolution_Index = (int)Math.Log(mm_Resolution, 2);
            for (int i = 0; i < IQDataResolution.Length / 2; i++)
            {
                IQDataResolution[2 * i] = IQDataResolutionOrg[2 * i] * Window[mm_Resolution_Index][i];
                IQDataResolution[2 * i + 1] = IQDataResolutionOrg[2 * i + 1] * Window[mm_Resolution_Index][i];
            }

            nIQDataAndFreq = new IQDataAndFreq(t.StartFreq, t.StopFreq, t.Type, t.RF_Gain, t.Digital_Gain);//构造结构体
            nIQDataAndFreq.Data = IQDataResolution;

            ///分发至时域处理类
            DataIQWaveform.PushData(IQDataResolutionOrg);

            //进入此处必然处理下一包 需要为下一包申请指向新空间
            IQDataResolution = new double[Resolution_length];
            IQDataResolutionOrg = new double[Resolution_length];
            
            ///分发至频域处理类
            DataFrequencySpectrum.PushData(nIQDataAndFreq);
        }
        #endregion
    }
}
