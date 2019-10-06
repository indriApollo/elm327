using System;

namespace IndriApollo.elm327
{
    class Program
    {
        static int Main(string[] args)
        {
            if(args.Length != 1)
            {
                Console.WriteLine("Missing serial port arg");
                return 1;
            }
            
            var elm = new ELM327OBD2();
            string elmVersion;

            Console.WriteLine($"Connecting to port {args[0]} ...");
            var status = elm.Connect(args[0], out elmVersion);
            if(status != ELM327OBD2.ConnectionStatus.CONNECTED)
            {
                Console.WriteLine($"Connection error: {status}");
                return 2;
            }
            Console.WriteLine(elmVersion);

            Console.WriteLine(elm.ReadBatteryVoltage());

            return 0;
        }
    }
}
