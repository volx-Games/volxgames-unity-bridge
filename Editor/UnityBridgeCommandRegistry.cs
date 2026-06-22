using System;
using System.Collections.Generic;

namespace VolxGames.UnityBridge.Editor
{
    public static class UnityBridgeCommandRegistry
    {
        private static readonly Dictionary<string, RegisteredCommand> Commands = new Dictionary<string, RegisteredCommand>(StringComparer.OrdinalIgnoreCase);

        public static void Register(string name, Func<string, UnityBridgeCustomCommandResult> handler, string description = "", string argumentHint = "")
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Command name is required.", nameof(name));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            Commands[name] = new RegisteredCommand
            {
                name = name,
                description = description ?? string.Empty,
                argumentHint = argumentHint ?? string.Empty,
                handler = handler
            };
        }

        public static bool TryExecute(string name, string argument, out UnityBridgeCustomCommandResult result)
        {
            RegisteredCommand command;
            if (Commands.TryGetValue(name, out command))
            {
                result = command.handler(argument ?? string.Empty);
                return true;
            }

            result = null;
            return false;
        }

        internal static List<UnityBridgeCommandDescriptor> ListCustomCommands()
        {
            var commands = new List<UnityBridgeCommandDescriptor>();
            foreach (var pair in Commands)
            {
                commands.Add(new UnityBridgeCommandDescriptor
                {
                    name = pair.Value.name,
                    description = pair.Value.description,
                    argumentHint = pair.Value.argumentHint,
                    source = "custom"
                });
            }

            commands.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
            return commands;
        }

        private sealed class RegisteredCommand
        {
            public string name;
            public string description;
            public string argumentHint;
            public Func<string, UnityBridgeCustomCommandResult> handler;
        }
    }
}
