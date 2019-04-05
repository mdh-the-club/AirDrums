using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AccelerometerVisualizer
{
    public partial class NotesForm : Form
    {
        public NotesForm()
        {
            InitializeComponent();
        }

        public void SetNotes(List<NoteData> notes)
        {
            int i = 1;
            string chartName, seriesName;


            foreach(NoteData note in notes)
            {
                chartName = "Note " + (i++).ToString();
                chartNotes.ChartAreas.Add(chartName);
                chartNotes.Series.Add(chartName);
                chartNotes.Series[chartName].ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Spline;
                foreach (double shit in note.SensorData.Select(x => x.Magnitute))
                    chartNotes.Series[chartName].Points.AddY(shit);
                chartNotes.Series[chartName].ChartArea = chartName;
            }

        }

    }
}
