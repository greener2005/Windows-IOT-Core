namespace GreenersScarySkull.BlueMix
{
    using System;
    using System.Diagnostics;
    using System.Text;
    using uPLibrary.Networking.M2Mqtt;
    using uPLibrary.Networking.M2Mqtt.Exceptions;

    /// <summary>
    /// Very quick implementation of MQTT for bluemix.  You can download a proper class that has all the features for this class.
    /// I just quickly wrote this up for demo purposes showing how easy it is to get bluemix working.
    /// </summary>
    public sealed class DeviceClient
    {
        private string _clientUsername;
        private string _clientPassword;
        private string _clientId;
        private string _orgId;
        internal static readonly string CLIENT_ID_DELIMITER = ":";
        internal static readonly string DOMAIN = ".messaging.internetofthings.ibmcloud.com";
        internal static readonly int MQTTS_PORT = 8883;
        internal MqttClient mqttClient;

        public DeviceClient(string orgId, string deviceType, string deviceID, string authtoken)
        {
            DeviceClientConnect(orgId, "d" + CLIENT_ID_DELIMITER + orgId + CLIENT_ID_DELIMITER + deviceType + CLIENT_ID_DELIMITER + deviceID, "use-token-auth", authtoken);
        }

        public void publishEvent(string evt, string format, string msg, byte qosLevel)
        {
            string topic = "iot-2/evt/" + evt + "/fmt/" + format;
            mqttClient.Publish(topic, Encoding.UTF8.GetBytes(msg), qosLevel, false);
        }

        public void publishEvent(string evt, string format, string msg)
        {
            publishEvent(evt, format, msg, 0);
        }

        public void DeviceClientConnect(string orgid, string clientId, string userName, string password)
        {
            _clientId = clientId;
            _clientUsername = userName;
            _clientPassword = password;
            _orgId = orgid;

            string hostName = orgid + DOMAIN;

            mqttClient = new MqttClient(hostName);
        }

        public void connect()
        {
            try
            {
                if (_orgId == "quickstart")
                {
                    mqttClient.Connect(_clientId);
                }
                else
                {
                    mqttClient.Connect(_clientId, _clientUsername, _clientPassword);
                }
            }
            catch (MqttClientException e)
            {
                Debug.WriteLine(e.Message);
            }
        }

        public void disconnect()
        {
            //Trace.WriteLine("Disconnecting from the IBM Internet of Things Foundation ...");
            try
            {
                mqttClient.Disconnect();

                //Trace.WriteLine("Successfully disconnected from the IBM Internet of Things Foundation");
            }
            catch (InvalidCastException e)
            {
                throw new Exception(e.Message);
            }
        }

        public bool isConnected()
        {
            return mqttClient.IsConnected;
        }

        public string toString()
        {
            return "[" + _clientId + "] " + "Connected = " + isConnected();
        }
    }
}