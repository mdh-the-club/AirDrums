﻿using System;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace AccelerometerVisualizer
{
    public partial class Form1 : Form
    {
        BindingList<DeviceInformation> deviceList = new BindingList<DeviceInformation>();
        DeviceWatcher deviceWatcher;

        public Form1()
        {
            InitializeComponent();
            Scan1();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            chart1.Series["accX"].Points.Clear();
            chart1.Series["accY"].Points.Clear();
            chart1.Series["accZ"].Points.Clear();


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
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            float z = reader.ReadSingle();

            

            this.chart1.Invoke((MethodInvoker)delegate {
                chart1.Series["accX"].Points.AddY(x);
                chart1.Series["accY"].Points.AddY(y);
                chart1.Series["accZ"].Points.AddY(z);

                if (chart1.Series["accX"].Points.Count > 200)
                {
                    chart1.Series["accX"].Points.RemoveAt(0);
                    chart1.Series["accY"].Points.RemoveAt(0);
                    chart1.Series["accZ"].Points.RemoveAt(0);

                }

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
    }
}

