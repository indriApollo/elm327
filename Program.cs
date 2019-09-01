using System;

namespace IndriApollo.elm327
{
    class Program
    {
        static void Main(string[] args)
        {
            var elm = new ELM327OBD2();
            string elmVersion;
            elm.Connect("/dev/ttyUSB0", out elmVersion);
            Console.WriteLine(elmVersion);
        }
    }
}
