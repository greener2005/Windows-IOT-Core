using Microsoft.IoT.Lightning.Providers;
using System;
using System.Diagnostics;
using Windows.Devices.Spi;
using Windows.System.Threading;

namespace GreenersScarySkull.MCP3008
{
    public sealed class LightingValueArgs
    {
        public int LightValue { get; set; }
    }

    public enum ChannelConfiguration
    {
        Differental,
        SingleEnded
    }

    /// <summary>
    /// This is a very quick way of just opening up the first channel of 8 on the MCP3008 for the
    /// direct memory map drivers. Again, you could easily turn this into a re-usable library. This
    /// is just for the demo to get the analog value of the LDR
    /// </summary>
    public sealed class MCP3008DirectMemoryMapDriver
    {
        internal event EventHandler<LightingValueArgs> LightingValueChanged;

        private ThreadPoolTimer _adcTimer;
        private SpiDevice _device;

        public MCP3008DirectMemoryMapDriver()
        {
            InitMCP3008();
        }

        public void RunAdcChannel(TimeSpan timeSpan)
        {
            _adcTimer = ThreadPoolTimer.CreatePeriodicTimer(Tick, timeSpan);
        }

        private async void InitMCP3008()
        {
            try
            {
                SpiConnectionSettings spiConnectionSettings = new SpiConnectionSettings(0)
                {
                    ClockFrequency = 500000, //Rated clock speed for the MCP3008 at 5v
                    Mode = SpiMode.Mode0,
                    SharingMode = SpiSharingMode.Exclusive
                };
                SpiController controller =
                    (await SpiController.GetControllersAsync(LightningSpiProvider.GetSpiProvider()))[0];

                _device = controller.GetDevice(spiConnectionSettings);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{nameof(InitMCP3008)} threw this exception: {ex.Message}");
            }
        }

        public int ReadRawValueAtChannel(ChannelConfiguration channelConfiguration, int channel)
        {
            try
            {
                if (_device == null) return -1;

                byte[] readBuffer = new byte[3];
                byte[] writeBuffer = { (byte)channelConfiguration, (byte)(channel + 8 << 4), 0x00 };
                _device.TransferFullDuplex(writeBuffer, readBuffer);

                return ((readBuffer[1] & 3) << 8) + readBuffer[2];
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{nameof(ReadRawValueAtChannel)} threw this exception: {ex.Message}");
            }
            return -1;
        }

        private void Tick(ThreadPoolTimer sender)
        {
            try
            {
                LightingValueChanged?.Invoke(this, new LightingValueArgs { LightValue = ReadRawValueAtChannel(ChannelConfiguration.SingleEnded, 0) });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{nameof(Tick)} threw this exception: {ex.Message}");
            }
        }
    }
}