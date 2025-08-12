using System;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace AudioSFV;

public class App : Application {
	public const string AppName = "AudioSFV";

	public override void Initialize() {
		Name = AppName;
		Styles.Add(new FluentTheme());
		Styles.Add(new StyleInclude(baseUri: null) {
			Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml")
		});
	}

	public override void OnFrameworkInitializationCompleted() {
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
			MainWindow window = new();

			desktop.MainWindow = window;

			string[] args = desktop.Args ?? [];
			if (args.Length == 1) {
				window.OpenSfva(args[0]);
			}

			if (Current?.TryGetFeature(typeof(IActivatableLifetime)) is IActivatableLifetime activatable) {
				activatable.Activated += (_, e) => {
					if (e is FileActivatedEventArgs fe && fe.Files.Count == 1) {
						string? path = fe.Files[0].TryGetLocalPath();
						if (!String.IsNullOrWhiteSpace(path)) {
							Dispatcher.UIThread.Post(() => window.OpenSfva(path!));
						}
					}
				};
			}
		}

		base.OnFrameworkInitializationCompleted();
	}
}
