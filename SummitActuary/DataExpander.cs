using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Flee.PublicTypes;

namespace SummitActuary
{
    public class DataExpander<T> where T : class, new()
    {
        private readonly Type[] types;
        private readonly string[] keys;
        private readonly ExpressionContext context;
        private readonly Dictionary<string, IGenericExpression<object>> compiledExpressions;
        private readonly Dictionary<string, PropertyInfo> propertyMap;

        // Constructor for the DataExpander using generic type T
        public DataExpander()
        {
            Type modelType = typeof(T);

            // Get public properties from the class
            var properties = modelType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            this.types = properties.Select(p => p.PropertyType).ToArray();
            this.keys = properties.Select(p => p.Name).ToArray();

            // Map properties for faster access
            propertyMap = properties.ToDictionary(p => p.Name, p => p);

            // Setup expression context
            context = new ExpressionContext();
            context.Imports.AddType(typeof(DataExpanderFunctions));
            compiledExpressions = new Dictionary<string, IGenericExpression<object>>();
            InitializeContextVariables();
        }

        // Returns the property types of the model
        public IEnumerable<Type> GetTypes() => types;

        // Returns the property names of the model
        public IEnumerable<string> GetKeys() => keys;

        // Main method to expand input values into a list of model objects
        public List<T> ExpandData(IEnumerable<string> inputValues)
        {
            if (inputValues == null)
                throw new ArgumentNullException(nameof(inputValues), "Input values must not be null.");

            // Convert IEnumerable<string> to Dictionary<string, string>
            // Assuming that the values are provided in the order of keys (property names)
            var inputDict = new Dictionary<string, string>();
            var valuesList = inputValues.ToList();

            // Map each key to corresponding value if available
            for (int i = 0; i < keys.Length && i < valuesList.Count; i++)
            {
                inputDict[keys[i]] = valuesList[i];
            }

            // Call the original ExpandData method with the dictionary
            return ExpandData(inputDict);
        }

        public List<T> ExpandData(Dictionary<string, string> inputValues)
        {
            if (inputValues == null)
                throw new ArgumentNullException(nameof(inputValues), "Input values must not be null.");

            // Prepare input values array
            var valueArray = new string[keys.Length];

            // Map dictionary values to array
            for (int i = 0; i < keys.Length; i++)
            {
                string key = keys[i];
                valueArray[i] = inputValues.TryGetValue(key, out string value) ? value : string.Empty;
            }

            var sortedIndices = SortIndicesByDependency(valueArray).Reverse().ToArray();
            var expandedRows = ExpandRowRecursive(valueArray, sortedIndices, 0, new Dictionary<string, object>());

            // Remove duplicates
            var uniqueRows = new HashSet<string>();
            var result = new List<T>();

            foreach (var row in expandedRows)
            {
                var rowString = string.Join("|", row);
                if (uniqueRows.Add(rowString))
                {
                    // Create model instance and populate properties
                    var instance = new T();

                    for (int i = 0; i < keys.Length; i++)
                    {
                        var propertyName = keys[i];
                        if (propertyMap.TryGetValue(propertyName, out PropertyInfo property))
                        {
                            if (i < row.Length)
                            {
                                var value = ConvertValue(row[i], property.PropertyType);
                                property.SetValue(instance, value);
                            }
                        }
                    }

                    result.Add(instance);
                }
            }

            return result;
        }

        // Initializes variables in the expression context
        private void InitializeContextVariables()
        {
            for (int i = 0; i < keys.Length; i++)
            {
                context.Variables[keys[i]] = GetDefaultValue(types[i]);
            }
        }

        // Returns default value for a given type
        private static object GetDefaultValue(Type type)
        {
            if (type == typeof(string)) return string.Empty;
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        // Simplified check for expression
        // - For int/double types: Returns true if not only digits and decimal point
        // - For other types: Returns true only if contains "If("
        private bool IsExpression(string value, Type type)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            // Trim whitespace
            string trimmedValue = value.Trim();

            // Check for numeric types (int, double)
            if (type == typeof(int) || type == typeof(double))
            {
                // Check if string contains only digits and decimal point
                bool isNumber = trimmedValue.All(c => char.IsDigit(c) || c == '.');

                // If not a number, treat as expression
                return !isNumber;
            }

