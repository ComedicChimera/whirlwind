using System.Collections.Generic;

namespace Whirlwind.Types
{
    static class MirrorType
    {
        public static StructType Element(IDataType dt)
        {
            var elementStruct = new StructType("Element");
            elementStruct.AddMember("next", new SimpleType(SimpleType.DataType.BOOL));
            elementStruct.AddMember("val", dt);

            return elementStruct;
        }

        public static ObjectInstance Future(IDataType dt)
        {
            // add body to future type
            return new ObjectInstance("Future", new Semantic.SymbolTable(), new List<IDataType>(), false);
        }
    }
}
