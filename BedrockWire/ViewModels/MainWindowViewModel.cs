using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using BedrockWire.Models;
using BedrockWire.Models.PacketFields;
using BedrockWire.Utils;
using BedrockWire.Views;
using BedrockWireAuthDump;
using BedrockWireFormat;
using BedrockWireProxy;
using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using Packet = BedrockWire.Models.Packet;

namespace BedrockWire.ViewModels
{
    public class MainWindowViewModel : ViewModelBase, IPacketWriter
    {
        public uint PacketOrder { get; set; }
        public ConcurrentQueue<Packet> PacketList { get; set; }
        public ObservableCollection<Packet> FilteredPacketList { get; set; }
        public DataGridCollectionView FilteredPacketListView { get; set; }
        private Dictionary<int, PacketDefinition> PacketDefinitions { get; set; }
        public string DecodeTime { get; set; }
        public string FilterTime { get; set; }

        public bool IsLive { get; set; }
        public BedrockWireProxy.BedrockWireProxy Proxy { get; set; }
        public BlockingCollection<Packet> PacketReadQueue { get; set; }

        private bool filterOutNoise;
        public bool FilterOutNoise
        {
            get => filterOutNoise;
            set {
                filterOutNoise = value;
                RefreshFilters();
            }
        }

        private bool filterOutGood;
        public bool FilterOutGood
        {
            get => filterOutGood;
            set
            {
                filterOutGood = value;
                RefreshFilters();
            }
        }
        

        private string filterText;
        public string FilterText
        {
            get => filterText;
            set
            {
                filterText = value;
                RefreshFilters();
            }
        }

        private Packet selectedPacket;
        public Packet SelectedPacket
        {
            get => selectedPacket;
            set => this.RaiseAndSetIfChanged(ref selectedPacket, value);
        }

        public string StatusText { get; set; }

        public MainWindowViewModel()
        {
            PacketOrder = 0;
            PacketList = new ConcurrentQueue<Packet>();
            FilteredPacketList = new ObservableCollection<Packet>();
            FilteredPacketListView = new DataGridCollectionView(FilteredPacketList);
            FilteredPacketListView.SortDescriptions.Clear();
            FilteredPacketListView.SortDescriptions.Add(DataGridSortDescription.FromComparer(new PacketComparer()));
            PacketDefinitions = new Dictionary<int, PacketDefinition>();
            FilterOutGood = false;
            FilterOutNoise = false;
            IsLive = false;
            PacketReadQueue = new BlockingCollection<Packet>();
            UpdateStatusText();

            if(Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Exit += (sender, args) =>
                {
                    if (Proxy != null)
                    {
                        Proxy.Stop();
                        Proxy = null;
                    }
                };
            }
        }

        private void DecodePacket(Packet packet)
        {
            packet.Name = "UNKNOWN_PACKET";
            packet.Error = null;
            packet.Decoded = null;
            if (PacketDefinitions != null && PacketDefinitions.ContainsKey(packet.Id))
            {
                packet.Name = PacketDefinitions[packet.Id].Name;

                try
                {
                    using (MemoryStream stream = new MemoryStream(packet.Payload))
                    {
                        using (BinaryReader reader2 = new BinaryReader(stream))
                        {
                            packet.Decoded = PacketDecoder.Decode(reader2, PacketDefinitions[packet.Id].Fields);
                            if (stream.Position != stream.Length)
                            {
                                packet.Error = (stream.Length - stream.Position) + " bytes left unread";
                            }
                        }
                    }

                }
                catch (Exception ex)
                {
                    packet.Error = ex.Message;
                }
            }
            else
            {
                packet.Error = "Unknown packet id: " + packet.Id;
            }
        }

        private void UpdateStatusText()
        {
            StatusText = "Loaded " + PacketDefinitions.Count + " packet definitions. Showing " + FilteredPacketList.Count + "/" + PacketList.Count + " packets. " + PacketList.Where(p => p.Error != null).Count() + "/" + PacketList.Count + " have errors.";
            StatusText += " Decode Time: " + DecodeTime + " FilterTime: " + FilterTime;
            this.RaisePropertyChanged(nameof(StatusText));
        }

