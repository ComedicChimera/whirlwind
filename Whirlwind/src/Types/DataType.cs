namespace Whirlwind.Types
{
    interface IDataType
    {
        // coerce other to self
        bool Coerce(IDataType other);
        // get a given data type classifier as a string
        TypeClassifier Classify();
        // check two data types for perfect equality (rarely used)
        bool Equals(IDataType other);
        // return whether or not a given data type is constant
        bool Constant();
        // returns the interface of a given type
        InterfaceType GetInterface(); 
    }

    class DataType
    {
        private bool _constant = false;
        private InterfaceType _interf;
        public bool Coerce(IDataType other)
        {
            if (other.Classify() == TypeClassifier.NULL || other.Classify() == TypeClassifier.TEMPLATE_PLACEHOLDER)
                return true;

            if (other.Classify() == TypeClassifier.REFERENCE)
                return Coerce(((ReferenceType)other).DataType);

            return _coerce(other);
        }

        protected virtual bool _coerce(IDataType other) => false;

        public bool Constant() => _constant;

        public InterfaceType GetInterface() => _interf;
    }

    class NullType : IDataType
    {
        public bool Coerce(IDataType other) => true;

        public TypeClassifier Classify() => TypeClassifier.NULL;

        public bool Equals(IDataType other) => false;
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
        FUNCTION,
        OBJECT,
        OBJECT_INSTANCE,
        TEMPLATE,
        TEMPLATE_ALIAS,
        TEMPLATE_PLACEHOLDER,
        PACKAGE,
        ENUM,
        ENUM_MEMBER,
        NULL,
        REFERENCE,
        AGENT,
        SELF // self referential type
    }
}
