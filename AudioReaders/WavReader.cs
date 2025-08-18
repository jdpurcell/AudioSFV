using System;
using System.Buffers.Binary;
using System.IO;

namespace AudioSFV;

public class WavReader : IDisposable {
	private readonly BinaryReader _reader;
	private byte[] _buffer = [];

	public long FileSampleCount { get; private set; }
	public int SampleRate { get; private set; }
	public int ChannelCount { get; private set; }
	public int BitsPerSample { get; private set; }
	public int BytesPerSample { get; private set; }

	public long RunningSampleCount { get; private set; }

	public int BlockAlign => BytesPerSample * ChannelCount;

	public WavReader(Stream input) {
		_reader = new BinaryReader(input);
		try {
			ReadHeaders();
		}
		catch {
			_reader.Dispose();
			throw;
		}
	}

	public WavReader(string path)
		: this(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan))
	{
	}

	public void Dispose() {
		_reader.Dispose();
	}

	private void ReadHeaders() {
		static uint FourCc(ReadOnlySpan<byte> chars) =>
			BinaryPrimitives.ReadUInt32LittleEndian(chars);

		if (_reader.ReadUInt32() != FourCc("RIFF"u8))
			throw new InvalidDataException("RIFF marker not found.");

		long fileRemainingLength = _reader.ReadUInt32();

		if (_reader.ReadUInt32() != FourCc("WAVE"u8))
			throw new InvalidDataException("WAVE marker not found.");
		fileRemainingLength -= 4;

		bool foundDataChunk = false;
		bool foundFormatChunk = false;
		while (fileRemainingLength > 0 && !foundDataChunk) {
			if (fileRemainingLength < 8)
				throw new InvalidDataException("Incomplete chunk header.");

			uint chunkType = _reader.ReadUInt32();
			uint chunkSize = _reader.ReadUInt32();
			fileRemainingLength -= 8;
			if (chunkSize > fileRemainingLength)
				throw new InvalidDataException("Chunk size exceeds file size.");

			if (chunkType == FourCc("fmt "u8)) {
				if (foundFormatChunk)
					throw new InvalidDataException("Found duplicate format chunk.");
				if (chunkSize < 4)
					throw new InvalidDataException("Incomplete format chunk.");
				ushort formatCode = _reader.ReadUInt16();
				uint minFormatChunkSize = formatCode switch {
					WaveFormatPcm => 16,
					WaveFormatExtensible => 40,
					_ => throw new NotSupportedException("Unsupported format code.")
				};
				if (chunkSize < minFormatChunkSize)
					throw new InvalidDataException("Invalid format chunk size.");

				ChannelCount = _reader.ReadUInt16();
				SampleRate = checked((int)_reader.ReadUInt32());
				_reader.ReadUInt32(); // Average bytes per second
				int blockAlign = _reader.ReadUInt16();
				BitsPerSample = _reader.ReadUInt16();

				BytesPerSample = (BitsPerSample + 7) / 8;
				if (blockAlign != BlockAlign)
					throw new InvalidDataException("Block align does not match expected value.");

				if (formatCode == WaveFormatExtensible) {
					ushort extensionSize = _reader.ReadUInt16();
					if (extensionSize < 22)
						throw new InvalidDataException("Invalid format extension size.");

					int validBitsPerSample = _reader.ReadUInt16();
					_reader.ReadUInt32(); // Channel mask
					Guid subformatCode = _reader.ReadGuid();

					if (BitsPerSample % 8 != 0)
						throw new InvalidDataException("Bits per sample is not byte aligned.");
					if (validBitsPerSample > BitsPerSample || validBitsPerSample < BitsPerSample - 7)
						throw new InvalidDataException("Valid bits per sample is outside expected range.");
					if (subformatCode != WaveSubformatPcm)
						throw new NotSupportedException("Unsupported subformat code.");

					BitsPerSample = validBitsPerSample;
				}

				// Skip extra bytes in format chunk
				for (uint i = minFormatChunkSize; i < chunkSize; i++) {
					_reader.ReadByte();
				}

				foundFormatChunk = true;
			}
			else if (chunkType == FourCc("data"u8)) {
				if (!foundFormatChunk)
					throw new InvalidDataException("Format chunk must precede data chunk.");
				if (chunkSize % BlockAlign != 0)
					throw new InvalidDataException("Data chunk size is not block aligned.");

				FileSampleCount = chunkSize / BlockAlign;

				foundDataChunk = true;
			}
			else {
				// Skip other chunks
				for (uint i = 0; i < chunkSize; i++) {
					_reader.ReadByte();
				}
			}

			bool hasPadding = chunkSize % 2 != 0;
			if (hasPadding) {
				_reader.ReadByte();
			}
			fileRemainingLength -= chunkSize + (hasPadding ? 1 : 0);
		}

		if (!foundFormatChunk)
			throw new InvalidDataException("Format chunk not found.");
		if (!foundDataChunk)
			throw new InvalidDataException("Data chunk not found.");
	}

	public ReadOnlySpan<byte> ReadSamples(int requestedSampleCount) {
		if (requestedSampleCount <= 0)
			throw new ArgumentOutOfRangeException(nameof(requestedSampleCount));
		int actualSampleCount = (int)Math.Min(requestedSampleCount, FileSampleCount - RunningSampleCount);
		int byteCount = actualSampleCount * BlockAlign;
		if (_buffer.Length < byteCount) {
			_buffer = new byte[byteCount];
		}
		Span<byte> bufferSpan = _buffer.AsSpan(0, byteCount);
		_reader.ReadExactly(bufferSpan);
		RunningSampleCount += actualSampleCount;
		return bufferSpan;
	}

	private const ushort WaveFormatPcm = 1;
	private const ushort WaveFormatExtensible = 0xFFFE;

	private static readonly Guid WaveSubformatPcm = new(1, 0, 16, 128, 0, 0, 170, 0, 56, 155, 113);
}

file static class ExtensionMethods {
	public static void ReadExactly(this BinaryReader reader, Span<byte> buffer) {
		reader.BaseStream.ReadExactly(buffer);
	}

	public static Guid ReadGuid(this BinaryReader reader) {
		Span<byte> guidBytes = stackalloc byte[16];
		reader.ReadExactly(guidBytes);
		return new Guid(guidBytes);
	}
}
