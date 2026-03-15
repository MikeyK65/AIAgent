using AIAgent.Views;

namespace AIAgent;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
       Routing.RegisterRoute(nameof(MessageDetailPage), typeof(MessageDetailPage));
	}
}
