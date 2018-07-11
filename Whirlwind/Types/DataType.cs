namespace Whirlwind.Types
{
    interface IDataType
    {
        // coerce other to self
        bool Coerce(IDataType other);
        // get a given data type classifier as a string
        string Classify();
    }

    static class TypeCast
    {
        static bool DynamicCast(IDataType dt1, IDataType dt2) => false;

        // add literal parameter once valid
        static bool StaticCast() => false;

        static bool ValidityCast(IDataType dt1, IDataType dt2) => false;
    }

}
