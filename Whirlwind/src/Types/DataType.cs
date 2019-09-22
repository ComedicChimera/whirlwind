using System.Collections.Generic;
using System.Linq;

namespace Whirlwind.Types
{
    static class InterfaceRegistry
    {
        // store the type interfaces
        public static readonly Dictionary<DataType, InterfaceType> StandardInterfaces
            = new Dictionary<DataType, InterfaceType>();

        // store the generic bound type interfaces
        public static readonly Dictionary<GenericBindDiscriminator, GenericBinding> GenericInterfaces
            = new Dictionary<GenericBindDiscriminator, GenericBinding>();

        // we check generic interface matches first
        // if a generic match occurs but a derivation fails the
        // type access is explicitly invalid regardless of any future matches
        // if the generic match is successful, then we use that type interface to
        // derive into any future matches and if none exist, still return true
        // if there are no generic matches then use the regular pool
        // we only return false if a generic derivation fails or if no matches
        // in either category are found. 
        public static bool GetTypeInterface(DataType dt, out InterfaceType typeInterf)
        {
            typeInterf = null;

            foreach (var item in GenericInterfaces)
            {
                if (item.Key.MatchType(dt, out List<DataType> inferredTypes))
                {
                    item.Value.Body.CreateGeneric(inferredTypes, out DataType rawType);

                    typeInterf = (InterfaceType)rawType;

                    foreach (var elem in item.Value.StandardImplements)
                    {
                        if (!elem.Derive(typeInterf, true))
                            return false;
                    }

                    var genericVars = Enumerable.Range(0, inferredTypes.Count)
                        .ToDictionary(i => item.Key.GenericVariables[i].Name, i => inferredTypes[i]
                        );

                    foreach (var elem in item.Value.GenericImplements)
                    {
                        if (!elem.CreateGeneric(inferredTypes, out DataType res))
                            return false;

                        res.Constant = false;

                        if (res is InterfaceType it)
                        {
                            if (!it.Derive(typeInterf, true))
                                return false;
                        }
                        else
                            return false;
                    }

                    break;
                }
            }

            foreach (var item in StandardInterfaces)
            {
                if (MatchStdType(dt, item.Key))
                {
                    if (typeInterf != null)
                    {
                        var baseInterf = item.Value;

                        typeInterf.Derive(baseInterf, true);
                    }
                    else
                        typeInterf = item.Value;

                    return true;
                }
            }

            if (typeInterf != null)
                return true;

            typeInterf = null;
            return false;
        }

        private static bool MatchStdType(DataType lookup, DataType other)
        {
            lookup = lookup.ConstCopy();
            other = other.ConstCopy();

            if (lookup is StructType st && other is StructType)
                return st.Coerce(other);
            else if (lookup is CustomInstance cnt && other is CustomType)
                return other.Equals(cnt.Parent);
            else if (lookup is ArrayType at && other is ArrayType oat && oat.Size == -1)
                return oat.ElementType.Equals(at.ElementType);

            return other.Equals(lookup);
        }
    }

    abstract class DataType
    {
        // store constancy
        public bool Constant = false;

        // check if another data type can be coerced to this type
        public virtual bool Coerce(DataType other)
        {
            if (other is IncompleteType)
                return true;

            if (!Constant && other.Constant)
                return false;

            // super form should never be used as a literal type
            if (other is InterfaceType it && it.SuperForm)
                return false;

            if (other.Classify() == TypeClassifier.NULL || other.Classify() == TypeClassifier.GENERIC_PLACEHOLDER)
                return true;

            if (other is GenericAlias gp)
                return Coerce(gp.ReplacementType);

            return _coerce(other);
        }

        // internal coerce method
        protected virtual bool _coerce(DataType other) => false;

        protected abstract bool _equals(DataType other);

        // returns the types interface
        public virtual InterfaceType GetInterface()
        {
            if (InterfaceRegistry.GetTypeInterface(this, out InterfaceType ift))
                return ift;

            if (new[] { TypeClassifier.GENERIC_ALIAS, TypeClassifier.GENERIC, TypeClassifier.GENERIC_PLACEHOLDER ,
                TypeClassifier.GENERIC_GROUP, TypeClassifier.FUNCTION_GROUP }.Contains(Classify()))
                return new InterfaceType("TypeInterf:" + ToString());

            // don't create entries for nulls and voids
            if (this is NoneType || this is NullType)
                return new InterfaceType("TypeInterf:" + ToString());

            InterfaceRegistry.StandardInterfaces[this] = new InterfaceType("TypeInterf:" + ToString());

            return InterfaceRegistry.StandardInterfaces[this];
        }

        // check two data types for perfect equality
        public bool Equals(DataType other)
        {
            if (other == null)
                return false;

            if (other is GenericAlias gp)
                return Equals(gp.ReplacementType);

            if (Constant == other.Constant)
                return _equals(other);

            return false;
        }

        public override bool Equals(object obj)
        {
            if (obj is DataType dt && obj != null)
                return Equals(dt);

            return base.Equals(obj);
        }

        public DataType Copy()
        {
            var newDt = ConstCopy();
            newDt.Constant = Constant;

            return newDt;
        }


        // get a given data type classifier as a string
        public abstract TypeClassifier Classify();

        // returns a constant copy of a given data type
        public abstract DataType ConstCopy();
    }

    // pure none type; means nothing (no value)
    class NoneType : DataType
    {
        public override bool Coerce(DataType other) => true;

        public override TypeClassifier Classify() => TypeClassifier.NONE;

        protected override bool _equals(DataType other) => other is NoneType;

        public override DataType ConstCopy()
            => new NoneType() { Constant = true };

        public override string ToString() => "none";
    }

    // any type; means something but with no type
    class AnyType : DataType
    {
        public override bool Coerce(DataType other) => true;

        public override TypeClassifier Classify() => TypeClassifier.ANY;

        protected override bool _equals(DataType other) => other is AnyType;

        public override DataType ConstCopy()
            => new AnyType() { Constant = true };

        public override string ToString() => "any";
    }

    // type that means there is a value, but it has no type
    // this is still considered a "void type", but it isn't really
    class NullType : DataType
    {
        public override bool Coerce(DataType other) => true;

        public override TypeClassifier Classify() => TypeClassifier.NULL;

        protected override bool _equals(DataType other) => true;

        public override DataType ConstCopy()
           => new NullType() { Constant = true };

        public override string ToString() => "null";
    }

    class IncompleteType : DataType
    {
        public override bool Coerce(DataType other) => true;

        public override TypeClassifier Classify() => TypeClassifier.INCOMPLETE;

        protected override bool _equals(DataType other) => false;

        public override DataType ConstCopy()
            => new IncompleteType() { Constant = true };
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
        TYPE_CLASS,
        TYPE_CLASS_INSTANCE,
        FUNCTION,
        FUNCTION_GROUP,
        GENERIC,
        GENERIC_ALIAS,
        GENERIC_PLACEHOLDER,
        GENERIC_GROUP,
        PACKAGE,
        NONE,
        NULL,
        ANY,
        INCOMPLETE,
        SELF, // self-referential type
        GENERIC_SELF, // generic self-referential type
        GENERIC_SELF_INSTANCE // instance of a generic self type
    }
}