        public void OpenStreamFinal(Stream stream, Action? finishCallback = null)
        {
            PacketOrder = 0;
            PacketList.Clear();
            FilteredPacketList.Clear();

            StatusText = "Loading...";
            this.RaisePropertyChanged(nameof(StatusText));

            new Thread(() =>
            {
                byte[] header = new byte[4];
                stream.Read(header, 0, 4);

                if (header[0] != 66 || header[1] != 68 || header[2] != 87)
                {
                    return;
                }

                byte version = header[3];

                if (version != 1)
                {
                    return;
                }

                BinaryReader reader = new BinaryReader(new DeflateStream(stream, CompressionMode.Decompress));

                Stopwatch sw2 = new Stopwatch();
                sw2.Start();

                using (ManualResetEvent resetEvent = new ManualResetEvent(false))
                {
                    int packets = 0;
                    int packetsFinished = 0;
                    bool waiting = false;
                    DedicatedThreadPool threadPool = new DedicatedThreadPool(new DedicatedThreadPoolSettings(Environment.ProcessorCount / 2));

                    try
                    {
                        while (true)
                        {
                            byte direction = reader.ReadByte();
                            byte packetId = reader.ReadByte();
                            ulong time = reader.ReadUInt64();
                            int length = reader.ReadInt32();
                            byte[] payload = reader.ReadBytes(length);

                            string name = "UNKNOWN_PACKET";

                            var packet = new Packet() { OrderId = PacketOrder++, Direction = direction == 0 ? "C -> S" : "S -> C", Id = packetId, Payload = payload, Time = time, Length = (ulong)length };
                            PacketList.Enqueue(packet);

                            Interlocked.Increment(ref packets);
                            threadPool.QueueUserWorkItem(() =>
                            {
                                Stopwatch sw = new Stopwatch();
                                sw.Start();
                                DecodePacket(packet);
                                sw.Stop();
                                packet.DecodeTime = sw.Elapsed.ToString();

                                Interlocked.Increment(ref packetsFinished);
                                if (Interlocked.Decrement(ref packets) == 0 && waiting)
                                    resetEvent.Set();
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                    }

                    if(packetsFinished != PacketList.Count)
                    {
                        waiting = true;
                        resetEvent.WaitOne();
                    }

                    sw2.Stop();
                    DecodeTime = sw2.Elapsed.ToString();
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        RefreshFilters();
                        UpdateStatusText();
                        if(finishCallback != null)
                        {
                            finishCallback();
                        }
                    });

                    reader.Close();
                }
            }).Start();
        }

        public void OpenStreamNonFinal(Stream stream, Action? finishCallback = null)
        {
            PacketOrder = 0;
            PacketList.Clear();
            FilteredPacketList.Clear();

            new Thread(() =>
            {
                BinaryReader headerReader = new BinaryReader(stream);
                byte[] header = headerReader.ReadBytes(4);


                if (header[0] != 66 || header[1] != 68 || header[2] != 87)
                {
                    return;
                }

                byte version = header[3];

                if (version != 1)
                {
                    return;
                }

                BinaryReader reader = new BinaryReader(new DeflateStream(stream, CompressionMode.Decompress));

                Stopwatch sw2 = new Stopwatch();
                sw2.Start();

                using (ManualResetEvent resetEvent = new ManualResetEvent(false))
                {
                    DedicatedThreadPool threadPool = new DedicatedThreadPool(new DedicatedThreadPoolSettings(Environment.ProcessorCount / 2));

                    try
                    {
                        while (true)
                        {
                            byte direction = reader.ReadByte();
                            byte packetId = reader.ReadByte();
                            ulong time = reader.ReadUInt64();
                            int length = reader.ReadInt32();
                            byte[] payload = reader.ReadBytes(length);

                            string name = "UNKNOWN_PACKET";

                            var packet = new Packet() { OrderId = PacketOrder++, Direction = direction == 0 ? "C -> S" : "S -> C", Id = packetId, Payload = payload, Time = time, Length = (ulong)length };
                            PacketList.Enqueue(packet);

                            threadPool.QueueUserWorkItem(() =>
                            {
                                Stopwatch sw = new Stopwatch();
                                sw.Start();
                                DecodePacket(packet);
                                sw.Stop();
                                packet.DecodeTime = sw.Elapsed.ToString();

                                if(IsPacketVisible(packet))
                                {
                                    Dispatcher.UIThread.Post(() =>
                                    {
                                        FilteredPacketList.Add(packet);
                                        UpdateStatusText();
                                    }, DispatcherPriority.MinValue);
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                    }

                    sw2.Stop();
                    DecodeTime = sw2.Elapsed.ToString();
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        UpdateStatusText();
                        if (finishCallback != null)
                        {
                            finishCallback();
                        }
                    });

                    reader.Close();
                }
            }).Start();
        }

        public void OpenQueueNonFinal(BlockingCollection<Packet> queue)
        {
            PacketOrder = 0;
            PacketList.Clear();
            FilteredPacketList.Clear();

            new Thread(() =>
            {
                Stopwatch sw2 = new Stopwatch();
                sw2.Start();

                using (ManualResetEvent resetEvent = new ManualResetEvent(false))
                {
                    DedicatedThreadPool threadPool = new DedicatedThreadPool(new DedicatedThreadPoolSettings(Environment.ProcessorCount / 2));

                    try
                    {
                        while (true)
                        {
                            // TODO: memory leak here, this loop needs to close
                            var packet = queue.Take();
                            PacketList.Enqueue(packet);

                            threadPool.QueueUserWorkItem(() =>
                            {
                                Stopwatch sw = new Stopwatch();
                                sw.Start();
                                DecodePacket(packet);
                                sw.Stop();
                                packet.DecodeTime = sw.Elapsed.ToString();

                                if (IsPacketVisible(packet))
                                {
                                    Dispatcher.UIThread.Post(() =>
                                    {
                                        FilteredPacketList.Add(packet);
                                        UpdateStatusText();
                                    }, DispatcherPriority.MinValue);
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                    }

                    sw2.Stop();
                    DecodeTime = sw2.Elapsed.ToString();
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        UpdateStatusText();
                    });
                }
            }).Start();
        }

        public async void OnOpenCommand(Window window)
        {
            var dlg = new OpenFileDialog();
            dlg.Filters.Add(new FileDialogFilter() { Name = "BedrockWire Files", Extensions = { "bw" } });
            dlg.AllowMultiple = false;

            var result = await dlg.ShowAsync(window);
            if (result != null && result.Length > 0)
            {
                var stream = new FileStream(result[0], FileMode.Open);

                var spinner = new SpinnerDialog();
                spinner.DataContext = "Loading...";
                spinner.ShowDialog(window);
                OpenStreamFinal(stream, () =>
                {
                    spinner.Close();
                });
            }
        }

        public async void OnStartProxyCommand(Window window)
        {
            var dlg = new StartProxyDialog();
            dlg.DataContext = new StartProxyDialogViewModel();
            ProxySettings proxySettings = await dlg.ShowDialog<ProxySettings>(window);

            IPEndPoint remoteServer = IPEndPoint.Parse(proxySettings.RemoteServerAddress);
            AuthData authData = new AuthData();
            authData.Chain = proxySettings.Auth.Chain;
            authData.MinecraftKeyPair = AuthDump.DeserializeKeys(proxySettings.Auth.PrivateKey, proxySettings.Auth.PublicKey);

            Proxy = new BedrockWireProxy.BedrockWireProxy(authData, proxySettings.ProxyPort, remoteServer, this);
            Proxy.Start();

            IsLive = true;
            this.RaisePropertyChanged(nameof(IsLive));

            OpenQueueNonFinal(PacketReadQueue);
        }

        public async void OnStopProxyCommand(Window window)
        {
            if (IsLive && Proxy != null)
            {
                Proxy.Stop();
                IsLive = false;
                this.RaisePropertyChanged(nameof(IsLive));
                Proxy = null;
                PacketReadQueue.CompleteAdding();
            }
        }

        public async void OnClearCommand(Window window)
        {
            PacketOrder = 0;
            PacketList.Clear();
            FilteredPacketList.Clear();
        }

        private bool IsPacketVisible(Packet packet)
        {
            if (FilterOutNoise && (packet.Id == 111 || packet.Id == 40 || packet.Id == 58 || packet.Id == 144))
            {
                return false;
            }

            if(FilterOutGood && packet.Error == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(FilterText))
            {
                if (FilterText.StartsWith("0x") && FilterText.Length > 2)
                {
                    try
                    {
                        int id = Convert.ToInt32(FilterText, 16);
                        if (packet.Id != id)
                        {
                            return false;
                        }
                    }
                    catch (Exception)
                    {
                    }

                }
                else if (!packet.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            return true;

        }

        private void RefreshFilters()
        {
            FilteredPacketList.Clear();

            foreach(Packet packet in PacketList)
            {
                if(IsPacketVisible(packet))
                {
                    FilteredPacketList.Add(packet);
                }
            }

            UpdateStatusText();
        }

        private List<PacketField> GetSubFields(XmlNode node)
        {
            List<PacketField> list = new List<PacketField>();

            foreach (XmlNode fieldNode in node.ChildNodes)
            {
                if (fieldNode.NodeType == XmlNodeType.Element)
                {
                    list.Add(ParseField(fieldNode));
                }

            }

            return list;
        }

        private PacketField ParseField(XmlNode node)
        {
            string name = node.Name;
            switch(name)
            {
                case "switch":
                    return new SwitchPacketField()
                    {
                        Name = node.Attributes.GetNamedItem("name")?.InnerText,
                        ReferencesId = node.Attributes.GetNamedItem("ref")?.InnerText,
                        SubFields = new List<CasePacketField>(GetSubFields(node).Cast<CasePacketField>()) 
                    };
                case "list":
                    return new ListPacketField()
                    {
                        Name = node.Attributes.GetNamedItem("name")?.InnerText,
                        ReferencesId = node.Attributes.GetNamedItem("ref")?.InnerText,
                        SubFields = GetSubFields(node)
                    };
                case "flags":
                    return new FlagsPacketField()
                    {
                        Name = node.Attributes.GetNamedItem("name")?.InnerText,
                        ReferencesId = node.Attributes.GetNamedItem("ref")?.InnerText,
                        SubFields = new List<CasePacketField>(GetSubFields(node).Cast<CasePacketField>())
                    };
                case "case":
                    return new CasePacketField()
                    {
                        Value = node.Attributes.GetNamedItem("value").InnerText,
                        SubFields = GetSubFields(node)
                    };
                case "conditional":
                    return new ConditionalPacketField()
                    {
                        Name = node.Attributes.GetNamedItem("name")?.InnerText,
                        ReferencesId = node.Attributes.GetNamedItem("ref")?.InnerText,
                        Condition = node.Attributes.GetNamedItem("condition").InnerText,
                        SubFields = GetSubFields(node),
                    };
                case "field":
                    return new ValuePacketField()
                    {
                        Name = node.Attributes.GetNamedItem("name")?.InnerText,
                        Type = node.Attributes.GetNamedItem("type")?.InnerText,
                        ReferenceId = node.Attributes.GetNamedItem("refId")?.InnerText
                    };
                default:
                    throw new Exception("Unknown field type: " + name);

            }
        }

        public async void DecodeCommand(Packet packet)
        {
            DecodePacket(packet);
        }

        public async void OnLoadProtocolCommand(Window window)
        {
            var dlg = new OpenFileDialog();
            dlg.Filters.Add(new FileDialogFilter() { Name = "Bedrock Protocol Definition", Extensions = { "xml" } });
            dlg.AllowMultiple = false;

            var result = await dlg.ShowAsync(window);
            if (result != null && result.Length > 0)
            {
                PacketDefinitions = new Dictionary<int, PacketDefinition>();

                XmlDocument doc = new XmlDocument();
                doc.Load(result[0]);

                FieldDecoders.CustomDecoders.Clear();

                foreach (XmlNode node in doc.DocumentElement.SelectNodes("child::types")[0])
                {
                    if (node.Name == "type")
                    {
                        string name = node.Attributes.GetNamedItem("name").InnerText;

                        List<PacketField> fields = new List<PacketField>();

                        foreach (XmlNode fieldNode in node.ChildNodes)
                        {
                            if (fieldNode.NodeType == XmlNodeType.Element)
                            {
                                fields.Add(ParseField(fieldNode));
                            }
                        }

                        FieldDecoders.CustomDecoders.Add(name, fields);
                    }
                }

                foreach (XmlNode node in doc.DocumentElement.SelectNodes("child::packets")[0])
                {
                    if(node.Name == "packet")
                    {
                        int id = Convert.ToInt32(node.Attributes.GetNamedItem("id").InnerText, 16);
                        string name = node.Attributes.GetNamedItem("name").InnerText;

                        List<PacketField> fields = new List<PacketField>();

                        foreach(XmlNode fieldNode in node.ChildNodes)
                        {
                            if (fieldNode.NodeType == XmlNodeType.Element)
                            {
                                fields.Add(ParseField(fieldNode));
                            }
                        }

                        PacketDefinitions[id] = new PacketDefinition() { Name = name, Fields = fields };
                    }
                }

                foreach(Packet packet in PacketList)
                {
                    DecodePacket(packet);
                }

                RefreshFilters();
            }
        }

        public void WritePacket(PacketDirection direction, RawMinecraftPacket packet, ulong time)
        {
            PacketReadQueue.Add(new Packet() { OrderId = PacketOrder++, Name = "UNKNOWN_PACKET", Direction = direction == 0 ? "C -> S" : "S -> C", Id = packet.Id, Payload = packet.Payload.ToArray(), Time = time, Length = (ulong)packet.Payload.Length });
        }
    }
}
