using System;
using System.Collections.Generic;
using System.Text;

namespace Whirlwind.Types
{
    class Future : IDataType
    {
        public readonly IDataType Type;

        public Future(IDataType type)
        {
            Type = type;
        }

        public string Classify() => "FUTURE";
        public bool Coerce(IDataType other) => other == this;
    }
}
