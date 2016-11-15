using System.Collections.Generic;
using System.Reflection;
using LINQPad.Extensibility.DataContext;
using LinqToDAX;

namespace TabularLinqPadDriver
{
    public class TabularDriver : DynamicDataContextDriver
    {
        public override string Author => "Rasmus Oudal Edberg";

        public override string Name => "SSAS Tabular";

        public override string GetConnectionDescription(IConnectionInfo cxInfo)
        {
            var prop = new TabularProperties(cxInfo);
            return $"{prop.Server}-{prop.Database}";
        }

        public override bool ShowConnectionDialog(IConnectionInfo cxInfo, bool isNewConnection)
        {
            var database = "AW Tabular Model SQL 2014";
            var server = "DESKTOP-0OF9FA6";

            if (isNewConnection) new TabularProperties(cxInfo) { Server = server, Database = database };

            return true;
        }

        public override List<ExplorerItem> GetSchemaAndBuildAssembly(IConnectionInfo cxInfo, AssemblyName assemblyToBuild, ref string nameSpace, ref string typeName)
        {
            var schema = SchemaBuilder.GetSchemaAndBuildAssembly(new TabularProperties(cxInfo), assemblyToBuild, ref nameSpace, ref typeName);
            return schema;
        }

        public override ParameterDescriptor[] GetContextConstructorParameters(IConnectionInfo cxInfo)
        {
            return new ParameterDescriptor[] {new ParameterDescriptor("connection", typeof(string).FullName)};
        }

        public override object[] GetContextConstructorArguments(IConnectionInfo cxInfo)
        {
            var connection = new TabularProperties(cxInfo).GetConnection();
            return new object[] { connection};
        }

        public override void InitializeContext(IConnectionInfo cxInfo, object context, QueryExecutionManager executionManager)
        {
            Logger logger = message => executionManager.SqlTranslationWriter.WriteLine(message);
            context.GetType().GetEvent("ProviderLog")?.AddEventHandler(context, logger);
        }

        public override IEnumerable<string> GetAssembliesToAdd(IConnectionInfo cxInfo)
        {
            return new string[] { "LinqToDAX.dll", "TabularEntities.dll" };
        }

        public override IEnumerable<string> GetNamespacesToAdd(IConnectionInfo cxInfo)
        {
            return new string[] { "LinqToDAX", "TabularEntities"};
        }
    }
}
