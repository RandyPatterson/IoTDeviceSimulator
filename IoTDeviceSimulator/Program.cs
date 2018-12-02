using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace IoTDeviceSimulator
{
    /// <summary>
    /// Device Simulator that generates a random temperature and sends telemetry to an IoT Hub
    /// </summary>
    internal class Program
    {
        private static DeviceClient deviceClient;
        private const string deviceConnString = "[replace with your connection string]";
        private const string deviceId = "DevSim01";
        private static readonly TwinCollection twinProperties = new TwinCollection();
        private static volatile int freq = 5000;
        private static volatile bool _sendTelemetry = true;
        private static decimal _currentTemperature = 26; //starting temperature

        public static void Main(string[] args)
        {
            ConsoleWrite("Simulated device. Press any key to exit.\n");
            deviceClient = DeviceClient.CreateFromConnectionString(deviceConnString, TransportType.Mqtt);
            deviceClient.ProductInfo = "IoT Workshop Simulated Device";

            //Start sending Telemetry as a background thread
            SendDeviceToCloudMessagesAsync();

            //Start receiving Cloud to Device messages as a background thread
            ReceiveCloudToDeviceAsync();

            //Setup Direct Methods
            deviceClient.SetMethodHandlerAsync("start", StartTelemetry, null);
            deviceClient.SetMethodHandlerAsync("stop", StopTelemetry, null);
            deviceClient.SetMethodHandlerAsync("upload", UploadFileAsync, null);
            deviceClient.SetMethodHandlerAsync("temperature", SetTemperature, null);

            //Set callback method when Desired Twin property changes
            deviceClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChanged, null).Wait();

            Console.ReadKey();
        }

        /// <summary>
        ///     Background task for sending telemetry using frequency from Reported property 'freq'
        /// </summary>
        private static async void SendDeviceToCloudMessagesAsync()
        {
            int messageId = 1;
            Random rand = new Random(DateTime.Now.Millisecond);

            while (true)
            {
                if (_sendTelemetry)
                {
                    //Generate a random number
                    decimal delta = (decimal)Math.Round(rand.NextDouble(), 2);
                    //Odd numbers positive, Even negative
                    delta *= (delta * 100 % 2 == 0 ? -1 : 1);
                    //increase or decrease the temperature by a random amount
                    _currentTemperature += delta;

                    var telemetryDataPoint = new
                    {
                        deviceId = deviceId,
                        messageId = messageId++,
                        temperature = _currentTemperature,
                    };

                    //Convert telemetry to JSON format
                    string messageString = JsonConvert.SerializeObject(telemetryDataPoint);
                    var message = new Message(Encoding.ASCII.GetBytes(messageString));

                    //Send Message to IoT Hub
                    try
                    {
                        await deviceClient.SendEventAsync(message);
                        ConsoleWrite($"{DateTime.Now} > Sending message: {messageString}");
                    }
                    catch (Exception ex)
                    {
                        ConsoleWrite($"Error: {ex.Message}", ConsoleColor.Red);
                    }
                }

                await Task.Delay(freq);
            }
        }

        /// <summary>
        ///     Background task for receiving Cloud to Device messages.
        /// </summary>
        private static async void ReceiveCloudToDeviceAsync()
        {
            while (true)
            {
                //Receive C2D Message, timeout after 10 seconds
                Message receivedMessage = await deviceClient.ReceiveAsync(TimeSpan.FromSeconds(10));
                if (receivedMessage == null)
                    continue;

                string payload = Encoding.ASCII.GetString(receivedMessage.GetBytes());
                ConsoleWrite($"\n{payload}", ConsoleColor.Yellow);
                //Let IoT Hub know message was successfully received
                await deviceClient.CompleteAsync(receivedMessage);
            }
        }

        /// <summary>
        /// Direct Method to stop sending telemetry
        /// </summary>
        /// <param name="methodRequest"></param>
        /// <param name="userContext"></param>
        /// <returns></returns>
        private static async Task<MethodResponse> StopTelemetry(MethodRequest methodRequest, object userContext)
        {
            _sendTelemetry = false;
            ConsoleWrite($"\n{ DateTime.Now} Telemetry Stopped", ConsoleColor.Blue);

            //Send response back to caller
            var result = "{'message':'Stop Succeeded'}";
            return new MethodResponse(Encoding.UTF8.GetBytes(result), 200);
        }

        /// <summary>
        /// Direct Method to start sending telemetry
        /// </summary>
        /// <param name="methodRequest"></param>
        /// <param name="userContext"></param>
        /// <returns></returns>
        private static async Task<MethodResponse> StartTelemetry(MethodRequest methodRequest, object userContext)
        {
            _sendTelemetry = true;
            ConsoleWrite($"\n{ DateTime.Now} Telemetry Started", ConsoleColor.Blue);

            //Send response back to caller
            var result = "{'message':'Start Succeeded'}";
            return new MethodResponse(Encoding.UTF8.GetBytes(result), 200);
        }

        /// <summary>
        /// Direct Method to upload files
        /// </summary>
        /// <param name="methodRequest"></param>
        /// <param name="userContext"></param>
        /// <returns></returns>
        private static async Task<MethodResponse> UploadFileAsync(MethodRequest methodRequest, object userContext)
        {
            string fileName = @"image1.jpg";
            string blobName = $"image-{DateTime.UtcNow.ToString("s")}.jpg";
            using (var sourceData = new FileStream(fileName, FileMode.Open))
            {
                //Upload file to blob storage
                await deviceClient.UploadToBlobAsync(blobName, sourceData);
                ConsoleWrite($"Uploaded to blob file: {blobName}", ConsoleColor.Blue);
            }
            var message = $"'File {fileName} was uploaded to blob storage as {blobName}'";
            //Return success to IoT Hub
            return new MethodResponse(Encoding.UTF8.GetBytes(message), 200);
        }

        /// <summary>
        /// Direct method to change the current temperature. Useful for testing alerts
        /// </summary>
        /// <param name="methodRequest"></param>
        /// <param name="userContext"></param>
        /// <returns></returns>
        private static async Task<MethodResponse> SetTemperature(MethodRequest methodRequest, object userContext)
        {
            var result = "{'message':'Set Temperature Succeeded'}";
            decimal payload;
            if (decimal.TryParse(methodRequest.DataAsJson, out payload))
            {
                _currentTemperature = payload;
                ConsoleWrite($"Set Temperature to {payload}", ConsoleColor.Green);
                return new MethodResponse(Encoding.UTF8.GetBytes(result), 200);
            }
            else
            {
                result = "{'message':'Set Temperature Failed: Temperature not passed in or not a number'}";
                ConsoleWrite(result, ConsoleColor.Red);
                return new MethodResponse(Encoding.UTF8.GetBytes(result), 400);
            }


        }

        /// <summary>
        ///     Notification when a Desired Twin Property Changes
        /// </summary>
        /// <param name="desiredProperties"></param>
        /// <param name="userContext"></param>
        /// <returns></returns>
        private static async Task OnDesiredPropertyChanged(TwinCollection desiredProperties, object userContext)
        {
            ConsoleWrite("\nDesired property change:", ConsoleColor.Green);
            ConsoleWrite(JsonConvert.SerializeObject(desiredProperties), ConsoleColor.Green);

            //Update telemetry generation frequency if Desired property 'freq' exists
            if (desiredProperties.Contains("freq"))
            {
                int freq = (int)desiredProperties["freq"];
                if (freq > 0)
                {
                    ConsoleWrite($"Telemetry frequency changed to {freq}ms", ConsoleColor.Green);
                    Program.freq = freq;
                    //Update reported property to reflect new telemetry frequency
                    twinProperties["freq"] = Program.freq;
                    deviceClient.UpdateReportedPropertiesAsync(twinProperties).Wait();
                }
            }
        }

        /// <summary>
        /// helper method to display messages in color to the console. Resets to original text color when finished
        /// </summary>
        /// <param name="message">Message to display</param>
        /// <param name="color">The <see cref="ConsoleColor">Color</see> to display text 
        /// or <see cref="ConsoleColor.White">White</see> if not specified</param>
        private static void ConsoleWrite(string message, ConsoleColor color = ConsoleColor.White)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }
}