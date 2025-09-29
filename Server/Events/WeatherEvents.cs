using Common;
using System;

namespace Server.Events
{
    public class WeatherEvents
    {
        public event EventHandler<string> OnTransferStarted;
        public event EventHandler<WeatherSample> OnSampleReceived;
        public event EventHandler<string> OnTransferCompleted;
        public event EventHandler<string> OnWarningRaised;
        public event EventHandler<string> SHSpike;            
        public event EventHandler<string> OutOfBandWarning;
        public event EventHandler<string> HISpike;


        public void RaiseOnTransferStarted(string sessionId)
        {
            Console.WriteLine($"Transfer započet: {sessionId}");
            OnTransferStarted?.Invoke(this, sessionId);
        }

        public void RaiseOnSampleReceived(WeatherSample sample)
        {
            Console.WriteLine($"Uzorak primljen: T={sample.T}, Sh={sample.sh}");
            OnSampleReceived?.Invoke(this, sample);
        }

        public void RaiseHISpike(string message)
        {
            Console.WriteLine(message);
            HISpike?.Invoke(this, message);
        }

        public void RaiseOnTransferCompleted(string sessionId)
        {
            Console.WriteLine($"Transfer završen: {sessionId}");
            OnTransferCompleted?.Invoke(this, sessionId);
        }

        public void RaiseOnWarningRaised(string warning)
        {
            Console.WriteLine($"Upozorenje: {warning}");
            OnWarningRaised?.Invoke(this, warning);
        }

        public void RaiseSHSpike(string message)
        {
            Console.WriteLine(message);
            SHSpike?.Invoke(this, message);
        }

        public void RaiseOutOfBandWarning(string message)
        {
            Console.WriteLine(message);
            OutOfBandWarning?.Invoke(this, message);
        }
    }
}
