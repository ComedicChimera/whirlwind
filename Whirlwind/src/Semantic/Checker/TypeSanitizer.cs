using System;

using Whirlwind.Types;

namespace Whirlwind.Semantic.Checker
{
    partial class Checker
    {
        public static DataType SanitizeType(DataType dt, Exception onError)
        {
            DataType rtVal;

            if (dt is SelfType st)
            {
                if (st.Initialized)
                    rtVal = st.DataType;
                else
                    throw onError;
            }
            else if (dt is GenericSelfType gst)
            {
                if (gst.GenericSelf == null)
                    throw onError;
                else
                    rtVal = gst.GenericSelf;
            }
            else if (dt is GenericSelfInstanceType gsit)
            {
                if (gsit.GenericSelf == null)
                    throw onError;
                else
                {
                    gsit.GenericSelf.CreateGeneric(gsit.TypeList, out DataType result);
                    rtVal = result;
                }
            }
            else
                return dt;

            switch (rtVal)
            {
                case StructType sst:
                    rtVal = sst.GetInstance();
                    break;
                case InterfaceType it:
                    rtVal = it.GetInstance();
                    break;
                case CustomType ct:
                    rtVal = ct.GetInstance();
                    break;
            }

            // perserve value category during sanitization
            rtVal.Category = dt.Category;

            return rtVal;
        }
    }
}
