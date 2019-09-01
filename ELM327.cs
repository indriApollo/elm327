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
                if(!versionString.StartsWith("ELM327"))
                {
                    return ConnectionStatus.ELM_NOT_DETECTED;
                }
            }
            catch(TimeoutException)
            {
                return ConnectionStatus.ELM_NOT_DETECTED;
            }

            return ConnectionStatus.CONNECTED;
        }
    }
}