using Oxygendsl.Core;
using System;
using System.Collections.Generic;

namespace Oxygendsl.Language
{
    public enum OxygenDeclarationKind
    {
        Registration,
        Value
    }

    public sealed class OxygenDeclaration
    {
        public OxygenDeclaration(string name, OxygenDeclarationKind kind)
            : this(name, kind, null)
        {
        }

        public OxygenDeclaration(string name, OxygenDeclarationKind kind, ContentRegistrationReader reader)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("A declaration name is required.", nameof(name));
            if (reader != null && kind != OxygenDeclarationKind.Registration)
            {
                throw new ArgumentException("Only a registration declaration can read content.", nameof(reader));
            }
            Name = name;
            Kind = kind;
            Reader = reader;
        }

        public string Name { get; }
        public OxygenDeclarationKind Kind { get; }
        public ContentRegistrationReader Reader { get; }
    }

    public static class OxygenDeclarations
    {
        private static readonly object Sync = new object();
        private static readonly List<OxygenDeclaration> Items = new List<OxygenDeclaration>();
        private static readonly HashSet<string> Names = new HashSet<string>(StringComparer.Ordinal);

        static OxygenDeclarations()
        {
            Register(new OxygenDeclaration("mood", OxygenDeclarationKind.Registration));
            Register(new OxygenDeclaration("memory", OxygenDeclarationKind.Registration));
            Register(new OxygenDeclaration("template", OxygenDeclarationKind.Registration));
            Register(new OxygenDeclaration("define", OxygenDeclarationKind.Registration));
            Register(new OxygenDeclaration("patch", OxygenDeclarationKind.Registration));
            Register(new OxygenDeclaration("thought_rule", OxygenDeclarationKind.Registration));
            Register(new OxygenDeclaration("gate_rule", OxygenDeclarationKind.Registration));
            Register(new OxygenDeclaration("def_ref", OxygenDeclarationKind.Value));
            Register(new OxygenDeclaration("native_type", OxygenDeclarationKind.Value));
        }

        public static IReadOnlyList<OxygenDeclaration> All
        {
            get
            {
                lock (Sync)
                {
                    return Items.ToArray();
                }
            }
        }

        public static void Register(OxygenDeclaration declaration)
        {
            if (declaration == null) throw new ArgumentNullException(nameof(declaration));
            lock (Sync)
            {
                if (!Names.Add(declaration.Name))
                {
                    throw new InvalidOperationException("Declaration '" + declaration.Name + "' is already registered.");
                }

                Items.Add(declaration);
            }
        }

        public static bool IsName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            lock (Sync)
            {
                return Names.Contains(name);
            }
        }

        public static bool IsRegistration(string name)
        {
            OxygenDeclaration declaration;
            return TryGet(name, out declaration) && declaration.Kind == OxygenDeclarationKind.Registration;
        }

        public static bool TryGet(string name, out OxygenDeclaration declaration)
        {
            declaration = null;
            if (string.IsNullOrEmpty(name)) return false;
            lock (Sync)
            {
                if (!Names.Contains(name)) return false;
                for (int index = 0; index < Items.Count; index++)
                {
                    if (string.Equals(Items[index].Name, name, StringComparison.Ordinal))
                    {
                        declaration = Items[index];
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
