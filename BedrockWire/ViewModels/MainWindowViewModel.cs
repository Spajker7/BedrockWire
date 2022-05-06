using Avalonia.Controls;
using BedrockWire.Models;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Xml;

namespace BedrockWire.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public List<Packet> PacketList { get; set; }
        public ObservableCollection<Packet> FilteredPacketList { get; set; }
        private Dictionary<int, PacketDefinition> PacketDefintions { get; set; }

        private bool filterOutMoveDelta;
        public bool FilterOutMoveDelta
        {
            get => filterOutMoveDelta;
            set {
                filterOutMoveDelta = value;
                RefreshFilters();
            }
        }

        private Packet selectedPacket;
        public Packet SelectedPacket
        {
            get => selectedPacket;
            set => this.RaiseAndSetIfChanged(ref selectedPacket, value);
        }

        public MainWindowViewModel()
        {
            PacketList = new List<Packet>();
            FilteredPacketList = new ObservableCollection<Packet>();
        }

        public async void OnOpenCommand(Window window)
        {
            var dlg = new OpenFileDialog();
            dlg.Filters.Add(new FileDialogFilter() { Name = "BedrockWire Files", Extensions = { "bw" } });
            dlg.AllowMultiple = false;

            var result = await dlg.ShowAsync(window);
            if(result != null && result.Length > 0)
            {
                PacketList = new List<Packet>();
                this.RaisePropertyChanged(nameof(PacketList));

                using (BinaryReader reader = new BinaryReader(new FileStream(result[0], FileMode.Open)))
                {
                    try
                    {
                        while(true)
                        {
                            byte direction = reader.ReadByte();
                            byte packetId = reader.ReadByte();
                            ulong time = reader.ReadUInt64();
                            int length = reader.ReadInt32();
                            byte[] payload = reader.ReadBytes(length);

                            string name = "UNKNOWN_PACKET";
                            bool hasError = true;

                            Dictionary<object, object>? data = new Dictionary<object, object>();

                            if (PacketDefintions != null && PacketDefintions.ContainsKey(packetId))
                            {
                                hasError = false;
                                name = PacketDefintions[packetId].Name;

                                try
                                {
                                    data = PacketDefintions[packetId].PacketDecoder.Decode(payload);
                                }
                                catch (Exception ex)
                                {
                                    hasError = true;
                                }

                                if(data == null)
                                {
                                    data = new Dictionary<object, object>();
                                    hasError = true;
                                }
                            }

                            PacketList.Add(new Packet() { Direction = direction == 0 ? "C -> S" : "S -> C", Id = packetId, Name = name, Payload = payload, Decoded = data, HasError = hasError, Time = time, Length = (ulong) length});
                        }
                        
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine(1);
                    }

                    RefreshFilters();
                }
            }
        }

        private void RefreshFilters()
        {
            FilteredPacketList.Clear();

            foreach(Packet packet in PacketList)
            {
                if(FilterOutMoveDelta && packet.Id == 111)
                {
                    continue;
                }
                FilteredPacketList.Add(packet);
            }
        }

        public async void OnLoadProtocolCommand(Window window)
        {
            var dlg = new OpenFileDialog();
            dlg.Filters.Add(new FileDialogFilter() { Name = "Bedrock Protocol Definition", Extensions = { "xml" } });
            dlg.AllowMultiple = false;

            var result = await dlg.ShowAsync(window);
            if (result != null && result.Length > 0)
            {
                PacketDefintions = new Dictionary<int, PacketDefinition>();

                XmlDocument doc = new XmlDocument();
                doc.Load(result[0]);

                foreach (XmlNode node in doc.DocumentElement.SelectNodes("descendant::packets")[0])
                {
                    if(node.Name == "packet")
                    {
                        int id = Convert.ToInt32(node.Attributes.GetNamedItem("id").InnerText, 16);
                        string name = node.Attributes.GetNamedItem("name").InnerText;
                        

                        PacketDecoder decoder = new PacketDecoder();
                        decoder.Fields = new List<PacketField>();

                        foreach(XmlNode fieldNode in node.SelectNodes("descendant::field"))
                        {
                            decoder.Fields.Add(new PacketField() { Name = fieldNode.Attributes.GetNamedItem("name").InnerText, Type = fieldNode.Attributes.GetNamedItem("type").InnerText });
                        }

                        PacketDefintions[id] = new PacketDefinition() { Name = name, PacketDecoder = decoder };
                    }
                }
            }
        }
    }
}
