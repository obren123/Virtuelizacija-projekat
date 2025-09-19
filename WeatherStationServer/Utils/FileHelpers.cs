using System;
using System.Collections.Generic;
using System.IO;

namespace Server.Utils
{
    public static class FileHelpers
    {
        public static void CreateWriters(
            string dataDirectory,
            string sessionId,
            out StreamWriter measurementsWriter,
            out StreamWriter rejectsWriter)
        {
            Directory.CreateDirectory(dataDirectory);

            string measurementsFile = Path.Combine(dataDirectory, $"{sessionId}_measurements.csv");
            string rejectsFile = Path.Combine(dataDirectory, $"{sessionId}_rejects.csv");

            measurementsWriter = new StreamWriter(measurementsFile, true);
            rejectsWriter = new StreamWriter(rejectsFile, true);

            measurementsWriter.WriteLine("T,Tpot,Tdew,Sh,Rh,Date");
            rejectsWriter.WriteLine("T,Tpot,Tdew,Sh,Rh,Date,Reason");

            Console.WriteLine($"Session started: {sessionId}");
            Console.WriteLine($"Measurement file: {measurementsFile}");
            Console.WriteLine($"Rejects file: {rejectsFile}");
        }

        public static void CloseWriters(
            Dictionary<string, StreamWriter> measurementsWriters,
            Dictionary<string, StreamWriter> rejectsWriters,
            string sessionId)
        {
            if (measurementsWriters.ContainsKey(sessionId))
            {
                measurementsWriters[sessionId]?.Close();
                measurementsWriters[sessionId]?.Dispose();
                measurementsWriters.Remove(sessionId);
            }

            if (rejectsWriters.ContainsKey(sessionId))
            {
                rejectsWriters[sessionId]?.Close();
                rejectsWriters[sessionId]?.Dispose();
                rejectsWriters.Remove(sessionId);
            }
        }

        public static void DisposeAll(
            Dictionary<string, StreamWriter> measurementsWriters,
            Dictionary<string, StreamWriter> rejectsWriters)
        {
            foreach (var writer in measurementsWriters.Values)
            {
                writer?.Close();
                writer?.Dispose();
            }

            foreach (var writer in rejectsWriters.Values)
            {
                writer?.Close();
                writer?.Dispose();
            }

            measurementsWriters.Clear();
            rejectsWriters.Clear();
        }
    }
}
