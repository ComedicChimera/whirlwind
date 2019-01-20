using System.Collections.Generic;

using Whirlwind.Semantic;

namespace Whirlwind.Types
{
    class AgentType : IDataType
    {
        public string Name { get; private set; }

        private List<Symbol> _state;
        private List<IDataType> _handleTypes;

        public AgentType(string name, List<Symbol> state, List<IDataType> handleTypes)
        {
            Name = name;
            _state = state;
            _handleTypes = handleTypes;
        }

        // agents can never be stored in variables (they are universally static)
        public bool Coerce(IDataType other) => false;
        public bool Equals(IDataType other) => false;

        public TypeClassifier Classify() => TypeClassifier.AGENT;
    }
}
