using System;
using System.Collections.Generic;
using System.Text;

namespace Whirlwind.Types
{
    static class MirrorType
    {
        public static StructType Future(DataType returnType)
            // find way to construct actual future type
            => new StructType("Future", false);
    }
}
