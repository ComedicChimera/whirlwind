namespace Whirlwind.Types
{
    interface IDataType
    {
        // coerce other to self
        bool Coerce(IDataType other);
        // get a given data type classifier as a string
        string Classify();
    }
}
