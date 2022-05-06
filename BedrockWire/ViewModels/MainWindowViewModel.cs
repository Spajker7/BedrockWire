using Avalonia.Controls;
using BedrockWire.Models;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
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

                using (BinaryReader reader = new BinaryReader(new DeflateStream(new FileStream(result[0], FileMode.Open), CompressionMode.Decompress)))
                {
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
                            bool hasError = true;

                            Dictionary<object, object>? data = new Dictionary<object, object>();

                            if (PacketDefintions != null && PacketDefintions.ContainsKey(packetId))
                            {
                                hasError = false;
                                name = PacketDefintions[packetId].Name;

                                try
                                {
                                    using (MemoryStream stream = new MemoryStream(payload))
                                    {
                                        using (BinaryReader reader2 = new BinaryReader(stream))
                                        {
                                            data = PacketDefintions[packetId].PacketDecoder.Decode(reader2);
                                            if (stream.Position != stream.Length)
                                            {
                                                hasError = true;
                                            }
                                        }
                                    }

                                }
                                catch (Exception ex)
                                {
                                    hasError = true;
                                }

                                if (data == null)
                                {
                                    data = new Dictionary<object, object>();
                                    hasError = true;
                                }
                            }

                            PacketList.Add(new Packet() { Direction = direction == 0 ? "C -> S" : "S -> C", Id = packetId, Name = name, Payload = payload, Decoded = data, HasError = hasError, Time = time, Length = (ulong)length });
                        }

                    }
                    catch (Exception ex)
                    {
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

                if(!string.IsNullOrEmpty(FilterText))
                {
                    if(FilterText.StartsWith("0x") && FilterText.Length > 2)
                    {
                        try
                        {
                            int id = Convert.ToInt32(FilterText, 16);
                            if (packet.Id != id)
                            {
                                continue;
                            }
                        }
                        catch(Exception)
                        {
                        }
                        
                    }
                    else if(!packet.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                FilteredPacketList.Add(packet);
            }
        }

        private PacketField ParseField(XmlNode node)
        {
            PacketField packetField = new PacketField() {
                Name = node.Attributes.GetNamedItem("name").InnerText,
                Type = node.Attributes.GetNamedItem("type").InnerText,
                IsSwitch = node.Attributes.GetNamedItem("switch") != null,
                Case = node.Attributes.GetNamedItem("case")?.InnerText,
                Conditional = node.Attributes.GetNamedItem("conditional")?.InnerText,
                SubFields = new List<PacketField>(),
                IgnoreContainer = bool.Parse(node.Attributes.GetNamedItem("ignoreContainer")?.InnerText ?? "false")
            };

            foreach (XmlNode fieldNode in node.SelectNodes("child::field"))
            {
                packetField.SubFields.Add(ParseField(fieldNode));
            }

            return packetField;
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

                foreach (XmlNode node in doc.DocumentElement.SelectNodes("child::types")[0])
                {
                    if (node.Name == "type")
                    {
                        string name = node.Attributes.GetNamedItem("name").InnerText;


                        PacketDecoder decoder = new PacketDecoder();
                        decoder.Fields = new List<PacketField>();

                        foreach (XmlNode fieldNode in node.SelectNodes("child::field"))
                        {
                            decoder.Fields.Add(ParseField(fieldNode));
                        }

                        FieldDecoders.CustomDecoders.Add(name, decoder);
                    }
                }

                foreach (XmlNode node in doc.DocumentElement.SelectNodes("child::packets")[0])
                {
                    if(node.Name == "packet")
                    {
                        int id = Convert.ToInt32(node.Attributes.GetNamedItem("id").InnerText, 16);
                        string name = node.Attributes.GetNamedItem("name").InnerText;
                        

                        PacketDecoder decoder = new PacketDecoder();
                        decoder.Fields = new List<PacketField>();

                        foreach(XmlNode fieldNode in node.SelectNodes("child::field"))
                        {
                            decoder.Fields.Add(ParseField(fieldNode));
                        }

                        PacketDefintions[id] = new PacketDefinition() { Name = name, PacketDecoder = decoder };
                    }
                }
            }
        }
    }
}
