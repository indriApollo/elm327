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

            elm.ListSupportedPids();
            Console.WriteLine($"Distance travelled with MIL on: {elm.ReadDistanceTravelledWithMILOn()}");

            var mstatus = elm.ReadMonitorStatusSinceDtcsCleared();
            Console.WriteLine($"MIL/CEL: {mstatus.MIL}");
            Console.WriteLine($"DTC count: {mstatus.DTC_CNT}");

            bool run = true;
            while(run)
            {
                Console.WriteLine($"Battery voltage: {elm.ReadBatteryVoltage()}");
                Console.WriteLine($"RPM: {elm.ReadEngineRPM()}");
                Console.WriteLine($"Speed: {elm.ReadVehicleSpeed()} Kph");
                Console.WriteLine($"Coolant: {elm.ReadEngineCoolantTemperature()} C");
                Console.WriteLine($"Intake air temperature: {elm.ReadIntakeAirTemperature()} C");
                Console.WriteLine($"Engine load: {elm.ReadCalculatedEngineLoad()} %");
                Console.WriteLine($"Throttle: {elm.ReadThrottlePosition()} %");
                Console.WriteLine($"Intake manifold absolute  pressure: {elm.ReadIntakeManifoldAbsolutePressure()} Kpa");
            }

            return 0;
        }
    }
}
