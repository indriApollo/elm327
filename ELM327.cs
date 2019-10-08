using System;
using System.IO.Ports;
using System.Collections.Generic;

namespace IndriApollo.elm327
{
    class ELM327OBD2
    {
        public enum ConnectionStatus : sbyte
        {
            CONNECTED = 0,
            UNAUTHORIZED = -1,
            INVALID_PORT = -2,
            ALREADY_OPEN = -3,
            ELM_NOT_DETECTED = -4
        };

        public enum FuelSystemStatus : byte
        {
            NOT_PRESENT = 0,
            OPEN_LOOP_INSUFFICIENT_ENGINE_TEMPERATURE = 1,
            CLOSED_LOOP_USING_OXYGEN_SENSOR_FEEDBACK_TO_DETERMINE_FUEL_MIX = 2,
            OPEN_LOOP_ENGINE_LOAD_OR_DECELERATION_FUEL_CUT = 4,
            OPEN_LOOP_SYSTEM_FAILURE = 8,
            CLOSED_LOOP_USING_OXYGEN_SENSOR_BUT_FEEDBACK_FAULT = 16
        };

        public enum IgnitionType
        {
            SPARK_IGNITION = 0,
            COMPRESSION_IGNITION = 1
        };

        public enum EngineTestComponent
        {
            COMPONENTS = 2,
            FUEL_SYSTEM = 1,
            MISFIRE = 0,
            // Spark ignition test at offset 10
            EGR_SYSTEM = 17,
            OXYGEN_SENSOR_HEATER = 16,
            OXYGEN_SENSOR = 15,
            AC_REFRIGERANT = 14,
            SECONDARY_AIR_SYSTEM = 13,
            EVAPORATIVE_SYSTEM = 12,
            HEATED_CATALYST = 11,
            CATALYST = 10,
            // Compression ignition test at offset 10
            EGR_VVT_SYSTEM = 27,
            PM_FILTER_MONITORING = 26,
            EXHAUST_GAS_SENSOR = 25,
            // RESERVED
            BOOST_PRESSURE = 23,
            // RESERVED
            NOX_SCR_MONITOR = 21,
            NMHC_CATALYST = 20
        };

        public struct EngineTest
        {
            public EngineTestComponent Component { get; set; }
            public bool TestAvailable { get; set; }
            public bool TestIncomplete { get; set; }
        };

        public struct MonitorStatus
        {
            public bool MIL { get; set; }
            public byte DTC_CNT { get; set; }
            public IgnitionType IGNITION_TYPE { get; set; }
            public EngineTest[] TESTS { get; set; }
        };

        public const int BAUDRATE = 38400;
        public const Parity PARITY = Parity.None;
        public int DATABITS = 8;
        public StopBits STOPBITS = StopBits.One;
        private SerialPort _serialPort;

        public ELM327OBD2()
        {
            _serialPort = new SerialPort();
            _serialPort.BaudRate = BAUDRATE;
            _serialPort.Parity = PARITY;
            _serialPort.DataBits = DATABITS;
            _serialPort.StopBits = STOPBITS;
            _serialPort.NewLine = "\r";
        }

        public ConnectionStatus Connect(string portName)
        {
            return Connect(portName, out _);
        }

        public ConnectionStatus Connect(string portName, out string versionString)
        {
            versionString = null;
            _serialPort.PortName = portName;
            // try to open the serial port
            try
            {
                _serialPort.Open();
            }
            catch(UnauthorizedAccessException)
            {
                return ConnectionStatus.UNAUTHORIZED;
            }
            catch(ArgumentException)
            {
                return ConnectionStatus.INVALID_PORT;
            }
            catch(InvalidOperationException)
            {
                return ConnectionStatus.ALREADY_OPEN;
            }
            // verify that we are connected to an elm327 (or compatible clone)
            // by performing a reset
            try
            {
                _serialPort.ReadTimeout = 1000;
                _serialPort.WriteLine("ATZ");
                _serialPort.ReadLine(); // echo
                _serialPort.ReadLine(); // <cr>
                _serialPort.ReadLine(); // <cr>
                versionString = _serialPort.ReadLine();
                _serialPort.DiscardInBuffer();

                if(!versionString.StartsWith("ELM327"))
                {
                    return ConnectionStatus.ELM_NOT_DETECTED;
                }

                _serialPort.WriteLine("ATSP0");
                _serialPort.ReadLine(); // echo
                _serialPort.ReadLine(); // OK
                _serialPort.DiscardInBuffer();
            }
            catch(TimeoutException)
            {
                return ConnectionStatus.ELM_NOT_DETECTED;
            }

            return ConnectionStatus.CONNECTED;
        }

