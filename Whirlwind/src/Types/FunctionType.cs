﻿using System.Collections.Generic;
using Whirlwind.Semantic;

namespace Whirlwind.Types
{
    // declared parameters
    struct Parameter
    {
        public string Name;
        public IDataType DataType;
        public bool Optional;
        public bool Indefinite;
        public TreeNode DefaultValue;

        public Parameter(string name, IDataType dt)
        {
            Name = name;
            DataType = dt;
            Optional = false;
            Indefinite = false;
            DefaultValue = new TreeNode("", DataType);
        }

        public Parameter(string name, IDataType dt, bool optional, bool indefinite, TreeNode defaultVal)
        {
            Name = name;
            DataType = dt;
            Optional = optional;
            Indefinite = indefinite;
            DefaultValue = defaultVal;
        }

        public bool Compare(Parameter other)
        {
            if (other.Name != Name)
                return false;
            if (!DataType.Coerce(other.DataType))
                return false;
            if (!Indefinite != other.Indefinite)
                return false;
            return true;
        }
    }

    // incoming parameters
    struct ParameterValue
    {
        public bool HasName;
        public string Name;
        public IDataType DataType;

        public ParameterValue(IDataType dt)
        {
            HasName = false;
            Name = "";
            DataType = dt;
        }

        public ParameterValue(string name, IDataType dt)
        {
            HasName = true;
            Name = name;
            DataType = dt;

        }
    }

    class FunctionType : IDataType
    {
        public readonly IDataType ReturnType;
        public readonly List<Parameter> Parameters;
        public readonly bool Async;

        public FunctionType(List<Parameter> parameters, IDataType returnType, bool async)
        {
            Parameters = parameters;
            ReturnType = async ? new Future(returnType) : returnType;
            Async = async;
        }

        public string Classify() => "FUNCTION";

        public bool Coerce(IDataType other)
        {
            if (other.Classify() == "FUNCTION")
            {
                var otherFunction = ((FunctionType)other);
                if (otherFunction.Async != Async)
                    return false;
                if (Parameters.Count != otherFunction.Parameters.Count)
                    return false;
                for (int i = 0; i < Parameters.Count; i++)
                {
                    if (!Parameters[i].Compare(otherFunction.Parameters[i]))
                        return false;
                }
                if (ReturnType.Coerce(((FunctionType)other).ReturnType))
                    return true;
                return false;
            }
            return false;
        }

        // add parameter matching functionality
        public bool MatchParameters(List<ParameterValue> incomingParameters)
        {
            return false;
        }
    }
}