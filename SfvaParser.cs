using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace AudioSFV;

public static class SfvaParser {
	public static List<SfvaEntry> Parse(string path) {
		List<SfvaEntry> entries = new();

		foreach (string lineStr in File.ReadLines(path)) {
			if (String.IsNullOrWhiteSpace(lineStr))
				continue;

			// Skip comments
			if (lineStr.StartsWith(';'))
				continue;

			ReadOnlySpan<char> line = lineStr.AsSpan().TrimEnd();

			int pos = line.LastIndexOf(' ');
			if (pos == -1)
				throw new FormatException("Missing delimiter.");

			ReadOnlySpan<char> fileName = line[..pos].TrimEnd();
			ReadOnlySpan<char> checksum = line[(pos + 1)..];

			if (fileName.Length == 0)
				throw new FormatException("Missing filename.");
			if (checksum.Length != 8)
				throw new FormatException("Incorrect CRC length.");

			entries.Add(new SfvaEntry(
				fileName.ToString(),
				BinaryPrimitives.ReadUInt32BigEndian(Convert.FromHexString(checksum)))
			);
		}

		return entries;
	}
}

public record SfvaEntry(string FileName, uint ExpectedCrc);
