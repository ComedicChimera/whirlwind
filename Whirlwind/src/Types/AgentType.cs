using System.Collections.Generic;

using Whirlwind.Semantic;

namespace Whirlwind.Types
{
    class AgentType : DataType
    {
        public string Name { get; private set; }

        private readonly List<Symbol> _state;
        private readonly List<DataType> _handleTypes;

        public AgentType(string name, List<Symbol> state, List<DataType> handleTypes)
        {
            Name = name;
            _state = state;
            _handleTypes = handleTypes;
        }

        // agents can never be stored in variables (they are universally static)
        public override bool Coerce(DataType other) => false;
        public override bool Equals(DataType other) => false;

        public override TypeClassifier Classify() => TypeClassifier.AGENT;
    }
}
