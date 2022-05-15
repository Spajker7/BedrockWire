using MiNET.Utils.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiNET
{
    public class MiNetServer
    {
        public static DedicatedThreadPool FastThreadPool { get; set; } = new DedicatedThreadPool(new DedicatedThreadPoolSettings(100, "Fast_Thread"));
        public static RecyclableMemoryStreamManager MemoryStreamManager { get; set; } = new RecyclableMemoryStreamManager();
    }
}
