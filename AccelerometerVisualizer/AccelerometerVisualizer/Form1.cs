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

        bool isRecording = false;
        List<SensorData> sensorData = new List<SensorData>();

        public Form1()
        {
            InitializeComponent();

           
            InitializeMidiInstrumentsCombo();
            InitializeDataGridView();
            MidiPlayer.OpenMidi();

            Scan1();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            chart1.Series["accX"].Points.Clear();
            chart1.Series["accY"].Points.Clear();
            chart1.Series["accZ"].Points.Clear();

            chart1.Series["gyroX"].Points.Clear();
            chart1.Series["gyroY"].Points.Clear();
            chart1.Series["gyroZ"].Points.Clear();


            ConnectDevice(comboBox1.SelectedValue as DeviceInformation);
            
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

        private void DeviceWatcher_Stopped(DeviceWatcher sender, object args)
        {
            
        }

        private void DeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object args)
        {
            Console.WriteLine("Done");
        }

        private void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            this.comboBox1.Invoke((MethodInvoker)delegate {
                // Running on the UI thread
                //comboBox1.Items.Add(args.Id);
                deviceList = new BindingList<DeviceInformation>(deviceList.Where(x => x.Id != args.Id).ToList());
                comboBox1.DataSource = deviceList;
                comboBox1.DisplayMember = "Id";
            });
        }

        private void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate args)
        {
        }

        private void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            this.comboBox1.Invoke((MethodInvoker)delegate {
                // Running on the UI thread
                //comboBox1.Items.Add(args.Id);
                deviceList.Add(args);
                comboBox1.DataSource = deviceList;
                comboBox1.DisplayMember = "Name";
            });
        }

        void Characteristic_ValueChanged(GattCharacteristic sender,
                                                                            GattValueChangedEventArgs args)
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


        bool playLock = false;

        void PlotData(float ax, float ay, float az, float gx, float gy, float gz, float m)
        {
            this.chart1.BeginInvoke((MethodInvoker)delegate {


                sensorData.Add(new SensorData(ax, ay, az, gx, gy, gz));

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

                }
                curX = ax;
                curY = ay;
                curZ = az;

                if (az > 200 && !playLock)
                {
                    GeneralMidiPercussion percussion = (GeneralMidiPercussion)Enum.Parse(typeof(GeneralMidiPercussion), comboBox2.SelectedItem.ToString(), true);
                    MidiPlayer.Play(new NoteOn(0, percussion, 127));
                    playLock = true;
                }

                if (az < 200 && playLock)
                    playLock = false;

            });
        }

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

        private void customizeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            
        }


        #region -- MIDI and CONFIG code --

        private List<NoteData> notes = new List<NoteData>();
        private volatile float curX, curY, curZ;

        private void InitializeMidiInstrumentsCombo()
        {
            string[] names;
            names = Enum.GetNames(typeof(GeneralMidiPercussion));
            
            for (int i = 0; i < names.Length; i++)
                comboBox2.Items.Add(names[i]);
            comboBox2.SelectedIndex = 0;
        }

        private void InitializeDataGridView()
        {
            BindingSource bs = new BindingSource();
            bs.DataSource = notes;
            dataGridView1.DataSource = bs;
            
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

        private void button4_Click(object sender, EventArgs e)
        {
            serialPort1.Open();
        }

        private void comboBox3_SelectedIndexChanged(object sender, EventArgs e)
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

        private void button5_Click(object sender, EventArgs e)
        {
            isRecording = !isRecording;
            if (isRecording)
            {
                sensorData = new List<SensorData>();
                button5.Text = "S T O P";
            }
            else
            {
                button5.Text = "R E C";
            }
        }

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
                }
            }
            catch (IOException)
            {
            }

            Console.WriteLine(string.Format("{0}, {1}, {2}", notes[0].Corr(notes[1]), notes[0].Corr(notes[2]), notes[0].Corr(notes[0])));
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

        private void button3_Click(object sender, EventArgs e)
        {
            GeneralMidiPercussion percussion = (GeneralMidiPercussion)Enum.Parse(typeof(GeneralMidiPercussion), comboBox2.SelectedItem.ToString(), true);
            MidiPlayer.Play(new NoteOn(0, percussion, 127));

        }

        #endregion

        private void button2_Click(object sender, EventArgs e)
        {
            GeneralMidiPercussion percussion = (GeneralMidiPercussion)Enum.Parse(typeof(GeneralMidiPercussion), comboBox2.SelectedItem.ToString(), true);
            notes.Add(new NoteData(sensorData, percussion, 0));
            BindingSource bs = new BindingSource();
            bs.DataSource = notes;
            dataGridView1.DataSource = bs;
        }
    }
}

