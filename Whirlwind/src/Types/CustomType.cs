using System.Collections.Generic;
using System.Linq;

namespace Whirlwind.Types
{
    class CustomType : DataType
    {
        public string Name { get; private set; }
        public List<CustomInstance> Instances { get; private set; }

        private bool _instance;

        public CustomType(string name)
        {
            Name = name;
            Instances = new List<CustomInstance>();

            Constant = true;
        }

        public bool AddInstance(CustomInstance instance)
        {
            if (Instances.Any(x => x.Equals(instance)))
                return false;

            Instances.Add(instance);
            return true;
        }

        public bool CreateInstance(DataType dt, out CustomAlias cAlias)
        {
            var matches = Instances.Where(x => x is CustomAlias)
                .Select(x => (CustomAlias)x)
                .Where(x => x.Type.Equals(dt));

            if (matches.Count() == 1)
            {
                cAlias = matches.First();
                return true;
            }
            
            cAlias = new CustomAlias(this, new SimpleType());
            return false;
        }

        public CustomType CreateInstance()
        {
            // work on custom instance
        }

        public bool CreateInstance(string name, List<DataType> values, out CustomNewType cnType)
        {
            var matches = Instances.Where(x => x is CustomInstance)
                .Select(x => (CustomNewType)x);

            foreach (var match in matches)
            {
                if (match.Name == name || match.Values.Count == values.Count)
                {
                    if (match.Values.Zip(values, (a, b) => a.Equals(b)).All(x => x))
                    {
                        cnType = new CustomNewType(this, name, values);
                        return true;
                    }
                }
            }

            cnType = new CustomNewType(this, "", new List<DataType>());
            return false;
        }

        public override TypeClassifier Classify() => TypeClassifier.TYPE_CLASS;

        public override bool Equals(DataType other)
        {
            if (other is CustomType cType)
            {
                if (Name != cType.Name)
                    return false;

                if (Instances.Count == cType.Instances.Count)
                    return Instances.Zip(cType.Instances, (a, b) => a.Equals(b)).All(x => x);
            }

            return false;
        }

        public override bool Coerce(DataType other)
        {
            if (other is CustomType)
                return Equals(other);
            else if (other is CustomInstance instance)
                return Equals(instance.Parent);

            return false;
        }

        public override DataType ConstCopy()
            => new CustomType(Name)
            {
                Constant = true,
                Instances = Instances
            };
    }

    abstract class CustomInstance : DataType
    {
        public CustomType Parent { get; protected set; }

        public override TypeClassifier Classify() => TypeClassifier.TYPE_CLASS_INSTANCE;

        public override bool Coerce(DataType other)
        {
            if (other is CustomType)
                return Parent.Equals(other);
            else if (other is CustomInstance cInst)
                return Parent.Equals(cInst.Parent);

            return false;
        }
    }

    class CustomNewType : CustomInstance
    {
        public readonly string Name;
        public readonly List<DataType> Values;

        public CustomNewType(CustomType parent, string name, List<DataType> values)
        {
            Parent = parent;
            Name = name;
            Values = values;
        }

        public override bool Equals(DataType other)
        {
            if (other is CustomNewType cnType)
            {
                if (!Parent.Equals(cnType.Parent) || Name != cnType.Name)
                    return false;

                return Values.Zip(cnType.Values, (a, b) => a.Equals(b)).All(x => x);
            }

            return false;
        }

        public override DataType ConstCopy()
            => new CustomNewType(Parent, Name, Values) { Constant = true };  
    }

    class CustomAlias : CustomInstance
    {
        public readonly DataType Type;

        public CustomAlias(CustomType parent, DataType dt)
        {
            Parent = parent;
            Type = dt;
        }

        public override bool Equals(DataType other)
        {
            if (other is CustomAlias cAlias)
                return Parent.Equals(cAlias.Parent) && Type.Equals(cAlias.Type);

            return false;
        }

        public override DataType ConstCopy()
            => new CustomAlias(Parent, Type) { Constant = true };
    }
}
