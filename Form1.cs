using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.WebSockets;
using System.Diagnostics;
using System.Windows.Forms;
using System.Threading;
namespace algetiming_streamer
{
    public partial class TimeStreamerForm : Form
    {
        private Alge.TimyUsb timyUsb;
        private ClientWebSocket webSocket;
#if DEBUG
        static String WSURI = "ws://127.0.0.1:8080/time";
#else
        private static String WSURI = "ws://default-dot-streaming-clock.appspot.com/time";
#endif
        //file to write resuts, just in case
        private String fileName = "climbing_results.txt";
        private System.IO.StreamWriter resultsFile;

        //keepalive timer, so the connection won't stop
        private System.Windows.Forms.Timer keepaliveTimer;
        public static int SERVERKEEPALIVE = 60 * 1000;

        //retry-system for failed messages
        public static int RETRYTIME = 500;
        private List<String> failedMessageList;
        private System.Windows.Forms.Timer retryTimer;

        //the connection seeems to be alive for an hour, reconstruct it again before it goes bad
        public static int SERVERRECONNECTTIMER = 55 * 60 * 1000 + 500;
        private System.Windows.Forms.Timer serverReconnectTimer;

        public TimeStreamerForm()
        {
            InitializeComponent();
            failedMessageList = new List<String>();
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            String filename = System.IO.Path.GetDirectoryName(Application.ExecutablePath) +"\\"+ fileName;
            if (!File.Exists(filename))
            {
                resultsFile = File.CreateText(filename);
            }
            else
            {
                resultsFile = File.AppendText(filename);
            }
            webSocket = new ClientWebSocket();
            webSocket.ConnectAsync(new Uri(WSURI), CancellationToken.None);

            //start the timyUSB stuff
            timyUsb = new Alge.TimyUsb(this);
            timyUsb.Start();

            timyUsb.DeviceConnected += new EventHandler<Alge.DeviceChangedEventArgs>(timyUsb_DeviceConnected);
            timyUsb.DeviceDisconnected += new EventHandler<Alge.DeviceChangedEventArgs>(timyUsb_DeviceDisconnected);
            timyUsb.PnPDeviceAttached += new EventHandler(timyUsb_PnPDeviceAttached);
            timyUsb.PnPDeviceDetached += new EventHandler(timyUsb_PnPDeviceDetached);
       
            timyUsb.LineReceived += new EventHandler<Alge.DataReceivedEventArgs>(timyUsb_LineReceived);
            timyUsb.HeartbeatReceived += new EventHandler<Alge.HeartbeatReceivedEventArgs>(timyUsb_HeartbeatReceived);


            
            keepaliveTimer = new System.Windows.Forms.Timer();
            keepaliveTimer.Interval = SERVERKEEPALIVE;
            keepaliveTimer.Tick += new EventHandler(keepaliveTimer_Tick);
            keepaliveTimer.Start();

            retryTimer = new System.Windows.Forms.Timer();
            retryTimer.Interval = RETRYTIME;
            retryTimer.Tick += new EventHandler(retryTick);

            serverReconnectTimer = new System.Windows.Forms.Timer();
            serverReconnectTimer.Interval = SERVERRECONNECTTIMER;
            serverReconnectTimer.Tick += new EventHandler(keepTheConnectionAlive);
            serverReconnectTimer.Start();
#if DEBUG
            AddLogLine("Starting app in DEBUG");
#else
            AddLogLine("Starting app in RELEASE");
#endif
            AddLogLine("Process is " + (IntPtr.Size == 8 ? "x64" : "x86"));
        }

        private void reConnectWebSocket()
        {
            try
            {
                //socket was not healthy
                webSocket.Dispose();
                webSocket = new ClientWebSocket();
                webSocket.ConnectAsync(new Uri(WSURI), CancellationToken.None);
                AddLogLine("Created a new websocket");
            }
        
            catch (Exception ex)
            {
                AddLogLine("reconnecting the websocket failed" + ex.Message);
            }
        }

        private void keepTheConnectionAlive(object sender, EventArgs e)
        {
            //the connection must be re-created at sometimes to keep things on-going
            reConnectWebSocket();
        }

