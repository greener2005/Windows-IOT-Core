using System;

namespace GreenersScarySkull.Services.Interfaces
{
    public interface ICloudDataService : IDisposable
    {
        void PublishData(string PublishEventName, string DataItem);
    }
}