            // For other types, only check for "If(" function
            return trimmedValue.Contains("If(");
        }

        // Splits a string by commas while preserving function parentheses
        // Example: "A, B, Max(10, 20), C" -> ["A", "B", "Max(10, 20)", "C"]
        // Tracks parentheses count to only split at commas outside of function calls
        private List<string> SplitPreservingFunctions(string input)
        {
            var result = new List<string>();
            var currentPart = new System.Text.StringBuilder();
            int parenthesesCount = 0;

            foreach (char c in input)
            {
                if (c == '(') parenthesesCount++;
                else if (c == ')') parenthesesCount--;

                if (c == ',' && parenthesesCount == 0)
                {
                    result.Add(currentPart.ToString());
                    currentPart.Clear();
                }
                else
                {
                    currentPart.Append(c);
                }
            }

            if (currentPart.Length > 0)
            {
                result.Add(currentPart.ToString());
            }

            return result;
        }

        // Converts a string value to the specified type
        private object ConvertValue(string value, Type type)
        {
            if (string.IsNullOrEmpty(value))
            {
                return GetDefaultValue(type);
            }

            try
            {
                // Check for compiled expression
                if (compiledExpressions.TryGetValue(value, out var expression))
                {
                    var result = expression.Evaluate();
                    return Convert.ChangeType(result, type);
                }

                // Handle enum values
                if (type.IsEnum)
                {
                    return Enum.Parse(type, value, true);
                }

                // Default conversion
                return Convert.ChangeType(value, type);
            }
            catch
            {
                return GetDefaultValue(type);
            }
        }

        // Evaluates an expression using the current context
        private object EvaluateExpression(string expression, Dictionary<string, object> currentValues)
        {
            if (!compiledExpressions.TryGetValue(expression, out var compiledExpression))
            {
                compiledExpression = context.CompileGeneric<object>(expression);
                compiledExpressions[expression] = compiledExpression;
            }

            foreach (var pair in currentValues)
            {
                context.Variables[pair.Key] = pair.Value;
            }

            return compiledExpression.Evaluate();
        }

        // Expands a value into multiple possible values based on expressions
        private List<string> ExpandValue(string value, Type type, string key, Dictionary<string, object> currentValues)
        {
            if (string.IsNullOrEmpty(value))
            {
                object defaultValue = GetDefaultValue(type);
                return new List<string> { defaultValue?.ToString() ?? string.Empty };
            }

            var result = new HashSet<string>();
            var parts = SplitPreservingFunctions(value);

            foreach (var part in parts)
            {
                var trimmedPart = part.Trim();
                if (trimmedPart.Contains("~"))
                {
                    var range = trimmedPart.Split('~');
                    if (range.Length == 2)
                    {
                        var start = EvaluateExpression(range[0], currentValues);
                        var end = EvaluateExpression(range[1], currentValues);

                        if (start is int startInt && end is int endInt)
                        {
                            for (int i = startInt; i <= endInt; i++)
                            {
                                result.Add(i.ToString());
                            }
                        }
                        else
                        {
                            result.Add(trimmedPart);
                        }
                    }
                    else
                    {
                        result.Add(trimmedPart);
                    }
                }
                else if (IsExpression(trimmedPart, type))
                {
                    var evaluatedValue = EvaluateExpression(trimmedPart, currentValues);
                    result.Add(evaluatedValue.ToString());
                }
                else
                {
                    result.Add(trimmedPart);
                }
            }

            return result.Count > 0 ? result.ToList() : new List<string> { string.Empty };
        }

        // Checks if an expression contains a specific variable reference
        // Example: ContainsVariable("x + 5", "x", variables) returns true
        private bool ContainsVariable(string expression, string variable, HashSet<string> contextVariables)
        {
            if (contextVariables.Contains(variable))
            {
                var pattern = $@"\b{Regex.Escape(variable)}\b";
                return Regex.IsMatch(expression, pattern);
            }
            return false;
        }

