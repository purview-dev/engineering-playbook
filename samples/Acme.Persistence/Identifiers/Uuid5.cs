using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace Acme;

/// <summary>Deterministic RFC 4122 version-5 (SHA-1) UUID generation.</summary>
public static class Uuid5
{
	public static readonly Guid NamespaceDns = new("6ba7b810-9dad-11d1-80b4-00c04fd430c8");
	public static readonly Guid NamespaceUrl = new("6ba7b811-9dad-11d1-80b4-00c04fd430c8");
	public static readonly Guid NamespaceOid = new("6ba7b812-9dad-11d1-80b4-00c04fd430c8");
	public static readonly Guid NamespaceX500 = new("6ba7b814-9dad-11d1-80b4-00c04fd430c8");

	[SuppressMessage(
		"Security",
		"CA5350:Do Not Use Weak Cryptographic Algorithms",
		Justification = "SHA-1 is mandated by RFC 4122 for UUIDv5."
	)]
	public static Guid Create(Guid namespaceId, string name)
	{
		ArgumentNullException.ThrowIfNull(name);

		// The text form is big-endian. Guid.ToByteArray() and new Guid(byte[]) use a mixed-endian
		// layout and produce the wrong hash for every namespace whose first three groups are not
		// palindromic — which is most of them.
		Span<byte> namespaceBytes = stackalloc byte[16];
		namespaceId.TryWriteBytes(namespaceBytes, bigEndian: true, out _);

		var nameBytes = Encoding.UTF8.GetBytes(name);
		var hashInput = new byte[16 + nameBytes.Length];
		namespaceBytes.CopyTo(hashInput.AsSpan(0, 16));
		nameBytes.CopyTo(hashInput, 16);

		var hash = SHA1.HashData(hashInput);

		var result = hash.AsSpan(0, 16);
		result[6] = (byte)((result[6] & 0x0F) | 0x50); // version 5
		result[8] = (byte)((result[8] & 0x3F) | 0x80); // RFC 4122 variant

		return new Guid(result, bigEndian: true);
	}
}
