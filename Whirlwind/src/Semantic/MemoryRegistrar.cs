using System;
using System.Collections.Generic;
using System.Text;

namespace Whirlwind.Semantic
{
    class MemTable
    {
        private int[] _scopePath;
        MemScope _tableScope;

        private class MemOwner
        {
            public int OwnerId;
            public List<int> MarkedScopes;

            public MemOwner(int id)
            {
                OwnerId = id;
            }
        }

        private class MemScope
        {
            public List<MemScope> SubScopes;
            public List<MemOwner> Owners;

            public MemScope()
            {
                SubScopes = new List<MemScope>();
                Owners = new List<MemOwner>();
            }           
        }

        public MemTable()
        {
            _scopePath = new int[0];
        }

        public void AddOwner(int id)
        {
            MemScope scope = _tableScope;

            for (int i = 0; i < _scopePath.Length; i++)
                scope = _tableScope.SubScopes[i];

            scope.Owners.Add(new MemOwner(id));
        }

        public void AddScope()
        {
            MemScope scope = _tableScope;

            for (int i = 0; i < _scopePath.Length; i++)
                scope = _tableScope.SubScopes[i];

            scope.SubScopes.Add(new MemScope());
        }

        public void DescendScope()
        {

        }

        public void AscendScope()
        {

        }
    }

    class MemoryRegistrar
    {
        private bool _unclaimedResources = false;
        private MemTable _memTable;
        private int _idGen = 0;

        public MemoryRegistrar()
        {
            _memTable = new MemTable();
        }

        public void Alloc()
        {
            if (!_unclaimedResources)
                _unclaimedResources = true;
        }

        public int Claim()
        {
            _memTable.AddOwner(++_idGen);
            return _idGen;
        }

        public void Mark(int id)
        {

        }

        public int Transfer(int id)
        {
            return 0;
        }

        public bool Validate()
        {
            return false;
        }

        public void Begin(bool conditional)
        {

        }
    }
}
