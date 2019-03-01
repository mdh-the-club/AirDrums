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
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        //public int MidiInstrument;
        //public int MidiNote;
        public GeneralMidiPercussion PercissionInst { get; set; }

        public NoteData(float x, float y, float z, GeneralMidiPercussion percInt)
        {
            X = x;
            Y = y;
            Z = z;
            PercissionInst = percInt;
        }
    }
}
