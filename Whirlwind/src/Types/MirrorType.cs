using System.Collections.Generic;

namespace Whirlwind.Types
{
    static class MirrorType
    {
        public static StructType Element(IDataType dt)
        {
            var elementStruct = new StructType("Element", false);
            elementStruct.AddMember("next", new SimpleType(SimpleType.DataType.BOOL));
            elementStruct.AddMember("val", dt);

            return elementStruct;
        }

        public static ObjectType Future(IDataType dt)
        {
            // add body to future type
            return new ObjectType("Future", true, false);
        }

        public static ObjectType BaseException()
        {
            // add body
            return new ObjectType("Exception", true, false);
        }
    }
}
