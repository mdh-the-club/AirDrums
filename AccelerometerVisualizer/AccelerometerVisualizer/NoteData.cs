using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Toub.Sound.Midi;

namespace AccelerometerVisualizer
{
    public class NoteData
    {
        public float X;
        public float Y;
        public float Z;
        //public int MidiInstrument;
        //public int MidiNote;
        public GeneralMidiPercussion PercissionInst;

        public NoteData(float x, float y, float z, GeneralMidiPercussion percInt)
        {
            X = x;
            Y = y;
            Z = z;
            PercissionInst = percInt;
        }
    }
}
