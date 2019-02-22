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
        static String WSURI = "ws://127.0.0.1:8080/time";
        // Timer keepaliveTimer;

        public static int SERVERKEEPALIVE = 60 * 1000;

        public Form1()
        {
            InitializeComponent();
            webSocket = new ClientWebSocket();
            webSocket.ConnectAsync(new Uri(WSURI), CancellationToken.None);

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
            /*
            timyUsb.BytesReceived += timyUsb_BytesReceived;
            timyUsb.RawReceived += new EventHandler<Alge.DataReceivedEventArgs>(timyUsb_RawReceived);
            timyUsb.PnPDeviceAttached += new EventHandler(timyUsb_PnPDeviceAttached);
            timyUsb.PnPDeviceDetached += new EventHandler(timyUsb_PnPDeviceDetached);
            timyUsb.HeartbeatReceived += new EventHandler<Alge.HeartbeatReceivedEventArgs>(timyUsb_HeartbeatReceived);*/

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
            String webMessage = null;
            String data = e.Data;
            String bib;
            String time = null;
            //if data starts with n, then it's bib update
            if (data.StartsWith("n"))
            {
                bib = data.Substring(1, 4);
                bib = bib.TrimStart('0');
                if (data.EndsWith("r"))
                {
                    webMessage = "sbr" + bib;
                }
                else if (data.EndsWith("l"))
                {
                    webMessage = "sbl" + bib;
                }
            } else
            {
                bib = data.Substring(1, 4);
                String cmd = data.Substring(5, 3);
                Debug.WriteLine("bib: " + bib + "cmd: " + cmd);
                //was it start:
                if (cmd.Equals("rC0")) {
                    //it is START
                    webMessage = "srs";
                } else if (cmd.Equals("lC3"))
                {
                    //started already on right-start
                    return;
                } else if (cmd.Equals("rC2"))
                {
                    //right side FALSE START
                    webMessage = "srf";

                } else if (cmd.Equals("lC5"))
                {
                    //left side FALSE START
                    webMessage = "slf";

                }
                else if (cmd.Equals("rc1"))
                {
                    //right side finished
                    time = getTimeinMS(data.Substring(10,13));
                    webMessage = "ssr"+time+"b"+bib;
                    Debug.WriteLine(time);
                }
                else if(cmd.Equals("lc4"))
                {
                    //left side finished
                    time = getTimeinMS(data.Substring(10, 13));
                    webMessage = "ssl" + time + "b" + bib;
                    Debug.WriteLine(time);
                }
            }
            //did we actually construct a command
            if (webMessage != null)
            {
                byte[] sendBody = Encoding.UTF8.GetBytes(webMessage);
                webSocket.SendAsync(new ArraySegment<byte>(sendBody), WebSocketMessageType.Text, true, CancellationToken.None);
            }
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
        private String getTimeinMS(String time)
        {
            String min = time.Substring(3, 2);
            String sec = time.Substring(6, 2);
            String rest = time.Substring(9, 3);
            int total = Int32.Parse(min) * 60 * 1000 + Int32.Parse(sec)*1000 + Int32.Parse(rest);
            return total.ToString(); 
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            timyUsb.DeviceConnected -= new EventHandler<Alge.DeviceChangedEventArgs>(timyUsb_DeviceConnected);
            timyUsb.DeviceDisconnected -= new EventHandler<Alge.DeviceChangedEventArgs>(timyUsb_DeviceDisconnected);
            timyUsb.LineReceived -= new EventHandler<Alge.DataReceivedEventArgs>(timyUsb_LineReceived);
            /*timyUsb.BytesReceived -= timyUsb_BytesReceived;
            timyUsb.RawReceived -= new EventHandler<Alge.DataReceivedEventArgs>(timyUsb_RawReceived);
            timyUsb.PnPDeviceAttached -= new EventHandler(timyUsb_PnPDeviceAttached);
            timyUsb.PnPDeviceDetached -= new EventHandler(timyUsb_PnPDeviceDetached);
            timyUsb.HeartbeatReceived -= new EventHandler<Alge.HeartbeatReceivedEventArgs>(timyUsb_HeartbeatReceived);*/
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void listBox1_SelectedIndexChanged_1(object sender, EventArgs e)
        {

        }
    }
}
