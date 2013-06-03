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

namespace GuruxAMI.Gateway
{
    public class GXAmiGateway : IGXMedia, IDisposable
    {
        private GXSynchronousMediaBase m_syncBase;
        internal GXAmiClient Client;
        internal Gurux.Communication.GXClient GXClient;
        bool Connected = false;
        string Error;
        AutoResetEvent Executed = new AutoResetEvent(false);
        GXAmiTask ExecutedTask;
        ClientConnectedEventHandler m_OnClientConnected;
        ClientDisconnectedEventHandler m_OnClientDisconnected;
        internal IGXMedia Target;
        internal ReceivedEventHandler m_OnReceived;
        internal ErrorEventHandler m_OnError;
        MediaStateChangeEventHandler m_OnMediaStateChange;
        TraceEventHandler m_OnTrace;
        readonly object m_Synchronous = new object();

        /// <summary>
        /// Default Constructor.
        /// </summary>
        public GXAmiGateway()
        {
            m_syncBase = new GXSynchronousMediaBase(1024);
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public GXAmiGateway(string host, int port, string user, string pw, Gurux.Communication.GXClient gxClient)
        {
            m_syncBase = new GXSynchronousMediaBase(1024);
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
            this.Client = client;
            GXClient = gxClient;
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
            get;
            set;
        }

        public void Open()
        {
            Close();
            Target.Validate();
            Client.OnTasksAdded += new TasksAddedEventHandler(m_Client_OnTasksAdded);
            Client.OnDeviceErrorsAdded += new DeviceErrorsAddedEventHandler(Client_OnDeviceErrorsAdded);
            Client.OnTasksRemoved += new TasksRemovedEventHandler(Client_OnTasksRemoved);
            Client.StartListenEvents();
            ExecutedTask = Client.MediaOpen(DataCollector, Target.MediaType, Target.Settings);
            Executed.WaitOne();
            Connected = true;
        }

        public bool IsOpen
        {
            get 
            {
                //TODO: Check from the DC...
                return false;
            }
        }

        /// <summary>
        /// Data collector is added reply for send data.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="tasks"></param>
        void m_Client_OnTasksAdded(object sender, GXAmiTask[] tasks)
        {
            foreach (GXAmiTask it in tasks)
            {
                if (it.TaskType == TaskType.MediaWrite)
                {
                    if (ExecutedTask != null && ExecutedTask.Id == it.Id)
                    {
                        continue;
                    }
                    Client.RemoveTask(it, true);
                    List<string> arr = new List<string>(it.Data.Split(new string[] { Environment.NewLine }, StringSplitOptions.None));
                    string media = arr[0];
                    string mediaSettings = arr[1];
                    string data;
                    if (arr.Count == 3)
                    {
                        data = arr[2];
                    }
                    else
                    {
                        arr.RemoveRange(0, 2);
                        data = string.Join(Environment.NewLine, arr.ToArray());
                    }
                    byte[] buff = Gurux.Common.GXCommon.HexToBytes(data, false);
                    int bytes = buff.Length;
                    if (this.IsSynchronous)
                    {
                        lock (m_syncBase.m_ReceivedSync)
                        {
                            int index = m_syncBase.m_ReceivedSize;
                            m_syncBase.AppendData(buff, 0, bytes);
                            if (bytes != 0 && Eop != null) //Search Eop if given.
                            {
                                if (Eop is Array)
                                {
                                    foreach (object eop in (Array)Eop)
                                    {
                                        bytes = Gurux.Shared.GXCommon.IndexOf(m_syncBase.m_Received, Gurux.Shared.GXCommon.GetAsByteArray(eop), index, m_syncBase.m_ReceivedSize);
                                        if (bytes != -1)
                                        {
                                            break;
                                        }
                                    }
                                }
                                else
                                {
                                    bytes = Gurux.Shared.GXCommon.IndexOf(m_syncBase.m_Received, Gurux.Shared.GXCommon.GetAsByteArray(Eop), index, m_syncBase.m_ReceivedSize);
                                }
                            }
                            if (bytes != -1)
                            {
                                m_syncBase.m_ReceivedEvent.Set();
                            }
                        }
                    }
                    else
                    {
                        if (m_OnReceived != null)
                        {
                            m_syncBase.m_ReceivedSize = 0;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Task is executed.
        /// </summary>
        void Client_OnTasksRemoved(object sender, GXAmiTask[] tasks)
        {
            foreach (GXAmiTask it in tasks)
            {
                if (ExecutedTask != null && it.Id == ExecutedTask.Id)
                {
                    Executed.Set();
                }
            }
        }

        /// <summary>
        /// If task is failed. If task failed dialog is not closed.
        /// </summary>
        void Client_OnDeviceErrorsAdded(object sender, GXAmiDeviceError[] errors)
        {
            foreach (GXAmiDeviceError it in errors)
            {
                if (it.TaskID == ExecutedTask.Id)
                {
                    Error = it.Message;
                    break;
                }
            }
        }

        /// <summary>
        /// Close Data Collector Media.
        /// </summary>
        public void Close()
        {
            if (Client != null && Connected)
            {
                ExecutedTask = Client.MediaClose(DataCollector, Target.MediaType, Target.Settings);
                Executed.WaitOne();
                Connected = false;
                ExecutedTask = null;
                Client.OnTasksAdded -= new TasksAddedEventHandler(m_Client_OnTasksAdded);
                Client.OnTasksRemoved -= new TasksRemovedEventHandler(Client_OnTasksRemoved);
                Client.OnDeviceErrorsAdded -= new DeviceErrorsAddedEventHandler(Client_OnDeviceErrorsAdded);
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
            ExecutedTask = Client.Write(DataCollector, Target.MediaType, Target.Settings, 
                Gurux.Communication.Common.GXConverter.GetBytes(data, false), 0, Target.Eop);
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
                    this.DataCollector = new Guid(arr[0]);
                    Media = arr[1];
                    MediaSettings = arr[2];
                }
            }
        }

        /// <inheritdoc cref="IGXMedia.Synchronous"/>
        public object Synchronous
        {
            get 
            {
                return m_Synchronous;
            }
        }

        /// <inheritdoc cref="IGXMedia.IsSynchronous"/>
        public bool IsSynchronous
        {
            get
            {
                bool reserved = System.Threading.Monitor.TryEnter(m_Synchronous, 0);
                if (reserved)
                {
                    System.Threading.Monitor.Exit(m_Synchronous);
                }
                return !reserved;
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
            get;
            set;
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
        /// Media settings of the data Collector.
        /// </summary>
        public string MediaSettings
        {
            get;
            set;
        }



        #region IDisposable Members

        public void Dispose()
        {
            Close();
        }

        #endregion
    }
}
