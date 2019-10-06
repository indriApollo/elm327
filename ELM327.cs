using System;
using System.IO.Ports;

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
        }

        public struct MonitorStatus
        {
            public bool MIL { get; set; }
            public byte DTC_CNT { get; set; }
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
                bool pidSupported = (((bitmask << i)&0x80000000) != 0);
                Console.WriteLine($"{(StandardPids.Pids)(i+1)} {(pidSupported ? "yes" : "no")}");
            }
        }

        public MonitorStatus ReadMonitorStatusSinceDtcsCleared()
        {
            //
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