        // Analyzes dependencies between variables and sorts them for correct evaluation order
        // Example: For "x=5~10, y=x+1, z=Max(x,y)", returns indices so x is processed first, then y, then z
        private int[] SortIndicesByDependency(string[] values)
        {
            var dependencies = new Dictionary<int, HashSet<int>>();
            var contextVariables = new HashSet<string>(context.Variables.Keys);

            for (int i = 0; i < values.Length; i++)
            {
                dependencies[i] = new HashSet<int>();
                for (int j = 0; j < values.Length; j++)
                {
                    if (i != j && ContainsVariable(values[i], keys[j], contextVariables))
                    {
                        dependencies[i].Add(j);
                    }
                }
            }

            return TopologicalSort(dependencies);
        }

        // Performs topological sort to handle dependencies between variables
        // Ensures that variables are processed in order: dependencies first, then dependent variables
        // Example: If y depends on x, and z depends on both, the order will be: x, y, z
        private int[] TopologicalSort(Dictionary<int, HashSet<int>> dependencies)
        {
            var sorted = new List<int>();
            var visited = new HashSet<int>();
            var tempMark = new HashSet<int>();

            void Visit(int node)
            {
                if (tempMark.Contains(node))
                {
                    throw new InvalidOperationException("Circular dependency detected");
                }
                if (!visited.Contains(node))
                {
                    tempMark.Add(node);
                    foreach (var dependent in dependencies[node])
                    {
                        Visit(dependent);
                    }
                    tempMark.Remove(node);
                    visited.Add(node);
                    sorted.Insert(0, node);
                }
            }

            for (int i = 0; i < dependencies.Count; i++)
            {
                if (!visited.Contains(i))
                {
                    Visit(i);
                }
            }

            return sorted.ToArray();
        }

        // Recursively expands rows by processing each value according to dependency order
        // This ensures that variables like "y = x + 5" are evaluated after "x = 1~10"
        // Example: If x=1~3 and y=x*2, this will generate pairs: (1,2), (2,4), (3,6)
        private List<string[]> ExpandRowRecursive(string[] row, int[] sortedIndices, int currentIndex, Dictionary<string, object> currentValues)
        {
            if (currentIndex >= sortedIndices.Length)
            {
                return new List<string[]> { row.ToArray() };
            }

            int index = sortedIndices[currentIndex];
            var expandedValues = ExpandValue(row[index], types[index], keys[index], currentValues);
            var result = new List<string[]>();
            var processedCombinations = new HashSet<string>();

            foreach (var value in expandedValues)
            {
                var newRow = row.ToArray();
                newRow[index] = value;

                var newCurrentValues = new Dictionary<string, object>(currentValues);
                newCurrentValues[keys[index]] = ConvertValue(value, types[index]);

                // Update context for expression evaluation
                context.Variables[keys[index]] = newCurrentValues[keys[index]];

                var subResults = ExpandRowRecursive(newRow, sortedIndices, currentIndex + 1, newCurrentValues);

                foreach (var subRow in subResults)
                {
                    // Create unique string for duplicate detection
                    var combination = string.Join("|", subRow);

                    // Only add unique combinations
                    if (processedCombinations.Add(combination))
                    {
                        result.Add(subRow);
                    }
                }
            }

            return result;
        }
    }

    public static class DataExpanderFunctions
    {
        public static double Max(params double[] vals) => vals.Max();

        public static double Min(params double[] vals) => vals.Min();

        public static int Max(params int[] vals) => vals.Max();

        public static int Min(params int[] vals) => vals.Min();

        public static int Choose(int index, params int[] items)
        {
            if (index < 1)
                throw new Exception("Choose function Index must be greater than 0");

            if (index > items.Length)
                throw new Exception($"Choose function Index {index} is larger than the number of items {items.Length}");

            return items[index - 1];
        }

        public static double Choose(int index, params double[] items)
        {
            if (index < 1)
                throw new Exception("Choose function Index must be greater than 0");

            if (index > items.Length)
                throw new Exception($"Choose function Index {index} is larger than the number of items {items.Length}");

            return items[index - 1];
        }

        public static string Choose(int index, params string[] items)
        {
            if (index < 1)
                throw new Exception("Choose function Index must be greater than 0");

            if (index > items.Length)
                throw new Exception($"Choose function Index {index} is larger than the number of items {items.Length}");

            return items[index - 1];
        }

        public static string Join(string separator, params object[] args) => string.Join(separator, args);
    }
}
