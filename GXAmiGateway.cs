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
    public class GXAmiGateway : IGXMedia, IGXMediaContainer, IDisposable
    {
        internal GXAmiClient Client;
        bool GatewayGetValue;
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
        public GXAmiGateway(string host, string user, string pw, Gurux.Communication.GXClient gxClient)
        {
            m_syncBase = new GXSynchronousMediaBase(1024);
            WaitTime = -1;
            this.Host = host;
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
            Client.OnTasksAdded += new TasksAddedEventHandler(Client_OnTasksAdded);
            Client.OnTasksClaimed += new TasksClaimedEventHandler(Client_OnTasksClaimed);
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (Client != null)
            {
                Client.OnTasksAdded -= new TasksAddedEventHandler(Client_OnTasksAdded);
                Client.OnTasksClaimed -= new TasksClaimedEventHandler(Client_OnTasksClaimed);
            }
            (this as IGXMedia).Close();
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
                    System.Diagnostics.Debug.WriteLine("Gateway received data: " + it.Id + " " + it.Data);
                    string[] tmp = it.Data.Split(new string[]{"\r\n"}, StringSplitOptions.None);
                    byte[] buff = Gurux.Common.GXCommon.HexToBytes(tmp[tmp.Length - 1], false);
                    int bytes = buff.Length;
                    (TargetMedia as IGXVirtualMedia).DataReceived(buff, null);                  
                }
                //Remove task.
                Client.RemoveTask(it);
            }            
        }

        /// <summary>
        /// Target media of GXMedia Container.
        /// </summary>
        public IGXMedia Media
        {
            get
            {
                if (TargetMedia == null && !string.IsNullOrEmpty(MediaName))
                {
                    Initialize();
                    Gurux.Communication.GXClient cl = new Gurux.Communication.GXClient();
                    Media = cl.SelectMedia(MediaName);
                    if (TargetMedia == null)
                    {
                        throw new Exception(MediaName + " media not found.");
                    }                    
                    TargetMedia.Settings = MediaSettings;
                }
                return TargetMedia;
            }
            set
            {
                if (TargetMedia != value)
                {
                    if (TargetMedia != null)
                    {                        
                        (TargetMedia as INotifyPropertyChanged).PropertyChanged -= new PropertyChangedEventHandler(OnMediaPropertyChanged);
                        (TargetMedia as IGXVirtualMedia).OnGetPropertyValue -= new GetPropertyValueEventHandler(OnGetPropertyValue);
                        TargetMedia.OnMediaStateChange -= new MediaStateChangeEventHandler(TargetMedia_OnMediaStateChange);
                        (TargetMedia as IGXVirtualMedia).OnDataSend -= new ReceivedEventHandler(OnDataSend);                        
                    }
                    TargetMedia = value;
                    if (TargetMedia != null)
                    {
                        (TargetMedia as IGXVirtualMedia).Virtual = true;
                        if ((this as IGXMedia).ConfigurableSettings != 0)
                        {
                            TargetMedia.ConfigurableSettings = (this as IGXMedia).ConfigurableSettings;
                        }
                        (TargetMedia as IGXVirtualMedia).OnGetPropertyValue += new GetPropertyValueEventHandler(OnGetPropertyValue);
                        (TargetMedia as INotifyPropertyChanged).PropertyChanged += new PropertyChangedEventHandler(OnMediaPropertyChanged);
                        TargetMedia.OnMediaStateChange += new MediaStateChangeEventHandler(TargetMedia_OnMediaStateChange);
                        (TargetMedia as IGXVirtualMedia).OnDataSend += new ReceivedEventHandler(OnDataSend);                        
                    }
                }
            }
        }

        /// <summary>
        /// Media sends new data to the device.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnDataSend(object sender, ReceiveEventArgs e)
        {
            if (!IsOpen)
            {
                throw new Exception("Media is not open.");
            }
            Send(e.Data, e.SenderInfo);
        }

        void TargetMedia_OnMediaStateChange(object sender, MediaStateEventArgs e)
        {
            if (e.State == MediaState.Opening)
            {
                OpenMedia();
            }
            else if (e.State == MediaState.Closing)
            {
                //Mikko (this as IGXMedia).Close();
                CloseMedia();
            }
        }

        public string[] GetPropertyValues(string[] propertyNames)
        {
            List<string> values = new List<string>();
            if (propertyNames != null && propertyNames.Length != 0)
            {
                if (!UseCache())
                {
                    return Client.GetMediaProperties(DataCollector, Media.MediaType, Media.Name, propertyNames);
                }                
                PropertyDescriptorCollection list = TypeDescriptor.GetProperties(Media);
                foreach (string it in propertyNames)
                {
                    object value = list[it].GetValue(Media);
                    values.Add(Convert.ToString(value));
                }                
            }
            return values.ToArray();
        }

        public string GetPropertyValue(string propertyName)
        {
            if (!UseCache())
            {                
                return Client.GetMediaProperties(DataCollector, Media.MediaType, Media.Name, new string[] { propertyName })[0];
            }
            PropertyDescriptor prop = TypeDescriptor.GetProperties(Media)[propertyName];
            object value = prop.GetValue(Media);
            return Convert.ToString(value);
        }

        bool UseCache()
        {            
            if (!IsOpen)
            {
                return true;
            }
            if (Media is Gurux.Serial.GXSerial)
            {
                PropertyDescriptor prop = TypeDescriptor.GetProperties(Media)["DtrEnable"];
                if (!Convert.ToBoolean(prop.GetValue(Media)))
                {
                    return true;
                }
            }
            return false;
        }

        public void SetPropertyValues(Dictionary<string, object> properties)
        {
            if (properties.Count != 0)
            {
                PropertyDescriptorCollection list = TypeDescriptor.GetProperties(Media);
                foreach (var it in properties)
                {
                    list[it.Key].SetValue(Media, it.Value);
                }
                if (!UseCache())
                {
                    Client.SetMediaProperties(DataCollector, Media.MediaType, Media.Name, properties);
                }
            }
        }

        public void SetPropertyValue(string propertyName, object value)
        {
            PropertyDescriptor prop = TypeDescriptor.GetProperties(Media)[propertyName];
            prop.SetValue(Media, value);
            if (!UseCache())
            {
                OnMediaPropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        string OnGetPropertyValue(string propertyName)
        {
            try
            {
                // Changed properties are saved and updated when connection is established.                
                if (!GatewayGetValue && IsOpen)
                {
                    return Client.GetMediaProperties(DataCollector, Media.MediaType, Media.Name, new string[] { propertyName })[0];
                }                
            }
            catch (Exception ex)
            {
                //TODO: Show error. GXCommon.ShowError(this, ex);
            }
            return null;
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
                    GatewayGetValue = true;
                    PropertyDescriptor prop = TypeDescriptor.GetProperties(Media)[e.PropertyName];
                    object value = prop.GetValue(Media);
                    Dictionary<string, object> properties = new Dictionary<string, object>();
                    properties.Add(e.PropertyName, value);
                    Client.SetMediaProperties(DataCollector, Media.MediaType, Media.Name, properties);
                }
            }
            catch (Exception ex)
            {
                //TODO: Show error. GXCommon.ShowError(this, ex);
            }
            finally
            {
                GatewayGetValue = false;
            }
        }

        #region IGXMedia Members

        /// <summary>
        /// GXAMIGateway component sends received data through this method.
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
                m_OnTrace(this, new TraceEventArgs(TraceTypes.Error, ex, null));
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
                if (Media != null)
                {
                    return Media.Trace;
                }
                return TraceLevel.Off;
            }
            set
            {
                if (Media != null)
                {
                    Media.Trace = value;
                }
            }
        }

        internal void Initialize()
        {
            if (Client == null && !string.IsNullOrEmpty(Host))
            {
                string baseUr = Host;
                if (baseUr.Contains("*"))
                {
                    baseUr = baseUr.Replace("*", "localhost");
                }
                Client = new GXAmiClient(baseUr, UserName, Password);
                Client.OnTasksAdded += new TasksAddedEventHandler(Client_OnTasksAdded);
                Client.OnTasksClaimed += new TasksClaimedEventHandler(Client_OnTasksClaimed);
            }
        }

        void IGXMedia.Open()
        {
            if (TargetMedia != null)
            {
                TargetMedia.Open();
            }
        }

        void OpenMedia()
        {
            if (!IsOpen)
            {
                lock (m_Synchronous)
                {
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
                    task = Client.MediaOpen(DataCollector, TargetMedia.MediaType, TargetMedia.Name, TargetMedia.Settings, ececuted);                    
                    System.Diagnostics.Debug.WriteLine("Gateway Open: " + task.Id);
                }
                catch (Exception)
                {
                    VirtualOpen = false;
                    throw;
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
                                System.Diagnostics.Debug.WriteLine("Gateway claimed task: " + it.Id);                                
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
        /// Abort all tasks on close.
        /// </summary>
        void IGXMedia.Close()
        {
            if (VirtualOpen)
            {
                VirtualOpen = false;
                if (TargetMedia != null)
                {
                    TargetMedia.Close();
                }
                lock (ExecutedEvents)
                {
                    foreach (AutoResetEvent it in ExecutedEvents.Values)
                    {
                        it.Set();
                    }
                }
            }
        }
     
        /// <summary>
        /// Close Data Collector Media.
        /// </summary>        
        void CloseMedia()
        {
            if (Media != null && IsOpen)
            {
                if (m_OnMediaStateChange != null)
                {
                    m_OnMediaStateChange(this, new MediaStateEventArgs(MediaState.Closing));
                }
                AutoResetEvent ececuted = new AutoResetEvent(false);
                GXAmiTask task = null;
                lock (m_Synchronous)
                {
                    task = Client.MediaClose(DataCollector, TargetMedia.MediaType, TargetMedia.Name, ececuted);
                    VirtualOpen = false;
                    Client.StopListenEvents();
                    System.Diagnostics.Debug.WriteLine("Gateway Close: " + task.Id);
                }
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
            byte[] tmp = Gurux.Common.GXCommon.GetAsByteArray(data);
            AutoResetEvent executed = new AutoResetEvent(false);
            GXAmiTask task = null;
            lock (m_Synchronous)
            {
                task = Client.MediaWrite(DataCollector, Media.MediaType, Media.Name, tmp, executed);
                System.Diagnostics.Debug.WriteLine("Gateway send data: " + task.Id + " " + task.Data);
            }                
            System.Diagnostics.Debug.WriteLine("Gateway handled send task: " + task.Id);
            if (!IsOpen)
            {
                throw new Exception("Media closed.");
            }
        }

        /// <summary>
        /// Media types that can be used.
        /// </summary>
        public string[] AllowerMediaTypes
        {
            get;
            set;
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
                string str = this.DataCollector.ToString() + ";" + MediaName + ";" + MediaSettings;
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
                        MediaName = arr[1];
                        MediaSettings = arr[2];
                    }
                }
            }
        }

        /// <inheritdoc cref="IGXMedia.Tag"/>
        public object Tag
        {
            get;
            set;
        }

        /// <inheritdoc cref="IGXMedia.MediaContainer"/>
        IGXMediaContainer IGXMedia.MediaContainer
        {
            get
            {
                throw new ArgumentException("There can't be a media container for a gateway.");
            }
            set
            {
                throw new ArgumentException("There can't be a media container for a gateway.");
            }
        }

        /// <inheritdoc cref="IGXMedia.Synchronous"/>
        public object Synchronous
        {
            get 
            {
                return Media.Synchronous;
            }
        }

        /// <inheritdoc cref="IGXMedia.IsSynchronous"/>
        public bool IsSynchronous
        {
            get
            {
                return Media.IsSynchronous;
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
                return Media.BytesSent; 
            }
        }

        public ulong BytesReceived
        {
            get 
            {
                return Media.BytesReceived; 
            }
        }

        public void ResetByteCounters()
        {
            Media.ResetByteCounters();
        }

        public void Validate()
        {
            if (Media != null)
            {
                Media.Validate();
            }
        }

        public object Eop
        {
            get
            {
                if (Media != null)
                {
                    return Media.Eop;
                }
                return null;
            }
            set
            {
                if (Media != null)
                {
                    Media.Eop = value;
                }
            }
        }

        int IGXMedia.ConfigurableSettings
        {
            get
            {
                return Media.ConfigurableSettings;
            }
            set
            {
                Media.ConfigurableSettings = value;
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
        /// Get name of active DC.
        /// </summary>
        public string DataCollectorName
        {
            get;
            internal set;
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
        /// Data Collector Media type.
        /// </summary>
        public string MediaName
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
