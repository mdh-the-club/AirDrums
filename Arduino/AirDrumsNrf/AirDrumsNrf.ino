// Copyright (c) Sandeep Mistry. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Import libraries (BLEPeripheral depends on SPI)
#include <SPI.h>
#include <Adafruit_LIS3DH.h>
#include <Adafruit_Sensor.h>

#include <BLEPeripheral.h>

// Used for software SPI
#define LIS3DH_CLK 7
#define LIS3DH_MISO 4
#define LIS3DH_MOSI 6
// Used for hardware & software SPI
#define LIS3DH_CS 3

// software SPI
Adafruit_LIS3DH lis = Adafruit_LIS3DH(LIS3DH_CS, LIS3DH_MOSI, LIS3DH_MISO, LIS3DH_CLK);
// hardware SPI
//Adafruit_LIS3DH lis = Adafruit_LIS3DH(LIS3DH_CS);
// I2C
//Adafruit_LIS3DH lis = Adafruit_LIS3DH();

// Adjust this number for the sensitivity of the 'click' force
// this strongly depend on the range! for 16G, try 5-10
// for 8G, try 10-20. for 4G try 20-40. for 2G try 40-80
#define CLICKTHRESHHOLD 80

#include <BLEPeripheral.h>

#define MIDI_SERVICE_UUID "03b80e5a-ede8-4b33-a751-6ce34ec4c700"
#define MIDI_CHARACTERISTIC_UUID "7772e5db-3868-4112-a1a9-f2669d106bf3"

#define ACCELEROMETER_SERVICE_UUID "E95D0753-251D-470A-A062-FA1922DFA9A8"
#define ACCELEROMETER_CHARACTERISTIC_UUID "E95DCA4B-251D-470A-A062-FA1922DFA9A8"
//custom boards may override default pin definitions with BLEPeripheral(PIN_REQ, PIN_RDY, PIN_RST)
BLEPeripheral blePeripheral = BLEPeripheral();

// uuid's can be:
//   16-bit: "ffff"
//  128-bit: "19b10010e8f2537e4f6cd104768a1214" (dashed format also supported)

typedef struct MIDIPacket
{
  uint8_t Header;
  uint8_t TimeStamp;
  uint8_t Status;
  uint8_t Note;
  uint8_t Velocity;
};

typedef struct AccelerometerData
{
  float x;
  float y;
  float z;
};

// create one or more services
BLEService service = BLEService(MIDI_SERVICE_UUID);
BLETypedCharacteristic<MIDIPacket> characteristicBLEMIDI = BLETypedCharacteristic<MIDIPacket>(MIDI_CHARACTERISTIC_UUID, BLERead | BLEWrite | BLENotify);

BLEService accService = BLEService(ACCELEROMETER_SERVICE_UUID);
BLETypedCharacteristic<AccelerometerData> characteristicAccelerometer = BLETypedCharacteristic<AccelerometerData>(ACCELEROMETER_CHARACTERISTIC_UUID, BLERead | BLEWrite | BLENotify);

BLEDescriptor descriptor = BLEDescriptor("2902", "value");

MIDIPacket midiPacket = {
    0x80, // header
    0x80, // timestamp, not implemented
    0x00, // status
    0x3c, // 0x3c == 60 == middle c
    0x00  // velocity
};

bool deviceConnected = false;

void blePeripheralConnectHandler(BLECentral &central)
{
  // central connected event handler
  Serial.print(F("Connected event, central: "));
  Serial.println(central.address());
  deviceConnected = true;
}

void blePeripheralDisconnectHandler(BLECentral &central)
{
  // central disconnected event handler
  Serial.print(F("Disconnected event, central: "));
  Serial.println(central.address());
  deviceConnected = false;
}

