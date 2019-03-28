// Copyright (c) Sandeep Mistry. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Import libraries (BLEPeripheral depends on SPI)
#include <SPI.h>

#include <MPU6050_tockn.h>
#include <Wire.h>

MPU6050 mpu6050(Wire);

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
  uint16_t ax;
  uint16_t ay;
  uint16_t az;

  uint16_t gx;
  uint16_t gy;
  uint16_t gz;
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

  Wire.begin();
  mpu6050.begin();
  mpu6050.calcGyroOffsets(true);
}

long timer = 0;

void loop()
{
  // poll peripheral
  blePeripheral.poll();
  mpu6050.update();

  if (millis() - timer > 1000)
  {

    Serial.println("=======================================================");
    Serial.print("temp : ");
    Serial.println(mpu6050.getTemp());
    Serial.print("accX : ");
    Serial.print(mpu6050.getAccX());
    Serial.print("\taccY : ");
    Serial.print(mpu6050.getAccY());
    Serial.print("\taccZ : ");
    Serial.println(mpu6050.getAccZ());

    Serial.print("gyroX : ");
    Serial.print(mpu6050.getGyroX());
    Serial.print("\tgyroY : ");
    Serial.print(mpu6050.getGyroY());
    Serial.print("\tgyroZ : ");
    Serial.println(mpu6050.getGyroZ());

    Serial.print("accAngleX : ");
    Serial.print(mpu6050.getAccAngleX());
    Serial.print("\taccAngleY : ");
    Serial.println(mpu6050.getAccAngleY());

    Serial.print("gyroAngleX : ");
    Serial.print(mpu6050.getGyroAngleX());
    Serial.print("\tgyroAngleY : ");
    Serial.print(mpu6050.getGyroAngleY());
    Serial.print("\tgyroAngleZ : ");
    Serial.println(mpu6050.getGyroAngleZ());

    Serial.print("angleX : ");
    Serial.print(mpu6050.getAngleX());
    Serial.print("\tangleY : ");
    Serial.print(mpu6050.getAngleY());
    Serial.print("\tangleZ : ");
    Serial.println(mpu6050.getAngleZ());
    Serial.println("=======================================================\n");
    timer = millis();
  }

  AccelerometerData ad = {
      mpu6050.getRawAccX(),
      mpu6050.getRawAccY(),
      mpu6050.getRawAccZ(),

      mpu6050.getRawGyroX(),
      mpu6050.getRawGyroY(),
      mpu6050.getRawGyroZ(),
  };

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
