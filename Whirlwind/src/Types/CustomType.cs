using System;
using System.Collections.Generic;
using System.Text;

namespace Whirlwind.Types
{
    class CustomType : DataType
    {
        private readonly List<CustomInstance> _instances;

        public CustomType(List<CustomInstance> instances)
        {
            _instances = instances;
        }

        public override TypeClassifier Classify() => TypeClassifier.TYPE_CLASS;

        public override bool Equals(DataType other)
        {
            throw new NotImplementedException();
        }

        public override bool Coerce(DataType other)
        {
            return base.Coerce(other);
        }
    }

    abstract class CustomInstance : DataType
    {
        public CustomType Parent { get; private set; }

        public override TypeClassifier Classify() => TypeClassifier.TYPE_CLASS_INSTANCE;
    }

    class CustomNewType : CustomInstance
    {
        public override bool Equals(DataType other)
        {
            throw new NotImplementedException();
        }

        public override bool Coerce(DataType other)
        {
            return base.Coerce(other);
        }
    }

    class CustomAlias : CustomInstance
    {
        public override bool Equals(DataType other)
        {
            throw new NotImplementedException();
        }

        public override bool Coerce(DataType other)
        {
            return base.Coerce(other);
        }
    }
}
