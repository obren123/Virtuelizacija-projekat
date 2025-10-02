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
            out FileWriter measurementsWriter,
            out FileWriter rejectsWriter)
        {
            Directory.CreateDirectory(dataDirectory);
            string measurementsFile = Path.Combine(dataDirectory, $"{sessionId}_measurements.csv");
            string rejectsFile = Path.Combine(dataDirectory, $"{sessionId}_rejects.csv");

            measurementsWriter = null;
            rejectsWriter = null;

        try
        {
            measurementsWriter = new FileWriter(measurementsFile, true);
            rejectsWriter = new FileWriter(rejectsFile, true);

            measurementsWriter.WriteLine("T,Tpot,Tdew,Sh,Rh,Date");
            rejectsWriter.WriteLine("T,Tpot,Tdew,Sh,Rh,Date,Reason");
        }
        catch
        {
             measurementsWriter?.Dispose();
             rejectsWriter?.Dispose();
             throw;
         }
    }


        public static void CloseWriters(
            Dictionary<string, FileWriter> measurementsWriters,
            Dictionary<string, FileWriter> rejectsWriters,
            string sessionId)
        {
            if (measurementsWriters.ContainsKey(sessionId))
            {
                measurementsWriters[sessionId]?.Dispose();
                measurementsWriters.Remove(sessionId);
            }
            if (rejectsWriters.ContainsKey(sessionId))
            {
                rejectsWriters[sessionId]?.Dispose();
                rejectsWriters.Remove(sessionId);
            }
        }

        public static void DisposeAll(
            Dictionary<string, FileWriter> measurementsWriters,
            Dictionary<string, FileWriter> rejectsWriters)
        {
            foreach (var w in measurementsWriters.Values) w?.Dispose();
            foreach (var w in rejectsWriters.Values) w?.Dispose();
            measurementsWriters.Clear();
            rejectsWriters.Clear();
        }

    }
}
