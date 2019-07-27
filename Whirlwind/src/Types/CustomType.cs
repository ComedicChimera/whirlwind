using System.Collections.Generic;
using System.Linq;

namespace Whirlwind.Types
{
    class CustomType : DataType
    {
        public string Name { get; private set; }
        public List<CustomInstance> Instances { get; private set; }

        public CustomType(string name)
        {
            Name = name;
            Instances = new List<CustomInstance>();

            Constant = true;
        }

        public CustomType(CustomType ct)
        {
            Name = ct.Name;
            Instances = ct.Instances;
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
            
            cAlias = new CustomAlias(this, new VoidType());
            return false;
        }

        // doesn't actually create an instance, just removes the constancy from the type class
        public CustomType GetInstance() => new CustomType(this);

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

        public bool GetInstanceByName(string name, out CustomNewType result)
        {
            var matches = Instances.Where(x => x is CustomNewType)
                .Select(x => (CustomNewType)x)
                .Where(x => x.Name == name)
                .ToList();

            result = matches.FirstOrDefault();

            return result != null;
        }

        public override TypeClassifier Classify() => TypeClassifier.TYPE_CLASS;

        protected override bool _equals(DataType other)
        {
            if (other is CustomType cType)
                return Name == cType.Name;

            return false;
        }

        protected override bool _coerce(DataType other)
        {
            if (other is CustomType)
                return Equals(other);
            else if (other is CustomInstance instance)
                return Equals(instance.Parent.GetInstance());
            else
                return Instances.Where(x => x is CustomAlias).Any(x => ((CustomAlias)x).Type.Coerce(other));
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

        protected override bool _coerce(DataType other)
        {
            if (other is CustomType)
                return Parent.Equals(other.ConstCopy());
            else if (other is CustomInstance cInst)
                return Parent.Equals(cInst.Parent);

            var aliases = Parent.Instances.Where(x => x is CustomAlias)
                .Select(x => (CustomAlias)x);

            if (aliases.Count() > 0 && aliases.Any(x => x.Type.Coerce(other)))
                return true;

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

        protected override bool _equals(DataType other)
        {
            if (other is CustomNewType cnType)
            {
                if (!Parent.Equals(cnType.Parent) || Name != cnType.Name)
                    return false;

                return true;
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

        protected override bool _equals(DataType other)
        {
            if (other is CustomAlias cAlias)
                return Parent.Equals(cAlias.Parent) && Type.Equals(cAlias.Type);

            return false;
        }

        public override DataType ConstCopy()
            => new CustomAlias(Parent, Type) { Constant = true };
    }
}
