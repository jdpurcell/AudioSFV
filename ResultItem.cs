using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AudioSFV;

public class ResultItem : INotifyPropertyChanged {
	private string _fileName = "";
	private string _crc = "";
	private VerificationStatus _status = VerificationStatus.Pending;
	private long? _introSilenceSamples;
	private long? _outroSilenceSamples;

	public string FileName {
		get => _fileName;
		set { if (value != _fileName) { _fileName = value; OnPropertyChanged(); } }
	}

	public string Crc {
		get => _crc;
		set { if (value != _crc) { _crc = value; OnPropertyChanged(); } }
	}

	public VerificationStatus Status {
		get => _status;
		set { if (value != _status) { _status = value; OnPropertyChanged(); } }
	}

	public long? IntroSilenceSamples {
		get => _introSilenceSamples;
		set { if (value != _introSilenceSamples) { _introSilenceSamples = value; OnPropertyChanged(); } }
	}

	public long? OutroSilenceSamples {
		get => _outroSilenceSamples;
		set { if (value != _outroSilenceSamples) { _outroSilenceSamples = value; OnPropertyChanged(); } }
	}

	public event PropertyChangedEventHandler? PropertyChanged;
	private void OnPropertyChanged([CallerMemberName] string? name = null) =>
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public enum VerificationStatus {
	Pending,
	Processing,
	Passed,
	Failed
}