        private void retryTick(object sender, EventArgs e)
        {
            AddLogLine("Failed stuff, retrying");
            if (webSocket.State != WebSocketState.Open)
            {
                return;
            }
            if (failedMessageList.Count() > 0)
            {
                String message = failedMessageList[0];
                byte[] sendBody = Encoding.UTF8.GetBytes(message);
                try
                {
                    webSocket.SendAsync(new ArraySegment<byte>(sendBody), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    AddLogLine("Retry send message exception: " + ex.Message);
                }
                //ok, message sent, remove it from the LIST
                AddLogLine("RetrySent for message " + message+ " succeeded");
                failedMessageList.RemoveAt(0);
            }
            else
            {
                //no data in the buffer, stop timer
                retryTimer.Stop();
            }
        }
        private void keepaliveTimer_Tick(object sender, EventArgs e)
        {
            //Just write something to keep the connection alive, 
            //for PING I would have to read for PONG, I don't want to overcomplicate this C# implementation
            var message = "PING";
            byte[] sendBody = Encoding.UTF8.GetBytes(message);
            if (webSocket.State != WebSocketState.Open)
            {
                reConnectWebSocket();
                return; //no need to send anything this time
            }
            try
            {
                webSocket.SendAsync(new ArraySegment<byte>(sendBody), WebSocketMessageType.Text, true, CancellationToken.None);
                AddLogLine("PING");
            }
            catch(Exception ex)
            {
                AddLogLine("keepalive exception: "+ ex.Message);
                failedMessageList.Add(message);
                retryTimer.Start();
            }
        }

        void timyUsb_HeartbeatReceived(object sender, Alge.HeartbeatReceivedEventArgs e)
        {
            Debug.WriteLine("Heartbeat: " + e.Time.ToString());
        }

        void timyUsb_PnPDeviceDetached(object sender, EventArgs e)
        {
            AddLogLine("Device detached");
        }

        void timyUsb_PnPDeviceAttached(object sender, EventArgs e)
        {
            AddLogLine("Device attached");
        }


        void timyUsb_LineReceived(object sender, Alge.DataReceivedEventArgs e)
        {
            Debug.WriteLine("Device " + e.Device.Id + " Line: " + e.Data);
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
                    writeResults("bib: " + bib + " false start");
                } else if (cmd.Equals("lC5"))
                {
                    //left side FALSE START
                    webMessage = "slf";
                    writeResults("bib: " + bib + " false start");
                }
                else if (cmd.Equals("rc1"))
                {
                    //right side finished
                    time = getTimeinMS(data.Substring(10,13));
                    webMessage = "ssr"+time+"b"+bib;
                    Debug.WriteLine(time);
                    writeResults("bib: " + bib + " time: "+ data.Substring(10, 13));
                }
                else if(cmd.Equals("lc4"))
                {
                    //left side finished
                    time = getTimeinMS(data.Substring(10, 13));
                    webMessage = "ssl" + time + "b" + bib;
                    Debug.WriteLine(time);
                    writeResults("bib: " + bib + " time: " + data.Substring(10, 13));
                }
            }
            //did we actually construct a command
            if (webMessage != null)
            {
                byte[] sendBody = Encoding.UTF8.GetBytes(webMessage);
                if (webSocket.State != WebSocketState.Open)
                {
                    reConnectWebSocket();
                }

                try
                {
                    //Send the message
                    webSocket.SendAsync(new ArraySegment<byte>(sendBody), WebSocketMessageType.Text, true, CancellationToken.None);
                    resetTimer(); //connection is alive, so reset the timer
                } catch (Exception ex)
                {
                    AddLogLine("Websocket send mesage failed: " + ex.Message);
                    failedMessageList.Add(webMessage);
                    retryTimer.Start();
                }
            }
        }

        private void resetTimer()
        {
            keepaliveTimer.Stop();
            keepaliveTimer.Start();
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
            status.Items.Insert(0, str);
        }

        private void writeResults(String str)
        {
            results.Items.Insert(0, str);
            resultsFile.WriteLine(str);
            resultsFile.Flush();
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
            timyUsb.PnPDeviceAttached -= new EventHandler(timyUsb_PnPDeviceAttached);
            timyUsb.PnPDeviceDetached -= new EventHandler(timyUsb_PnPDeviceDetached);
            timyUsb.HeartbeatReceived -= new EventHandler<Alge.HeartbeatReceivedEventArgs>(timyUsb_HeartbeatReceived);
            resultsFile.Flush();
            resultsFile.Close();
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void listBox1_SelectedIndexChanged_1(object sender, EventArgs e)
        {

        }
    }
}
