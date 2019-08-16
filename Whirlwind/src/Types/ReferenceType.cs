using System.Collections.Generic;
using System.Linq;

namespace Whirlwind.Types
{
    // self referential type
    class SelfType : DataType
    {
        private readonly string _name;

        // not readonly to allow late modification
        public DataType DataType;
        public bool Initialized = false;

        public SelfType(string name, DataType dt)
        {
            _name = name;
            DataType = dt;
        }

        protected override bool _equals(DataType other)
        {
            if (other is SelfType st && _name == st._name)
                return true;
            else if (DataType.Classify() == TypeClassifier.INTERFACE &&
                other.Classify() == TypeClassifier.INTERFACE_INSTANCE)
            {
                // allow for self interface types to not be problematic
                return ((InterfaceType)DataType).GetInstance().Equals(other);
            }
            else if (DataType.Classify() == TypeClassifier.STRUCT &&
                other.Classify() == TypeClassifier.STRUCT_INSTANCE)
            {
                // allow for self struct types to not be problematic
                return ((StructType)DataType).GetInstance().Equals(other);
            }

            return DataType.Equals(other);
        }

        protected override bool _coerce(DataType other)
        {
            if (other is SelfType st && _name == st._name)
                return true;

            else if (DataType.Classify() == TypeClassifier.INTERFACE &&
                other.Classify() == TypeClassifier.INTERFACE_INSTANCE)
            {
                // allow for self interface types to not be problematic
                return ((InterfaceType)DataType).GetInstance().Coerce(other);
            }
            else if (DataType.Classify() == TypeClassifier.STRUCT &&
                other.Classify() == TypeClassifier.STRUCT_INSTANCE)
            {
                // allow for self struct types to not be problematic
                return ((StructType)DataType).GetInstance().Coerce(other);
            }

            return DataType.Coerce(other);
        }

        public override TypeClassifier Classify() => TypeClassifier.SELF;

        public override DataType ConstCopy()
            => new SelfType(_name, DataType) { Constant = true };
    }

    // used as a self-referential type for generics
    class GenericSelfType : DataType
    {
        private readonly string _name;
        private List<GenericSelfInstanceType> _selfInstances;

        public GenericType GenericSelf;

        public readonly List<GenericVariable> GenericVariables;

        public GenericSelfType(string name, List<GenericVariable> genVars)
        {
            _name = name;
            GenericVariables = genVars;
            _selfInstances = new List<GenericSelfInstanceType>();
        }

        // possibly add check to see where failure occured specifically
        public void SetGeneric(GenericType gt)
        {
            GenericSelf = gt;

            foreach (var selfInstance in _selfInstances)
                selfInstance.GenericSelf = gt;
        }

        public bool GetInstance(List<DataType> types, out DataType gsit)
        {
            gsit = null;

            if (GenericVariables.Count != types.Count)
                return false;

            for (int i = 0; i < types.Count; i++)
            {
                if (GenericVariables[i].Restrictors.Count > 0 &&
                    !GenericVariables[i].Restrictors.Any(x => x.Coerce(types[i])))
                    return false;
            }

            if (_selfInstances.Any(x => x.TypeList.EnumerableEquals(types)))
                gsit = _selfInstances.Where(x => x.TypeList.EnumerableEquals(types)).First();
            else
            {
                _selfInstances.Add(new GenericSelfInstanceType(types, _name));
                gsit = _selfInstances.Last();
            }

            return true;
        }

        public override TypeClassifier Classify() => TypeClassifier.GENERIC_SELF;

        public override DataType ConstCopy()
        {
            return new GenericSelfType(_name, GenericVariables)
            {
                GenericSelf = GenericSelf,

                Constant = true
            };
        }

        protected override bool _coerce(DataType other)
        {
            if (other is GenericSelfType gst)
                return _name == gst._name;
            else if (other is GenericType)
                return GenericSelf != null && GenericSelf.Coerce(other);

            return false;
        }

        protected override bool _equals(DataType other)
        {
            if (other is GenericSelfType gst)
                return _name == gst._name;
            else if (other is GenericType)
                return GenericSelf != null && GenericSelf.Equals(other);

            return false;
        }
    }

    // used for handling instances of generic self-types (ie. when things get rough)
    class GenericSelfInstanceType : DataType
    {
        private readonly string _name;

        public readonly List<DataType> TypeList;

        public GenericType GenericSelf;

        public GenericSelfInstanceType(List<DataType> typeList, string name)
        {
            TypeList = typeList;
            _name = name;
        }

        public override TypeClassifier Classify() => TypeClassifier.GENERIC_SELF_INSTANCE;

        public override DataType ConstCopy() => new GenericSelfInstanceType(TypeList, _name) { Constant = true };

        protected override bool _coerce(DataType other)
        {
            if (other is GenericSelfInstanceType gsit)
                return _name == gsit._name;
            else if (other is GenericType)
                return GenericSelf != null && GenericSelf.Coerce(other);
            else if (GenericSelf != null && GenericSelf.CreateGeneric(TypeList, out DataType result))
                return result.Coerce(other);

            return false;
        }

        protected override bool _equals(DataType other)
        {
            if (other is GenericSelfInstanceType gsit)
                return _name == gsit._name && TypeList.EnumerableEquals(gsit.TypeList);
            else if (other is GenericType)
                return GenericSelf != null && GenericSelf.Equals(other);
            else if (GenericSelf != null && GenericSelf.CreateGeneric(TypeList, out DataType result))
            {
                if (result is InterfaceType it)
                    result = it.GetInstance();
                else if (result is StructType st)
                    result = st.GetInstance();

                return result.Equals(other);
            }


            return false;
        }
    }
}
