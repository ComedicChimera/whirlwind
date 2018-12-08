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
    }

    class DataType
    {
        private DataType() { }
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
        ENUM_MEMBER
    }
}
