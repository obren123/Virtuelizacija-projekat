using Server.Services;
using System;
using System.ServiceModel;

namespace Server
{
    class Program
    {
        static void Main(string[] args)
        {
            var weatherService = new WeatherService();

            weatherService.OnTransferStarted += (sender, sessionId) =>
            {
                WriteEvent($"PRENOS ZAPOČET: {sessionId}");
            };

            weatherService.OnSampleReceived += (sender, sample) =>
            {
                WriteEvent($"UZORAK PRIMLJEN u {sample.Date:yyyy-MM-dd HH:mm:ss}");
            };

            weatherService.OnTransferCompleted += (sender, sessionId) =>
            {
                WriteEvent($"PRENOS ZAVRŠEN: {sessionId}");
            };

            weatherService.OnWarningRaised += (sender, warning) =>
            {
                WriteEvent($"UPOZORENJE: {warning}");
            };

            weatherService.OnSHSpike += (sender, message) =>
            {
                WriteEvent($"SH SPIKE DETEKTOVAN: {message}");
            };

            weatherService.OnHISpike += (sender, message) =>
            {
                WriteEvent($"HI SPIKE DETEKTOVAN: {message}");
            };

            weatherService.OnOutOfBandWarning += (sender, message) =>
            {
                WriteEvent($"SH OUT OF BOUNDS: {message}");
            };

            using (ServiceHost host = new ServiceHost(typeof(WeatherService)))
            {
                host.Opened += (s, e) =>
                {
                    Console.WriteLine("===========================================");
                    Console.WriteLine("    SERVER VREMENSKE STANICE POKRENUT      ");
                    Console.WriteLine("===========================================");
                    Console.ResetColor();
                    Console.WriteLine("Slušam na: net.tcp://localhost:8000/WeatherService");
                    Console.WriteLine("Pritisnite ENTER da zaustavite servis...\n");
                };

                try
                {
                    host.Open();
                    Console.ReadLine();
                    host.Close();
                }
                catch (Exception ex)
                {
                    WriteEvent($"GREŠKA: {ex.Message}");
                    host.Abort();
                }
            }
        }

        static void WriteEvent(string message)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        }
    }
}
