using System;

namespace IndriApollo.elm327
{
    class Program
    {
        static int Main(string[] args)
        {
            var elm = new ELM327OBD2();
            elm.ListSupportedPids();
            Console.WriteLine(elm.ReadCalculatedEngineLoad());
            Console.WriteLine(elm.ReadEngineCoolantTemperature());
            var fss = elm.ReadFuelSystemStatus();
            Console.WriteLine(fss[0]);
            Console.WriteLine(fss[1]);
            Console.WriteLine(elm.ReadEngineRPM());
            var status = elm.ReadMonitorStatusSinceDtcsCleared();
            Console.WriteLine($"MIL/CEL: {status.MIL}");
            Console.WriteLine($"DTC count: {status.DTC_CNT}");
            Console.WriteLine($"Ignition type: {status.IGNITION_TYPE}");
            for(byte i = 0; i < status.TESTS.Length; i++)
            {
                Console.WriteLine($"Component: {status.TESTS[i].Component}");
                Console.WriteLine($"Test available: {(status.TESTS[i].TestAvailable ? "yes" : "no")}");
                Console.WriteLine($"Test incomplete: {(status.TESTS[i].TestIncomplete ? "yes" : "no")}");
            }
            /*if(args.Length != 1)
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

            Console.WriteLine(elm.ReadBatteryVoltage());*/

            return 0;
        }
    }
}
