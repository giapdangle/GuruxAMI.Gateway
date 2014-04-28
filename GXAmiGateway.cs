using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gurux.Common;
using System.Diagnostics;
using GuruxAMI.Common;
using System.Threading;
using GuruxAMI.Client;
using Gurux.Shared;
using GuruxAMI.Common.Messages;
using ServiceStack.ServiceClient.Web;
using System.ComponentModel;

namespace GuruxAMI.Gateway
{
    public class GXAmiGateway : IGXMedia, IDisposable
    {
        internal GXAmiClient Client;
        public Gurux.Communication.GXClient GXClient;        
        ClientConnectedEventHandler m_OnClientConnected;
        ClientDisconnectedEventHandler m_OnClientDisconnected;
        IGXMedia TargetMedia;
        internal ReceivedEventHandler m_OnReceived;
        internal ErrorEventHandler m_OnError;
        MediaStateChangeEventHandler m_OnMediaStateChange;
        TraceEventHandler m_OnTrace;
        Dictionary<GXAmiTask, AutoResetEvent> ExecutedEvents = new Dictionary<GXAmiTask, AutoResetEvent>();
        GXSynchronousMediaBase m_syncBase;
        bool VirtualOpen;
        readonly object m_Synchronous = new object();

        /// <summary>
        /// Default Constructor.
        /// </summary>
        public GXAmiGateway()
        {
            m_syncBase = new GXSynchronousMediaBase(1024);
            WaitTime = -1;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public GXAmiGateway(string host, int port, string user, string pw, Gurux.Communication.GXClient gxClient)
        {
            m_syncBase = new GXSynchronousMediaBase(1024);
            WaitTime = -1;
            this.Host = host;
            this.Port = port;
            this.UserName = user;
            this.Password = pw;
            GXClient = gxClient;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public GXAmiGateway(GXAmiClient client, Gurux.Communication.GXClient gxClient)
        {
            m_syncBase = new GXSynchronousMediaBase(1024);
            WaitTime = -1;
            this.Client = client;
            GXClient = gxClient;
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (Client != null)
            {
                Client.OnTasksAdded -= new TasksAddedEventHandler(Client_OnTasksAdded);
                Client.OnTasksClaimed -= new TasksClaimedEventHandler(Client_OnTasksClaimed);
            }
            Close();
        }

        #endregion

        void Client_OnTasksAdded(object sender, GXAmiTask[] tasks)
        {
            foreach (GXAmiTask it in tasks)
            {
                System.Diagnostics.Debug.Assert(it.SenderDataCollectorGuid == this.DataCollector);
                //DC notifies that state of media is changed.
                if (it.TaskType == TaskType.MediaState)
                {
                    string[] tmp = it.Data.Split(Environment.NewLine.ToCharArray());
                    int type = int.Parse(tmp[tmp.Length - 1]);
                    if (type == (int)MediaState.Open)
                    {                        
                        lock (ExecutedEvents)
                        {                     
                            foreach(var t in ExecutedEvents)
                            {
                                if (t.Key.TaskType == TaskType.MediaOpen)
                                {                                    
                                    t.Value.Set();
                                }
                            }
                        }
                        if (m_OnMediaStateChange != null)
                        {
                            m_OnMediaStateChange(this, new MediaStateEventArgs(MediaState.Open));
                        }
                    }
                    else if (type == (int)MediaState.Closed)
                    {
                        lock (ExecutedEvents)
                        {
                            foreach (var t in ExecutedEvents)
                            {
                                if (t.Key.TaskType == TaskType.MediaClose)
                                {                                    
                                    t.Value.Set();
                                }
                            }                            
                        }
                        if (m_OnMediaStateChange != null)
                        {
                            m_OnMediaStateChange(this, new MediaStateEventArgs(MediaState.Closed));
                        }
                    }
                }
                //DC sends received data from the media.
                else if (it.TaskType == TaskType.MediaWrite)
                {
                    //System.Diagnostics.Debug.WriteLine("Gateway received data: " + it.Id + " " + it.Data);
                    string[] tmp = it.Data.Split(Environment.NewLine.ToCharArray());
                    byte[] buff = Gurux.Common.GXCommon.HexToBytes(tmp[tmp.Length - 1], false);
                    int bytes = buff.Length;
                    if (IsSynchronous)
                    {
                        lock (m_syncBase.m_ReceivedSync)
                        {
                            int index = m_syncBase.m_ReceivedSize;
                            m_syncBase.AppendData(buff, 0, bytes);
                            if (Trace == TraceLevel.Verbose && m_OnTrace != null)
                            {
                                TraceEventArgs arg = new TraceEventArgs(TraceTypes.Received, buff, 0, bytes);
                                m_OnTrace(this, arg);
                            }
                            m_syncBase.m_ReceivedEvent.Set();
                        }
                    }
                    else
                    {
                        if (m_OnReceived != null)
                        {
                            m_syncBase.m_ReceivedSize = 0;
                            byte[] data = new byte[bytes];
                            Array.Copy(buff, data, bytes);
                            if (Trace == TraceLevel.Verbose && m_OnTrace != null)
                            {
                                m_OnTrace(this, new TraceEventArgs(TraceTypes.Received, data));
                            }
                            m_OnReceived(this, new ReceiveEventArgs(data, DataCollector.ToString()));
                        }
                        else if (Trace == TraceLevel.Verbose && m_OnTrace != null)
                        {
                            m_OnTrace(this, new TraceEventArgs(TraceTypes.Received, buff, 0, bytes));
                        }
                    }
                }
                //Remove task.
                Client.RemoveTask(it);
            }            
        }

        public IGXMedia Target
        {
            get
            {
                return TargetMedia;
            }
            set
            {
                if (TargetMedia != value)
                {
                    if (TargetMedia != null)
                    {
                        (TargetMedia as INotifyPropertyChanged).PropertyChanged -= new PropertyChangedEventHandler(OnMediaPropertyChanged);
                    }
                    TargetMedia = value;
                    if (TargetMedia != null)
                    {
                        (TargetMedia as INotifyPropertyChanged).PropertyChanged += new PropertyChangedEventHandler(OnMediaPropertyChanged);
                    }
                }
            }
        }

        /// <summary>
        /// Send updated media setting to the selected DC.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnMediaPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            try
            {
                // Changed properties are saved and updated when connection is established.                
                if (IsOpen)
                {
                    PropertyDescriptor prop = TypeDescriptor.GetProperties(Target)[e.PropertyName];
                    object value = prop.GetValue(Target);
                    Dictionary<string, object> properties = new Dictionary<string, object>();
                    properties.Add(e.PropertyName, value);
                    Client.SetMediaProperties(DataCollector, Target.MediaType, Target.Settings, properties);
                }
            }
            catch (Exception ex)
            {
                //TODO: Show error. GXCommon.ShowError(this, ex);
            }
        }

        #region IGXMedia Members

        /// <summary>
        /// GXGateway component sends received data through this method.
        /// </summary>
        public event ReceivedEventHandler OnReceived
        {
            add
            {
                m_OnReceived += value;
            }
            remove
            {
                m_OnReceived -= value;
            }
        }

        void NotifyError(Exception ex)
        {
            if (m_OnError != null)
            {
                m_OnError(this, ex);
            }
            if (Trace >= TraceLevel.Error && m_OnTrace != null)
            {
                m_OnTrace(this, new TraceEventArgs(TraceTypes.Error, ex));
            }
        }


        /// <summary>
        /// Errors that occur after the connection is established, are sent through this method. 
        /// </summary>       
        public event ErrorEventHandler OnError
        {
            add
            {

                m_OnError += value;
            }
            remove
            {
                m_OnError -= value;
            }
        }

        /// <summary>
        /// Media component sends notification, when its state changes.
        /// </summary>       
        public event MediaStateChangeEventHandler OnMediaStateChange
        {
            add
            {
                m_OnMediaStateChange += value;
            }
            remove
            {
                m_OnMediaStateChange -= value;
            }
        }

        /// <summary>
        /// Called when the client is establishing a connection with a Net Server.
        /// </summary>
        public event ClientConnectedEventHandler OnClientConnected
        {
            add
            {
                m_OnClientConnected += value;
            }
            remove
            {
                m_OnClientConnected -= value;
            }
        }


        /// <summary>
        /// Called when the client has been disconnected from the network server.
        /// </summary>
        public event ClientDisconnectedEventHandler OnClientDisconnected
        {
            add
            {
                m_OnClientDisconnected += value;
            }
            remove
            {
                m_OnClientDisconnected -= value;
            }
        }

        /// <inheritdoc cref="TraceEventHandler"/>
        public event TraceEventHandler OnTrace
        {
            add
            {
                m_OnTrace += value;
            }
            remove
            {
                m_OnTrace -= value;
            }
        }        


        public void Copy(object target)
        {
            throw new NotImplementedException();
        }

        string IGXMedia.Name
        {
            get
            {
                return "AMI Gateway";
            }
        }

        public System.Diagnostics.TraceLevel Trace
        {
            get
            {
                if (Target != null)
                {
                    return Target.Trace;
                }
                return TraceLevel.Off;
            }
            set
            {
                if (Target != null)
                {
                    Target.Trace = value;
                }
            }
        }

        internal void Initialize()
        {
            if (Client == null && !string.IsNullOrEmpty(Host))
            {
                string host = Host;
                if (host == "*")
                {
                    host = "localhost";
                }
                bool webServer = host.StartsWith("http://");
                if (!webServer && (string.IsNullOrEmpty(host) || Port == 0))
                {
                    throw new ArgumentException("Invalid url");
                }
                string baseUr;
                if (webServer)
                {
                    baseUr = host;
                }
                else
                {
                    baseUr = "http://" + host + ":" + Port + "/";
                }
                Client = new GXAmiClient(baseUr, UserName, Password);
                Client.OnTasksAdded += new TasksAddedEventHandler(Client_OnTasksAdded);
                Client.OnTasksClaimed += new TasksClaimedEventHandler(Client_OnTasksClaimed);
            }
        }

        public void Open()
        {
            if (!IsOpen)
            {
                lock (m_Synchronous)
                {
                    Initialize();
                    if (Target == null)
                    {
                        Gurux.Communication.GXClient cl = new Gurux.Communication.GXClient();
                        Target = cl.SelectMedia(Media);
                        if (Target == null)
                        {
                            throw new Exception(Media + " media not found.");
                        }
                        Target.Settings = MediaSettings;
                    }
                    //Reset last position if Eop is used.
                    lock (m_syncBase.m_ReceivedSync)
                    {
                        m_syncBase.m_ReceivedSize = 0;
                    }
                    VirtualOpen = true;
                }
                if (m_OnMediaStateChange != null)
                {
                    m_OnMediaStateChange(this, new MediaStateEventArgs(MediaState.Opening));
                }
                Client.StartListenEvents();
                AutoResetEvent ececuted = new AutoResetEvent(false);
                GXAmiTask task = null;
                try
                {                   
                    task = Client.MediaOpen(DataCollector, TargetMedia.MediaType, TargetMedia.Settings);
                    lock (ExecutedEvents)
                    {
                        ExecutedEvents.Add(task, ececuted);
                    }
                    //System.Diagnostics.Debug.WriteLine("Gateway Open: " + task.Id);
                    //Wait until Virtual media is open connection.
                    if (!ececuted.WaitOne(WaitTime))
                    {
                        throw new Exception("Open Failed. Wait time occurred.");
                    }
                }
                catch (Exception)
                {
                    VirtualOpen = false;
                }
                finally
                {
                    if (task != null)
                    {
                        lock (ExecutedEvents)
                        {
                            ExecutedEvents.Remove(task);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Wait until DC is claimed send data.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="tasks"></param>
        void Client_OnTasksClaimed(object sender, GXAmiTask[] tasks)
        {
            lock (m_Synchronous)
            {
                lock (ExecutedEvents)
                {
                    foreach (GXAmiTask it in tasks)
                    {
                        System.Diagnostics.Debug.Assert(it.SenderDataCollectorGuid == this.DataCollector);
                        if (it.TaskType == TaskType.MediaWrite)
                        {
                            bool claimed = false;
                            foreach (var t in ExecutedEvents)
                            {
                                if (t.Key.Id == it.Id)
                                {
                                    claimed = true;
                                    t.Value.Set();
                                    break;
                                }
                            }
                            if (claimed)
                            {
                                //System.Diagnostics.Debug.WriteLine("Gateway claimed task: " + it.Id);                                
                                break;
                            }
                        }
                    }
                }
            }
        }

        public bool IsOpen
        {
            get 
            {                
                lock (m_Synchronous)
                {
                    return VirtualOpen;
                }
            }
        }
     
        /// <summary>
        /// Close Data Collector Media.
        /// </summary>
        public void Close()
        {
            if (Target != null && IsOpen)
            {
                if (m_OnMediaStateChange != null)
                {
                    m_OnMediaStateChange(this, new MediaStateEventArgs(MediaState.Closing));
                }
                AutoResetEvent ececuted = new AutoResetEvent(false);
                GXAmiTask task = null;
                try
                {
                    lock (m_Synchronous)
                    {
                        task = Client.MediaClose(DataCollector, TargetMedia.MediaType, TargetMedia.Settings);
                        lock (ExecutedEvents)
                        {
                            ExecutedEvents.Add(task, ececuted);
                        }
                        //System.Diagnostics.Debug.WriteLine("Gateway Close: " + task.Id);
                    }
                    //Wait until Virtual media is closed the connection.
                    if (!ececuted.WaitOne(WaitTime))
                    {
                        lock (m_Synchronous)
                        {
                            VirtualOpen = false;
                        }
                        Client.StopListenEvents();
                        throw new Exception("Close Failed. Wait time occurred.");
                    }
                    lock (m_Synchronous)
                    {
                        VirtualOpen = false;
                    }
                }
                finally
                {
                    if (task != null)
                    {
                        lock (ExecutedEvents)
                        {
                            ExecutedEvents.Remove(task);
                        }
                    }
                }
                Client.StopListenEvents();
            }
        }

        public bool Properties(System.Windows.Forms.Form parent)
        {
            return new Gurux.Shared.PropertiesForm(PropertiesForm, GuruxAMI.Gateway.Properties.Resources.SettingsTxt, IsOpen).ShowDialog(parent) == System.Windows.Forms.DialogResult.OK;
        }

        public System.Windows.Forms.Form PropertiesForm
        {
            get 
            {
                return new Settings(this);
            }
        }

        public void Send(object data, string receiver)
        {
            //Reset last position if Eop is used.
            lock (m_syncBase.m_ReceivedSync)
            {
                m_syncBase.m_LastPosition = 0;
            }
            byte[] tmp = (byte[])data;
            AutoResetEvent executed = new AutoResetEvent(false);
            GXAmiTask task = null;
            try
            {
                lock (m_Synchronous)
                {
                    task = Client.Write(DataCollector, Target.MediaType, Target.Settings, tmp, tmp.Length, Eop);
                    lock (ExecutedEvents)
                    {                        
                        ExecutedEvents.Add(task, executed);
                    }
                    //System.Diagnostics.Debug.WriteLine("Gateway send data: " + Client.Instance.ToString() + " " + task.Id + " " + task.Data);
                }                
                //Wait until data is send (task is claimed).
                if (!executed.WaitOne(WaitTime))
                {
                    throw new Exception("Send Failed. Wait time occurred.");
                }            
            }
            finally
            {
                if (task != null)
                {
                    lock (ExecutedEvents)
                    {
                        ExecutedEvents.Remove(task);
                    }
                }
            }
            //System.Diagnostics.Debug.WriteLine("Gateway handled send task: " + task.Id);
        }

        public string MediaType
        {
            get 
            {
                return "Gateway";
            }
        }

        public bool Enabled
        {
            get 
            {
                return true; 
            }
        }

        public string Settings
        {
            get
            {
                string str = this.DataCollector.ToString() + ";" + Media + ";" + MediaSettings;
                return str;
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    string[] arr = value.Split(new char[] { ';' });
                    if (arr.Length == 3)
                    {
                        this.DataCollector = new Guid(arr[0]);
                        Media = arr[1];
                        MediaSettings = arr[2];
                    }
                }
            }
        }

        /// <inheritdoc cref="IGXMedia.Synchronous"/>
        public object Synchronous
        {
            get 
            {
                return Target.Synchronous;
            }
        }

        /// <inheritdoc cref="IGXMedia.IsSynchronous"/>
        public bool IsSynchronous
        {
            get
            {
                return Target.IsSynchronous;
            }
        }

        public bool Receive<T>(ReceiveParameters<T> args)
        {
            return m_syncBase.Receive(args);
        }

        /// <inheritdoc cref="IGXMedia.ResetSynchronousBuffer"/>
        public void ResetSynchronousBuffer()
        {
            lock (m_syncBase.m_ReceivedSync)
            {
                m_syncBase.m_ReceivedSize = 0;
            }
        }

        public ulong BytesSent
        {
            get 
            {
                return Target.BytesSent; 
            }
        }

        public ulong BytesReceived
        {
            get 
            {
                return Target.BytesReceived; 
            }
        }

        public void ResetByteCounters()
        {
            Target.ResetByteCounters();
        }

        public void Validate()
        {
            if (Target != null)
            {
                Target.Validate();
            }
        }

        public object Eop
        {
            get
            {
                if (Target != null)
                {
                    return Target.Eop;
                }
                return null;
            }
            set
            {
                if (Target != null)
                {
                    Target.Eop = value;
                }
            }
        }

        public int ConfigurableSettings
        {
            get
            {
                return Target.ConfigurableSettings;
            }
            set
            {
                Target.ConfigurableSettings = value;
            }
        }

        #endregion

        
        /// <summary>
        /// GuruxAMI host name.
        /// </summary>
        public string Host
        {
            get;
            set;
        }

        /// <summary>
        /// GuruxAMI Port number.
        /// </summary>
        public int Port
        {
            get;
            set;
        }

        /// <summary>
        /// GuruxAMI User Name.
        /// </summary>
        public string UserName
        {
            get;
            set;
        }

        /// <summary>
        /// GuruxAMI Password.
        /// </summary>
        public string Password
        {
            get;
            set;
        }

        /// <summary>
        /// Data Collector where Device Profile is imported.
        /// </summary>
        public Guid DataCollector
        {
            get;
            set;
        }

        /// <summary>
        /// Data Collector Media.
        /// </summary>
        public string Media
        {
            get;
            set;
        }

        /// <summary>
        /// How long is waited media to execute given operation.
        /// </summary>
        public int WaitTime
        {
            get;
            set;
        }

        /// <summary>
        /// Media settings of the data Collector.
        /// </summary>
        public string MediaSettings
        {
            get;
            set;
        }        
    }
}
