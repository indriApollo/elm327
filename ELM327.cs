using System;
using System.IO.Ports;
using System.Collections.Generic;
using System.Globalization;

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

        public const int BAUDRATE = 115200;
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
                Transmit("ATZ");
                Receive(1000, false, false); // <cr>
                Receive(1000, false, false); // <cr>
                versionString = Receive(1000, true, false);

                if(!versionString.StartsWith("ELM327"))
                {
                    return ConnectionStatus.ELM_NOT_DETECTED;
                }

                // Set interface protocol to auto
                Transmit("ATSP0");
                Receive(1000, true, false); // OK
            }
            catch(TimeoutException)
            {
                return ConnectionStatus.ELM_NOT_DETECTED;
            }

            return ConnectionStatus.CONNECTED;
        }

        public bool SetBaudrateTo115200()
        {
            if(!Transmit("ATPP0CSV23"))
            {
                return false;
            }
            if(Receive(1000, true, false) != "OK")
            {
                return false;
            }

            if(!Transmit("ATPP0CON"))
            {
                return false;
            }
            if(Receive(1000, true, false) != "OK")
            {
                return false;
            }

            return true;
        }

        public string ReadBatteryVoltage()
        {
            Transmit("ATRV");
            return Receive(1000, true, false);
        }

        public StandardPids.Pids[] ListSupportedPids()
        {
            List<StandardPids.Pids> supportedPids = new List<StandardPids.Pids>();

            // const string testStr = "41 00 BE 3E F8 11";
            
            byte offset = 0;
            UInt32 bitmask;
            do
            {
                Transmit($"01{offset.ToString("X2")}");
                string res = Receive(1000, false, false); // result or SEARCHING...
                if(res == "SEARCHING...")
                {
                    res = Receive(10000, false, false);
                }
                FlushRx();

                string bitmaskStr = res.Substring(6).Replace(" ", String.Empty);
                bitmask = UInt32.Parse(bitmaskStr, NumberStyles.HexNumber);

                for(byte i = offset; i < offset+31; i++)
                {
                    bool pidSupported = ((bitmask&(0x80000000>>i)) != 0);
                    if(pidSupported)
                    {
                        supportedPids.Add((StandardPids.Pids)(i+1));
                    }
                    Console.WriteLine($"{(StandardPids.Pids)(i+1)} {(pidSupported ? "yes" : "no")}");
                }
                offset+=32;
            }
            while(((bitmask&1) != 0));

            return supportedPids.ToArray();
        }

        public MonitorStatus ReadMonitorStatusSinceDtcsCleared()
        {
            Transmit("0101");
            string res = Receive(); // result
            UInt32 bitmask = UInt32.Parse(res, NumberStyles.HexNumber);

            // const UInt32 testData = 0x00276101;
            byte A = (byte)(bitmask>>24);
            byte B = (byte)((bitmask>>16)&0xFF);
            byte C = (byte)((bitmask>>8)&0xFF);
            byte D = (byte)(bitmask&0xFF);
            
            MonitorStatus status = new MonitorStatus();

            // parse byte A
            status.MIL = (A&(1<<7)) != 0;
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
            // const UInt16 testData = 0x0BB0;
            Transmit("010C");
            string res = Receive();
            if(res == null)
            {
                return 0;
            }
            UInt16 rawRpm = UInt16.Parse(res, NumberStyles.HexNumber);
            return (UInt16)(rawRpm>>2);
        }

        public byte ReadVehicleSpeed()
        {
            Transmit("010D");
            string res = Receive();
            if(res == null)
            {
                return 0;
            }
            return byte.Parse(res, NumberStyles.HexNumber);
        }

        public byte ReadIntakeManifoldAbsolutePressure()
        {
            //const byte testByte = 0x25;
            Transmit("010B");
            string res = Receive();
            if(res == null)
            {
                return 0;
            }
            return byte.Parse(res, NumberStyles.HexNumber);
        }

        public UInt16 ReadDistanceTravelledWithMILOn()
        {
            Transmit("0121");
            string res = Receive();
            if(res == null)
            {
                return 0;
            }
            return UInt16.Parse(res, NumberStyles.HexNumber);
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
            //const byte testByte = 126;
            Transmit("0104");
            string res = Receive();
            if(res == null)
            {
                return 0;
            }
            byte rawLoad = byte.Parse(res, NumberStyles.HexNumber);
            return (byte)(rawLoad/2.55f);
        }

        public byte ReadThrottlePosition()
        {
            //const byte testByte = 126;
            Transmit("0111");
            string res = Receive();
            if(res == null)
            {
                return 0;
            }
            byte rawLoad = byte.Parse(res, NumberStyles.HexNumber);
            return (byte)(rawLoad/2.55f);
        }

        public Int16 ReadEngineCoolantTemperature()
        {
            //const byte testByte = 0x35;
            Transmit("0105");
            string res = Receive();
            if(res == null)
            {
                return 0;
            }
            byte rawTemp = byte.Parse(res, NumberStyles.HexNumber);
            return (byte)(rawTemp - 40);
        }

        public Int16 ReadIntakeAirTemperature()
        {
            //const byte testByte = 0x35;
            Transmit("010F");
            string res = Receive();
            if(res == null)
            {
                return 0;
            }
            byte rawTemp = byte.Parse(res, NumberStyles.HexNumber);
            return (byte)(rawTemp - 40);
        }

        private bool Transmit(string cmd, bool echo=true)
        {
            try
            {
                //Console.WriteLine($"Tx: {cmd}");
                _serialPort.WriteLine(cmd);
                if(echo)
                {
                    return Receive(1000, false, false).Replace(">", "") == cmd; // echo (sometimes with > prompt)
                }
                return true;
            }
            catch(Exception e)
            {
                Console.WriteLine($"Transmit error: {e}");
                return false;
            }
        }

        private string Receive(int timeout=1000, bool flush=true, bool trim=true)
        {
            try
            {
                //Console.WriteLine($"Rx timeout: {timeout}");
                _serialPort.ReadTimeout = timeout;
                string rx = _serialPort.ReadLine();
                //Console.WriteLine($"Rx: {rx}");
                if(flush)
                {
                    FlushRx();
                }
                if(trim)
                {
                    rx = rx.Substring(6).Replace(" ", String.Empty);
                }
                return rx;
            }
            catch(Exception e)
            {
                Console.WriteLine($"Receive error: {e}");
                return null;
            }
        }

        private void FlushRx()
        {
            _serialPort.ReadTimeout = 100;
            try
            {
                while(true)
                {
                    // Read and discard anything till timeout
                    _serialPort.ReadLine();
                }
            }
            catch(TimeoutException)
            {
                //Console.WriteLine("Rx was flushed");
            }
            catch(Exception e)
            {
                Console.WriteLine($"Flush Rx error: {e}");
            }
        }
    }
}