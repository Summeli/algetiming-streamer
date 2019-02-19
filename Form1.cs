using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.WebSockets;
using System.Threading;
using System.Diagnostics;
using System.Windows.Forms;

namespace algetiming_streamer
{
    public partial class Form1 : Form
    {
        Alge.TimyUsb timyUsb;
        ClientWebSocket webSocket;
       // Timer keepaliveTimer;

        public static int SERVERKEEPALIVE = 60 * 1000;

        public Form1()
        {
            InitializeComponent();
            webSocket = new ClientWebSocket();
            webSocket.ConnectAsync(new Uri("ws://127.0.0.1:8080/time"), CancellationToken.None);

            /*
            keepaliveTimer = new Timer();
            keepaliveTimer.Interval = SERVERKEEPALIVE; // 45 mins
            keepaliveTimer.Tick += new EventHandler(keepaliveTimer_Tick);
            keepaliveTimer.Start();*/
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            timyUsb = new Alge.TimyUsb(this);
            timyUsb.Start();



            timyUsb.DeviceConnected += new EventHandler<Alge.DeviceChangedEventArgs>(timyUsb_DeviceConnected);
            timyUsb.DeviceDisconnected += new EventHandler<Alge.DeviceChangedEventArgs>(timyUsb_DeviceDisconnected);
            //I'm only reading the line, since it's enough
            timyUsb.LineReceived += new EventHandler<Alge.DataReceivedEventArgs>(timyUsb_LineReceived);
            //timyUsb.BytesReceived += timyUsb_BytesReceived;
            // timyUsb.RawReceived += new EventHandler<Alge.DataReceivedEventArgs>(timyUsb_RawReceived);
            //timyUsb.PnPDeviceAttached += new EventHandler(timyUsb_PnPDeviceAttached);
            //timyUsb.PnPDeviceDetached += new EventHandler(timyUsb_PnPDeviceDetached);
            //timyUsb.HeartbeatReceived += new EventHandler<Alge.HeartbeatReceivedEventArgs>(timyUsb_HeartbeatReceived);

            AddLogLine("Process is " + (IntPtr.Size == 8 ? "x64" : "x86"));
        }

        private void keepaliveTimer_Tick(object sender, EventArgs e)
        {
            var message = "PING";
            byte[] sendBody = Encoding.UTF8.GetBytes(message);
            webSocket.SendAsync(new ArraySegment<byte>(sendBody), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        void timyUsb_BytesReceived(object sender, Alge.BytesReceivedEventArgs e)
        {
            AddLogLine("Device " + e.Device.Id + " Bytes: " + e.Data.Length);
        }

        void timyUsb_HeartbeatReceived(object sender, Alge.HeartbeatReceivedEventArgs e)
        {
            AddLogLine("Heartbeat: " + e.Time.ToString());
        }

        void timyUsb_PnPDeviceDetached(object sender, EventArgs e)
        {
            AddLogLine("Device detached");
        }

        void timyUsb_PnPDeviceAttached(object sender, EventArgs e)
        {
            AddLogLine("Device attached");
        }

        void timyUsb_RawReceived(object sender, Alge.DataReceivedEventArgs e)
        {

                AddLogLine("Device " + e.Device.Id + " Raw: " + e.Data);

        }

        void timyUsb_LineReceived(object sender, Alge.DataReceivedEventArgs e)
        {
            AddLogLine("Device " + e.Device.Id + " Line: " + e.Data);

            //send message to the server
            var message = "Message " + e.Data;
            byte[] sendBody = Encoding.UTF8.GetBytes(message);
            webSocket.SendAsync(new ArraySegment<byte>(sendBody), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private void ProcessProgramResponse(string line)
        {
            String programString = line.Substring(6);
            AddLogLine("Active program: '" + programString + "'");
        }

        void timyUsb_DeviceDisconnected(object sender, Alge.DeviceChangedEventArgs e)
        {
            AddLogLine("Device " + e.Device.Id + " disconnected, " + timyUsb.ConnectedDevicesCount + " total connected");
        }

        void timyUsb_DeviceConnected(object sender, Alge.DeviceChangedEventArgs e)
        {
            AddLogLine("Device " + e.Device.Id + " connected, " + timyUsb.ConnectedDevicesCount + " total connected");
        }

        void AddLogLine(String str)
        {
            Debug.WriteLine(str);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            timyUsb.DeviceConnected -= new EventHandler<Alge.DeviceChangedEventArgs>(timyUsb_DeviceConnected);
            timyUsb.DeviceDisconnected -= new EventHandler<Alge.DeviceChangedEventArgs>(timyUsb_DeviceDisconnected);
            timyUsb.LineReceived -= new EventHandler<Alge.DataReceivedEventArgs>(timyUsb_LineReceived);
            //timyUsb.BytesReceived -= timyUsb_BytesReceived;
            //timyUsb.RawReceived -= new EventHandler<Alge.DataReceivedEventArgs>(timyUsb_RawReceived);
            //timyUsb.PnPDeviceAttached -= new EventHandler(timyUsb_PnPDeviceAttached);
            //timyUsb.PnPDeviceDetached -= new EventHandler(timyUsb_PnPDeviceDetached);
            //timyUsb.HeartbeatReceived -= new EventHandler<Alge.HeartbeatReceivedEventArgs>(timyUsb_HeartbeatReceived);
        }


 
    }
}
