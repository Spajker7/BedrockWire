using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BedrockWire.Models
{
    public class Packet : ReactiveObject
    {
        public uint OrderId { get; set; }
        public string Direction { get; set; }
        public int Id { get; set; }
        public ulong Time { get; set; }
        public ulong Length { get; set; }
        public byte[] Payload { get; set; }
        

        // Only name, decoded and error can be changed in runtime
        private string _name;
        private Dictionary<object, object> _decoded;
        private string _error;
        private string _decodeTime;

        public string Name 
        {
            get { return _name; }
            set { this.RaiseAndSetIfChanged(ref _name, value); }
        }
        public Dictionary<object, object> Decoded
        {
            get { return _decoded; }
            set { this.RaiseAndSetIfChanged(ref _decoded, value); }
        }
        public string Error
        {
            get { return _error; }
            set { this.RaiseAndSetIfChanged(ref _error, value); }
        }

        public string DecodeTime
        {
            get { return _decodeTime; }
            set { this.RaiseAndSetIfChanged(ref _decodeTime, value); }
        }
    }
}
