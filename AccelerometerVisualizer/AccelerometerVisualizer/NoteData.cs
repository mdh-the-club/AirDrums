using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Runtime.Serialization;

using Toub.Sound.Midi;

namespace AccelerometerVisualizer
{
    [Serializable]
    public struct SensorData
    {
        public double Ax;
        public double Ay;
        public double Az;
        public double Gx;
        public double Gy;
        public double Gz;

        public double Magnitute;



        public SensorData(double ax, double ay, double az, double gx, double gy, double gz)
        {
            Ax = ax;
            Ay = ay;
            Az = az;

            Magnitute = (float)Math.Sqrt(ax * ax + ay * ay + az * az);

            Gx = gx;
            Gy = gy;
            Gz = gz;
           
        }
    }

    [Serializable]
    public class NoteData
    {
        public double Intensity
        {
            get;
            set;
        }

        public List<SensorData> SensorData
        {
            private set;
            get;
        }

        public GeneralMidiPercussion PercissionInst { get; set; }

        public NoteData(List<SensorData> sensorData, GeneralMidiPercussion percInt, double intensity)
        {
            SensorData = sensorData;
            Intensity = intensity;
            PercissionInst = percInt;
        }



        public double Corr(NoteData otherData)
        {
            int sizeToCompare = Math.Min(SensorData.Count, otherData.SensorData.Count);
            double sx, sy, sxx, syy, sxy;

            sx = sy = sxx = syy = sxy = 0;

            for(int i=0; i<sizeToCompare;i++)
            {
                double x = this.SensorData[i].Magnitute;
                double y = otherData.SensorData[i].Magnitute;

                sx += x;
                sy += y;
                sxx += x * x;
                syy += y * y;
                sxy += x * y;
            }

            double cov = sxy / sizeToCompare - sx * sy / sizeToCompare / sizeToCompare;
            double sigmax = Math.Sqrt(sxx / sizeToCompare - sx * sx / sizeToCompare / sizeToCompare);
            double sigmay = Math.Sqrt(syy / sizeToCompare - sy * sy / sizeToCompare / sizeToCompare);

            return cov / sigmax / sigmay;
        }

        //private double SDv(List<SensorData> data)
        //{

        //}
    }
}
