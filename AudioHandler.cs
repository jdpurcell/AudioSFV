using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Hashing;
using SimpleFlac;

namespace AudioSFV;

public static class AudioHandler {
	public static Result Process(string path) {
		string extension = Path.GetExtension(path);
		Crc32 crc = new();
		long bytesProcessed = 0;
		long firstPopulatedByte = -1;
		long lastPopulatedByte = -1;

		void ProcessData(ReadOnlySpan<byte> data) {
			crc.Append(data);
			for (int i = 0; i < data.Length; i++) {
				if (data[i] != 0) {
					if (firstPopulatedByte == -1) {
						firstPopulatedByte = bytesProcessed + i;
					}
					lastPopulatedByte = bytesProcessed + i;
				}
			}
			bytesProcessed += data.Length;
		}

		int blockAlign;
		if (extension.Equals(".flac", StringComparison.OrdinalIgnoreCase)) {
			blockAlign = ProcessFlac(path, ProcessData);
		}
		else {
			throw new NotSupportedException("File extension not supported.");
		}

		Span<byte> hash = stackalloc byte[4];
		crc.GetCurrentHash(hash);
		return new Result {
			Crc = BinaryPrimitives.ReadUInt32LittleEndian(hash),
			IntroSilenceSamples = (firstPopulatedByte != -1 ? firstPopulatedByte : bytesProcessed) / blockAlign,
			OutroSilenceSamples = (bytesProcessed - lastPopulatedByte - 1) / blockAlign
		};
	}

	private static int ProcessFlac(string path, AudioCallback processData) {
		using FlacDecoder decoder = new(path, new() { ValidateOutputHash = false });
		while (decoder.DecodeFrame()) {
			processData(decoder.BufferBytes.AsSpan(0, decoder.BufferByteCount));
		}
		return decoder.BlockAlign;
	}

	public class Result {
		public uint Crc { get; init; }
		public long IntroSilenceSamples { get; init; }
		public long OutroSilenceSamples { get; init; }
	}

	private delegate void AudioCallback(ReadOnlySpan<byte> span);
}
