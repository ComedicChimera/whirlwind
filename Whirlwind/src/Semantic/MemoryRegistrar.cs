using System.Collections.Generic;

namespace Whirlwind.Semantic
{
    // The MemoryRegistrar Class
    // Facilitates the majority of Whirlwind's memory model
    // from an abstract side by keeping track of owners and resources
    // Note: in owner table: -1 res id == deleted/doesn't exist
    // and -2 res id == surrogate owner (like an owning function argument)
    // with no specific resource tied to it
    // and -3 res id == struct owner (like an owning struct member variable)
    class MemoryRegistrar
    {
        private static int _ownerIdCounter = 0, _resourceIdCounter = 0;

        private readonly Dictionary<int, int> _ownerTable;
        private readonly List<int> _unclaimedResources;

        public MemoryRegistrar()
        {
            _ownerTable = new Dictionary<int, int>();
            _unclaimedResources = new List<int>();
        }

        public void MakeResource()
        {
            _unclaimedResources.Add(_resourceIdCounter++);
        }

        public List<int> GetAllUnclaimedResources()
            => _unclaimedResources;

        // creates a new owner with nothing
        public int MakeOwner(int resource)
        {
            int ownerId = _ownerIdCounter++;

            _ownerTable[ownerId] = resource;

            return ownerId;
        }

        // -1 id means failure
        public int ClaimOwnership(int resId)
        {
            if (_unclaimedResources.Contains(resId))
            {
                _unclaimedResources.Remove(resId);

                int ownerId = _ownerIdCounter++;
                _ownerTable[ownerId] = resId;

                return ownerId;
            }

            return -1;
        }

        // -1 id means failure
        public int GetOwnedResource(int ownerId)
        {
            if (_ownerTable.ContainsKey(ownerId))
                return _ownerTable[ownerId];

            return -1;
        }

        // implicitly creates owner if necessary
        public bool TransferOwnership(int oldOwner, int newOwner)
        {
            if (_ownerTable.ContainsKey(oldOwner))
            {
                if (_ownerTable.ContainsKey(newOwner) && _ownerTable[newOwner] != -1)
                    return false;
                else if (_ownerTable[oldOwner] == -3)
                    return false;

                if (!_ownerTable.ContainsKey(newOwner) || _ownerTable[newOwner] > -2)
                    _ownerTable[newOwner] = _ownerTable[oldOwner];

                _ownerTable[oldOwner] = -1;

                return true;
            }

            return false;
        }
        
        public bool DeleteResource(int ownerId)
        {
            if (_ownerTable.ContainsKey(ownerId))
            {
                _ownerTable[ownerId] = -1;

                return true;
            }

            return false;
        }
    }
}
