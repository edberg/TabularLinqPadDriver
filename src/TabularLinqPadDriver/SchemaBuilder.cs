using LINQPad.Extensibility.DataContext;
using LinqToDAX;
using Microsoft.AnalysisServices.Tabular;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TabularLinqPadDriver.Models;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using Assembly = System.Reflection.Assembly;

namespace TabularLinqPadDriver
{
    class SchemaBuilder
    {
        internal static List<ExplorerItem> GetSchemaAndBuildAssembly(TabularProperties props, AssemblyName name, ref string nameSpace, ref string typeName)
        {
            var tables = GenerateModel(props.GetConnection(), props.Database);
            var cu = GenerateCompilationSyntax(tables, nameSpace, out typeName);
            var code = GenerateCode(cu);
            
            var assembly = GenerateAssembly(code, name);
            var schema = GenerateSchema(tables);
            return schema;
        }

        private static CompilationUnitSyntax GenerateCompilationSyntax(List<TableModel> tables, string nameSpace, out string typeName)
        {
            typeName = "TabularDataContext";
            var cu = CompilationUnit()
                            .AddUsings(UsingDirective(IdentifierName("System")))
                            .AddUsings(UsingDirective(IdentifierName("System.Linq")))
                            .AddUsings(UsingDirective(IdentifierName("LinqToDAX")))
                            .AddUsings(UsingDirective(IdentifierName("TabularEntities")));

            var ns = NamespaceDeclaration(IdentifierName(nameSpace));

            //Context
            var contextclass = ClassDeclaration(typeName)
                    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                    .WithMembers(List(new MemberDeclarationSyntax[]
                        {
                            FieldDeclaration(VariableDeclaration(IdentifierName("TabularQueryProvider"))
                                .WithVariables(SingletonSeparatedList(VariableDeclarator(Identifier("provider")))))
                                .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword))),
                            EventDeclaration(IdentifierName("Logger"),Identifier("ProviderLog"))
                                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                                .WithAccessorList(AccessorList(List(new AccessorDeclarationSyntax[]
                                {
                                    AccessorDeclaration(SyntaxKind.AddAccessorDeclaration,
                                        Block(SingletonList<StatementSyntax>(ExpressionStatement(AssignmentExpression(SyntaxKind.AddAssignmentExpression,
                                                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,IdentifierName("provider"),IdentifierName("Log")),IdentifierName("value")))))),
                                    AccessorDeclaration(SyntaxKind.RemoveAccessorDeclaration,
                                        Block(SingletonList<StatementSyntax>(ExpressionStatement(AssignmentExpression(SyntaxKind.SubtractAssignmentExpression,
                                                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,IdentifierName("provider"),IdentifierName("Log")),IdentifierName("value"))))))
                                })))
                        }));


            var contextbody = Block(SingletonList<StatementSyntax>(ExpressionStatement(
                                    AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, IdentifierName("provider"),
                                        ObjectCreationExpression(IdentifierName("TabularQueryProvider"))
                                        .WithArgumentList(ArgumentList(SingletonSeparatedList<ArgumentSyntax>(Argument(IdentifierName("connectionString")))))))));

            //Context properties
            tables.ForEach(t =>
            {
                contextclass = contextclass.AddMembers(PropertyDeclaration(GenericName(Identifier("IQueryable"))
                                .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList<TypeSyntax>(IdentifierName(t.Identifier)))), Identifier(t.Identifier))
                                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                                .WithAccessorList(AccessorList(List<AccessorDeclarationSyntax>(new AccessorDeclarationSyntax[]{
                                        AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
                                        AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword)))
                                        .WithSemicolonToken(Token(SyntaxKind.SemicolonToken))}))));

                contextbody = contextbody.AddStatements(ExpressionStatement(
                                    AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, ThisExpression(), IdentifierName(t.Identifier)),
                                        ObjectCreationExpression(GenericName(Identifier("TabularTable")).WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList<TypeSyntax>(IdentifierName(t.Identifier)))))
                                        .WithArgumentList(ArgumentList(SingletonSeparatedList<ArgumentSyntax>(Argument(IdentifierName("provider"))))))));
            });

            var contextconstructor = ConstructorDeclaration(Identifier(typeName))
                                        .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                                        .WithParameterList(ParameterList(SeparatedList<ParameterSyntax>(new SyntaxNodeOrToken[]{
                                            Parameter(Identifier("connectionString")).WithType(PredefinedType(Token(SyntaxKind.StringKeyword)))})))
                                        .WithBody(contextbody);
            contextclass = contextclass.AddMembers(contextconstructor);
            ns = ns.AddMembers(contextclass);

            //Tables
            tables.ForEach(t =>
            {
                var tableclass = ClassDeclaration(t.Identifier)
                                .WithAttributeLists(SingletonList<AttributeListSyntax>(AttributeList(SingletonSeparatedList<AttributeSyntax>(Attribute(IdentifierName("TabularTableMapping"))
                                .WithArgumentList(AttributeArgumentList(SingletonSeparatedList<AttributeArgumentSyntax>(AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(t.Name))))))))))
                                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                                .WithBaseList(BaseList(SingletonSeparatedList<BaseTypeSyntax>(SimpleBaseType(IdentifierName("ITabularData")))));

                t.Columns.ForEach(c =>
                {
                    var identifier = c.Identifier == t.Identifier ? "Content" : c.Identifier;
                    var memberdef = PropertyDeclaration(IdentifierName(c.Type), Identifier(identifier))
                                    .WithAttributeLists(SingletonList<AttributeListSyntax>(AttributeList(SingletonSeparatedList<AttributeSyntax>(Attribute(IdentifierName("TabularMapping"))
                                        .WithArgumentList(AttributeArgumentList(SeparatedList<AttributeArgumentSyntax>(new SyntaxNodeOrToken[]{AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression,Literal(c.Name))),
                                                Token(SyntaxKind.CommaToken),AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression,Literal(t.Name)))})))))))
                                    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                                    .WithAccessorList(AccessorList(List<AccessorDeclarationSyntax>(new AccessorDeclarationSyntax[]
                                        {
                                    AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
                                     AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
                                        })));
                    tableclass = tableclass.AddMembers(memberdef);
                });

                ns = ns.AddMembers(tableclass);
            });

            //Measures
            var measures = (from t in tables
                            from m in t.Measures
                            select m).ToList();
            var measureclass = ClassDeclaration("Measures")
                            .WithModifiers(TokenList(new[] { Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword) }));
            measures.ForEach(m =>
            {

                var method1 = MethodDeclaration(IdentifierName(m.Type), Identifier(m.Identifier))
                                .WithAttributeLists(SingletonList<AttributeListSyntax>(AttributeList(SingletonSeparatedList<AttributeSyntax>(Attribute(IdentifierName("TabularMeasureMapping"))
                                            .WithArgumentList(AttributeArgumentList(SingletonSeparatedList<AttributeArgumentSyntax>(AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(m.Name))))))))))
                                .WithModifiers(TokenList(new[] { Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword) }))
                                .WithParameterList(ParameterList(SingletonSeparatedList<ParameterSyntax>(Parameter(Identifier("table")).WithModifiers(TokenList(Token(SyntaxKind.ThisKeyword))).WithType(IdentifierName("ITabularData")))))
                                .WithBody(Block(SingletonList<StatementSyntax>(ThrowStatement(ObjectCreationExpression(QualifiedName(IdentifierName("System"), IdentifierName("NotImplementedException")))
                                            .WithArgumentList(ArgumentList(SingletonSeparatedList<ArgumentSyntax>(Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal("This method is only available in a LinqToDAX Query"))))))))));
                measureclass = measureclass.AddMembers(method1);
                var method2 = MethodDeclaration(IdentifierName(m.Type), Identifier(m.Identifier))
                            .WithAttributeLists(SingletonList<AttributeListSyntax>(AttributeList(SingletonSeparatedList<AttributeSyntax>(Attribute(IdentifierName("TabularMeasureMapping"))
                                            .WithArgumentList(AttributeArgumentList(SingletonSeparatedList<AttributeArgumentSyntax>(AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(m.Name))))))))))
                            .WithModifiers(TokenList(new[] { Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword) }))
                            .WithParameterList(ParameterList(SeparatedList<ParameterSyntax>(new SyntaxNodeOrToken[]{
                                    Parameter(Identifier("table")).WithModifiers(TokenList(Token(SyntaxKind.ThisKeyword))).WithType(IdentifierName("ITabularData")),
                                    Token(SyntaxKind.CommaToken),
                                    Parameter(Identifier("filter")).WithType(PredefinedType(Token(SyntaxKind.BoolKeyword)))})))
                            .WithBody(Block(SingletonList<StatementSyntax>(ThrowStatement(ObjectCreationExpression(IdentifierName("NotImplementedException"))
                                            .WithArgumentList(ArgumentList(SingletonSeparatedList<ArgumentSyntax>(Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal("This method is only available in a LinqToDAX Query"))))))))));
                measureclass = measureclass.AddMembers(method2);
            });
            ns = ns.AddMembers(measureclass);

            cu = cu.AddMembers(ns);
            return cu;
        }

        private static List<TableModel> GenerateModel(string connection, string database)
        {
            var server = new Server();
            server.Connect(connection);

            var db = server.Databases[database];
            var tables = (from t in db.Model.Tables.Cast<Table>()
                          select new TableModel
                          {
                              Identifier = t.Name.ToIdentifier(),
                              Name = $"'{t.Name}'",
                              Columns = (from c in t.Columns.Cast<Column>()
                                         where c.Type != ColumnType.RowNumber && c.DataType != DataType.Binary
                                         select new ColumnModel
                                         {
                                             Identifier = c.Name.ToIdentifier(),
                                             Name = $"'{t.Name}'[{c.Name}]",
                                             Type = c.DataType.ToString(),
                                             IsKey = c.IsKey,
                                         }
                                       ).ToList(),
                              References = (from r in db.Model.Relationships.Cast<Relationship>()
                                            where r.Name == t.Name
                                            select r.ToTable?.Name).ToList(),
                              Measures = (from m in t.Measures.Cast<Measure>()
                                          select new MeasureModel
                                          {
                                              Identifier = m.Name.Replace(" ", "_").Replace("-", "_").Replace("__", "_").Replace("__", "_"),
                                              Name = $"[{m.Name}]",
                                              Type = m.DataType.ToString(),
                                          }).ToList(),
                          }).ToList();

            server.Disconnect();
            return tables;
        }

        private static string GenerateCode(CompilationUnitSyntax cu)
        {
            try
            {
                var cw = new AdhocWorkspace();
                var options = cw.Options
                                .WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInMethods, true)
                                .WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInTypes, true);
                var node = Formatter.Format(cu, cw, options);

                var sb = new StringBuilder();
                using (var writer = new StringWriter(sb)) { node.WriteTo(writer); }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }

        private static Assembly GenerateAssembly(string code, AssemblyName name)
        {
            //Compile the source code
            var references = new MetadataReference[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(TabularQueryProvider).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(TabularTableMappingAttribute).Assembly.Location),

            };
            var st = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create(name.FullName, new[] { st }, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            
            var result = compilation.Emit(name.CodeBase);
            if (!result.Success)
            {
                var failures = result.Diagnostics.Where(diagnostic =>
                    diagnostic.IsWarningAsError ||
                    diagnostic.Severity == DiagnosticSeverity.Error);


                var message = string.Join("\n", failures.Select(x => $"{x.Id}: {x.GetMessage()}"));
                throw new InvalidOperationException("Compilation failures!\n\n" + message + "\n\nCode:\n\n" + code);
            }
            var assembly = Assembly.Load(name);
            return assembly;
        }

        private static List<ExplorerItem> GenerateSchema(List<TableModel> tables)
        {
            var result = new List<ExplorerItem>();
            var measures = from t in tables
                           where t.Measures.Count() > 0
                           select new ExplorerItem(t.Identifier, ExplorerItemKind.Category, ExplorerIcon.TableFunction)
                           {
                               ToolTipText = t.Name,
                               Children = (from m in t.Measures
                                           select new ExplorerItem(m.Identifier, ExplorerItemKind.QueryableObject, ExplorerIcon.TableFunction)
                                           {
                                               ToolTipText = m.Name
                                           }).ToList(),
                           };
            result.AddRange(measures);

            var schema = (from t in tables
                          select new ExplorerItem(t.Identifier, ExplorerItemKind.QueryableObject, ExplorerIcon.Table)
                          {
                              ToolTipText = t.Name,
                              Children = (from c in t.Columns
                                          select new ExplorerItem(c.Identifier, ExplorerItemKind.Property, c.IsKey ? ExplorerIcon.Key : ExplorerIcon.Column)
                                          {
                                              
                                              ToolTipText = c.Name
                                          }).ToList(),
                         }).ToList();
            result.AddRange(schema);
            return result;
        }
    }

    public static class Extensions
    {
        public static string ToIdentifier(this string columnName)
        {
            var x = columnName.Replace(".", "");
            var c = x.TrimStart('[').TrimEnd(']').Replace(" ", "_").Replace("-", "_").Replace("__", "_");
            if (c.Contains("_"))
            {
                var parts = c.Split('_');
                if (parts.Count() > 1)
                {
                    return parts.Select(s => s.Capitalize()).Aggregate((s1, s2) => s1 + s2);
                }
                return parts[0].Capitalize();
            }
            return x;
        }

        public static string Capitalize(this string input)
        {
            if (input == null || input.Length <= 1) return input;
            var cs = input.ToLower();
            var cap = cs[0].ToString().ToUpper();
            return cap + cs.Substring(1);
        }
    }
}