        public string ReadBatteryVoltage()
        {
            string voltage = null;
            _serialPort.ReadTimeout = 1000;
            _serialPort.WriteLine("ATRV");
            _serialPort.ReadLine(); // echo
            voltage = _serialPort.ReadLine(); // voltage
            _serialPort.DiscardInBuffer();

            return voltage;
        }

        public void ListSupportedPids()
        {
            const string testStr = "41 00 BE 3E F8 11";
            //const string testStr = "41 20 80 00 00 00";
            string bitmaskStr = testStr.Substring(6).Replace(" ", String.Empty);
            UInt32 bitmask = UInt32.Parse(bitmaskStr, System.Globalization.NumberStyles.HexNumber);
            Console.WriteLine(bitmask);
            for(byte i = 0; i < 32; i++)
            {
                bool pidSupported = ((bitmask&(1<<i)) != 0);
                Console.WriteLine($"{(StandardPids.Pids)(i+1)} {(pidSupported ? "yes" : "no")}");
            }
        }

        public MonitorStatus ReadMonitorStatusSinceDtcsCleared()
        {
            const UInt32 testData = 0x81068000;
            byte A = (byte)(testData>>24);
            byte B = (byte)((testData>>16)&0xFF);
            byte C = (byte)((testData>>8)&0xFF);
            byte D = (byte)(testData&0xFF);
            
            MonitorStatus status = new MonitorStatus();

            // parse byte A
            status.MIL = (A&(1<<7)) != 1;
            status.DTC_CNT = (byte)(A&~(1<<7));
            
            List<EngineTest> tests = new List<EngineTest>();
            // parse byte B
            status.IGNITION_TYPE = (B&(1<<3)) == 0 ? IgnitionType.SPARK_IGNITION : IgnitionType.COMPRESSION_IGNITION;
            for(byte i = 0; i < 3; i++)
            {
                tests.Add(new EngineTest {
                    Component = (EngineTestComponent)i,
                    TestAvailable = (B&(1<<i)) != 0,
                    TestIncomplete = (B&(1<<(i+4))) != 0
                });
            }
            // parse bytes C & D
            byte enumOffset = (byte)(status.IGNITION_TYPE == IgnitionType.SPARK_IGNITION  ? 10 : 20);
            for(byte i = 0; i < 8; i++)
            {
                if(status.IGNITION_TYPE == IgnitionType.COMPRESSION_IGNITION && (i == 2 || i == 4))
                {
                    continue; // skip RESERVED
                }

                tests.Add(new EngineTest {
                    Component = (EngineTestComponent)i+enumOffset,
                    TestAvailable = (C&(1<<i)) != 0,
                    TestIncomplete = (D&(1<<(i))) != 0
                });
            }
            status.TESTS = tests.ToArray();

            return status;
        }

        public UInt16 ReadEngineRPM()
        {
            const UInt16 testData = 0x0BB0;
            return testData>>2;
        }

        public byte ReadIntakeManifoldAbsolutePressure()
        {
            const byte testByte = 0x25;
            return testByte;
        }

        public FuelSystemStatus[] ReadFuelSystemStatus()
        {
            const UInt16 testData = 0x0200;
            FuelSystemStatus fuelSystem1 = (FuelSystemStatus)(testData>>8);
            FuelSystemStatus fuelSystem2 = (FuelSystemStatus)(testData&0x00FF);
            return new FuelSystemStatus[] { fuelSystem1, fuelSystem2 };
        }

        public byte ReadCalculatedEngineLoad()
        {
            const byte testByte = 126;
            return (byte)(testByte/2.55f);
        }

        public Int16 ReadEngineCoolantTemperature()
        {
            const byte testByte = 0x35;
            return testByte - 40;
        }
    }
}