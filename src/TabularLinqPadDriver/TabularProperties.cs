using LINQPad.Extensibility.DataContext;
using System.Net;
using System.Xml.Linq;

namespace TabularLinqPadDriver
{
    class TabularProperties
    {
        readonly IConnectionInfo _cxInfo;
        readonly XElement _driverData;

        public TabularProperties(IConnectionInfo cxInfo)
        {
            _cxInfo = cxInfo;
            _driverData = cxInfo.DriverData;
        }

        public bool Persist
        {
            get { return _cxInfo.Persist; }
            set { _cxInfo.Persist = value; }
        }

        public string Server
        {
            get { return (string)_driverData.Element("Server") ?? ""; }
            set { _driverData.SetElementValue("Server", value); }
        }

        public string Database
        {
            get { return (string)_driverData.Element("Database") ?? ""; }
            set { _driverData.SetElementValue("Database", value); }
        }

        public string Domain
        {
            get { return (string)_driverData.Element("Domain") ?? ""; }
            set { _driverData.SetElementValue("Domain", value); }
        }

        public string UserName
        {
            get { return (string)_driverData.Element("UserName") ?? ""; }
            set { _driverData.SetElementValue("UserName", value); }
        }

        public string Password
        {
            get { return _cxInfo.Decrypt((string)_driverData.Element("Password") ?? ""); }
            set { _driverData.SetElementValue("Password", _cxInfo.Encrypt(value)); }
        }

        public string GetConnection()
        {
            var baseconn = $"Data Source={Server};Initial Catalog={Database};Provider=MSOLAP;";

            if (!string.IsNullOrEmpty(Domain))
                return $"{baseconn}User ID={Domain}\\{UserName};Password={Password}";
            if (!string.IsNullOrEmpty(UserName))
                return $"{baseconn}User ID={UserName};Password={Password}";

            return baseconn;
        }
    }
}
