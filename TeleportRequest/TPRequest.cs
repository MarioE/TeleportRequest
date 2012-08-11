using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TeleportRequest
{
    public class TPRequest
    {
        public bool dir;
        public byte dst;
        public byte src;
        public int timeout;

        public TPRequest(byte src, byte dst, bool dir, int timeout)
        {
            this.dir = dir;
            this.dst = dst;
            this.src = src;
            this.timeout = timeout;
        }
    }
}
