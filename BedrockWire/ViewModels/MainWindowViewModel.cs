using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using BedrockWire.Core;
using BedrockWire.Core.Model;
using BedrockWire.Models;
using BedrockWire.Utils;
using BedrockWire.Views;
using BedrockWireAuthDump;
using BedrockWireProxy;
using ReactiveUI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using Packet = BedrockWire.Core.Model.Packet;

namespace BedrockWire.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public ConcurrentQueue<Packet> PacketList { get; set; }
        public ObservableCollection<Packet> FilteredPacketList { get; set; }
        public DataGridCollectionView FilteredPacketListView { get; set; }
        private ProtocolDefinition ProtocolDefinition { get; set; }
        public string DecodeTime { get; set; }

        public bool IsLive { get; set; }
        private BedrockWireProxy.BedrockWireProxy Proxy { get; set; }
        public ProxyPacketReader ProxyPacketReader { get; set; }

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
            PacketList = new ConcurrentQueue<Packet>();

            ProtocolDefinition = new ProtocolDefinition()
            {
                PacketDefinitions = new Dictionary<int, PacketDefinition>(),
                CustomTypes = new Dictionary<string, List<Core.Model.PacketFields.PacketField>>()
            };

            FilteredPacketList = new ObservableCollection<Packet>();
            FilteredPacketListView = new DataGridCollectionView(FilteredPacketList);
            FilteredPacketListView.SortDescriptions.Clear();
            FilteredPacketListView.SortDescriptions.Add(DataGridSortDescription.FromComparer(new PacketComparer()));
            FilterOutGood = false;
            FilterOutNoise = false;

            IsLive = false;

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
            PacketDecoder.Decode(ProtocolDefinition, packet);
        }

        private void UpdateStatusText()
        {
            StatusText = "Loaded " + ProtocolDefinition.PacketDefinitions.Count + " packet definitions. Showing " + FilteredPacketList.Count + "/" + PacketList.Count + " packets. " + PacketList.Where(p => p.Error != null).Count() + "/" + PacketList.Count + " have errors.";
            
            if(!string.IsNullOrEmpty(DecodeTime))
            {
                StatusText += " Decode Time: " + DecodeTime;
            }
            
            this.RaisePropertyChanged(nameof(StatusText));
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

                PacketList.Clear();
                RefreshFilters();

                new Thread(() =>
                {
                    var threadPool = new DedicatedThreadPool(new DedicatedThreadPoolSettings(Environment.ProcessorCount / 2));
                    var packetReader = new StreamPacketReader(stream);
                    uint decodedPackets = 0;

                    using (ManualResetEvent resetEvent = new ManualResetEvent(false))
                    {
                        packetReader.Read((packet) =>
                        {
                            PacketList.Enqueue(packet);
                            threadPool.QueueUserWorkItem(() =>
                            {
                                DecodePacket(packet);
                                Interlocked.Increment(ref decodedPackets);
                                resetEvent.Set();
                            });
                        });

                        // all packets read

                        while (PacketList.Count != decodedPackets)
                        {
                            resetEvent.WaitOne();
                            resetEvent.Reset();
                        }

                        // All read and decoded, filter
                        stream.Close();

                        Dispatcher.UIThread.Post(() =>
                        {
                            RefreshFilters();
                            spinner.Close();
                        });
                    }
                }).Start();
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

            ProxyPacketReader = new ProxyPacketReader();

            Proxy = new BedrockWireProxy.BedrockWireProxy(authData, proxySettings.ProxyPort, remoteServer, ProxyPacketReader);
            Proxy.Start();

            IsLive = true;
            this.RaisePropertyChanged(nameof(IsLive));

            PacketList.Clear();
            RefreshFilters();
            DecodeTime = null;
            UpdateStatusText();

            new Thread(() =>
            {
                var threadPool = new DedicatedThreadPool(new DedicatedThreadPoolSettings(Environment.ProcessorCount / 2));

                ProxyPacketReader.Read((packet) =>
                {
                    PacketList.Enqueue(packet);
                    threadPool.QueueUserWorkItem(() =>
                    {
                        DecodePacket(packet);

                        if (IsPacketVisible(packet))
                        {
                            Dispatcher.UIThread.Post(() =>
                            {
                                FilteredPacketList.Add(packet);
                                UpdateStatusText();
                            }, DispatcherPriority.MinValue);
                        }
                    });
                });
            }).Start();
        }

        public async void OnStopProxyCommand(Window window)
        {
            if (IsLive && Proxy != null)
            {
                Proxy.Stop();
                IsLive = false;
                this.RaisePropertyChanged(nameof(IsLive));
                Proxy = null;
                ProxyPacketReader.StopWriting();
            }
        }

        public async void OnSaveCommand(Window window)
        {
            if(PacketList.Count > 0)
            {
                SaveFileDialog dlg = new SaveFileDialog();
                dlg.Title = "Save Document As...";
                dlg.InitialFileName = "dump.bw";
                dlg.Filters.Add(new FileDialogFilter() { Name = "BedrockWire Files", Extensions = { "bw" } });
                dlg.DefaultExtension = "bw";

                var fileName = await dlg.ShowAsync(window);
                using (var stream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                {
                    var writer = BedrockWireFormat.BedrockWireFormat.GetWriter(stream);
                    BedrockWireFormat.BedrockWireFormat.WriteHeader(stream);

                    var spinner = new SpinnerDialog();
                    spinner.DataContext = "Saving...";
                    spinner.ShowDialog(window);

                    foreach (var packet in PacketList.OrderBy(packet => packet.Index))
                    {
                        writer.Write(new BedrockWireFormat.Packet()
                        {
                            Direction = packet.Direction.Equals("C -> S") ? BedrockWireFormat.PacketDirection.Serverbound : BedrockWireFormat.PacketDirection.Clientbound, // TODO: fix this
                            Id = (byte)packet.Id,
                            Payload = packet.Payload,
                            Length = (uint)packet.Payload.Length,
                            Time = packet.Time
                        }); ;
                    }

                    spinner.Close();
                }
            }
        }

        public async void OnClearCommand(Window window)
        {
            PacketList.Clear();
            FilteredPacketList.Clear();
        }

        private bool IsPacketVisible(Packet packet)
        {
            if (FilterOutNoise && (packet.Id == 111 || packet.Id == 40 || packet.Id == 58 || packet.Id == 144 || packet.Id == 39))
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

        public async void OnDecodePacketCommand()
        {
            DecodePacket(SelectedPacket);
            this.RaisePropertyChanged(nameof(SelectedPacket));
        }

        public async void OnLoadProtocolCommand(Window window)
        {
            var dlg = new OpenFileDialog();
            dlg.Filters.Add(new FileDialogFilter() { Name = "Bedrock Protocol Definition", Extensions = { "xml" } });
            dlg.AllowMultiple = false;

            var result = await dlg.ShowAsync(window);
            if (result != null && result.Length > 0)
            {
                ProtocolDefinition = ProtocolDefinitionParser.Parse(result[0]);

                var spinner = new SpinnerDialog();
                spinner.DataContext = "Loading...";
                spinner.ShowDialog(window);

                RefreshFilters();

                new Thread(() =>
                {
                    var threadPool = new DedicatedThreadPool(new DedicatedThreadPoolSettings(Environment.ProcessorCount / 2));
                    uint decodedPackets = 0;

                    using (ManualResetEvent resetEvent = new ManualResetEvent(false))
                    {
                        foreach (Packet packet in PacketList)
                        {
                            threadPool.QueueUserWorkItem(() =>
                            {
                                DecodePacket(packet);
                                Interlocked.Increment(ref decodedPackets);
                                resetEvent.Set();
                            });
                        }

                        // all packets read

                        while (PacketList.Count != decodedPackets)
                        {
                            resetEvent.WaitOne();
                            resetEvent.Reset();
                        }

                        // All read and decoded, filter

                        Dispatcher.UIThread.Post(() =>
                        {
                            RefreshFilters();
                            spinner.Close();
                        });
                    }
                }).Start();
            }
        }
    }
}
