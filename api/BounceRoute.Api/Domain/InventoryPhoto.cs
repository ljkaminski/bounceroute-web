namespace BounceRoute.Api.Domain;

// The picture of an inventory item, kept in its own table so listing inventory never loads image bytes.
// Stored in the database rather than on disk because free hosts wipe their disks on restart.
public sealed class InventoryPhoto
{
	public int InventoryItemId { get; set; }

	public string ContentType { get; set; } = string.Empty;

	public byte[] Data { get; set; } = [];

	public DateTimeOffset UploadedAt { get; set; }
}
