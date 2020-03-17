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
            
            cAlias = new CustomAlias(this, new NoneType());
            return false;
        }

        // returns a custom opaque type (used type representing a custom instance)
        public CustomInstance GetInstance() => new CustomOpaqueType(this);

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

        public override string ToString()
            => Name;

        public override uint SizeOf()
        {
            if (Instances.Any(x => x is CustomNewType cnt && cnt.Values.Count > 0))
                return WhirlGlobals.POINTER_SIZE + 6;

            return 2;
        }
    }

    abstract class CustomInstance : DataType
    {
        public CustomType Parent { get; protected set; }
        public bool NeedsConstruction = false;

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

        public override string ToString()
            => Parent.Name;

        public override uint SizeOf() => Parent.SizeOf();
    }

    // used as CustomInstance without specific value
    class CustomOpaqueType : CustomInstance
    {
        public CustomOpaqueType(CustomType parent)
        {
            Parent = parent;
        }

        public override DataType ConstCopy() 
            => new CustomOpaqueType(Parent) { Constant = true };

        protected override bool _equals(DataType other)
        {
            if (other is CustomType ct)
                return Parent.Equals(ct);
            else if (other is CustomInstance)
                return other is CustomOpaqueType cot && Parent.Equals(cot.Parent);

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

            NeedsConstruction = Values.Count > 0;
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

        public override string ToString()
            => Parent.Name + "::" + Name;
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

        public override string ToString()
            => Parent.Name + "::" + Type.ToString();
    }
}
