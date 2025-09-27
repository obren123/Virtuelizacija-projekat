using Server.Services;
using System;
using System.ServiceModel;

namespace WeatherStationServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var weatherService = new WeatherService();

            weatherService.OnTransferStarted += (sender, sessionId) =>
            {
                WriteEvent($"TRANSFER STARTED: {sessionId}", ConsoleColor.Green);
            };

            weatherService.OnSampleReceived += (sender, sample) =>
            {
                WriteEvent($"SAMPLE RECEIVED at {sample.Date:yyyy-MM-dd HH:mm:ss}", ConsoleColor.Cyan);
            };

            weatherService.OnTransferCompleted += (sender, sessionId) =>
            {
                WriteEvent($"TRANSFER COMPLETED: {sessionId}", ConsoleColor.Green);
            };

            weatherService.OnWarningRaised += (sender, warning) =>
            {
                WriteEvent($"WARNING: {warning}", ConsoleColor.Yellow);
            };

            using (ServiceHost host = new ServiceHost(typeof(WeatherService)))
            {
                host.Opened += (s, e) =>
                {
                    Console.WriteLine("===========================================");
                    Console.WriteLine("      WEATHER SERVICE SERVER STARTED       ");
                    Console.WriteLine("===========================================");
                    Console.ResetColor();
                    Console.WriteLine("Listening on: net.tcp://localhost:8000/WeatherService");
                    Console.WriteLine("Press ENTER to stop the service...\n");
                };

                try
                {
                    host.Open();
                    Console.ReadLine();
                    host.Close();
                }
                catch (Exception ex)
                {
                    WriteEvent($"ERROR: {ex.Message}", ConsoleColor.Red);
                    host.Abort();
                }
            }
        }

        static void WriteEvent(string message, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
            Console.ResetColor();
        }
    }
}
