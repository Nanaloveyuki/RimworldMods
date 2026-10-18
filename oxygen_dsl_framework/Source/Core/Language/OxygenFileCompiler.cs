using System;
using System.Collections.Generic;
using Oxygendsl.Core;

namespace Oxygendsl.Language
{
    public sealed class OxygenFileCompilation
    {
        internal OxygenFileCompilation(ContentPlan content, OxygenSettingsPlan settings,
            IReadOnlyList<ScriptDiagnostic> diagnostics, IReadOnlyList<ContentImport> imports)
        { Content = content; Settings = settings; Diagnostics = diagnostics; Imports = imports; }
        public ContentPlan Content { get; }
        public OxygenSettingsPlan Settings { get; }
        public IReadOnlyList<ScriptDiagnostic> Diagnostics { get; }
        public IReadOnlyList<ContentImport> Imports { get; }
        public bool Success { get { return Content != null && Settings != null && Diagnostics.Count == 0; } }
    }

    public static class OxygenFileCompiler
    {
        // Library shape is a property of the declarations, never of the filename.
        public static bool IsLibrary(ArtiProgram program)
        {
            if (program == null) return false;
            foreach (ArtiStatement statement in program.Statements)
                if (!(statement is ArtiUseStatement || statement is ArtiVariableDeclarationStatement
                    || statement is ArtiFunctionDeclarationStatement || statement is ArtiStructDeclarationStatement
                    || statement is ArtiTraitDeclarationStatement || statement is ArtiImplDeclarationStatement)) return false;
            return true;
        }

        public static bool HasNativeDeclarations(ArtiProgram program)
        {
            if (program == null) return false;
            foreach (ArtiStatement statement in program.Statements)
            {
                var expression = statement as ArtiExpressionStatement;
                var call = expression == null ? null : expression.Expression as ArtiCallExpression;
                var name = call == null ? null : call.Target as ArtiNameExpression;
                if (name != null && (name.Name == "template" || name.Name == "define" || name.Name == "patch"
                    || OxygenDeclarations.TryGet(name.Name, out OxygenDeclaration declaration) && declaration.Reader != null)) return true;
            }
            return false;
        }

        public static OxygenFileCompilation Compile(string source, bool irisMenusAvailable,
            IOxygenModuleCatalog modules = null, IOxygenStandardModuleProvider standardModules = null,
            IOxygenModuleProvider hostModules = null, IOxygenLocalization localization = null)
        {
            return Compile(new ArtiParser().Parse(source ?? string.Empty), irisMenusAvailable, modules, standardModules, hostModules, localization);
        }

        public static OxygenFileCompilation Compile(ArtiParseResult parsed, bool irisMenusAvailable,
            IOxygenModuleCatalog modules = null, IOxygenStandardModuleProvider standardModules = null,
            IOxygenModuleProvider hostModules = null, IOxygenLocalization localization = null)
        {
            var diagnostics = new List<ScriptDiagnostic>();
            AddDiagnostics(diagnostics, parsed.Diagnostics);
            if (diagnostics.Count == 0)
                AddDiagnostics(diagnostics, new ArtiAnalyzer().Analyze(parsed.Program, IsLibrary(parsed.Program)).Diagnostics);
            if (diagnostics.Count != 0) return Failed(diagnostics);
            var executor = new ArtiExecutor();
            var declarations = new List<object>();
            var provider = new OxygenSettingsCompiler.SettingsModules(irisMenusAvailable, declarations, standardModules);
            ArtiExecutionResult result = executor.Execute(parsed.Program,
                new ArtiExecutionContext(modules: modules, standardModules: provider, hostModules: hostModules, localization: localization));
            AddDiagnostics(diagnostics, result.Diagnostics);
            if (diagnostics.Count != 0) return Failed(diagnostics);
            ContentCompilation content = ContentCompiler.RegisterPrepared(parsed.Program, executor, modules);
            diagnostics.AddRange(content.Diagnostics);
            if (!content.Success) return Failed(diagnostics);
            OxygenSettingsCompilation settings = OxygenSettingsCompiler.RegisterPrepared(declarations, executor, diagnostics);
            return new OxygenFileCompilation(content.Plan, settings.Plan, diagnostics, content.Imports);
        }

        static OxygenFileCompilation Failed(List<ScriptDiagnostic> diagnostics)
        { return new OxygenFileCompilation(null, null, diagnostics, Array.Empty<ContentImport>()); }

        static void AddDiagnostics(List<ScriptDiagnostic> destination, IEnumerable<ArtiDiagnostic> diagnostics)
        {
            foreach (ArtiDiagnostic diagnostic in diagnostics)
                if (diagnostic.Severity == ArtiDiagnosticSeverity.Error)
                    destination.Add(new ScriptDiagnostic(diagnostic.GetMessage(ArtiDiagnosticLanguage.English), diagnostic.Span.Line, diagnostic.Span.Column));
        }
    }
}
