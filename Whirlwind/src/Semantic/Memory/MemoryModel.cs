using System;
using System.Collections.Generic;
using System.Linq;

using Whirlwind.Types;

namespace Whirlwind.Semantic.Memory
{
    struct Owner
    {
        private static int _idCounter = 0;

        public int Id;
        public string Name;

        public Owner(string name)
        {
            Id = _idCounter++;
            Name = name;
        }

        public override bool Equals(object obj)
        {
            if (obj is Owner owner && obj != null)
                return Id == owner.Id && Name == owner.Name;

            return base.Equals(obj);
        }
    }

    class MemoryModel
    {
        private static int _resCounter = 0;

        private readonly Dictionary<int, DataType> _resources;
        private readonly Dictionary<Owner, int> _owners;
        private readonly List<int> _unclaimedResources;

        public MemoryModel()
        {
            _resources = new Dictionary<int, DataType>();
            _owners = new Dictionary<Owner, int>();
            _unclaimedResources = new List<int>();
        }

        public void MakeResource(DataType dt)
        {
            int resId = _resCounter++;

            _resources[resId] = dt;
            _unclaimedResources.Add(resId);
        }

        public bool ClaimResource(string name)
        {
            if (_unclaimedResources.Count > 0)
            {
                if (_owners.Any(x => x.Key.Equals(name)))
                    return false;

                _owners.Add(new Owner(name), _unclaimedResources.Last());
                _unclaimedResources.RemoveLast();

                return true;
            }

            return false;
        }

        public bool ClaimResource(string name, int id)
        {
            if (_unclaimedResources.Contains(id))
            {
                if (_owners.Any(x => x.Key.Equals(name)))
                    return false;

                _owners.Add(new Owner(name), id);
                _unclaimedResources.Remove(id);

                return true;
            }

            return false;
        }

        public Owner GetRecentOwner() => _owners.Last().Key;

        public bool DeleteByOwner(Owner owner)
        {
            if (_owners.Any(x => x.Equals(owner)))
            {
                var id = _owners[owner];
                _owners.Remove(owner);
                _resources.Remove(id);

                return true;
            }

            return false;
        }
    }
}