void setup()
{
  Serial.begin(115200);
#if defined(__AVR_ATmega32U4__)
  delay(5000); //5 seconds delay for enabling to see the start up comments on the serial board
#endif

  blePeripheral.setLocalName("BLEMidi");                  // optional
  blePeripheral.setAdvertisedServiceUuid(service.uuid()); // optional
  //blePeripheral.setAdvertisingInterval();

  // add attributes (services, characteristics, descriptors) to peripheral
  blePeripheral.addAttribute(service);
  blePeripheral.addAttribute(characteristicBLEMIDI);

  blePeripheral.addAttribute(accService);
  blePeripheral.addAttribute(characteristicAccelerometer);
  blePeripheral.addAttribute(descriptor);

  // assign event handlers for connected, disconnected to peripheral
  blePeripheral.setEventHandler(BLEConnected, blePeripheralConnectHandler);
  blePeripheral.setEventHandler(BLEDisconnected, blePeripheralDisconnectHandler);

  // begin initialization
  blePeripheral.begin();

  if (!lis.begin(0x19))
  { // change this to 0x19 for alternative i2c address
    Serial.println("Couldnt start");
    while (1)
      ;
  }

  lis.setRange(LIS3DH_RANGE_2_G); // 2, 4, 8 or 16 G!

  Serial.print("Range = ");
  Serial.print(2 << lis.getRange());
  Serial.println("G");

  // 0 = turn off click detection & interrupt
  // 1 = single click only interrupt output
  // 2 = double click only interrupt output, detect single click
  // Adjust threshhold, higher numbers are less sensitive
  lis.setClick(1, CLICKTHRESHHOLD);
  delay(500);
}

void loop()
{
  // poll peripheral
  blePeripheral.poll();
  /* Or....get a new sensor event, normalized */
  sensors_event_t event;
  lis.getEvent(&event);

  /* Display the results (acceleration is measured in m5/s^2) */
  Serial.print(event.acceleration.x);
  Serial.print(" ");
  Serial.print(event.acceleration.y);
  Serial.print(" ");
  Serial.print(event.acceleration.z);
  Serial.println();

  AccelerometerData ad = {event.acceleration.x,
                          event.acceleration.y,
                          event.acceleration.z};

  if (deviceConnected)
  {
    characteristicAccelerometer.setValue(ad);

    // uint8_t click = lis.getClick();
    // if (click == 0)
    //   return;
    // if (!(click & 0x30))
    //   return;
    // Serial.print("Click detected (0x");
    // Serial.print(click, HEX);
    // Serial.print("): ");
    // if (click & 0x10)
    //   Serial.print(" single click");
    // if (click & 0x20)
    //   Serial.print(" double click");
    // Serial.println();

    // if (click & 0x10)
    // {
    //   // note down
    //   midiPacket.Status = 0x90;                   // note down, channel 0
    //   midiPacket.Velocity = 127;                  // velocity
    //   characteristicBLEMIDI.setValue(midiPacket); // packet, length in bytes
    //   //characteristic.notify();

    //   // play note for 500ms
    //   delay(500);

    //   // note up
    //   midiPacket.Status = 0x80;                   // note up, channel 0
    //   midiPacket.Velocity = 0;                    // velocity
    //   characteristicBLEMIDI.setValue(midiPacket); // packet, length in bytes)
    //   //characteristic.notify();
    // }
  }
  delay(50);
}

// void loop()
// {
//   BLECentral central = blePeripheral.central();

//   if (central)
//   {
//     // central connected to peripheral
//     Serial.print(F("Connected to central: "));
//     Serial.println(central.address());

//     while (central.connected())
//     {
//       //      // central still connected to peripheral
//       //      if (characteristic.written()) {
//       //        // central wrote new value to characteristic
//       //        Serial.println(characteristic.value(), DEC);
//       //
//       //        // set value on characteristic
//       //        characteristic.setValue(5);
//       //      }

//       /* Or....get a new sensor event, normalized */
//       sensors_event_t event;
//       lis.getEvent(&event);

//       /* Display the results (acceleration is measured in m5/s^2) */
//       Serial.print(event.acceleration.x);
//       Serial.print(" ");
//       Serial.print(event.acceleration.y);
//       Serial.print(" ");
//       Serial.print(event.acceleration.z);

//       Serial.println();

//       if (event.acceleration.z < 0)
//       {
//         // note down
//         midiPacket.Status = 0x90;            // note down, channel 0
//         midiPacket.Velocity = 127;           // velocity
//         characteristic.setValue(midiPacket); // packet, length in bytes
//         //characteristic.notify();

//         // play note for 500ms
//         delay(500);

//         // note up
//         midiPacket.Status = 0x80;            // note up, channel 0
//         midiPacket.Velocity = 0;             // velocity
//         characteristic.setValue(midiPacket); // packet, length in bytes)
//         //characteristic.notify();

//         delay(500);
//       }
//     }

//     // central disconnected
//     Serial.print(F("Disconnected from central: "));
//     Serial.println(central.address());
//   }
// }
