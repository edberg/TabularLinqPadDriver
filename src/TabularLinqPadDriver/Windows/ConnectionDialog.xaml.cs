using System.Windows;
using LINQPad.Extensibility.DataContext;

namespace TabularLinqPadDriver.Windows
{
    /// <summary>
    /// Interaction logic for ConnectionDialog.xaml
    /// </summary>
    public partial class ConnectionDialog : Window
	{
		TabularProperties _properties;

		public ConnectionDialog (IConnectionInfo cxInfo)
		{
			DataContext = _properties = new TabularProperties(cxInfo);
			Background = SystemColors.ControlBrush;
			InitializeComponent ();
		}	

		void btnOK_Click (object sender, RoutedEventArgs e)
		{
			DialogResult = true;
		}
	}
}
