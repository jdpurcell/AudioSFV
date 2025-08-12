using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace AudioSFV;

public class MainWindow : Window {
	private ObservableCollection<ResultItem> GridItems { get; } = [];

	public MainWindow() {
		Title = App.AppName;
		Width = 880;
		Height = 480;
		WindowStartupLocation = WindowStartupLocation.CenterScreen;

		DataGrid grid = new() {
			IsReadOnly = true,
			RowHeight = 22,
			CanUserSortColumns = false,
			ItemsSource = GridItems
		};

		// Hide cell focus visual
		grid.Styles.Add(
			new Style(x => x.OfType<DataGridCell>().Class(":focus").Template().OfType<Grid>().Name("FocusVisual")) {
				Setters = { new Setter(IsVisibleProperty, false) }
			}
		);

		// Reduce column header right side padding
		grid.Resources["DataGridSortIconMinWidth"] = 12.0;

		const double cellFontSize = 13;

		grid.Columns.Add(new DataGridTextColumn {
			Header = "File Name",
			Binding = new Binding(nameof(ResultItem.FileName)),
			Width = new DataGridLength(1, DataGridLengthUnitType.Star),
			FontSize = cellFontSize
		});

		grid.Columns.Add(new DataGridTextColumn {
			Header = "CRC",
			Binding = new Binding(nameof(ResultItem.Crc)),
			Width = DataGridLength.Auto,
			FontSize = cellFontSize,
			FontFamily = new FontFamily("Monaco, Consolas")
		});

		grid.Columns.Add(new DataGridTemplateColumn {
			Header = "Status",
			CellTemplate = new FuncDataTemplate<ResultItem>(
				(_, _) => {
					TextBlock tb = new() {
						HorizontalAlignment = HorizontalAlignment.Center,
						VerticalAlignment = VerticalAlignment.Center,
						FontWeight = FontWeight.Bold,
						FontSize = cellFontSize * (OperatingSystem.IsMacOS() ? 1.3 : 1.0)
					};
					tb.Bind(TextBlock.TextProperty, new Binding(nameof(ResultItem.Status)) { Converter = StatusToGlyphConverter });
					tb.Bind(TextBlock.ForegroundProperty, new Binding(nameof(ResultItem.Status)) { Converter = StatusToBrushConverter });
					return tb;
				},
				true),
			Width = DataGridLength.Auto
		});

		grid.Columns.Add(new DataGridTextColumn {
			Header = "Intro Silence",
			Binding = new Binding(nameof(ResultItem.IntroSilenceSamples)),
			Width = DataGridLength.Auto,
			FontSize = cellFontSize
		});

		grid.Columns.Add(new DataGridTextColumn {
			Header = "Outro Silence",
			Binding = new Binding(nameof(ResultItem.OutroSilenceSamples)),
			Width = DataGridLength.Auto,
			FontSize = cellFontSize
		});

		Content = grid;
	}

	public void OpenSfva(string sfvaPath) {
		string fullPath = Path.GetFullPath(sfvaPath);
		if (!File.Exists(fullPath)) {
			return;
		}

		List<SfvaEntry> entries;
		try {
			entries = SfvaParser.Parse(fullPath);
		}
		catch {
			return;
		}

		GridItems.Clear();
		foreach (SfvaEntry e in entries) {
			GridItems.Add(new ResultItem { FileName = e.FileName });
		}

		// Begin verification in background
		string baseDir = Path.GetDirectoryName(fullPath)!;
		Task.Run(() => VerifyFiles(baseDir, entries));
	}

	private void VerifyFiles(string baseDir, List<SfvaEntry> entries) {
		ConcurrentQueue<Job> jobs = new(
			entries.Zip(GridItems, (entry, item) => new Job(entry, item))
		);

		void RunJob(Job job) {
			(SfvaEntry entry, ResultItem item) = job;
			string filePath = Path.Combine(baseDir, entry.FileName);
			AudioHandler.Result? result;

			Dispatcher.UIThread.Post(() => {
				item.Status = VerificationStatus.Processing;
			});

			try {
				result = AudioHandler.Process(filePath);
			}
			catch {
				result = null;
			}

			Dispatcher.UIThread.Post(() => {
				if (result is not null) {
					item.Crc = result.Crc.ToString("X8");
					item.Status = result.Crc == entry.ExpectedCrc ? VerificationStatus.Passed : VerificationStatus.Failed;
					item.IntroSilenceSamples = result.IntroSilenceSamples;
					item.OutroSilenceSamples = result.OutroSilenceSamples;
				}
				else {
					item.Crc = "(error)";
				}
			});
		}

		Parallel.For(0, jobs.Count,
			new ParallelOptions { MaxDegreeOfParallelism = 4 },
			_ => {
				if (jobs.TryDequeue(out Job? job))
					RunJob(job);
			});
	}

	private static FuncValueConverter<VerificationStatus, string> StatusToGlyphConverter { get; } = new(
		s => s switch {
			VerificationStatus.Processing => "…",
			VerificationStatus.Passed => "\u2714\uFE0E",
			VerificationStatus.Failed => "\u2716\uFE0E",
			_ => ""
		}
	);

	private static FuncValueConverter<VerificationStatus, IImmutableSolidColorBrush> StatusToBrushConverter { get; } = new(
		s => s switch {
			VerificationStatus.Passed => Brushes.Green,
			VerificationStatus.Failed => Brushes.Red,
			_ => Brushes.Gray
		}
	);

	private record Job(SfvaEntry Entry, ResultItem Item);
}
