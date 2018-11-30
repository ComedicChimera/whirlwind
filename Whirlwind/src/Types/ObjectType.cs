using Whirlwind.Semantic;

using System;
using System.Linq;
using System.Collections.Generic;

// add partials and interfaces
namespace Whirlwind.Types
{
    class ObjectType : IDataType
    {
        private readonly Dictionary<string, Symbol> _members;
        private readonly List<Tuple<FunctionType, BlockNode>> _constructors;

        public readonly string Name;
        public readonly List<IDataType> Inherits;

        private bool _instance = false, _internal = false;

        public ObjectType(string name, bool instance, bool internalInstance)
        {
            Name = name;
            _members = new Dictionary<string, Symbol>();
            _constructors = new List<Tuple<FunctionType, BlockNode>>();
            Inherits = new List<IDataType>();
            _instance = instance;
            _internal = internalInstance;
        }

        // constructors not copied since they are only needed in the root type
        private ObjectType(string name, Dictionary<string, Symbol> members, List<IDataType> inherits, bool internalInstance)
        {
            Name = name;
            _members = members;
            _constructors = new List<Tuple<FunctionType, BlockNode>>();
            Inherits = inherits;
            _instance = true;
            _internal = internalInstance;
        }

        public bool AddConstructor(FunctionType ft, BlockNode body)
        {
            if (_constructors.Where(x => x.Item1.Coerce(ft)).Count() != 0)
                return false;
            _constructors.Add(new Tuple<FunctionType, BlockNode>(ft, body));
            return true;
        }

        public bool AddInherits(IDataType dt)
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

                if (_internal || !foundSymbol.Modifiers.Contains(Modifier.PRIVATE))
                {
                    member = foundSymbol;
                    return true;
                }
            }

            member = null;
            return false;
        }

        public bool GetConstructor(List<IDataType> parameters, out FunctionType constructor)
        {
            var constructors = _constructors.Where(x => x.Item1.MatchParameters(parameters)).ToList();
            
            if (constructors.Count() == 1)
            {
                constructor = constructors[0].Item1;
                return true;
            }
            constructor = null;
            return false;
        }

        public ObjectType GetInstance()
        {
            return new ObjectType(Name, _members, Inherits, false);
        }

        public ObjectType GetInternalInstance()
        {
            return new ObjectType(Name, _members, Inherits, true);
        }

        public TypeClassifier Classify() => _instance ? TypeClassifier.OBJECT_INSTANCE : TypeClassifier.OBJECT;

        public bool Coerce(IDataType dt)
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
                        if (!e1.Current.Equals(e2.Current))
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
