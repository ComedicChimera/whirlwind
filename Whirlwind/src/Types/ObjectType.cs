using Whirlwind.Semantic;

using System;
using System.Linq;
using System.Collections.Generic;

// add partials and interfaces
namespace Whirlwind.Types
{
    class ObjectType : DataType, IDataType
    {
        private readonly Dictionary<string, Symbol> _members;
        private readonly List<Tuple<FunctionType, bool>> _constructors;

        public readonly string Name;
        public readonly List<IDataType> Inherits;

        private bool _instance = false;

        public bool Internal { get; private set; }

        public ObjectType(string name, bool instance, bool internalInstance)
        {
            Name = name;
            _members = new Dictionary<string, Symbol>();
            _constructors = new List<Tuple<FunctionType, bool>>();
            Inherits = new List<IDataType>();
            _instance = instance;
            Internal = internalInstance;
        }

        // constructors not copied since they are only used in the static type
        private ObjectType(ObjectType obj, bool internalInstance)
        {
            Name = obj.Name;
            _members = obj._members;
            _constructors = new List<Tuple<FunctionType, bool>>();
            Inherits = obj.Inherits;
            _instance = true;
            Internal = internalInstance;
        }

        public bool AddConstructor(FunctionType ft, bool priv)
        {
            if (_constructors.Where(x => x.Item1.Coerce(ft)).Count() != 0)
                return false;

            _constructors.Add(new Tuple<FunctionType, bool>(ft, priv));
            return true;
        }

        public bool AddInherit(IDataType dt)
        {
            if (Inherits.Contains(dt))
            {
                Inherits.Add(dt);
                return true;
            }
            return false;
        }

        public bool AddMember(Symbol member)
        {
            if (!_members.ContainsKey(member.Name))
            {
                _members.Add(member.Name, member);
                return true;
            }

            return false;
        }

        public bool GetMember(string name, out Symbol member)
        {
            if (_members.ContainsKey(name))
            {
                Symbol foundSymbol = _members[name];

                if (Internal || !foundSymbol.Modifiers.Contains(Modifier.PRIVATE))
                {
                    member = foundSymbol;
                    return true;
                }
            }

            member = null;
            return false;
        }

        public bool GetConstructor(ArgumentList arguments, out FunctionType constructor)
        {
            var constructors = _constructors.Where(x => x.Item1.MatchArguments(arguments) && (!x.Item2 || Internal)).ToList();
            
            if (constructors.Count() == 1)
            {
                constructor = constructors[0].Item1;
                return true;
            }
            constructor = null;
            return false;
        }

        public ObjectType GetInstance()
            => new ObjectType(this, false);

        public ObjectType GetInternalInstance()
            => new ObjectType(this, true);

        public TypeClassifier Classify() => _instance ? TypeClassifier.OBJECT_INSTANCE : TypeClassifier.OBJECT;

        protected sealed override bool _coerce(IDataType dt)
        {
            if (_instance && dt.Classify() == TypeClassifier.OBJECT_INSTANCE)
            {
                ObjectType ot = (ObjectType)dt;

                if (ot.Name != Name)
                    return false;
                else if (ot.Inherits.Count != Inherits.Count || ot._members.Count != _members.Count)
                    return false;
                else if (!Enumerable.Range(0, Inherits.Count).All(i => Inherits[i].Coerce(ot.Inherits[i])))
                    return false;

                using (var e1 = _members.GetEnumerator())
                using (var e2 = ot._members.GetEnumerator())
                {
                    while (e1.MoveNext() && e2.MoveNext())
                    {
                        if (!e1.Current.Value.Equals(e2.Current.Value))
                            return false;
                    }
                }

                return true;
            }

            return false;
        }

        public bool Equals(IDataType dt) => Coerce(dt);
    }
}
