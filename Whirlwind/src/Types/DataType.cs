namespace Whirlwind.Types
{
    abstract class DataType
    {
        // store constancy
        private bool _constant = false;
        // store the types interface
        private InterfaceType _interf;

        // returns whether or not the type is constant
        public virtual bool Constant() => _constant;

        // check if another data type can be coerced to this type
        public virtual bool Coerce(DataType other)
        {
            if (other.Classify() == TypeClassifier.NULL || other.Classify() == TypeClassifier.TEMPLATE_PLACEHOLDER)
                return true;

            if (other.Classify() == TypeClassifier.REFERENCE)
                return Coerce(((ReferenceType)other).DataType);

            return _coerce(other);
        }

        // internal coerce method
        protected virtual bool _coerce(DataType other) => false;

        // returns the types interface
        public virtual InterfaceType GetInterface() => _interf;

        // get a given data type classifier as a string
        public abstract TypeClassifier Classify();

        // check two data types for perfect equality (rarely used)
        public abstract bool Equals(DataType other);
    }

    class NullType : DataType
    {
        public override bool Coerce(DataType other) => true;

        public override TypeClassifier Classify() => TypeClassifier.NULL;

        public override bool Equals(DataType other) => false;
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
