using System.Collections.Generic;
using System.Linq;

namespace Whirlwind.Types
{
    abstract class DataType
    {
        // store the types interface
        private static Dictionary<DataType, InterfaceType> _interfaces;

        // store constancy
        public bool Constant = false;

        // check if another data type can be coerced to this type
        public virtual bool Coerce(DataType other)
        {
            if (Constant && !other.Constant)
                return false;

            if (other.Classify() == TypeClassifier.NULL || other.Classify() == TypeClassifier.TEMPLATE_PLACEHOLDER)
                return true;

            if (other.Classify() == TypeClassifier.REFERENCE)
                return Coerce(((ReferenceType)other).DataType);

            return _coerce(other);
        }

        // internal coerce method
        protected virtual bool _coerce(DataType other) => false;

        // returns the types interface
        public virtual InterfaceType GetInterface()
        {
            if (_interfaces.Keys.Any(x => x.Coerce(this)))
                return _interfaces.Where(x => x.Key.Coerce(this)).First().Value;

            _interfaces[this] = new InterfaceType();

            return _interfaces[this];
        }

        // get a given data type classifier as a string
        public abstract TypeClassifier Classify();

        // returns a constant copy of a given data type
        public abstract DataType ConstCopy();

        // check two data types for perfect equality (rarely used)
        public abstract bool Equals(DataType other);

        public static bool operator==(DataType a, DataType b)
            => a.Equals(b);

        public static bool operator !=(DataType a, DataType b)
            => !a.Equals(b);
    }

    class NullType : DataType
    {
        public override bool Coerce(DataType other) => true;

        public override TypeClassifier Classify() => TypeClassifier.NULL;

        public override bool Equals(DataType other) => false;

        public override DataType ConstCopy()
            => new NullType() { Constant = true };
    }

    enum TypeClassifier
    {
        SIMPLE,
        ARRAY,
        LIST,
        DICT,
        POINTER,
        STRUCT,
        STRUCT_INSTANCE,
        TUPLE,
        INTERFACE,
        INTERFACE_INSTANCE,
        TYPE_CLASS,
        TYPE_CLASS_INSTANCE,
        FUNCTION,
        FUNCTION_GROUP,
        TEMPLATE,
        TEMPLATE_ALIAS,
        TEMPLATE_PLACEHOLDER,
        PACKAGE,
        NULL,
        REFERENCE,
        AGENT,
        SELF // self referential type
    }
}
