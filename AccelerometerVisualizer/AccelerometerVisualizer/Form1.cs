using System;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

using Toub.Sound.Midi;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace AccelerometerVisualizer
{
    public partial class Form1 : Form
    {
        BindingList<DeviceInformation> deviceList = new BindingList<DeviceInformation>();
        DeviceWatcher deviceWatcher;

        int sampleDataSize = 20;
        double correlationTreshold = 0.5;

        bool isRecording = false;
        bool isAirDrumming = false;
        bool areSamplesLoaded = false;

        List<SensorData> airDrumWindow = new List<SensorData>();        //In Air Drum mode this is the window that keeps the last sampleSizeData samples.
        List<SensorData> sensorData = new List<SensorData>();           //This is the list the builds up the charts.
        private List<NoteData> notes = new List<NoteData>();            //Used to store loaded notes information

        private volatile float curX, curY, curZ;

        bool playLock = false;

        public Form1()
        {
            InitializeComponent();

           
            InitializeMidiInstrumentsCombo();
            InitializeDataGridView();
            MidiPlayer.OpenMidi();

            Scan1();
        }
        

        #region -- Button Click Events --
        private void btnConnectBle_Click(object sender, EventArgs e)
        {
            chart1.Series["accX"].Points.Clear();
            chart1.Series["accY"].Points.Clear();
            chart1.Series["accZ"].Points.Clear();

            chart1.Series["gyroX"].Points.Clear();
            chart1.Series["gyroY"].Points.Clear();
            chart1.Series["gyroZ"].Points.Clear();


            ConnectDevice(cmbBleDevices.SelectedValue as DeviceInformation);

        }
        private void btnConnectSerial_Click(object sender, EventArgs e)
        {
            serialPort1.Open();
        }
        private void btnRec_Click(object sender, EventArgs e)
        {
            isRecording = !isRecording;
            if (isRecording)
            {
                sensorData = new List<SensorData>();
                btnRec.Text = "S T O P";
            }
            else
            {
                btnRec.Text = "R E C";
            }
        }
        private void btnPlay_Click(object sender, EventArgs e)
        {
            GeneralMidiPercussion percussion = (GeneralMidiPercussion)Enum.Parse(typeof(GeneralMidiPercussion), cmbInstrument.SelectedItem.ToString(), true);
            MidiPlayer.Play(new NoteOn(0, percussion, 127));

        }
        private void btnAdd_Click(object sender, EventArgs e)
        {
            GeneralMidiPercussion percussion = (GeneralMidiPercussion)Enum.Parse(typeof(GeneralMidiPercussion), cmbInstrument.SelectedItem.ToString(), true);
            notes.Add(new NoteData(sensorData, percussion, 0));
            BindingSource bs = new BindingSource();
            bs.DataSource = notes;
            dataGridView1.DataSource = bs;
        }
        #endregion

        #region -- Menu Events --
        private void saveDatasetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                using (Stream stream = File.Open(".\\test.bin", FileMode.Create))
                {
                    BinaryFormatter bin = new BinaryFormatter();
                    bin.Serialize(stream, notes);
                }
            }
            catch (IOException)
            {
            }
        }
        private void openDatasetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                using (Stream stream = File.Open(".\\test.bin", FileMode.Open))
                {
                    BinaryFormatter bin = new BinaryFormatter();
                    notes = bin.Deserialize(stream) as List<NoteData>;
                    btnAirDrum.Enabled = areSamplesLoaded = true;
                }
            }
            catch (IOException)
            {
            }

            //This was for testing the Correlation function
            //Console.WriteLine(string.Format("{0}, {1}, {2}", notes[0].Corr(notes[1]), notes[0].Corr(notes[2]), notes[0].Corr(notes[0])));
        }
        private void exportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int sampleNo = 0;
            foreach (var sample in notes)
            {
                // This text is always added, making the file longer over time
                // if it is not deleted.
                using (StreamWriter sw = File.AppendText(String.Format(".\\export{0}.cvs", sampleNo++)))
                {

                    foreach (SensorData s in sample.SensorData)
                    {
                        sw.WriteLine("{0},{1},{2},{3},{4},{5}", s.Ax, s.Ay, s.Az, s.Gx, s.Gy, s.Gz);
                    }
                }
            }
        }
        #endregion

        #region -- Serial and BLE --
        GattCharacteristic Characteristic;

        async void ConnectDevice(DeviceInformation deviceInfo)
        {
            // Note: BluetoothLEDevice.FromIdAsync must be called from a UI thread because it may prompt for consent.
            BluetoothLEDevice bluetoothLeDevice = await BluetoothLEDevice.FromIdAsync(deviceInfo.Id);
            GattDeviceServicesResult sr = await bluetoothLeDevice.GetGattServicesAsync();

            if (sr.Status == GattCommunicationStatus.Success)
            {
                var services = sr.Services;
                foreach (var s in services)
                {
                    if (s.Uuid.ToString() == "e95d0753-251d-470a-a062-fa1922dfa9a8")
                    {
                        GattCharacteristicsResult cr = await s.GetCharacteristicsAsync();
                        if (cr.Status == GattCommunicationStatus.Success)
                        {
                            var characteristics = cr.Characteristics;
                            foreach (var c in characteristics)
                            {
                                if (c.Uuid.ToString() == "e95dca4b-251d-470a-a062-fa1922dfa9a8")
                                {
                                    Characteristic = c;
                                    GattCommunicationStatus status = await c.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                                    if (status == GattCommunicationStatus.Success)
                                    {
                                        // Server has been informed of clients interest.
                                        c.ValueChanged += Characteristic_ValueChanged;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        private void Scan1()
        {
            // Query for extra properties you want returned
            string[] requestedProperties = { };

            deviceWatcher = DeviceInformation.CreateWatcher(
                                BluetoothLEDevice.GetDeviceSelectorFromPairingState(false),
                                requestedProperties,
                                DeviceInformationKind.AssociationEndpoint);

            // Register event handlers before starting the watcher.
            // Added, Updated and Removed are required to get all nearby devices
            deviceWatcher.Added += DeviceWatcher_Added;
            deviceWatcher.Updated += DeviceWatcher_Updated;
            deviceWatcher.Removed += DeviceWatcher_Removed;

            // EnumerationCompleted and Stopped are optional to implement.
            deviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;
            deviceWatcher.Stopped += DeviceWatcher_Stopped;

            // Start the watcher.
            deviceWatcher.Start();

        }
        private void serialPort1_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            string RxString = serialPort1.ReadLine();

            var values = RxString.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            if (values.Length != 3)
                return;
            var doubles = values.Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();

            if (doubles.Length == 3)
            {
                // PlotData((float)doubles[0], (float)doubles[1], (float)doubles[2]);


            }
        }
        private void DeviceWatcher_Stopped(DeviceWatcher sender, object args)
        {

        }
        private void DeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object args)
        {
            Console.WriteLine("Done");
        }
        private void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            this.cmbBleDevices.Invoke((MethodInvoker)delegate {
                // Running on the UI thread
                //comboBox1.Items.Add(args.Id);
                deviceList = new BindingList<DeviceInformation>(deviceList.Where(x => x.Id != args.Id).ToList());
                cmbBleDevices.DataSource = deviceList;
                cmbBleDevices.DisplayMember = "Id";
            });
        }
        private void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate args)
        {
        }
        private void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            this.cmbBleDevices.Invoke((MethodInvoker)delegate {
                // Running on the UI thread
                //comboBox1.Items.Add(args.Id);
                deviceList.Add(args);
                cmbBleDevices.DataSource = deviceList;
                cmbBleDevices.DisplayMember = "Name";
            });
        }
        void Characteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            // An Indicate or Notify reported that the value has changed.
            var reader = DataReader.FromBuffer(args.CharacteristicValue);
            reader.ByteOrder = ByteOrder.LittleEndian;
            float ax = reader.ReadInt16() / 16384.0f;
            float ay = reader.ReadInt16() / 16384.0f;
            float az = reader.ReadInt16() / 16384.0f;

            float gx = reader.ReadInt16() / 65.5f;
            float gy = reader.ReadInt16() / 65.5f;
            float gz = reader.ReadInt16() / 65.5f;


            float m = (float)Math.Sqrt(ax * ax + ay * ay + az * az);

            PlotData(ax, ay, az, gz, gy, gz, m);



        }
        #endregion

        #region -- MIDI Stuff Things --
        private void InitializeMidiInstrumentsCombo()
        {
            string[] names;
            names = Enum.GetNames(typeof(GeneralMidiPercussion));

            for (int i = 0; i < names.Length; i++)
                cmbInstrument.Items.Add(names[i]);
            cmbInstrument.SelectedIndex = 0;
        }
        #endregion


        void PlotData(float ax, float ay, float az, float gx, float gy, float gz, float m)
        {
            this.chart1.BeginInvoke((MethodInvoker)delegate {

                sensorData.Add(new SensorData(ax, ay, az, gx, gy, gz));

                if (isAirDrumming)
                {
                    airDrumWindow.Add(sensorData.ElementAt(sensorData.Count - 1));
                    if (airDrumWindow.Count >= sampleDataSize)
                        airDrumWindow.RemoveAt(0);

                    NoteData tempNote = new NoteData(airDrumWindow, GeneralMidiPercussion.AcousticSnare, 127);
                    Dictionary<NoteData, double> scores = new Dictionary<NoteData, double>();
                    foreach(NoteData note in notes)
                    {
                        scores.Add(note, note.Corr(tempNote));
                    }
                    KeyValuePair<NoteData, double> maxest = scores.Aggregate((x, y) => x.Value > y.Value ? x : y);
                    if (maxest.Value > correlationTreshold && !playLock)
                    {
                        MidiPlayer.Play(new NoteOn(0, maxest.Key.PercissionInst, 127));
                        playLock = true;
                    }
                    else if (maxest.Value < correlationTreshold && playLock)
                    {
                        playLock = false;
                    }
                }

                

                chart1.Series["accX"].Points.AddY(ax );
                chart1.Series["accY"].Points.AddY(ay);
                chart1.Series["accZ"].Points.AddY(az);

                chart1.Series["mag"].Points.AddY(m);


                chart1.Series["gyroX"].Points.AddY(gx);
                chart1.Series["gyroY"].Points.AddY(gy);
                chart1.Series["gyroZ"].Points.AddY(gz);

                if (chart1.Series["accX"].Points.Count > 200)
                {
                    chart1.Series["accX"].Points.RemoveAt(0);
                    chart1.Series["accY"].Points.RemoveAt(0);
                    chart1.Series["accZ"].Points.RemoveAt(0);

                    chart1.Series["mag"].Points.RemoveAt(0);

                    chart1.Series["gyroX"].Points.RemoveAt(0);
                    chart1.Series["gyroY"].Points.RemoveAt(0);
                    chart1.Series["gyroZ"].Points.RemoveAt(0);

                    sensorData.RemoveAt(0);
                }
                curX = ax;
                curY = ay;
                curZ = az;

                //These were only for tests 
                /*
                if (az > 200 && !playLock)
                {
                    GeneralMidiPercussion percussion = (GeneralMidiPercussion)Enum.Parse(typeof(GeneralMidiPercussion), cmbInstrument.SelectedItem.ToString(), true);
                    MidiPlayer.Play(new NoteOn(0, percussion, 127));
                    playLock = true;
                }

                if (az < 200 && playLock)
                    playLock = false;
                */
            });
        }

        private void InitializeDataGridView()
        {
            BindingSource bs = new BindingSource();
            bs.DataSource = notes;
            dataGridView1.DataSource = bs;
            
        }

        private void btnAirDrum_Click(object sender, EventArgs e)
        {
            if (isRecording)
            {
                MessageBox.Show("Idiot! You are in Recodring mode. Cannot record and playback at the same fucking time!");
                return;
            }

            isAirDrumming = true;
            airDrumWindow = new List<SensorData>();
        }

        private void cmbSerialDevices_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (serialPort1.IsOpen)
            {
                serialPort1.DataReceived -= serialPort1_DataReceived;
                serialPort1.Close();
                serialPort1.Dispose();
                GC.Collect();
                GC.Collect();
            }
            
        }

    }
}

